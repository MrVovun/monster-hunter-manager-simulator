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
    [SerializeField] private float defaultPrepTime = 180f;
    [SerializeField] private float defaultMissionTime = 300f;
    [SerializeField] private int defaultGoldPerDifficulty = 10;
    [SerializeField] private int defaultXpPerDifficulty = 5;

    public Order GenerateRandomOrder()
    {
        DifficultyEntry difficultyEntry = PickDifficulty();
        MonsterData monster = PickMonster();
        OrderFlavorEntry flavor = flavorLibrary != null ? flavorLibrary.GetRandomFlavor() : null;

        int difficultyValue = difficultyEntry != null ? difficultyEntry.difficultyValue : Random.Range(5, 15);
        string monsterName = monster != null && !string.IsNullOrWhiteSpace(monster.displayName)
            ? monster.displayName
            : "monster";

        Order order = new Order();
        order.orderTitle = BuildOrderTitle(flavor, monsterName);
        order.description = BuildOrderDescription(flavor, monsterName);
        order.monsterData = monster;
        order.difficulty = difficultyValue;
        order.goldReward = difficultyEntry != null ? difficultyEntry.goldReward : difficultyValue * defaultGoldPerDifficulty;
        order.xpReward = difficultyEntry != null ? difficultyEntry.xpReward : difficultyValue * defaultXpPerDifficulty;
        order.missionDuration = difficultyEntry != null ? difficultyEntry.missionTimeSeconds : defaultMissionTime;
        order.prepTimeLimit = difficultyEntry != null ? difficultyEntry.prepTimeSeconds : defaultPrepTime;
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

    private MonsterData PickMonster()
    {
        IList<MonsterData> pool = GetMonsterPool();
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

    private IList<MonsterData> GetMonsterPool()
    {
        if (monsterLibrary == null)
        {
            var config = GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null;
            if (config != null && config.monsterLibrary != null)
            {
                monsterLibrary = config.monsterLibrary;
            }
        }

        if (monsterLibrary != null)
        {
            var monsters = monsterLibrary.GetMonsters();
            if (monsters != null && monsters.Count > 0)
            {
                return monsters;
            }
        }

        if (fallbackMonsters != null && fallbackMonsters.Count > 0)
        {
            return fallbackMonsters;
        }

        return null;
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
