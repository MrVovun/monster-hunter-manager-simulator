using System.Collections.Generic;
using UnityEngine;

public enum OrderState
{
    Offered,      // Currently shown via client interaction
    Accepted,     // In Orders tab, awaiting party assignment
    InProgress,   // Party assigned, mission timer running
    Completed,    // Resolved successfully
    Failed,       // Resolved unsuccessfully
    Expired       // Not sent before PrepTimeLimit
}

[System.Serializable]
public class Order
{
    public string orderTitle;
    public string description;
    public MonsterType monsterType;
    public int difficulty;
    public int goldReward;
    public int xpReward;
    public float missionDuration; // In game seconds
    public float prepTimeLimit; // Time to assign party
    public int maxPartySize;
    public int minPartySize;
    public OrderState state;
    
    // Runtime data
    public List<Hunter> assignedHunters = new List<Hunter>();
    public MissionTimer prepTimer;
    public MissionTimer missionTimer;
    public System.Guid orderId;
    
    public Order()
    {
        orderId = System.Guid.NewGuid();
        assignedHunters = new List<Hunter>();
    }
    
    public bool IsActive()
    {
        return state == OrderState.Accepted || state == OrderState.InProgress;
    }
    
    public bool CanAssignParty()
    {
        return state == OrderState.Accepted && assignedHunters.Count < maxPartySize;
    }
    
    public int GetAssignedPartySize()
    {
        return assignedHunters.Count;
    }
}

