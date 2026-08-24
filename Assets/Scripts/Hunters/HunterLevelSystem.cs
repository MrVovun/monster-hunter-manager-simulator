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
        currentXP = Mathf.Max(0, currentXP + amount);
    }
    
    public bool CanLevelUp()
    {
        if (hunterData == null) return false;
        if (currentLevel >= GetMaxLevel()) return false;
        int xpNeeded = GetXPForNextLevel();
        return currentXP >= xpNeeded;
    }
    
    public int GetXPForNextLevel()
    {
        if (hunterData == null) return int.MaxValue;
        if (currentLevel >= GetMaxLevel()) return int.MaxValue;

        int xpRequirement = hunterData.GetXPRequirementForNextLevel(currentLevel);
        if (xpRequirement <= 0)
        {
            xpRequirement = hunterData.GetLastDefinedXPRequirement();
        }

        if (xpRequirement <= 0) return int.MaxValue;
        float multiplier = GetXpRequirementMultiplier();
        return Mathf.Max(1, Mathf.RoundToInt(xpRequirement * multiplier));
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
        return hunterData.GetLevelUpCostForLevel(currentLevel + 1);
    }

    private int GetMaxLevel()
    {
        if (hunterData == null) return currentLevel;
        int maxLevel = hunterData.GetMaxDefinedLevel();
        maxLevel += Mathf.Max(0, Mathf.RoundToInt(GetTraitEffectTotal(HunterTrait.BonusEffectType.MaxLevelBonus)));
        return Mathf.Max(hunterData.startingLevel, maxLevel);
    }

    private float GetXpRequirementMultiplier()
    {
        float multiplier = 1f;
        var traits = hunterData != null ? hunterData.traits : null;
        if (traits == null) return multiplier;

        foreach (var trait in traits)
        {
            if (trait == null || trait.bonusEffects == null) continue;
            foreach (var effect in trait.bonusEffects)
            {
                if (effect == null || effect.bonusType != HunterTrait.BonusEffectType.XpRequirementMultiplier) continue;
                multiplier *= effect.value <= 0f ? 1f : effect.value;
            }
        }

        return Mathf.Max(0.01f, multiplier);
    }

    private float GetTraitEffectTotal(HunterTrait.BonusEffectType effectType)
    {
        float total = 0f;
        var traits = hunterData != null ? hunterData.traits : null;
        if (traits == null) return total;

        foreach (var trait in traits)
        {
            if (trait == null || trait.bonusEffects == null) continue;
            foreach (var effect in trait.bonusEffects)
            {
                if (effect != null && effect.bonusType == effectType)
                {
                    total += effect.value;
                }
            }
        }

        return total;
    }

    public void DebugSetLevelAndXP(int level, int xp)
    {
        currentLevel = Mathf.Max(1, level);
        currentXP = Mathf.Max(0, xp);
    }
}
