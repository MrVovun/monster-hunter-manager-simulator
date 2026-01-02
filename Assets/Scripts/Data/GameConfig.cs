using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "Guild Manager/Game Config")]
public class GameConfig : ScriptableObject
{
    [Header("Core Data")]
    public GlobalHunterConfig hunterConfig;
    public MonsterLibrary monsterLibrary;
    public EvidenceTagLibrary evidenceTagLibrary;
    public List<InvestigationQuestion> defaultInvestigationQuestions = new List<InvestigationQuestion>();
    public List<ClientProfile> defaultClientProfiles = new List<ClientProfile>();
    [Header("Trophy Wall")]
    public TrophyWallConfig trophyWallConfig;
    [Header("Guild Constructions")]
    public List<GuildConstructionDefinition> guildConstructions = new List<GuildConstructionDefinition>();

    [Header("Time")]
    [Tooltip("Length of an in-game day in real-time seconds.")]
    public float dayLengthSeconds = 600f;

    [Tooltip("If true, investigation UI pauses global time (accessibility/testing).")]
    public bool allowInvestigationPauseToggle = true;

    [Header("Order Limits")]
    public List<OrderLimitTier> orderLimitByReputation = new List<OrderLimitTier>()
    {
        new OrderLimitTier{ requiredReputation = 0, orderLimit = 3 },
        new OrderLimitTier{ requiredReputation = 50, orderLimit = 4 },
        new OrderLimitTier{ requiredReputation = 100, orderLimit = 5 },
    };

    [Header("Mission Balance")]
    [Range(0f, 1f)] public float baseInjuryChance = 0.2f;
    [Range(0f, 1f)] public float baseDeathChance = 0.05f;
    [Header("Monster Trait Generation")]
    public List<TraitCountChance> traitCountChances = new List<TraitCountChance>
    {
        new TraitCountChance{ traitCount = 0, weight = 1f },
        new TraitCountChance{ traitCount = 1, weight = 2f },
        new TraitCountChance{ traitCount = 2, weight = 1.5f },
        new TraitCountChance{ traitCount = 3, weight = 1f },
    };

    [Header("Hunter Interaction")]
    [Tooltip("Base time in seconds to heal a wounded hunter.")]
    public float hunterHealDurationSeconds = 10f;

    public int GetOrderLimit(int reputation)
    {
        int limit = 0;
        foreach (var tier in orderLimitByReputation)
        {
            if (reputation >= tier.requiredReputation)
            {
                limit = Mathf.Max(limit, tier.orderLimit);
            }
        }
        return limit;
    }

    [Serializable]
    public class OrderLimitTier
    {
        public int requiredReputation;
        public int orderLimit = 3;
    }

    [Serializable]
    public class TraitCountChance
    {
        public int traitCount = 1;
        public float weight = 1f;
    }

    public int RollTraitCount(int min, int max)
    {
        min = Mathf.Max(0, min);
        max = Mathf.Max(min, max);
        if (traitCountChances == null || traitCountChances.Count == 0)
        {
            return UnityEngine.Random.Range(min, max + 1);
        }

        List<TraitCountChance> candidates = new List<TraitCountChance>();
        foreach (var entry in traitCountChances)
        {
            if (entry == null) continue;
            if (entry.traitCount < min || entry.traitCount > max) continue;
            if (entry.weight <= 0f) continue;
            candidates.Add(entry);
        }

        if (candidates.Count == 0)
        {
            return UnityEngine.Random.Range(min, max + 1);
        }

        float total = 0f;
        foreach (var entry in candidates)
        {
            total += Mathf.Max(0f, entry.weight);
        }
        if (total <= 0f)
        {
            return UnityEngine.Random.Range(min, max + 1);
        }

        float roll = UnityEngine.Random.Range(0f, total);
        float cumulative = 0f;
        foreach (var entry in candidates)
        {
            cumulative += Mathf.Max(0f, entry.weight);
            if (roll <= cumulative)
            {
                return entry.traitCount;
            }
        }

        return candidates[candidates.Count - 1].traitCount;
    }

    public GuildConstructionDefinition GetConstructionById(string id)
    {
        if (string.IsNullOrEmpty(id) || guildConstructions == null) return null;
        for (int i = 0; i < guildConstructions.Count; i++)
        {
            var def = guildConstructions[i];
            if (def == null) continue;
            if (string.Equals(def.ConstructionId, id, StringComparison.OrdinalIgnoreCase))
            {
                return def;
            }
        }
        return null;
    }
}
