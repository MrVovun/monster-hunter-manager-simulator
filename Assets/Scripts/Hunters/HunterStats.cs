using UnityEngine;

public class HunterStats : MonoBehaviour
{
    private HunterData hunterData;
    private int currentLevel;
    
    private int totalPower;
    private int totalDefense;
    private int totalResolve;
    
    public void Initialize(HunterData data, int level)
    {
        hunterData = data;
        currentLevel = level;
        CalculateStats();
    }
    
    public void UpdateLevel(int newLevel)
    {
        currentLevel = newLevel;
        CalculateStats();
    }
    
    private void CalculateStats()
    {
        if (hunterData == null) return;
        
        totalPower = hunterData.GetTotalPower(currentLevel);
        totalDefense = hunterData.GetTotalDefense(currentLevel);
        totalResolve = hunterData.GetTotalResolve(currentLevel);
    }
    
    public int GetPower()
    {
        return totalPower;
    }
    
    public int GetDefense()
    {
        return totalDefense;
    }
    
    public int GetResolve()
    {
        return totalResolve;
    }
    
    public int GetTotalPower()
    {
        // Total power for mission calculations (power + defense + resolve)
        return totalPower + totalDefense + totalResolve;
    }
    
}
