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
        
        // Calculate rewards
        if (report.success)
        {
            report.goldEarned = order.goldReward;
            report.reputationGained = Mathf.Max(0f, order.reputationReward);
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
            bool hunterInjured = guaranteedInjury;

            float injuryRisk = baseInjuryRisk;
            float deathRisk = baseDeathRisk;

            if (shouldRollInjury)
            {
                hunterInjured = Random.value < injuryRisk;
            }

            bool deathPrevented = successPreventsDeath || outcome.DeathPreventionActive;
            bool requiresInjuryForDeath = !outcome.AllowDeathWithoutInjury;
            bool canDie = !deathPrevented && (!requiresInjuryForDeath || hunterInjured);
            bool hunterDied = canDie && (Random.value < deathRisk);

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
                repManager.AddReputation(report.reputationGained);
            }
        }
        
        return report;
    }
}
