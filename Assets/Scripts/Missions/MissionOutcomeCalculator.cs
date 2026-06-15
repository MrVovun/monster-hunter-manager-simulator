using System;
using System.Collections.Generic;
using UnityEngine;

public static class MissionOutcomeCalculator
{
    public const float MaxSuccessChance = 200f;

    public static List<string> BuildSuccessTelemetryLines(Order order, List<Hunter> party)
    {
        return BuildSuccessTelemetryLines(order, party, GetTruthMonster(order), CollectMonsterTraits(order));
    }

    public static List<string> BuildPreviewSuccessTelemetryLines(Order order, List<Hunter> party)
    {
        return BuildSuccessTelemetryLines(order, party, GetPreviewMonster(order), CollectPreviewMonsterTraits(order));
    }

    private static List<string> BuildSuccessTelemetryLines(Order order, List<Hunter> party, MonsterData monsterForConditions, List<MonsterTrait> monsterTraits)
    {
        var lines = new List<string>();
        if (order == null)
        {
            lines.Add("No order selected.");
            return lines;
        }

        if (party == null || party.Count == 0)
        {
            lines.Add("No hunters assigned.");
            return lines;
        }

        int partySize = party.Count;
        var counteredTraits = BuildCounteredTraitSet(party, order, monsterForConditions, monsterTraits);
        foreach (var monsterTrait in monsterTraits)
        {
            if (monsterTrait == null) continue;
            if (IsCountered(monsterTrait, counteredTraits))
            {
                string name = string.IsNullOrWhiteSpace(monsterTrait.displayName) ? "Monster trait" : monsterTrait.displayName;
                lines.Add($"Countered: {name}");
            }
        }

        float successBonusTotal = 0f;
        float minSuccessTotal = 0f;

        foreach (var hunter in party)
        {
            var data = hunter?.Data;
            float briefingBonus = BriefingRoomManager.GetActiveDailyBonus(hunter);
            if (Mathf.Abs(briefingBonus) > 0.01f)
            {
                string hunterName = hunter != null && !string.IsNullOrWhiteSpace(hunter.name) ? hunter.name : "Hunter";
                successBonusTotal += briefingBonus;
                lines.Add($"{hunterName} - Briefing plan: {(briefingBonus >= 0f ? "+" : string.Empty)}{briefingBonus:0.#}% success");
            }

            KitchenRecipe kitchenRecipe = KitchenManager.GetActiveRecipe(hunter);
            if (kitchenRecipe != null && Mathf.Abs(kitchenRecipe.successChanceBonusPercent) > 0.01f)
            {
                string hunterName = hunter != null && !string.IsNullOrWhiteSpace(hunter.name) ? hunter.name : "Hunter";
                successBonusTotal += kitchenRecipe.successChanceBonusPercent;
                lines.Add($"{hunterName} - Ate {kitchenRecipe.GetDisplayName()}: {(kitchenRecipe.successChanceBonusPercent >= 0f ? "+" : string.Empty)}{kitchenRecipe.successChanceBonusPercent:0.#}% success");
            }

            float missedSleepPenalty = DormitoryManager.GetActiveMissedSleepPenaltyPercent(hunter);
            if (missedSleepPenalty > 0.01f)
            {
                string hunterName = hunter != null && !string.IsNullOrWhiteSpace(hunter.name) ? hunter.name : "Hunter";
                successBonusTotal -= missedSleepPenalty;
                lines.Add($"{hunterName} - Missed sleep: -{missedSleepPenalty:0.#}% success");
            }

            if (data == null || data.traits == null) continue;

            foreach (var trait in data.traits)
            {
                if (trait == null || trait.bonusEffects == null) continue;

                foreach (var effect in trait.bonusEffects)
                {
                    if (effect == null) continue;
                    if (!DoesConditionPassPreview(effect.condition, monsterForConditions, partySize, hunter, party, order, monsterTraits)) continue;

                    string hunterName = !string.IsNullOrWhiteSpace(hunter.name) ? hunter.name : "Hunter";
                    string traitName = !string.IsNullOrWhiteSpace(trait.displayName) ? trait.displayName : "Trait";
                    string suffix = BuildConditionSuffix(effect.condition);

                    switch (effect.bonusType)
                    {
                        case HunterTrait.BonusEffectType.AddSuccessChancePercent:
                            successBonusTotal += effect.value;
                            lines.Add($"{hunterName} - {traitName}: {(effect.value >= 0f ? "+" : string.Empty)}{effect.value:0.#}% success{suffix}");
                            break;
                        case HunterTrait.BonusEffectType.MinSuccessPercent:
                            minSuccessTotal = Mathf.Max(minSuccessTotal, effect.value);
                            lines.Add($"{hunterName} - {traitName}: minimum success {effect.value:0.#}%{suffix}");
                            break;
                        case HunterTrait.BonusEffectType.MissionTimeMultiplier:
                            lines.Add($"{hunterName} - {traitName}: x{(effect.value <= 0f ? 1f : effect.value):0.##} mission time{suffix}");
                            break;
                    }
                }
            }
        }

        if (Mathf.Abs(successBonusTotal) > 0.01f)
        {
            lines.Add($"Total flat success bonus: {(successBonusTotal >= 0f ? "+" : string.Empty)}{successBonusTotal:0.#}%");
        }
        if (minSuccessTotal > 0f)
        {
            lines.Add($"Minimum success floor: {minSuccessTotal:0.#}%");
        }

        if (lines.Count == 0)
        {
            lines.Add("No active success modifiers.");
        }

        return lines;
    }

    public static MissionOutcomeResult Evaluate(Order order, List<Hunter> party, MissionOutcomeConfig? configOverride = null)
    {
        return Evaluate(order, party, configOverride, GetTruthMonster(order), CollectMonsterTraits(order));
    }

    public static MissionOutcomeResult EvaluatePreview(Order order, List<Hunter> party, MissionOutcomeConfig? configOverride = null)
    {
        return Evaluate(order, party, configOverride, GetPreviewMonster(order), CollectPreviewMonsterTraits(order));
    }

    private static MissionOutcomeResult Evaluate(Order order, List<Hunter> party, MissionOutcomeConfig? configOverride, MonsterData monsterForConditions, List<MonsterTrait> monsterTraits)
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

        var counteredTraits = BuildCounteredTraitSet(party, order, monsterForConditions, monsterTraits);
        foreach (var monsterTrait in monsterTraits)
        {
            if (monsterTrait == null) continue;
            if (IsCountered(monsterTrait, counteredTraits)) continue;
            ApplyMonsterEffects(monsterTrait, monsterForConditions, ref requiredMultiplier, ref partyMultiplier, ref injuryChance, ref deathChance, ref guaranteeInjury, ref allowDeathWithoutInjury, ref capSuccessLimit, ref missionTimeMultiplier);
        }

        // Apply hunter-driven mission effects
        ApplyHunterMissionEffects(monsterForConditions, party, ref requiredMultiplier, ref partyMultiplier, ref injuryChance, ref deathChance, ref guaranteeInjury, ref allowDeathWithoutInjury, ref capSuccessLimit, ref missionTimeMultiplier);

        result.RequiredPower = Mathf.Max(1f, result.RequiredPower * Mathf.Max(0.01f, requiredMultiplier));
        result.PartyPower = Mathf.Max(0f, result.PartyPower * Mathf.Max(0f, partyMultiplier));

        float ratio = result.RequiredPower <= 0f ? MaxSuccessChance : (result.PartyPower / result.RequiredPower) * 100f;
        float baseSuccess = Mathf.Clamp(ratio, 0f, MaxSuccessChance);

        var aggregate = AggregateHunterBonuses(order, monsterForConditions, monsterTraits, party);
        missionTimeMultiplier *= Mathf.Clamp(1f - (aggregate.missionTimeReductionPercent / 100f), 0.01f, 1f);
        missionTimeMultiplier *= Mathf.Max(0.01f, aggregate.missionTimeMultiplier);
        float successChance = Mathf.Clamp(baseSuccess + aggregate.successChanceBonus, 0f, MaxSuccessChance);
        successChance = Mathf.Clamp(successChance, 0f, capSuccessLimit);
        successChance = Mathf.Max(successChance, aggregate.minSuccessPercent);
        result.SuccessChancePercent = successChance;
        result.MissionTimeMultiplier = Mathf.Max(0.01f, missionTimeMultiplier);

        injuryChance = Mathf.Clamp01(injuryChance * aggregate.injuryChanceMultiplier * Mathf.Clamp01(1f - (aggregate.woundChanceReductionPercent / 100f)));
        deathChance = Mathf.Clamp01(deathChance * aggregate.deathChanceMultiplier * Mathf.Clamp01(1f - (aggregate.deathChanceReductionPercent / 100f)));

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

    private static List<MonsterTrait> CollectPreviewMonsterTraits(Order order)
    {
        var list = new List<MonsterTrait>();
        if (order == null) return list;

        var caseData = order.investigationCase;
        if (caseData == null || caseData.truthTraits == null || caseData.confirmedTraitIds == null || caseData.confirmedTraitIds.Count == 0)
        {
            return list;
        }

        foreach (var trait in caseData.truthTraits)
        {
            if (trait == null || string.IsNullOrEmpty(trait.traitId)) continue;
            if (caseData.confirmedTraitIds.Exists(id => string.Equals(id, trait.traitId, StringComparison.OrdinalIgnoreCase)))
            {
                list.Add(trait);
            }
        }

        return list;
    }

    private static MonsterData GetTruthMonster(Order order)
    {
        return order != null ? order.monsterData : null;
    }

    private static MonsterData GetPreviewMonster(Order order)
    {
        if (order == null) return null;
        return order.declaredMonster != null ? order.declaredMonster : order.monsterData;
    }

    private static HashSet<string> BuildCounteredTraitSet(List<Hunter> party, Order order, MonsterData monsterForConditions, List<MonsterTrait> monsterTraits)
    {
        var countered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (party == null) return countered;

        int partySize = party.Count;
        foreach (var hunter in party)
        {
            var data = hunter?.Data;
            foreach (var kitchenCounter in KitchenManager.GetActiveCounteredTraitKeys(hunter))
            {
                if (!string.IsNullOrWhiteSpace(kitchenCounter))
                {
                    countered.Add(kitchenCounter);
                }
            }

            if (data == null || data.traits == null) continue;
            foreach (var trait in data.traits)
            {
                if (trait == null) continue;
                if (trait.counters != null)
                {
                    foreach (var counter in trait.counters)
                    {
                        if (counter == null) continue;
                        if (!string.IsNullOrEmpty(counter.traitId))
                        {
                            countered.Add(counter.traitId);
                        }
                        if (!string.IsNullOrEmpty(counter.displayName))
                        {
                            countered.Add(counter.displayName);
                        }
                    }
                }

                if (trait.conditionalCounters == null) continue;
                foreach (var conditionalCounter in trait.conditionalCounters)
                {
                    if (conditionalCounter == null || conditionalCounter.counteredTrait == null) continue;
                    if (!DoesConditionPass(conditionalCounter.condition, monsterForConditions, partySize, hunter, party, order, monsterTraits)) continue;

                    if (!string.IsNullOrEmpty(conditionalCounter.counteredTrait.traitId))
                    {
                        countered.Add(conditionalCounter.counteredTrait.traitId);
                    }
                    if (!string.IsNullOrEmpty(conditionalCounter.counteredTrait.displayName))
                    {
                        countered.Add(conditionalCounter.counteredTrait.displayName);
                    }
                }
            }
        }

        return countered;
    }

    private static bool IsCountered(MonsterTrait trait, HashSet<string> counteredTraits)
    {
        if (trait == null || counteredTraits == null) return false;

        // Match by ID if available
        if (!string.IsNullOrEmpty(trait.traitId) && counteredTraits.Contains(trait.traitId))
        {
            return true;
        }

        // Fallback: match by display name to be resilient to mismatched IDs
        if (!string.IsNullOrEmpty(trait.displayName))
        {
            return counteredTraits.Contains(trait.displayName);
        }

        return false;
    }

    private static void ApplyMonsterEffects(MonsterTrait trait, MonsterData monsterForConditions, ref float requiredMultiplier, ref float partyMultiplier, ref float injuryChance, ref float deathChance, ref bool guaranteeInjury, ref bool allowDeathWithoutInjury, ref float capSuccessLimit, ref float missionTimeMultiplier)
    {
        if (trait == null || trait.missionEffects == null) return;

        foreach (var effect in trait.missionEffects)
        {
            if (effect == null) continue;
            if (effect.targetMonster != null)
            {
                if (monsterForConditions != effect.targetMonster) continue;
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

    private static void ApplyHunterMissionEffects(MonsterData monsterForConditions, List<Hunter> party, ref float requiredMultiplier, ref float partyMultiplier, ref float injuryChance, ref float deathChance, ref bool guaranteeInjury, ref bool allowDeathWithoutInjury, ref float capSuccessLimit, ref float missionTimeMultiplier)
    {
        if (party == null) return;
        foreach (var hunter in party)
        {
            var data = hunter?.Data;
            if (data == null || data.traits == null) continue;
            foreach (var trait in data.traits)
            {
                if (trait == null || trait.missionEffects == null) continue;
                foreach (var effect in trait.missionEffects)
                {
                    if (effect == null) continue;
                    if (effect.targetMonster != null && monsterForConditions != effect.targetMonster) continue;
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
        }
    }

    private static HunterBonusAggregate AggregateHunterBonuses(Order order, MonsterData monster, List<MonsterTrait> monsterTraits, List<Hunter> party)
    {
        var aggregate = HunterBonusAggregate.CreateDefault();
        if (party == null) return aggregate;

        var appliedSingles = new HashSet<HunterTrait.BonusEffect>();
        int partySize = party.Count;

        foreach (var hunter in party)
        {
            float briefingBonus = BriefingRoomManager.GetActiveDailyBonus(hunter);
            if (Mathf.Abs(briefingBonus) > 0.01f)
            {
                aggregate.successChanceBonus += briefingBonus;
            }

            KitchenRecipe kitchenRecipe = KitchenManager.GetActiveRecipe(hunter);
            if (kitchenRecipe != null)
            {
                aggregate.successChanceBonus += kitchenRecipe.successChanceBonusPercent;
            }

            float missedSleepPenalty = DormitoryManager.GetActiveMissedSleepPenaltyPercent(hunter);
            if (missedSleepPenalty > 0.01f)
            {
                aggregate.successChanceBonus -= missedSleepPenalty;
            }

            var data = hunter?.Data;
            if (data == null || data.traits == null) continue;

            foreach (var trait in data.traits)
            {
                if (trait == null) continue;

                if (trait.bonusEffects == null) continue;
                foreach (var effect in trait.bonusEffects)
                {
                    if (effect == null) continue;
                    if (!DoesConditionPass(effect.condition, monster, partySize, hunter, party, order, monsterTraits)) continue;

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
                        case HunterTrait.BonusEffectType.MissionTimeMultiplier:
                            aggregate.missionTimeMultiplier *= effect.value <= 0f ? 1f : effect.value;
                            break;
                        case HunterTrait.BonusEffectType.UpkeepCostMultiplier:
                            // handled elsewhere
                            break;
                        case HunterTrait.BonusEffectType.RewardMultiplier:
                        case HunterTrait.BonusEffectType.RewardFlat:
                        case HunterTrait.BonusEffectType.GuardianSacrifice:
                        case HunterTrait.BonusEffectType.MentorGrantXP:
                        case HunterTrait.BonusEffectType.FailureRescueSuccessChancePercent:
                        case HunterTrait.BonusEffectType.RerollNegativeRolls:
                        case HunterTrait.BonusEffectType.RecruitmentRarityWeightMultiplier:
                        case HunterTrait.BonusEffectType.MaxLevelBonus:
                        case HunterTrait.BonusEffectType.XpRequirementMultiplier:
                            // handled in MissionResolver / upkeep hooks
                            break;
                    }
                }
            }
        }

        var kitchenAggregate = KitchenManager.GetActiveBuffAggregate(party);
        aggregate.woundChanceReductionPercent = kitchenAggregate.woundChanceReductionPercent;
        aggregate.deathChanceReductionPercent = kitchenAggregate.deathChanceReductionPercent;
        aggregate.missionTimeReductionPercent = kitchenAggregate.missionTimeReductionPercent;
        aggregate.successChanceBonus = Mathf.Clamp(aggregate.successChanceBonus, -MaxSuccessChance, MaxSuccessChance);
        return aggregate;
    }

    public static bool DoesConditionPass(HunterTrait.BonusCondition condition, MonsterData monster, int partySize)
    {
        return DoesConditionPass(condition, monster, partySize, null, null, null, null);
    }

    public static bool DoesConditionPass(HunterTrait.BonusCondition condition, MonsterData monster, int partySize, Hunter hunter, List<Hunter> party, Order order, List<MonsterTrait> monsterTraits)
    {
        return DoesConditionPassInternal(condition, monster, partySize, hunter, party, order, monsterTraits, rollProc: true);
    }

    public static bool DoesConditionPassWithoutProc(HunterTrait.BonusCondition condition, MonsterData monster, int partySize)
    {
        return DoesConditionPassInternal(condition, monster, partySize, null, null, null, null, rollProc: false);
    }

    public static bool DoesConditionPassWithoutProc(HunterTrait.BonusCondition condition, MonsterData monster, int partySize, Hunter hunter, List<Hunter> party, Order order, List<MonsterTrait> monsterTraits)
    {
        return DoesConditionPassInternal(condition, monster, partySize, hunter, party, order, monsterTraits, rollProc: false);
    }

    private static bool DoesConditionPassPreview(HunterTrait.BonusCondition condition, MonsterData monster, int partySize, Hunter hunter, List<Hunter> party, Order order, List<MonsterTrait> monsterTraits)
    {
        return DoesConditionPassInternal(condition, monster, partySize, hunter, party, order, monsterTraits, rollProc: false);
    }

    private static bool DoesConditionPassInternal(HunterTrait.BonusCondition condition, MonsterData monster, int partySize, Hunter hunter, List<Hunter> party, Order order, List<MonsterTrait> monsterTraits, bool rollProc)
    {
        if (condition == null) return true;

        float proc = condition.procChancePercent;
        if (proc <= 0f) proc = 100f; // treat unset as always apply
        if (rollProc && proc < 100f)
        {
            float roll = UnityEngine.Random.Range(0f, 100f);
            if (roll > Mathf.Max(0f, proc))
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

        if (condition.requiredMonsterTrait != null && !MonsterTraitsContain(monsterTraits, condition.requiredMonsterTrait))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(condition.requiredMonsterTraitId) && !MonsterTraitsContain(monsterTraits, condition.requiredMonsterTraitId))
        {
            return false;
        }

        if (condition.minMissionDurationSeconds > 0f)
        {
            if (order == null || order.missionDuration < condition.minMissionDurationSeconds)
            {
                return false;
            }
        }

        if (condition.maxMissionDurationSeconds > 0f)
        {
            if (order == null || order.missionDuration > condition.maxMissionDurationSeconds)
            {
                return false;
            }
        }

        if (condition.requiresWeakestInParty && !IsWeakestInParty(hunter, party))
        {
            return false;
        }

        return true;
    }

    private static bool MonsterTraitsContain(List<MonsterTrait> monsterTraits, MonsterTrait requiredTrait)
    {
        if (requiredTrait == null || monsterTraits == null) return false;
        foreach (var trait in monsterTraits)
        {
            if (trait == null) continue;
            if (trait == requiredTrait) return true;
            if (!string.IsNullOrEmpty(requiredTrait.traitId) && string.Equals(trait.traitId, requiredTrait.traitId, StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.IsNullOrEmpty(requiredTrait.displayName) && string.Equals(trait.displayName, requiredTrait.displayName, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static bool MonsterTraitsContain(List<MonsterTrait> monsterTraits, string requiredTraitId)
    {
        if (string.IsNullOrWhiteSpace(requiredTraitId) || monsterTraits == null) return false;
        foreach (var trait in monsterTraits)
        {
            if (trait == null) continue;
            if (!string.IsNullOrEmpty(trait.traitId) && string.Equals(trait.traitId, requiredTraitId, StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.IsNullOrEmpty(trait.displayName) && string.Equals(trait.displayName, requiredTraitId, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static bool IsWeakestInParty(Hunter hunter, List<Hunter> party)
    {
        if (hunter == null || party == null || party.Count == 0) return false;
        var hunterStats = hunter.GetStats();
        if (hunterStats == null) return false;

        float hunterPower = hunterStats.GetTotalPower();
        foreach (var partyHunter in party)
        {
            if (partyHunter == null) continue;
            var stats = partyHunter.GetStats();
            if (stats == null) continue;
            if (stats.GetTotalPower() < hunterPower)
            {
                return false;
            }
        }
        return true;
    }

    private static string BuildConditionSuffix(HunterTrait.BonusCondition condition)
    {
        if (condition == null) return string.Empty;
        if (condition.procChancePercent > 0f && condition.procChancePercent < 100f)
        {
            return $" ({condition.procChancePercent:0.#}% chance)";
        }
        return string.Empty;
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
        public float woundChanceReductionPercent;
        public float deathChanceReductionPercent;
        public float missionTimeReductionPercent;
        public float missionTimeMultiplier;

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
                minSuccessPercent = 0f,
                woundChanceReductionPercent = 0f,
                deathChanceReductionPercent = 0f,
                missionTimeReductionPercent = 0f,
                missionTimeMultiplier = 1f
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
