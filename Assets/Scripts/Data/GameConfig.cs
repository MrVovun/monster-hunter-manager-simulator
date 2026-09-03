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
    [Tooltip("Success chance at or above this value guarantees mission success.")]
    public float successThresholdPercent = 100f;
    [Tooltip("Success chance at or above this value prevents normal wound rolls.")]
    public float woundProtectionThresholdPercent = 150f;
    [Tooltip("Success chance at or above this value qualifies the mission for bonus rewards.")]
    public float bonusRewardThresholdPercent = 200f;
    [Tooltip("Sending parties during this many final seconds of the workday counts as a late dispatch.")]
    public float lateDispatchWindowSeconds = 60f;
    [Tooltip("Flat success chance penalty applied to orders sent during the late dispatch window.")]
    public float lateDispatchSuccessPenaltyPercent = 10f;

    [Header("Referral Economy")]
    [Tooltip("Base share of the order gold reward paid when referring a well-documented case.")]
    [Range(0f, 1f)] public float referralRate = 0.25f;

    [Header("Dynamic Spawn Balance")]
    [Tooltip("Each reputation tier above an order's unlock tier multiplies that order's spawn weight by this value.")]
    [Range(0f, 1f)] public float lowerOrderDecay = 0.45f;
    [Tooltip("Lowest multiplier retained for old lower-tier orders so they remain possible.")]
    [Range(0f, 1f)] public float minOldOrderMultiplier = 0.02f;
    [Tooltip("Weight multiplier for orders from the current reputation tier. 1 means no extra weighting.")]
    public float currentTierOrderMultiplier = 1f;
    [Tooltip("Each reputation tier above a hunter's unlock tier multiplies that hunter's recruitment weight by this value.")]
    [Range(0f, 1f)] public float lowerRecruitDecay = 0.55f;

    [Header("Trait Reward Scaling")]
    [Tooltip("Reward multiplier bonus for each rolled monster trait on the order. 0.33 = +33% per trait.")]
    public float rewardBonusPerMonsterTrait = 0.33f;

    [Header("Guild Trust")]
    [Tooltip("Trust gained for a clean successful order.")]
    public int cleanSuccessTrustGain = 1;
    [Tooltip("Maximum Trust streak value used for reputation rewards and stored in saves.")]
    public int maxTrustStreak = 5;
    [Tooltip("Trust lost after a failed order, clamped at 0.")]
    public int failedOrderTrustLoss = 2;
    [Tooltip("When enabled, any failed order resets Trust to 0 instead of subtracting Failed Order Trust Loss.")]
    public bool resetTrustOnFailedOrder = true;
    [Tooltip("Each Trust point adds this multiplier to reputation rewards. 0.15 = +15% per Trust.")]
    public float trustReputationBonusPerStreak = 0.15f;
    [Tooltip("Multiplier applied to reputation rewards for clean successes.")]
    [Range(0f, 1f)] public float cleanSuccessReputationMultiplier = 1f;
    [Tooltip("Multiplier applied to reputation rewards for successful but messy orders.")]
    [Range(0f, 1f)] public float messySuccessReputationMultiplier = 0.65f;
    [Tooltip("Multiplier applied to reputation rewards for failed orders.")]
    [Range(0f, 1f)] public float failureReputationMultiplier = 0f;
    [Tooltip("Multiplier applied to reputation rewards for referred orders.")]
    [Range(0f, 1f)] public float referralReputationMultiplier = 0f;
    [Tooltip("Clean successes only add Trust when order tier is at least current reputation minus this value.")]
    public int trustEligibleTierBelowCurrentReputation = 1;

    [Header("Debt / Unpaid Upkeep")]
    public DebtSettings debtSettings = new DebtSettings();

    [Header("Action Time Costs (seconds)")]
    public ActionTimeSettings actionTimeSettings = new ActionTimeSettings();

    [Header("Dialogue")]
    [Tooltip("Real-time delay before a dialogue answer starts printing. Client profiles affect action time, not this wait.")]
    public float dialogueResponseDelaySeconds = 2f;

    [Header("Telemetry")]
    [Tooltip("Writes local playtest telemetry CSV files under Application.persistentDataPath. Keep this local-only for internal testing.")]
    public bool enableLocalTelemetry = true;
    [Tooltip("Write one row per tracked gameplay event.")]
    public bool writeLocalTelemetryEvents = true;
    [Tooltip("Append a compact session summary row when the session closes.")]
    public bool writeLocalTelemetrySessionSummary = true;
    [Tooltip("Folder name under Application.persistentDataPath used for local telemetry files.")]
    public string localTelemetryFolderName = "Telemetry";
    [Tooltip("Print the local telemetry output folder to the console at session start.")]
    public bool logLocalTelemetryPath;

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
        [Tooltip("Accepting an order.")]
        public float acceptOrderSeconds = 5f;
        [Tooltip("Referring an order to another guild.")]
        public float referOrderSeconds = 5f;
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
        [Tooltip("Fallback action-time cost for washing the main hall floor.")]
        public float washFloorSeconds = 5f;
        [Tooltip("Pass time amount per tap in the pass-time UI.")]
        public float passTimeStepSeconds = 60f;
    }

    [Serializable]
    public class DebtSettings
    {
        [Tooltip("Flat success chance penalty applied during the first consecutive unpaid day.")]
        public float unpaidDay1SuccessPenaltyPercent = 5f;
        [Tooltip("Flat success chance penalty applied during the second consecutive unpaid day.")]
        public float unpaidDay2SuccessPenaltyPercent = 15f;
        [Tooltip("Percentage of current reputation points lost on the first consecutive unpaid day.")]
        [Range(0f, 100f)] public float unpaidDay1ReputationPointLossPercent = 10f;
        [Tooltip("Percentage of current reputation points lost on the second consecutive unpaid day.")]
        [Range(0f, 100f)] public float unpaidDay2ReputationPointLossPercent = 25f;
        [Tooltip("Day 2 dismissals continue until remaining daily upkeep is at or below previous day's gross income.")]
        public bool dismissHuntersUntilUpkeepFitsPreviousIncome = true;
        [Tooltip("Debt dismissals will not reduce the roster below this many active hunters. Keep at least 1 if you want Day 3 game over to remain reachable.")]
        public int minimumHuntersAfterDebtDismissal = 1;
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
