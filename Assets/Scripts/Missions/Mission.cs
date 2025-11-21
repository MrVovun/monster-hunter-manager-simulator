using System.Collections.Generic;
using UnityEngine;

public class Mission
{
    public Order order;
    public List<Hunter> party;
    public MissionTimer timer;
    public bool isResolved = false;
    
    public Mission(Order order, List<Hunter> party)
    {
        this.order = order;
        this.party = party;
    }
    
    public int CalculatePartyPower()
    {
        int totalPower = 0;
        foreach (var hunter in party)
        {
            if (hunter != null && hunter.GetStats() != null)
            {
                totalPower += hunter.GetStats().GetTotalPower();
            }
        }
        return totalPower;
    }
    
    public float CalculateSuccessChance()
    {
        if (order == null || party == null || party.Count == 0) return 0f;
        
        int partyPower = CalculatePartyPower();
        int difficulty = order.difficulty;
        
        // Base calculation: party power vs difficulty
        float baseChance = 50f; // 50% base chance
        float powerDifference = partyPower - difficulty;
        float chanceModifier = powerDifference * 2f; // Each point of power difference = 2% chance
        
        float successChance = baseChance + chanceModifier;
        
        // Apply trait modifiers
        foreach (var hunter in party)
        {
            if (hunter != null && hunter.GetStats() != null)
            {
                successChance += hunter.GetStats().GetSuccessChanceModifier();
            }
        }
        
        // Add some randomness
        successChance += Random.Range(-5f, 5f);
        
        // Clamp between 5% and 95%
        successChance = Mathf.Clamp(successChance, 5f, 95f);
        
        return successChance;
    }
}

