using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DifficultyTable", menuName = "Guild Manager/Difficulty Table")]
public class DifficultyTable : ScriptableObject
{
    public List<DifficultyEntry> entries = new List<DifficultyEntry>();
}

[Serializable]
public class DifficultyEntry
{
    [Header("Reputation Gate")]
    public int minReputation;
    public int maxReputation;

    [Header("Difficulty")]
    [Tooltip("Used in success calculations.")]
    public int difficultyValue = 10;

    [Header("Timing")]
    public float prepTimeSeconds = 180f;
    public float missionTimeSeconds = 300f;

    [Header("Rewards")]
    public int goldReward = 100;
    public int xpReward = 50;

    [Header("Weight")]
    [Tooltip("How likely this entry is chosen when multiple are valid.")]
    public int weight = 1;
}
