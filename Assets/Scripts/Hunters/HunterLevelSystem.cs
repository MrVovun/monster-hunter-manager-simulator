using UnityEngine;

public class HunterLevelSystem : MonoBehaviour
{
    private HunterData hunterData;
    private int currentLevel;
    private int currentXP;
    
    public void Initialize(HunterData data)
    {
        hunterData = data;
        if (hunterData != null)
        {
            currentLevel = hunterData.startingLevel;
            currentXP = hunterData.startingXP;
        }
    }
    
    public void AddXP(int amount)
    {
        currentXP += amount;
        
        // Check for level ups
        while (CanLevelUp())
        {
            int xpNeeded = GetXPForNextLevel();
            if (xpNeeded == int.MaxValue)
            {
                currentXP = Mathf.Min(currentXP, xpNeeded);
                break;
            }
            if (currentXP >= xpNeeded)
            {
                currentXP -= xpNeeded;
                currentLevel++;
            }
            else
            {
                break;
            }
        }
    }
    
    public bool CanLevelUp()
    {
        if (hunterData == null) return false;
        int xpNeeded = GetXPForNextLevel();
        return currentXP >= xpNeeded;
    }
    
    public int GetXPForNextLevel()
    {
        if (hunterData == null) return int.MaxValue;
        int xpRequirement = hunterData.GetXPRequirementForNextLevel(currentLevel);
        return xpRequirement > 0 ? xpRequirement : int.MaxValue;
    }
    
    public bool LevelUp()
    {
        // Manual level up (requires gold payment)
        if (CanLevelUp())
        {
            int xpNeeded = GetXPForNextLevel();
            if (currentXP >= xpNeeded)
            {
                currentXP -= xpNeeded;
                currentLevel++;
                return true;
            }
        }
        return false;
    }
    
    public int GetCurrentLevel()
    {
        return currentLevel;
    }
    
    public int GetCurrentXP()
    {
        return currentXP;
    }
    
    public int GetXPProgress()
    {
        return currentXP;
    }
    
    public float GetXPProgressPercent()
    {
        int xpForNext = GetXPForNextLevel();
        if (xpForNext == int.MaxValue || xpForNext <= 0) return 1f;
        return Mathf.Clamp01((float)currentXP / xpForNext);
    }
    
    public int GetLevelUpCost()
    {
        if (hunterData == null) return 0;
        // Cost scales with level: base cost * level
        int baseCost = 100;
        return baseCost * currentLevel;
    }

    public void DebugSetLevelAndXP(int level, int xp)
    {
        currentLevel = Mathf.Max(1, level);
        currentXP = Mathf.Max(0, xp);
    }
}
