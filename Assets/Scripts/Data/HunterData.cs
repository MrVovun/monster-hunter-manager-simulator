using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "New Hunter", menuName = "Guild Manager/Hunter")]
public class HunterData : ScriptableObject
{
    [Header("Basic Info")]
    public string hunterName;
    public string hunterId;
    private static GlobalHunterConfig cachedConfig;
    public GlobalHunterConfig.RarityType rarity = GlobalHunterConfig.RarityType.Common;
    [TextArea(3, 6)] public string bio;

    [Header("Stat Block")]
    [Tooltip("Optional shared stat preset. Non-overridden stats below are copied from this asset in the editor.")]
    public HunterStatBlock statBlock;
    public HunterStatOverrides statOverrides = new HunterStatOverrides();
    
    [SerializeField, HideInInspector, FormerlySerializedAs("basePower")]
    private int legacyBasePower = 10;
    [SerializeField, HideInInspector, FormerlySerializedAs("powerPerLevel")]
    private int legacyPowerPerLevel = 2;
    [SerializeField, HideInInspector, FormerlySerializedAs("dailyUpkeepCost")]
    private int legacyDailyUpkeepCost = 10;
    [SerializeField, HideInInspector]
    private bool migratedLevelPower;
    [SerializeField, HideInInspector]
    private bool migratedLevelUpkeep;
    [SerializeField, HideInInspector]
    private bool migratedLevelUpCost;

    [Header("Traits")]
    public List<HunterTrait> traits = new List<HunterTrait>();
    
    [Header("Progression")]
    public int startingLevel = 1;
    public int startingXP = 0;
    [SerializeField] private List<LevelXPRequirement> levelXPTable = new List<LevelXPRequirement>();
    
    [Header("Unlock Requirements")]
    public int minReputation = 0; // Minimum reputation to unlock this hunter
    
    [Header("Visual")]
    public Sprite portrait; // For UI display
    public GameObject visualPrefab;
    [Tooltip("Optional P09 modular preset. When assigned, the hunter uses the preset's base visual prefab and applies its modular parts at spawn time.")]
    public P09HumanoidPreset p09VisualPreset;

    [Header("Dialogue")]
    [TextArea(2, 4)] public string greeting;
    [TextArea(2, 4)] public string healLine;
    [TextArea(2, 4)] public string goodbyeLine;
    public List<HunterDialogueQuestion> dialogueQuestions = new List<HunterDialogueQuestion>();
    public List<HunterMorningDialogueLine> morningDialogueLines = new List<HunterMorningDialogueLine>();

    
    private void OnEnable()
    {
        EnsureId();
        MigrateLegacyPowerTableIfNeeded();
        MigrateLegacyUpkeepTableIfNeeded();
        MigrateLegacyLevelUpCostTableIfNeeded();
        ApplyStatBlockDefaults();
        NormalizeStats();
    }

    // Calculated stats from the per-level table.
    public int GetTotalPower(int level)
    {
        level = Mathf.Max(1, level);
        if (levelXPTable == null || levelXPTable.Count == 0)
        {
            return GetLegacyPowerForLevel(level);
        }

        LevelXPRequirement bestEntry = null;
        foreach (var entry in levelXPTable)
        {
            if (entry == null) continue;
            if (entry.level == level)
            {
                return Mathf.Max(1, entry.power);
            }

            if (entry.level <= level && (bestEntry == null || entry.level > bestEntry.level))
            {
                bestEntry = entry;
            }
        }

        if (bestEntry != null)
        {
            return Mathf.Max(1, bestEntry.power);
        }

        foreach (var entry in levelXPTable)
        {
            if (entry != null)
            {
                return Mathf.Max(1, entry.power);
            }
        }

        return GetLegacyPowerForLevel(level);
    }

    public int GetUpkeepCost(int level)
    {
        level = Mathf.Max(1, level);
        var entry = GetBestLevelEntry(level);
        if (entry != null)
        {
            return Mathf.Max(0, entry.upkeep);
        }

        return Mathf.Max(0, legacyDailyUpkeepCost);
    }

    public int GetLevelUpCostForLevel(int level)
    {
        level = Mathf.Max(1, level);
        var entry = GetBestLevelEntry(level);
        if (entry != null)
        {
            return Mathf.Max(0, entry.levelUpCost);
        }

        return GetLegacyLevelUpCostForLevel(level);
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

    public int GetLastDefinedXPRequirement()
    {
        int level = startingLevel;
        int requirement = -1;
        if (levelXPTable != null)
        {
            foreach (var entry in levelXPTable)
            {
                if (entry == null) continue;
                if (entry.level >= level)
                {
                    level = entry.level;
                    requirement = Mathf.Max(1, entry.requiredXP);
                }
            }
        }
        return requirement;
    }

    private void OnValidate()
    {
        EnsureId();
        MigrateLegacyPowerTableIfNeeded();
        MigrateLegacyUpkeepTableIfNeeded();
        MigrateLegacyLevelUpCostTableIfNeeded();
        ApplyStatBlockDefaults();
        NormalizeStats();
    }

    public void ApplyStatBlockDefaults()
    {
        if (statBlock == null) return;

        if (statOverrides == null)
        {
            statOverrides = new HunterStatOverrides();
        }

        if (!statOverrides.overrideProgression)
        {
            startingLevel = statBlock.startingLevel;
            startingXP = statBlock.startingXP;
            levelXPTable = CloneLevelTable(statBlock.levelXPTable);
        }
        else if (!statOverrides.overrideUpkeep)
        {
            ApplyStatBlockUpkeepDefaults();
        }

        if (!statOverrides.overrideReputationRequirement)
        {
            minReputation = statBlock.minReputation;
        }
    }

    private void NormalizeStats()
    {
        legacyBasePower = Mathf.Max(1, legacyBasePower);
        legacyPowerPerLevel = Mathf.Max(0, legacyPowerPerLevel);
        legacyDailyUpkeepCost = Mathf.Max(0, legacyDailyUpkeepCost);
        startingLevel = Mathf.Max(1, startingLevel);
        startingXP = Mathf.Max(0, startingXP);
        minReputation = Mathf.Max(0, minReputation);
        EnsureLevelOnePowerEntry();
        SortLevelTable();
    }

    private void EnsureId()
    {
        if (string.IsNullOrWhiteSpace(hunterId))
        {
            hunterId = System.Guid.NewGuid().ToString("N");
        }
    }

    private void MigrateLegacyPowerTableIfNeeded()
    {
        if (migratedLevelPower) return;

        if (levelXPTable == null)
        {
            levelXPTable = new List<LevelXPRequirement>();
        }

        EnsureLevelOnePowerEntry();
        foreach (var entry in levelXPTable)
        {
            if (entry == null) continue;
            entry.power = GetLegacyPowerForLevel(entry.level);
        }

        migratedLevelPower = true;
    }

    private void MigrateLegacyUpkeepTableIfNeeded()
    {
        if (migratedLevelUpkeep) return;

        if (levelXPTable == null)
        {
            levelXPTable = new List<LevelXPRequirement>();
        }

        EnsureLevelOnePowerEntry();
        foreach (var entry in levelXPTable)
        {
            if (entry == null) continue;
            entry.upkeep = Mathf.Max(0, legacyDailyUpkeepCost);
        }

        migratedLevelUpkeep = true;
    }

    private void MigrateLegacyLevelUpCostTableIfNeeded()
    {
        if (migratedLevelUpCost) return;

        if (levelXPTable == null)
        {
            levelXPTable = new List<LevelXPRequirement>();
        }

        EnsureLevelOnePowerEntry();
        foreach (var entry in levelXPTable)
        {
            if (entry == null) continue;
            entry.levelUpCost = GetLegacyLevelUpCostForLevel(entry.level);
        }

        migratedLevelUpCost = true;
    }

    private void EnsureLevelOnePowerEntry()
    {
        if (levelXPTable == null)
        {
            levelXPTable = new List<LevelXPRequirement>();
        }

        bool hasLevelOne = false;
        foreach (var entry in levelXPTable)
        {
            if (entry == null) continue;
            entry.level = Mathf.Max(1, entry.level);
            entry.requiredXP = Mathf.Max(0, entry.requiredXP);
            entry.power = Mathf.Max(1, entry.power);
            entry.upkeep = Mathf.Max(0, entry.upkeep);
            entry.levelUpCost = Mathf.Max(0, entry.levelUpCost);
            if (entry.level == 1)
            {
                hasLevelOne = true;
            }
        }

        if (!hasLevelOne)
        {
            levelXPTable.Add(new LevelXPRequirement
            {
                level = 1,
                requiredXP = 0,
                power = GetLegacyPowerForLevel(1),
                upkeep = Mathf.Max(0, legacyDailyUpkeepCost),
                levelUpCost = 0
            });
        }
    }

    private int GetLegacyPowerForLevel(int level)
    {
        return Mathf.Max(1, legacyBasePower) + Mathf.Max(0, level - 1) * Mathf.Max(0, legacyPowerPerLevel);
    }

    private int GetLegacyLevelUpCostForLevel(int level)
    {
        return Mathf.Max(0, level - 1) * 100;
    }

    private LevelXPRequirement GetBestLevelEntry(int level)
    {
        if (levelXPTable == null || levelXPTable.Count == 0)
        {
            return null;
        }

        LevelXPRequirement bestEntry = null;
        foreach (var entry in levelXPTable)
        {
            if (entry == null) continue;
            if (entry.level == level)
            {
                return entry;
            }

            if (entry.level <= level && (bestEntry == null || entry.level > bestEntry.level))
            {
                bestEntry = entry;
            }
        }

        if (bestEntry != null)
        {
            return bestEntry;
        }

        foreach (var entry in levelXPTable)
        {
            if (entry != null)
            {
                return entry;
            }
        }

        return null;
    }

    private void ApplyStatBlockUpkeepDefaults()
    {
        if (statBlock == null) return;
        EnsureLevelOnePowerEntry();

        foreach (var entry in levelXPTable)
        {
            if (entry == null) continue;
            entry.upkeep = statBlock.GetUpkeepCost(entry.level);
        }
    }

    private void SortLevelTable()
    {
        if (levelXPTable == null) return;
        levelXPTable.Sort((a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            return a.level.CompareTo(b.level);
        });
    }

    private static List<LevelXPRequirement> CloneLevelTable(List<LevelXPRequirement> source)
    {
        List<LevelXPRequirement> result = new List<LevelXPRequirement>();
        if (source == null) return result;

        foreach (var entry in source)
        {
            if (entry == null) continue;
            result.Add(new LevelXPRequirement
            {
                level = Mathf.Max(1, entry.level),
                requiredXP = Mathf.Max(0, entry.requiredXP),
                power = Mathf.Max(1, entry.power),
                upkeep = Mathf.Max(0, entry.upkeep),
                levelUpCost = Mathf.Max(0, entry.levelUpCost)
            });
        }

        return result;
    }

    [System.Serializable]
    public class LevelXPRequirement
    {
        [Min(1)] public int level = 1;
        [Min(0)] public int requiredXP = 0;
        [Min(1)] public int power = 10;
        [Min(0)] public int upkeep = 10;
        [Tooltip("Gold paid to upgrade into this level. Level 2 is the price from level 1 to 2.")]
        [Min(0)] public int levelUpCost = 0;
    }

    [System.Serializable]
    public class HunterStatOverrides
    {
        [Tooltip("Use the hunter's own per-level Upkeep values instead of the stat block.")]
        public bool overrideUpkeep;
        [Tooltip("Use the hunter's own Starting Level, Starting XP, XP table, and per-level power instead of the stat block.")]
        public bool overrideProgression;
        [Tooltip("Use the hunter's own Minimum Reputation instead of the stat block.")]
        public bool overrideReputationRequirement;
    }

    public enum MorningDialogueCondition
    {
        Always,
        Unpaid,
        NotFed,
        Wounded,
        ReadyToLevelUp,
        OrderWithMonsterPresent
    }

    [System.Serializable]
    public class HunterMorningDialogueLine
    {
        [Tooltip("Optional stable id for save/load. If empty, the line index is used.")]
        public string lineId;
        public MorningDialogueCondition condition = MorningDialogueCondition.Always;
        [Tooltip("Used only for Order With Monster Present.")]
        public MonsterData monster;
        [Min(1)] public int weight = 1;
        [TextArea(2, 5)] public string responseText;
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
