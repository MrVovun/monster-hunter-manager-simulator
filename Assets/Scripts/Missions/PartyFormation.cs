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
        if (!hunter.IsAvailableForOrders()) return false;
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

        return MissionOutcomeCalculator.EvaluatePreview(currentOrder, currentParty).SuccessChancePercent;
    }
    
    public string GetRiskLevel()
    {
        float chance = CalculateSuccessChance();
        if (chance >= 150f) return "Overwhelming";
        if (chance >= 100f) return "Safe";
        if (chance >= 75f) return "Steady";
        if (chance >= 50f) return "Risky";
        if (chance >= 25f) return "Severe";
        return "Dire";
    }
    
    public Color GetRiskColor()
    {
        float chance = CalculateSuccessChance();
        if (chance >= 150f) return new Color(0.2f, 0.8f, 0.2f);
        if (chance >= 100f) return Color.green;
        if (chance >= 75f) return Color.yellow;
        if (chance >= 50f) return new Color(1f, 0.6f, 0.1f);
        if (chance >= 25f) return new Color(1f, 0.4f, 0.1f);
        return Color.red;
    }
}
