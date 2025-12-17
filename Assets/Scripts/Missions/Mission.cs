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
        var result = MissionOutcomeCalculator.Evaluate(order, party);
        return result.SuccessChancePercent;
    }
}
