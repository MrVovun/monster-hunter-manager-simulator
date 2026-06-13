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
    public float dayLengthSeconds = 36000f;

    [Tooltip("If true, investigation UI pauses global time (accessibility/testing).")]
    public bool allowInvestigationPauseToggle = true;

    [Header("Order Limits")]
    public List<OrderLimitTier> orderLimitByReputation = new List<OrderLimitTier>();

    [Header("Mission Balance")]
    [Range(0f, 1f)] public float baseInjuryChance = 0.2f;
    [Range(0f, 1f)] public float baseDeathChance = 0.05f;

    [Header("Action Time Costs (seconds)")]
    public ActionTimeSettings actionTimeSettings = new ActionTimeSettings();
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

    public int GetOrderLimit(int reputation) => 0;

    [Serializable]
    public class OrderLimitTier
    {
        public int requiredReputation;
        [Tooltip("Reputation points required to reach this reputation level.")]
        public int requiredReputationPoints;
        public int orderLimit = 3;
    }

    [Serializable]
    public class TraitCountChance
    {
        public int traitCount = 1;
        public float weight = 1f;
    }

    [Serializable]
    public class ActionTimeSettings
    {
        [Tooltip("Ringing the bell to call a client.")]
        public float ringBellSeconds = 5f;
        [Tooltip("Asking any question in a dialogue (investigation or hunter).")]
        public float questionSeconds = 5f;
        [Tooltip("Accepting an order.")]
        public float acceptOrderSeconds = 5f;
        [Tooltip("Dispatching a party (not counting mission duration).")]
        public float sendPartySeconds = 5f;
        [Tooltip("Treating hunter wounds. Uses the heal duration plus this bonus if set > 0.")]
        public float treatWoundsBonusSeconds = 0f;
        [Tooltip("Leveling up a hunter.")]
        public float levelUpSeconds = 5f;
        [Tooltip("Posting a hiring ad.")]
        public float postAdSeconds = 10f;
        [Tooltip("Reviewing a candidate / opening their profile.")]
        public float reviewCandidateSeconds = 5f;
        [Tooltip("Hiring or declining a candidate.")]
        public float hireOrDeclineSeconds = 5f;
        [Tooltip("Building or upgrading a construction.")]
        public float buildSeconds = 10f;
        [Tooltip("Choosing a kitchen recipe for the day.")]
        public float chooseKitchenRecipeSeconds = 10f;
        [Tooltip("Cleaning one dirty plate left by a hunter after eating.")]
        public float cleanKitchenPlateSeconds = 3f;
        [Tooltip("Changing sheets on a dirty dormitory bed.")]
        public float cleanDormitoryBedSeconds = 3f;
        [Tooltip("Changing sheets on a stale or unusable dormitory bed.")]
        public float cleanStaleDormitoryBedSeconds = 6f;
        [Tooltip("Pass time amount per tap in the pass-time UI.")]
        public float passTimeStepSeconds = 60f;
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
