using System;
using System.Collections.Generic;
using UnityEngine;

public static class MissionOutcomeCalculator
{
    public const float MaxSuccessChance = 200f;

    public static MissionOutcomeResult Evaluate(Order order, List<Hunter> party, MissionOutcomeConfig? configOverride = null)
    {
        var config = ResolveConfig(configOverride);
        var result = new MissionOutcomeResult();
        result.RequiredPower = Mathf.Max(1f, order != null ? order.difficulty : 1f);

        if (order == null || party == null || party.Count == 0)
        {
            result.SuccessChancePercent = 0f;
            result.PartyPower = 0f;
            result.FinalInjuryChance = Mathf.Clamp01(config.baseInjuryChance);
            result.FinalDeathChance = Mathf.Clamp01(config.baseDeathChance);
            return result;
        }

        float partyPower = 0f;
        foreach (var hunter in party)
        {
            var stats = hunter?.GetStats();
            if (stats == null) continue;
            partyPower += stats.GetTotalPower();
        }
        result.PartyPower = Mathf.Max(0f, partyPower);

        float requiredMultiplier = 1f;
        float partyMultiplier = 1f;
        float injuryChance = Mathf.Clamp01(config.baseInjuryChance);
        float deathChance = Mathf.Clamp01(config.baseDeathChance);
        bool guaranteeInjury = false;
        bool allowDeathWithoutInjury = false;
        float capSuccessLimit = MaxSuccessChance;
        float missionTimeMultiplier = 1f;

        var counteredTraits = BuildCounteredTraitSet(party);
        var monsterTraits = CollectMonsterTraits(order);
        foreach (var monsterTrait in monsterTraits)
        {
            if (monsterTrait == null) continue;
            if (IsCountered(monsterTrait, counteredTraits)) continue;
            ApplyMonsterEffects(monsterTrait, order, ref requiredMultiplier, ref partyMultiplier, ref injuryChance, ref deathChance, ref guaranteeInjury, ref allowDeathWithoutInjury, ref capSuccessLimit, ref missionTimeMultiplier);
        }

        // Apply hunter-driven mission effects
        ApplyHunterMissionEffects(order, party, ref requiredMultiplier, ref partyMultiplier, ref injuryChance, ref deathChance, ref guaranteeInjury, ref allowDeathWithoutInjury, ref capSuccessLimit, ref missionTimeMultiplier);

        result.RequiredPower = Mathf.Max(1f, result.RequiredPower * Mathf.Max(0.01f, requiredMultiplier));
        result.PartyPower = Mathf.Max(0f, result.PartyPower * Mathf.Max(0f, partyMultiplier));

        float ratio = result.RequiredPower <= 0f ? MaxSuccessChance : (result.PartyPower / result.RequiredPower) * 100f;
        float baseSuccess = Mathf.Clamp(ratio, 0f, MaxSuccessChance);

        var aggregate = AggregateHunterBonuses(order?.monsterData, party);
        float successChance = Mathf.Clamp(baseSuccess + aggregate.successChanceBonus, 0f, MaxSuccessChance);
        successChance = Mathf.Clamp(successChance, 0f, capSuccessLimit);
        successChance = Mathf.Max(successChance, aggregate.minSuccessPercent);
        result.SuccessChancePercent = successChance;
        result.MissionTimeMultiplier = Mathf.Max(0.01f, missionTimeMultiplier);

        injuryChance = Mathf.Clamp01(injuryChance * aggregate.injuryChanceMultiplier);
        deathChance = Mathf.Clamp01(deathChance * aggregate.deathChanceMultiplier);

        result.FinalInjuryChance = injuryChance;
        result.FinalDeathChance = deathChance;
        result.InjuriesGuaranteed = guaranteeInjury;
        result.AllowDeathWithoutInjury = allowDeathWithoutInjury;
        result.InjuryPreventionActive = aggregate.preventInjury;
        result.DeathPreventionActive = aggregate.preventDeath;
        result.AdditionalSuccessXP = aggregate.additionalSuccessXp;

        return result;
    }

    private static MissionOutcomeConfig ResolveConfig(MissionOutcomeConfig? configOverride)
    {
        if (configOverride.HasValue)
        {
            return configOverride.Value;
        }

        GameConfig config = GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null;
        return MissionOutcomeConfig.FromGameConfig(config);
    }

    private static List<MonsterTrait> CollectMonsterTraits(Order order)
    {
        var list = new List<MonsterTrait>();
        if (order == null) return list;

        var caseData = order.investigationCase;
        if (caseData != null && caseData.truthTraits != null && caseData.truthTraits.Count > 0)
        {
            list.AddRange(caseData.truthTraits);
        }
        return list;
    }

    private static HashSet<string> BuildCounteredTraitSet(List<Hunter> party)
    {
        var countered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (party == null) return countered;

        foreach (var hunter in party)
        {
            var data = hunter?.Data;
            if (data == null || data.traits == null) continue;
            foreach (var trait in data.traits)
            {
                if (trait == null || trait.counters == null) continue;
                foreach (var counter in trait.counters)
                {
                    if (counter == null || string.IsNullOrEmpty(counter.traitId)) continue;
                    countered.Add(counter.traitId);
                }
            }
        }

        return countered;
    }

    private static bool IsCountered(MonsterTrait trait, HashSet<string> counteredTraits)
    {
        if (trait == null || counteredTraits == null) return false;
        if (string.IsNullOrEmpty(trait.traitId)) return false;
        return counteredTraits.Contains(trait.traitId);
    }

    private static void ApplyMonsterEffects(MonsterTrait trait, Order order, ref float requiredMultiplier, ref float partyMultiplier, ref float injuryChance, ref float deathChance, ref bool guaranteeInjury, ref bool allowDeathWithoutInjury, ref float capSuccessLimit, ref float missionTimeMultiplier)
    {
        if (trait == null || trait.missionEffects == null) return;

        foreach (var effect in trait.missionEffects)
        {
            if (effect == null) continue;
            if (effect.targetMonster != null && order != null)
            {
                if (order.monsterData != effect.targetMonster) continue;
            }
            float value = effect.value;
            switch (effect.effectType)
            {
                case MonsterTrait.MissionEffectType.RequiredPowerMultiplier:
                    requiredMultiplier *= Mathf.Max(0.01f, value);
                    break;
                case MonsterTrait.MissionEffectType.PartyPowerMultiplier:
                    partyMultiplier *= Mathf.Max(0f, value);
                    break;
                case MonsterTrait.MissionEffectType.GuaranteeInjury:
                    guaranteeInjury = true;
                    break;
                case MonsterTrait.MissionEffectType.AllowDeathWithoutInjury:
                    allowDeathWithoutInjury = true;
                    break;
                case MonsterTrait.MissionEffectType.InjuryChanceAdd:
                    injuryChance += value;
                    break;
                case MonsterTrait.MissionEffectType.InjuryChanceMultiplier:
                    injuryChance *= Mathf.Max(0f, value);
                    break;
                case MonsterTrait.MissionEffectType.DeathChanceAdd:
                    deathChance += value;
                    break;
                case MonsterTrait.MissionEffectType.DeathChanceMultiplier:
                    deathChance *= Mathf.Max(0f, value);
                    break;
                case MonsterTrait.MissionEffectType.CapSuccess:
                    capSuccessLimit = Mathf.Min(capSuccessLimit, Mathf.Max(0f, value));
                    break;
                case MonsterTrait.MissionEffectType.MissionTimeMultiplier:
                    missionTimeMultiplier *= Mathf.Max(0.01f, value);
                    break;
            }
        }

        injuryChance = Mathf.Clamp01(injuryChance);
        deathChance = Mathf.Clamp01(deathChance);
    }

    private static void ApplyHunterMissionEffects(Order order, List<Hunter> party, ref float requiredMultiplier, ref float partyMultiplier, ref float injuryChance, ref float deathChance, ref bool guaranteeInjury, ref bool allowDeathWithoutInjury, ref float capSuccessLimit, ref float missionTimeMultiplier)
    {
        if (party == null) return;
        foreach (var hunter in party)
        {
            var data = hunter?.Data;
            if (data == null || data.traits == null) continue;
            foreach (var trait in data.traits)
            {
                if (trait == null || trait.missionEffects == null) continue;
                // Reuse monster effect application for hunter mission effects (optional monster targeting supported)
                var tempMonsterTrait = new MonsterTrait { missionEffects = trait.missionEffects };
                ApplyMonsterEffects(tempMonsterTrait, order, ref requiredMultiplier, ref partyMultiplier, ref injuryChance, ref deathChance, ref guaranteeInjury, ref allowDeathWithoutInjury, ref capSuccessLimit, ref missionTimeMultiplier);
            }
        }
    }

    private static HunterBonusAggregate AggregateHunterBonuses(MonsterData monster, List<Hunter> party)
    {
        var aggregate = HunterBonusAggregate.CreateDefault();
        if (party == null) return aggregate;

        var appliedSingles = new HashSet<HunterTrait.BonusEffect>();
        int partySize = party.Count;

        foreach (var hunter in party)
        {
            var data = hunter?.Data;
            if (data == null || data.traits == null) continue;

            foreach (var trait in data.traits)
            {
                if (trait == null) continue;

                if (trait.bonusEffects == null) continue;
                foreach (var effect in trait.bonusEffects)
                {
                    if (effect == null) continue;
                    if (!DoesConditionPass(effect.condition, monster, partySize)) continue;

                    bool applyEffect = effect.stacking == HunterTrait.TraitStackingMode.Additive || appliedSingles.Add(effect);
                    if (!applyEffect) continue;

                    switch (effect.bonusType)
                    {
                        case HunterTrait.BonusEffectType.AddSuccessChancePercent:
                            aggregate.successChanceBonus += effect.value;
                            break;
                        case HunterTrait.BonusEffectType.PreventInjury:
                            aggregate.preventInjury = true;
                            break;
                        case HunterTrait.BonusEffectType.PreventDeath:
                            aggregate.preventDeath = true;
                            break;
                        case HunterTrait.BonusEffectType.BonusSuccessXP:
                            aggregate.additionalSuccessXp += effect.value;
                            break;
                        case HunterTrait.BonusEffectType.ModifyInjuryChanceMultiplier:
                            aggregate.injuryChanceMultiplier *= effect.value <= 0f ? 1f : effect.value;
                            break;
                        case HunterTrait.BonusEffectType.ModifyDeathChanceMultiplier:
                            aggregate.deathChanceMultiplier *= effect.value <= 0f ? 1f : effect.value;
                            break;
                        case HunterTrait.BonusEffectType.MinSuccessPercent:
                            aggregate.minSuccessPercent = Mathf.Max(aggregate.minSuccessPercent, effect.value);
                            break;
                        case HunterTrait.BonusEffectType.UpkeepCostMultiplier:
                            // handled elsewhere
                            break;
                        case HunterTrait.BonusEffectType.RewardMultiplier:
                        case HunterTrait.BonusEffectType.RewardFlat:
                        case HunterTrait.BonusEffectType.GuardianSacrifice:
                        case HunterTrait.BonusEffectType.MentorGrantXP:
                            // handled in MissionResolver / upkeep hooks
                            break;
                    }
                }
            }
        }

        aggregate.successChanceBonus = Mathf.Clamp(aggregate.successChanceBonus, -MaxSuccessChance, MaxSuccessChance);
        return aggregate;
    }

    public static bool DoesConditionPass(HunterTrait.BonusCondition condition, MonsterData monster, int partySize)
    {
        if (condition == null) return true;

        if (condition.procChancePercent < 100f)
        {
            float roll = UnityEngine.Random.Range(0f, 100f);
            if (roll > Mathf.Max(0f, condition.procChancePercent))
            {
                return false;
            }
        }

        if (condition.minPartySize > 0 && partySize < condition.minPartySize)
        {
            return false;
        }

        if (condition.maxPartySize > 0 && partySize > condition.maxPartySize)
        {
            return false;
        }

        if (condition.targetMonster != null)
        {
            if (monster == null || condition.targetMonster != monster)
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(condition.requiredMonsterTagCategory))
        {
            string monsterValue = monster != null ? monster.GetTagValue(condition.requiredMonsterTagCategory) : null;
            if (string.IsNullOrEmpty(monsterValue) || !string.Equals(monsterValue, condition.requiredMonsterTagValue, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (condition.requiresSoloParty && partySize != 1)
        {
            return false;
        }

        return true;
    }

    private struct HunterBonusAggregate
    {
        public float successChanceBonus;
        public float injuryChanceMultiplier;
        public float deathChanceMultiplier;
        public bool preventInjury;
        public bool preventDeath;
        public float additionalSuccessXp;
        public float minSuccessPercent;

        public static HunterBonusAggregate CreateDefault()
        {
            return new HunterBonusAggregate
            {
                successChanceBonus = 0f,
                injuryChanceMultiplier = 1f,
                deathChanceMultiplier = 1f,
                preventInjury = false,
                preventDeath = false,
                additionalSuccessXp = 0f,
                minSuccessPercent = 0f
            };
        }
    }
}

public struct MissionOutcomeConfig
{
    public float baseInjuryChance;
    public float baseDeathChance;

    public static MissionOutcomeConfig Default => new MissionOutcomeConfig
    {
        baseInjuryChance = 0.2f,
        baseDeathChance = 0.05f
    };

    public static MissionOutcomeConfig FromGameConfig(GameConfig config)
    {
        if (config == null)
        {
            return Default;
        }

        return new MissionOutcomeConfig
        {
            baseInjuryChance = Mathf.Clamp01(config.baseInjuryChance),
            baseDeathChance = Mathf.Clamp01(config.baseDeathChance)
        };
    }
}

public class MissionOutcomeResult
{
    public float RequiredPower { get; internal set; }
    public float PartyPower { get; internal set; }
    public float SuccessChancePercent { get; internal set; }
    public float MissionTimeMultiplier { get; internal set; } = 1f;
    public float FinalInjuryChance { get; internal set; }
    public float FinalDeathChance { get; internal set; }
    public bool InjuriesGuaranteed { get; internal set; }
    public bool AllowDeathWithoutInjury { get; internal set; }
    public bool InjuryPreventionActive { get; internal set; }
    public bool DeathPreventionActive { get; internal set; }
    public float AdditionalSuccessXP { get; internal set; }

    public float SuccessRollThreshold => Mathf.Min(SuccessChancePercent, 100f);
    public bool InjuryProtectionFromSuccess => SuccessChancePercent > 100f;
    public bool DeathProtectionFromSuccess => SuccessChancePercent >= MissionOutcomeCalculator.MaxSuccessChance;
}
