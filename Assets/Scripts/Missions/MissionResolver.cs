using System.Collections.Generic;
using UnityEngine;

public class MissionResolver : MonoBehaviour
{
    [Header("Resolution Settings")]
    [SerializeField] private float baseInjuryChance = 0.2f; // 20% base chance
    [SerializeField] private float baseDeathChance = 0.05f; // 5% base chance
    [SerializeField] private float failureInjuryMultiplier = 2f; // Failed missions = higher injury risk
    [SerializeField] private float failureDeathMultiplier = 3f;
    
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
        float successChance = mission.CalculateSuccessChance();
        report.success = Random.Range(0f, 100f) < successChance;
        
        // Calculate rewards
        if (report.success)
        {
            report.goldEarned = order.goldReward;
            report.reputationGained = Mathf.Max(1, order.difficulty / 10); // Reputation based on difficulty
        }
        else
        {
            report.goldEarned = order.goldReward / 2; // Half reward on failure
            report.reputationGained = 0;
        }
        
        // Resolve each hunter
        foreach (var hunter in party)
        {
            if (hunter == null) continue;
            
            MissionReport.HunterResult result = new MissionReport.HunterResult();
            result.hunter = hunter;
            
            // Track level before awarding XP so we can report level-ups correctly
            int levelBeforeMission = hunter.GetLevel();
            
            // Calculate injury/death risk
            float injuryRisk = baseInjuryChance;
            float deathRisk = baseDeathChance;
            
            if (!report.success)
            {
                injuryRisk *= failureInjuryMultiplier;
                deathRisk *= failureDeathMultiplier;
            }
            
            // Apply trait modifiers
            if (hunter.GetStats() != null)
            {
                injuryRisk *= hunter.GetStats().GetInjuryRiskModifier();
                deathRisk *= hunter.GetStats().GetDeathRiskModifier();
            }
            
            // Roll for death first (if dead, can't be injured)
            result.died = Random.Range(0f, 1f) < deathRisk;
            
            if (result.died)
            {
                result.survived = false;
                result.injured = false;
                hunter.SetState(HunterState.Dead);
            }
            else
            {
                result.survived = true;
                result.injured = Random.Range(0f, 1f) < injuryRisk;
            }
            
            // Calculate XP gain
            if (result.survived)
            {
                result.xpGained = report.success ? order.xpReward : order.xpReward / 2;
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
            if (repManager != null && report.reputationGained > 0)
            {
                repManager.AddReputation(report.reputationGained);
            }
        }
        
        return report;
    }
}

