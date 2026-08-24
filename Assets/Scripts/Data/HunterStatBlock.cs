using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "HunterStatBlock", menuName = "Guild Manager/Hunter Stat Block")]
public class HunterStatBlock : ScriptableObject
{
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

    [Header("Progression")]
    [Min(1)] public int startingLevel = 1;
    [Min(0)] public int startingXP = 0;
    public List<HunterData.LevelXPRequirement> levelXPTable = new List<HunterData.LevelXPRequirement>();

    [Header("Unlock Requirements")]
    [Min(0)] public int minReputation = 0;

    private void OnValidate()
    {
        MigrateLegacyPowerTableIfNeeded();
        MigrateLegacyUpkeepTableIfNeeded();
        MigrateLegacyLevelUpCostTableIfNeeded();
        legacyBasePower = Mathf.Max(1, legacyBasePower);
        legacyPowerPerLevel = Mathf.Max(0, legacyPowerPerLevel);
        legacyDailyUpkeepCost = Mathf.Max(0, legacyDailyUpkeepCost);
        startingLevel = Mathf.Max(1, startingLevel);
        startingXP = Mathf.Max(0, startingXP);
        minReputation = Mathf.Max(0, minReputation);
        EnsureLevelOnePowerEntry();
        SortLevelTable();
    }

    private void OnEnable()
    {
        MigrateLegacyPowerTableIfNeeded();
        OnValidate();
    }

    private void MigrateLegacyPowerTableIfNeeded()
    {
        if (migratedLevelPower) return;

        if (levelXPTable == null)
        {
            levelXPTable = new List<HunterData.LevelXPRequirement>();
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
            levelXPTable = new List<HunterData.LevelXPRequirement>();
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
            levelXPTable = new List<HunterData.LevelXPRequirement>();
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
            levelXPTable = new List<HunterData.LevelXPRequirement>();
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
            levelXPTable.Add(new HunterData.LevelXPRequirement
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

    public int GetUpkeepCost(int level)
    {
        level = Mathf.Max(1, level);
        if (levelXPTable == null || levelXPTable.Count == 0)
        {
            return Mathf.Max(0, legacyDailyUpkeepCost);
        }

        HunterData.LevelXPRequirement bestEntry = null;
        foreach (var entry in levelXPTable)
        {
            if (entry == null) continue;
            if (entry.level == level)
            {
                return Mathf.Max(0, entry.upkeep);
            }

            if (entry.level <= level && (bestEntry == null || entry.level > bestEntry.level))
            {
                bestEntry = entry;
            }
        }

        if (bestEntry != null)
        {
            return Mathf.Max(0, bestEntry.upkeep);
        }

        foreach (var entry in levelXPTable)
        {
            if (entry != null)
            {
                return Mathf.Max(0, entry.upkeep);
            }
        }

        return Mathf.Max(0, legacyDailyUpkeepCost);
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
}
