using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Hunter", menuName = "Guild Manager/Hunter")]
public class HunterData : ScriptableObject
{
    [Header("Basic Info")]
    public string hunterName;
    private static GlobalHunterConfig cachedConfig;
    public GlobalHunterConfig.RarityType rarity = GlobalHunterConfig.RarityType.Common;
    [TextArea(3, 6)] public string bio;
    
    [Header("Base Stats")]
    [Range(1, 100)]
    public int basePower = 10;
    [Range(1, 100)]
    public int baseDefense = 10;
    [Range(1, 100)]
    public int baseResolve = 10;
    
    [Header("Traits")]
    public List<HunterTrait> traits = new List<HunterTrait>();
    
    [Header("Economy")]
    public int dailyUpkeepCost = 10;
    
    [Header("Progression")]
    public int startingLevel = 1;
    public int startingXP = 0;
    [SerializeField] private List<LevelXPRequirement> levelXPTable = new List<LevelXPRequirement>();
    
    [Header("Unlock Requirements")]
    public int minReputation = 0; // Minimum reputation to unlock this hunter
    
    [Header("Visual")]
    public Sprite portrait; // For UI display
    
    // Calculated stats (base + level bonuses)
    public int GetPowerAtLevel(int level)
    {
        int levelBonus = (level - 1) * 2; // +2 power per level
        return basePower + levelBonus;
    }
    
    public int GetDefenseAtLevel(int level)
    {
        int levelBonus = (level - 1) * 2; // +2 defense per level
        return baseDefense + levelBonus;
    }
    
    public int GetResolveAtLevel(int level)
    {
        int levelBonus = (level - 1) * 2; // +2 resolve per level
        return baseResolve + levelBonus;
    }
    
    public int GetTotalPower(int level)
    {
        int power = GetPowerAtLevel(level);
        foreach (var trait in traits)
        {
            power += Mathf.RoundToInt(power * trait.powerModifier);
        }
        return power;
    }
    
    public int GetTotalDefense(int level)
    {
        int defense = GetDefenseAtLevel(level);
        foreach (var trait in traits)
        {
            defense += Mathf.RoundToInt(defense * trait.defenseModifier);
        }
        return defense;
    }
    
    public int GetTotalResolve(int level)
    {
        int resolve = GetResolveAtLevel(level);
        foreach (var trait in traits)
        {
            resolve += Mathf.RoundToInt(resolve * trait.resolveModifier);
        }
        return resolve;
    }

    public int GetXPRequirementForLevel(int level)
    {
        if (levelXPTable == null || levelXPTable.Count == 0 || level <= startingLevel)
        {
            return -1;
        }

        foreach (var entry in levelXPTable)
        {
            if (entry != null && entry.level == level)
            {
                return Mathf.Max(1, entry.requiredXP);
            }
        }

        return -1;
    }

    public int GetXPRequirementForNextLevel(int currentLevel)
    {
        return GetXPRequirementForLevel(currentLevel + 1);
    }

    public int GetMaxDefinedLevel()
    {
        int max = startingLevel;
        if (levelXPTable != null)
        {
            foreach (var entry in levelXPTable)
            {
                if (entry != null)
                {
                    max = Mathf.Max(max, entry.level);
                }
            }
        }
        return max;
    }

    private void OnValidate()
    {
        if (levelXPTable != null)
        {
            levelXPTable.Sort((a, b) =>
            {
                if (a == null && b == null) return 0;
                if (a == null) return 1;
                if (b == null) return -1;
                return a.level.CompareTo(b.level);
            });
        }
    }

    [System.Serializable]
    public class LevelXPRequirement
    {
        [Min(2)] public int level = 2;
        [Min(1)] public int requiredXP = 100;
    }

    public static GlobalHunterConfig GetGlobalConfig()
    {
        if (cachedConfig == null)
        {
            cachedConfig = Resources.Load<GlobalHunterConfig>("GlobalHunterConfig");
        }
        return cachedConfig;
    }
}
