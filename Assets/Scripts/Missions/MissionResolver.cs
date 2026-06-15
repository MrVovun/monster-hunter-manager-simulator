using System.Collections.Generic;
using UnityEngine;

public class MissionResolver : MonoBehaviour
{
    public MissionReport ResolveMission(Order order, List<Hunter> party)
    {
        if (order == null || party == null || party.Count == 0)
        {
            Debug.LogWarning("Cannot resolve mission: invalid order or party");
            return null;
        }
        
        MissionReport report = new MissionReport();
        report.order = order;
        
        // Calculate success
        Mission mission = new Mission(order, party);
        MissionOutcomeConfig config = MissionOutcomeConfig.FromGameConfig(GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null);
        MissionOutcomeResult outcome = MissionOutcomeCalculator.Evaluate(order, party, config);
        float successChance = outcome.SuccessChancePercent;
        bool guaranteedSuccess = successChance >= 100f;
        float successRollThreshold = Mathf.Clamp(successChance, 0f, 100f);
        report.success = guaranteedSuccess || Random.Range(0f, 100f) < successRollThreshold;
        HashSet<Hunter> forcedWoundedHunters = new HashSet<Hunter>();
        if (!report.success)
        {
            report.success = TryApplyFailureRescue(order, party, forcedWoundedHunters);
        }
        
        // Calculate rewards
        if (report.success)
        {
            // Apply reward modifiers from hunter traits
            float rewardMultiplier = 1f;
            float rewardFlat = 0f;
            ApplyRewardBonuses(order, party, ref rewardMultiplier, ref rewardFlat);

            float gold = order.goldReward * rewardMultiplier + rewardFlat;
            report.goldEarned = Mathf.Max(0, Mathf.RoundToInt(gold));
            report.reputationGained = Mathf.Max(0f, order.reputationPointsReward);
        }
        else
        {
            report.goldEarned = order.goldReward / 2; // Half reward on failure
            report.reputationGained = 0f;
        }
        
        int successXpReward = Mathf.Max(0, Mathf.RoundToInt(order.xpReward + Mathf.Max(0f, outcome.AdditionalSuccessXP)));
        int failureXpReward = Mathf.Max(0, order.xpReward / 2);

        bool successPreventsInjury = outcome.InjuryProtectionFromSuccess;
        bool successPreventsDeath = outcome.DeathProtectionFromSuccess;
        bool guaranteedInjury = outcome.InjuriesGuaranteed && !outcome.InjuryPreventionActive;
        float baseInjuryRisk = Mathf.Clamp01(outcome.FinalInjuryChance);
        float baseDeathRisk = Mathf.Clamp01(outcome.FinalDeathChance);

        // Resolve each hunter
        foreach (var hunter in party)
        {
            if (hunter == null) continue;
            
            MissionReport.HunterResult result = new MissionReport.HunterResult();
            result.hunter = hunter;
            
            // Track level before awarding XP so we can report level-ups correctly
            int levelBeforeMission = hunter.GetLevel();
            
            bool injuryPrevented = successPreventsInjury || outcome.InjuryPreventionActive;
            bool shouldRollInjury = !guaranteedInjury && !injuryPrevented;
            bool hunterInjured = guaranteedInjury || forcedWoundedHunters.Contains(hunter);

            float injuryRisk = baseInjuryRisk;
            float deathRisk = baseDeathRisk;

            if (shouldRollInjury && !hunterInjured)
            {
                hunterInjured = RollNegativeChance(injuryRisk, HasRerollNegativeRolls(hunter));
            }

            bool deathPrevented = successPreventsDeath || outcome.DeathPreventionActive;
            bool requiresInjuryForDeath = !outcome.AllowDeathWithoutInjury;
            bool canDie = !deathPrevented && (!requiresInjuryForDeath || hunterInjured);
            bool hunterDied = canDie && RollNegativeChance(deathRisk, HasRerollNegativeRolls(hunter));

            result.died = hunterDied;
            if (hunterDied)
            {
                result.survived = false;
                result.injured = hunterInjured;
                hunter.SetState(HunterState.Dead);
            }
            else
            {
                result.survived = true;
                result.injured = hunterInjured;
                if (hunterInjured)
                {
                    var state = hunter.GetComponent<HunterInteractionState>();
                    if (state == null)
                    {
                        state = hunter.gameObject.AddComponent<HunterInteractionState>();
                    }
                    state.SetWounded(true);
                }
            }
            
            // Calculate XP gain
            if (result.survived)
            {
                result.xpGained = report.success ? successXpReward : failureXpReward;
                hunter.GainXP(result.xpGained);
                result.leveledUp = hunter.GetLevel() > levelBeforeMission;
            }
            
            report.hunterResults.Add(result);
        }

        ApplyGuardianSacrifice(report);
        
        // Apply rewards
        if (GameManager.Instance != null)
        {
            GoldManager goldManager = GameManager.Instance.GetGoldManager();
            if (goldManager != null)
            {
                goldManager.AddGold(report.goldEarned);
            }
            
            ReputationManager repManager = GameManager.Instance.GetReputationManager();
            if (repManager != null && report.reputationGained > 0f)
            {
                repManager.AddReputationPoints(report.reputationGained);
            }
        }
        
        return report;
    }

    private void ApplyRewardBonuses(Order order, List<Hunter> party, ref float rewardMultiplier, ref float rewardFlat)
    {
        if (party == null) return;
        int partySize = party.Count;
        List<MonsterTrait> monsterTraits = order != null && order.investigationCase != null ? order.investigationCase.truthTraits : null;

        foreach (var hunter in party)
        {
            var data = hunter?.Data;
            if (data == null || data.traits == null) continue;
            foreach (var trait in data.traits)
            {
                if (trait == null || trait.bonusEffects == null) continue;
                foreach (var effect in trait.bonusEffects)
                {
                    if (effect == null) continue;
                    if (effect.bonusType != HunterTrait.BonusEffectType.RewardMultiplier &&
                        effect.bonusType != HunterTrait.BonusEffectType.RewardFlat) continue;

                    MonsterData monster = order != null ? order.monsterData : null;
                    if (!MissionOutcomeCalculator.DoesConditionPassWithoutProc(effect.condition, monster, partySize, hunter, party, order, monsterTraits))
                    {
                        continue;
                    }

                    bool conditionPassed = MissionOutcomeCalculator.DoesConditionPass(effect.condition, monster, partySize, hunter, party, order, monsterTraits);
                    if (!conditionPassed && HasRerollNegativeRolls(hunter))
                    {
                        conditionPassed = MissionOutcomeCalculator.DoesConditionPass(effect.condition, monster, partySize, hunter, party, order, monsterTraits);
                    }

                    if (!conditionPassed) continue;

                    if (effect.bonusType == HunterTrait.BonusEffectType.RewardMultiplier)
                    {
                        float mult = effect.value <= 0f ? 1f : effect.value;
                        rewardMultiplier *= mult;
                    }
                    else if (effect.bonusType == HunterTrait.BonusEffectType.RewardFlat)
                    {
                        rewardFlat += effect.value;
                    }
                }
            }
        }
    }

    private void ApplyGuardianSacrifice(MissionReport report)
    {
        if (report == null || report.hunterResults == null || report.hunterResults.Count == 0) return;

        // find first guardian who is alive
        MissionReport.HunterResult guardian = null;
        foreach (var hr in report.hunterResults)
        {
            if (hr == null || hr.hunter == null) continue;
            var data = hr.hunter.Data;
            if (data == null || data.traits == null) continue;
            bool hasGuardian = data.traits.Exists(t => t != null && t.bonusEffects != null &&
                t.bonusEffects.Exists(be => be.bonusType == HunterTrait.BonusEffectType.GuardianSacrifice));
            if (hasGuardian && !hr.died)
            {
                guardian = hr;
                break;
            }
        }

        if (guardian == null) return;

        // find first death to redirect
        MissionReport.HunterResult victim = report.hunterResults.Find(hr => hr != null && hr.died);
        if (victim == null) return;

        // revive victim
        victim.died = false;
        victim.survived = true;
        if (victim.hunter != null && victim.hunter.GetState() == HunterState.Dead)
        {
            victim.hunter.SetState(victim.injured ? HunterState.Idle : HunterState.Idle);
        }

        // guardian dies
        guardian.died = true;
        guardian.survived = false;
        if (guardian.hunter != null)
        {
            guardian.hunter.SetState(HunterState.Dead);
        }
    }

    private bool TryApplyFailureRescue(Order order, List<Hunter> party, HashSet<Hunter> forcedWoundedHunters)
    {
        if (order == null || party == null || forcedWoundedHunters == null) return false;
        int partySize = party.Count;
        List<MonsterTrait> monsterTraits = order.investigationCase != null ? order.investigationCase.truthTraits : null;

        foreach (var hunter in party)
        {
            var data = hunter?.Data;
            if (data == null || data.traits == null) continue;

            foreach (var trait in data.traits)
            {
                if (trait == null || trait.bonusEffects == null) continue;
                foreach (var effect in trait.bonusEffects)
                {
                    if (effect == null || effect.bonusType != HunterTrait.BonusEffectType.FailureRescueSuccessChancePercent) continue;
                    if (!MissionOutcomeCalculator.DoesConditionPass(effect.condition, order.monsterData, partySize, hunter, party, order, monsterTraits)) continue;

                    float chance = Mathf.Clamp(effect.value, 0f, 100f);
                    if (Random.Range(0f, 100f) <= chance)
                    {
                        forcedWoundedHunters.Add(hunter);
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool RollNegativeChance(float chance01, bool rerollOnce)
    {
        chance01 = Mathf.Clamp01(chance01);
        float roll = Random.value;
        if (rerollOnce)
        {
            roll = Mathf.Max(roll, Random.value);
        }

        return roll < chance01;
    }

    private static bool HasRerollNegativeRolls(Hunter hunter)
    {
        var data = hunter?.Data;
        if (data == null || data.traits == null) return false;

        foreach (var trait in data.traits)
        {
            if (trait == null || trait.bonusEffects == null) continue;
            foreach (var effect in trait.bonusEffects)
            {
                if (effect != null && effect.bonusType == HunterTrait.BonusEffectType.RerollNegativeRolls)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
