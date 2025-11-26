using System.Collections.Generic;
using UnityEngine;

public class OrderGenerator : MonoBehaviour
{
    [Header("Difficulty Table")]
    [SerializeField] private DifficultyTable difficultyTable;

    [Header("Monsters")]
    [SerializeField] private List<MonsterData> monsters = new List<MonsterData>();

    [Header("Defaults (used if no data provided)")]
    [SerializeField] private float defaultPrepTime = 180f;
    [SerializeField] private float defaultMissionTime = 300f;
    [SerializeField] private int defaultGoldPerDifficulty = 10;
    [SerializeField] private int defaultXpPerDifficulty = 5;

    public Order GenerateRandomOrder()
    {
        DifficultyEntry difficultyEntry = PickDifficulty();
        MonsterData monster = PickMonster();

        int difficultyValue = difficultyEntry != null ? difficultyEntry.difficultyValue : Random.Range(5, 15);

        Order order = new Order();
        order.orderTitle = monster != null ? monster.displayName : "Monster Hunt";
        order.description = monster != null ? $"A {monster.displayName} is causing trouble." : "A dangerous creature needs to be dealt with.";
        order.monsterType = (MonsterType)Random.Range(0, System.Enum.GetValues(typeof(MonsterType)).Length);
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
        if (monsters == null || monsters.Count == 0)
        {
            return null;
        }

        int totalWeight = 0;
        foreach (var m in monsters) totalWeight += Mathf.Max(1, m.weight);
        int roll = Random.Range(0, totalWeight);
        int cumulative = 0;
        foreach (var m in monsters)
        {
            cumulative += Mathf.Max(1, m.weight);
            if (roll < cumulative)
            {
                return m;
            }
        }
        return monsters[monsters.Count - 1];
    }
}
