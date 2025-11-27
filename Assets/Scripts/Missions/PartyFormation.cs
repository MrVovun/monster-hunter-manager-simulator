using System.Collections.Generic;
using UnityEngine;

public class PartyFormation : MonoBehaviour
{
    [Header("Party Settings")]
    [SerializeField] private int maxPartySize = 5;
    
    private List<Hunter> currentParty = new List<Hunter>();
    private Order currentOrder;
    
    public void Initialize(Order order)
    {
        currentOrder = order;
        currentParty.Clear();
        
        if (order != null)
        {
            maxPartySize = order.maxPartySize;

            if (order.assignedHunters != null)
            {
                foreach (var hunter in order.assignedHunters)
                {
                    if (hunter != null && currentParty.Count < maxPartySize)
                    {
                        currentParty.Add(hunter);
                    }
                }
            }
        }
    }
    
    public bool AddHunter(Hunter hunter)
    {
        if (hunter == null) return false;
        if (currentParty.Count >= maxPartySize) return false;
        if (hunter.GetState() != HunterState.Idle) return false;
        if (currentParty.Contains(hunter)) return false;
        
        currentParty.Add(hunter);
        return true;
    }
    
    public bool RemoveHunter(Hunter hunter)
    {
        return currentParty.Remove(hunter);
    }
    
    public void ClearParty()
    {
        currentParty.Clear();
    }
    
    public List<Hunter> GetParty()
    {
        return new List<Hunter>(currentParty);
    }
    
    public int GetPartySize()
    {
        return currentParty.Count;
    }
    
    public int GetMaxPartySize()
    {
        return maxPartySize;
    }
    
    public bool IsPartyValid()
    {
        if (currentOrder == null) return false;
        return currentParty.Count >= currentOrder.minPartySize && 
               currentParty.Count <= currentOrder.maxPartySize;
    }
    
    public float CalculateSuccessChance()
    {
        if (currentOrder == null || currentParty.Count == 0) return 0f;
        
        Mission mission = new Mission(currentOrder, currentParty);
        return mission.CalculateSuccessChance();
    }
    
    public string GetRiskLevel()
    {
        float chance = CalculateSuccessChance();
        
        if (chance >= 80f) return "Low Risk";
        if (chance >= 60f) return "Moderate Risk";
        if (chance >= 40f) return "High Risk";
        return "Very High Risk";
    }
    
    public Color GetRiskColor()
    {
        float chance = CalculateSuccessChance();
        
        if (chance >= 80f) return Color.green;
        if (chance >= 60f) return Color.yellow;
        if (chance >= 40f) return new Color(1f, 0.5f, 0f); // Orange
        return Color.red;
    }
}

