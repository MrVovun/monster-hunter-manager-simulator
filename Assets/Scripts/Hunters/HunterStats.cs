using UnityEngine;

public class HunterStats : MonoBehaviour
{
    private HunterData hunterData;
    private int currentLevel;
    private int totalPower;
    private bool debugOverrideActive;
    private int debugOverridePower;
    
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
        if (debugOverrideActive) return;
        totalPower = hunterData.GetTotalPower(currentLevel);
    }
    
    public int GetTotalPower()
    {
        return debugOverrideActive ? debugOverridePower : totalPower;
    }
    
    public void SetDebugPowerOverride(int value)
    {
        debugOverrideActive = true;
        debugOverridePower = Mathf.Max(0, value);
    }

    public void ClearDebugPowerOverride()
    {
        debugOverrideActive = false;
        CalculateStats();
    }

    public bool HasDebugPowerOverride()
    {
        return debugOverrideActive;
    }
}
