using System.Collections.Generic;
using UnityEngine;

public class OrderGenerator : MonoBehaviour
{
    [Header("Difficulty Table")]
    [SerializeField] private DifficultyTable difficultyTable;
    [SerializeField] private bool debugLogging = false;

    [Header("Monsters")]
    [SerializeField] private MonsterLibrary monsterLibrary;
    [SerializeField] private List<MonsterData> fallbackMonsters = new List<MonsterData>();

    [Header("Flavor")]
    [SerializeField] private OrderFlavorLibrary flavorLibrary;
    [SerializeField] private string monsterNamePlaceholder = "<monster_name>";

    [Header("Defaults (used if no data provided)")]
    [SerializeField] private float defaultMissionTime = 300f;
    [SerializeField] private int defaultGoldPerDifficulty = 10;
    [SerializeField] private int defaultXpPerDifficulty = 5;
    [SerializeField] private float defaultReputationPerDifficulty = 0.1f;

    public Order GenerateRandomOrder()
    {
        DifficultyEntry difficultyEntry = PickDifficulty();
        OrderFlavorEntry flavor = flavorLibrary != null ? flavorLibrary.GetRandomFlavor() : null;

        int difficultyValue = difficultyEntry != null ? difficultyEntry.difficultyValue : Random.Range(5, 15);
        MonsterData monster = PickMonster(difficultyValue);
        if (monster == null)
        {
            monster = PickMonsterIgnoringDifficulty();
            if (monster == null)
            {
                string libName = monsterLibrary != null ? monsterLibrary.name : "null";
                int fallbackCount = fallbackMonsters != null ? fallbackMonsters.Count : 0;
                Debug.LogWarning($"[OrderGenerator] No monster could be selected. Difficulty={difficultyValue}, library={libName}, fallbackCount={fallbackCount}", this);
            }
        }
        string monsterName = monster != null && !string.IsNullOrWhiteSpace(monster.displayName)
            ? monster.displayName
            : "monster";

        Order order = new Order();
        order.monsterNamePlaceholder = string.IsNullOrWhiteSpace(monsterNamePlaceholder)
            ? Order.DefaultMonsterPlaceholder
            : monsterNamePlaceholder;
        order.orderTitle = BuildOrderTitle(flavor, monsterName);
        order.description = BuildOrderDescription(flavor, order.monsterNamePlaceholder);
        order.monsterData = monster;
        order.difficulty = difficultyValue;
        order.goldReward = difficultyEntry != null ? difficultyEntry.goldReward : difficultyValue * defaultGoldPerDifficulty;
        order.xpReward = difficultyEntry != null ? difficultyEntry.xpReward : difficultyValue * defaultXpPerDifficulty;
        float fallbackReputation = Mathf.Max(0f, difficultyValue * Mathf.Max(0f, defaultReputationPerDifficulty));
        order.reputationPointsReward = difficultyEntry != null ? Mathf.Max(0f, difficultyEntry.reputationPointsReward) : fallbackReputation;
        order.reputationTier = difficultyEntry != null ? Mathf.Max(0, difficultyEntry.minReputation) : Mathf.Max(0, GameManager.Instance != null ? GameManager.Instance.GetReputation() : 0);
        order.missionDuration = difficultyEntry != null ? difficultyEntry.missionTimeSeconds : defaultMissionTime;
        order.maxPartySize = 3;
        order.minPartySize = 1;
        order.state = OrderState.Offered;

        return order;
    }

    private DifficultyEntry PickDifficulty()
    {
        if (difficultyTable == null || difficultyTable.entries.Count == 0)
        {
            return null;
        }

        int reputation = GameManager.Instance != null ? GameManager.Instance.GetReputation() : 0;
        List<DifficultyEntry> eligible = new List<DifficultyEntry>();
        foreach (var entry in difficultyTable.entries)
        {
            if (reputation >= entry.minReputation && reputation <= entry.maxReputation)
            {
                eligible.Add(entry);
            }
        }

        if (eligible.Count == 0)
        {
            return null;
        }

        List<float> weights = new List<float>();
        float totalWeight = 0f;
        foreach (var e in eligible)
        {
            float weight = GetDifficultySelectionWeight(e, reputation);
            weights.Add(weight);
            totalWeight += weight;
        }

        if (totalWeight <= 0f)
        {
            return null;
        }

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        for (int i = 0; i < eligible.Count; i++)
        {
            cumulative += weights[i];
            if (roll < cumulative)
            {
                return eligible[i];
            }
        }

        return eligible[eligible.Count - 1];
    }

    private float GetDifficultySelectionWeight(DifficultyEntry entry, int currentReputation)
    {
        if (entry == null) return 0f;
        float weight = Mathf.Max(0f, entry.weight);
        if (weight <= 0f) return 0f;

        GameConfig config = GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null;
        if (config == null) return weight;

        int tierDelta = Mathf.Max(0, currentReputation - entry.minReputation);
        if (tierDelta > 0)
        {
            float decay = Mathf.Pow(Mathf.Clamp01(config.lowerOrderDecay), tierDelta);
            weight *= Mathf.Max(Mathf.Clamp01(config.minOldOrderMultiplier), decay);
        }
        else if (entry.minReputation == currentReputation)
        {
            weight *= Mathf.Max(0f, config.currentTierOrderMultiplier);
        }

        return weight;
    }

    private MonsterData PickMonster(int difficultyValue)
    {
        IList<MonsterData> pool = GetMonsterPool(difficultyValue);
        if (pool == null || pool.Count == 0)
        {
            return null;
        }

        float totalWeight = 0f;
        foreach (var m in pool)
        {
            totalWeight += GetMonsterSelectionWeight(m, difficultyValue);
        }

        if (totalWeight <= 0f)
        {
            return null;
        }

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        foreach (var m in pool)
        {
            cumulative += GetMonsterSelectionWeight(m, difficultyValue);
            if (roll < cumulative)
            {
                return m;
            }
        }
        return pool[pool.Count - 1];
    }

    private float GetMonsterSelectionWeight(MonsterData monster, int difficultyValue)
    {
        if (monster == null) return 0f;
        float baseWeight = Mathf.Max(1, monster.weight);
        return baseWeight * monster.GetDifficultySelectionMultiplier(difficultyValue);
    }

    private MonsterData PickMonsterIgnoringDifficulty()
    {
        var pool = GetMonsterPool(null);
        if (pool == null || pool.Count == 0) return null;
        return pool[Random.Range(0, pool.Count)];
    }

    private IList<MonsterData> GetMonsterPool(int? difficultyValue)
    {
        int reputation = GameManager.Instance != null ? GameManager.Instance.GetReputation() : 0;
        if (monsterLibrary == null)
        {
            var config = GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null;
            if (config != null && config.monsterLibrary != null)
            {
                monsterLibrary = config.monsterLibrary;
            }
        }

        IList<MonsterData> source = null;
        if (monsterLibrary != null)
        {
            var monsters = monsterLibrary.GetMonsters();
            if (monsters != null && monsters.Count > 0)
            {
                source = monsters;
            }
        }

        if (source == null && fallbackMonsters != null && fallbackMonsters.Count > 0)
        {
            source = fallbackMonsters;
        }

        if (source == null) return null;

        var filtered = FilterByReputation(source, reputation);
        var final = difficultyValue.HasValue ? FilterByDifficulty(filtered, difficultyValue.Value) : filtered;

        if (debugLogging)
        {
            int sourceCount = source != null ? source.Count : 0;
            int repCount = filtered != null ? filtered.Count : 0;
            int finalCount = final != null ? final.Count : 0;
            string diffLabel = difficultyValue.HasValue ? difficultyValue.Value.ToString() : "any";
            Debug.Log($"[OrderGenerator] Pool rep={reputation} diff={diffLabel} source={sourceCount} repFiltered={repCount} final={finalCount}", this);
        }

        return final;
    }

    private IList<MonsterData> FilterByReputation(IList<MonsterData> monsters, int reputation)
    {
        if (monsters == null) return null;
        List<MonsterData> filtered = new List<MonsterData>();
        foreach (var monster in monsters)
        {
            if (monster == null) continue;
            if (reputation >= monster.requiredReputation)
            {
                filtered.Add(monster);
            }
        }

        return filtered.Count > 0 ? (IList<MonsterData>)filtered : monsters;
    }

    private IList<MonsterData> FilterByDifficulty(IList<MonsterData> monsters, int difficultyValue)
    {
        if (monsters == null) return null;
        List<MonsterData> filtered = new List<MonsterData>();
        foreach (var monster in monsters)
        {
            if (monster == null) continue;
            if (difficultyValue >= monster.minimumDifficulty)
            {
                filtered.Add(monster);
            }
        }

        return filtered.Count > 0 ? (IList<MonsterData>)filtered : monsters;
    }

    private string BuildOrderTitle(OrderFlavorEntry flavor, string monsterName)
    {
        if (flavor != null && !string.IsNullOrWhiteSpace(flavor.title))
        {
            return ReplaceMonsterPlaceholder(flavor.title, monsterName);
        }

        return string.IsNullOrWhiteSpace(monsterName) ? "Monster Hunt" : $"{monsterName} Trouble";
    }

    private string BuildOrderDescription(OrderFlavorEntry flavor, string monsterName)
    {
        if (flavor != null && !string.IsNullOrWhiteSpace(flavor.description))
        {
            return ReplaceMonsterPlaceholder(flavor.description, monsterName);
        }

        if (string.IsNullOrWhiteSpace(monsterName))
        {
            return "A dangerous creature needs to be dealt with.";
        }

        return $"A {monsterName} is causing trouble.";
    }

    private string ReplaceMonsterPlaceholder(string template, string monsterName)
    {
        if (string.IsNullOrEmpty(template)) return template;

        string replacement = string.IsNullOrWhiteSpace(monsterName) ? "monster" : monsterName;

        if (!string.IsNullOrEmpty(monsterNamePlaceholder) && template.Contains(monsterNamePlaceholder))
        {
            return template.Replace(monsterNamePlaceholder, replacement);
        }

        return template;
    }
}
