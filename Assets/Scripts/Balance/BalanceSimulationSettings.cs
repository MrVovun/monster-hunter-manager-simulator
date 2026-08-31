using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BalanceSimulationSettings", menuName = "Guild Manager/Balance Simulation Settings")]
public class BalanceSimulationSettings : ScriptableObject
{
    [Header("Run")]
    [Min(1)] public int sessionsPerProfile = 10;
    [Min(1)] public int daysPerSession = 10;
    public int randomSeed = 12345;

    [Header("Starting State")]
    [Min(0)] public int startingGold = 100;
    [Min(0)] public float startingReputationPoints = 0f;
    [Min(1)] public int startingHunterCount = 3;

    [Header("Daily Behavior")]
    [Min(1)] public int maxClientsPerDay = 8;
    [Tooltip("If positive, the simulator will stop taking new clients when less than this much action-time remains.")]
    public float minimumSecondsToKeepBeforeNewClient = 0f;

    [Header("Data")]
    public GameConfig gameConfig;
    public DifficultyTable difficultyTable;
    public List<HunterData> hunterPool = new List<HunterData>();
    public List<BalanceSimulationProfile> profiles = new List<BalanceSimulationProfile>();
}
