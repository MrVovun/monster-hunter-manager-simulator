using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MissionReport
{
    public Order order;
    public bool success;
    public int goldEarned;
    public int reputationGained;
    
    [System.Serializable]
    public class HunterResult
    {
        public Hunter hunter;
        public bool survived;
        public bool injured;
        public bool died;
        public int xpGained;
        public bool leveledUp;
    }
    
    public List<HunterResult> hunterResults = new List<HunterResult>();
    
    public MissionReport()
    {
        hunterResults = new List<HunterResult>();
    }
    
    public int GetTotalXP()
    {
        int total = 0;
        foreach (var result in hunterResults)
        {
            total += result.xpGained;
        }
        return total;
    }
    
    public int GetSurvivorsCount()
    {
        int count = 0;
        foreach (var result in hunterResults)
        {
            if (result.survived && !result.died)
            {
                count++;
            }
        }
        return count;
    }
    
    public int GetDeathsCount()
    {
        int count = 0;
        foreach (var result in hunterResults)
        {
            if (result.died)
            {
                count++;
            }
        }
        return count;
    }
    
    public int GetInjuriesCount()
    {
        int count = 0;
        foreach (var result in hunterResults)
        {
            if (result.injured)
            {
                count++;
            }
        }
        return count;
    }
}

