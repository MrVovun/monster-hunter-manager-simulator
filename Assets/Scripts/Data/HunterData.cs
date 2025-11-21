using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Hunter", menuName = "Guild Manager/Hunter")]
public class HunterData : ScriptableObject
{
    [Header("Basic Info")]
    public string hunterName;
    public HunterClass hunterClass;
    public int rarity = 1; // For future use
    
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
    public int xpPerLevel = 100; // XP needed per level
    
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
}

