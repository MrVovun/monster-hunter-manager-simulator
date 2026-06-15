using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Hunter Trait", menuName = "Guild Manager/Hunter Trait")]
public class HunterTrait : ScriptableObject
{
    [Header("Identifiers")]
    [Tooltip("Optional trait ID used to counter specific monster traits.")]
    public string traitId;
    [Tooltip("Display name shown in UI.")]
    public string displayName;
    [TextArea(2, 4)]
    public string description;

    [Header("Visuals")]
    public Sprite icon;

    [Header("Bonus Effects")]
    public List<BonusEffect> bonusEffects = new List<BonusEffect>();

    [Header("Mission Effects")]
    public List<MonsterTrait.MissionEffect> missionEffects = new List<MonsterTrait.MissionEffect>();

    [Header("Counters")]
    [Tooltip("Monster traits this hunter trait counters (e.g., FireDamage, Flying).")]
    public List<MonsterTrait> counters = new List<MonsterTrait>();
    [Tooltip("Monster traits this hunter trait counters only when the condition passes.")]
    public List<ConditionalCounter> conditionalCounters = new List<ConditionalCounter>();

    private void OnEnable()
    {
        EnsureId();
    }

    private void OnValidate()
    {
        EnsureId();
    }

    private void EnsureId()
    {
        if (string.IsNullOrWhiteSpace(traitId))
        {
            traitId = Guid.NewGuid().ToString("N");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = traitId;
        }
    }

    [Serializable]
    public class BonusEffect
    {
        public BonusEffectType bonusType = BonusEffectType.AddSuccessChancePercent;
        [Tooltip("Meaning depends on effect type. Success bonuses are in percent points, chance modifiers are multipliers, XP bonuses are flat amounts.")]
        public float value = 0f;
        public TraitStackingMode stacking = TraitStackingMode.SingleInstance;
        public BonusCondition condition = new BonusCondition();
    }

    public enum BonusEffectType
    {
        AddSuccessChancePercent = 0,
        PreventInjury = 1,
        PreventDeath = 2,
        BonusSuccessXP = 3,
        ModifyInjuryChanceMultiplier = 4,
        ModifyDeathChanceMultiplier = 5,
        MinSuccessPercent = 6,
        UpkeepCostMultiplier = 7,
        RewardMultiplier = 8,
        RewardFlat = 9,
        GuardianSacrifice = 10,
        MentorGrantXP = 11,
        MissionTimeMultiplier = 12,
        FailureRescueSuccessChancePercent = 13,
        RerollNegativeRolls = 14,
        RecruitmentRarityWeightMultiplier = 15,
        MaxLevelBonus = 16,
        XpRequirementMultiplier = 17
    }

    public enum TraitStackingMode
    {
        SingleInstance = 0,
        Additive = 1
    }

    [Serializable]
    public class BonusCondition
    {
        [Tooltip("Optional evidence tag category that must match the monster (e.g., \"Faction\").")]
        public string requiredMonsterTagCategory;
        [Tooltip("Value that must match in the selected category (e.g., \"Greenskin\").")]
        public string requiredMonsterTagValue;
        [Tooltip("Optional: restrict this effect to a specific monster asset.")]
        public MonsterData targetMonster;
        [Tooltip("Optional minimum party size for this effect.")]
        public int minPartySize = 0;
        [Tooltip("Optional maximum party size for this effect (0 = no max).")]
        public int maxPartySize = 0;
        [Tooltip("Chance (0-100%) for this effect to apply each mission check.")]
        public float procChancePercent = 100f;
        [Tooltip("When enabled, this effect only applies if exactly one hunter is assigned.")]
        public bool requiresSoloParty = false;
        [Tooltip("Optional monster trait that must be present on the order.")]
        public MonsterTrait requiredMonsterTrait;
        [Tooltip("Optional monster trait ID/display name that must be present on the order.")]
        public string requiredMonsterTraitId;
        [Tooltip("Optional minimum mission duration in seconds.")]
        public float minMissionDurationSeconds = 0f;
        [Tooltip("Optional maximum mission duration in seconds (0 = no max).")]
        public float maxMissionDurationSeconds = 0f;
        [Tooltip("When enabled, this effect only applies if this hunter has the lowest power in the party. Ties count.")]
        public bool requiresWeakestInParty = false;
    }

    [Serializable]
    public class ConditionalCounter
    {
        public MonsterTrait counteredTrait;
        public BonusCondition condition = new BonusCondition();
    }
}
