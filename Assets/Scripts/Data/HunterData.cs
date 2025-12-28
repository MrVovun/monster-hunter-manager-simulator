using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Hunter", menuName = "Guild Manager/Hunter")]
public class HunterData : ScriptableObject
{
    [Header("Basic Info")]
    public string hunterName;
    public string hunterId;
    private static GlobalHunterConfig cachedConfig;
    public GlobalHunterConfig.RarityType rarity = GlobalHunterConfig.RarityType.Common;
    [TextArea(3, 6)] public string bio;
    
    [Header("Base Stats")]
    [Tooltip("Base combined combat power at level 1.")]
    [Range(1, 200)]
    public int basePower = 10;
    [Tooltip("Amount of combat power gained each level.")]
    [Range(0, 50)]
    public int powerPerLevel = 2;

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
    public GameObject visualPrefab;

    
    // Calculated stats (base + level bonuses)
    public int GetTotalPower(int level)
    {
        int levelBonus = Mathf.Max(0, level - 1) * powerPerLevel;
        return basePower + levelBonus;
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
        if (string.IsNullOrWhiteSpace(hunterId))
        {
            hunterId = System.Guid.NewGuid().ToString("N");
        }

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
        if (cachedConfig != null) return cachedConfig;

        if (GameManager.Instance != null)
        {
            cachedConfig = GameManager.Instance.GetGameConfig() != null
                ? GameManager.Instance.GetGameConfig().hunterConfig
                : null;
        }

        if (cachedConfig == null)
        {
            cachedConfig = Resources.Load<GlobalHunterConfig>("GlobalHunterConfig");
        }

        return cachedConfig;
    }
}
