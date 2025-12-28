using System.Collections.Generic;
using UnityEngine;

public class OrderGenerator : MonoBehaviour
{
    [Header("Difficulty Table")]
    [SerializeField] private DifficultyTable difficultyTable;

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
        order.reputationReward = difficultyEntry != null ? Mathf.Max(0f, difficultyEntry.reputationReward) : fallbackReputation;
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

        int totalWeight = 0;
        foreach (var e in eligible) totalWeight += Mathf.Max(1, e.weight);
        int roll = Random.Range(0, totalWeight);
        int cumulative = 0;
        foreach (var e in eligible)
        {
            cumulative += Mathf.Max(1, e.weight);
            if (roll < cumulative)
            {
                return e;
            }
        }

        return eligible[eligible.Count - 1];
    }

    private MonsterData PickMonster(int difficultyValue)
    {
        IList<MonsterData> pool = GetMonsterPool(difficultyValue);
        if (pool == null || pool.Count == 0)
        {
            return null;
        }

        int totalWeight = 0;
        foreach (var m in pool) totalWeight += Mathf.Max(1, m.weight);
        int roll = Random.Range(0, totalWeight);
        int cumulative = 0;
        foreach (var m in pool)
        {
            cumulative += Mathf.Max(1, m.weight);
            if (roll < cumulative)
            {
                return m;
            }
        }
        return pool[pool.Count - 1];
    }

    private MonsterData PickMonsterIgnoringDifficulty()
    {
        var pool = GetMonsterPool(null);
        if (pool == null || pool.Count == 0) return null;
        return pool[Random.Range(0, pool.Count)];
    }

    private IList<MonsterData> GetMonsterPool(int? difficultyValue)
    {
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

        var filtered = FilterByReputation(source);
        return difficultyValue.HasValue ? FilterByDifficulty(filtered, difficultyValue.Value) : filtered;
    }

    private IList<MonsterData> FilterByReputation(IList<MonsterData> monsters)
    {
        if (monsters == null) return null;
        int reputation = GameManager.Instance != null ? GameManager.Instance.GetReputation() : 0;
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
