using System;
using System.Collections.Generic;
using UnityEngine;

public class MainHallFloorDirtManager : MonoBehaviour
{
    public static MainHallFloorDirtManager Instance { get; private set; }

    [Serializable]
    private class DirtSaveData
    {
        public int dirtPoints;
    }

    [Serializable]
    public class DirtVisualThreshold
    {
        [Min(0)] public int minDirtPoints;
        [Min(0f)] public float rewardPenaltyPercent;
        public GameObject visualRoot;
    }

    private const string SaveKey = "MainHallFloorDirt";

    [Header("Dirt")]
    [SerializeField] private int maxDirtPoints = 10;
    [SerializeField] private int clientArrivalDirtPoints = 1;
    [SerializeField] private int hunterDepartureDirtPoints = 1;

    [Header("Reward Penalty")]
    [SerializeField] private float maxRewardPenaltyPercent = 50f;

    [Header("Cleaning Time")]
    [SerializeField] private bool useConfigBaseWashTime = true;
    [SerializeField] private float baseWashSeconds = 5f;
    [SerializeField] private float extraWashSecondsPerDirtPoint = 1f;

    [Header("Visuals")]
    [SerializeField] private List<DirtVisualThreshold> visualThresholds = new List<DirtVisualThreshold>();

    private int dirtPoints;

    public int DirtPoints => dirtPoints;
    public float CurrentRewardPenaltyPercent => CalculateRewardPenaltyPercent();

    public event Action OnDirtChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple MainHallFloorDirtManager instances found. The newest one will replace the static instance.", this);
        }

        Instance = this;
        LoadState();
        RefreshVisuals();
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnValidate()
    {
        maxDirtPoints = Mathf.Max(0, maxDirtPoints);
        clientArrivalDirtPoints = Mathf.Max(0, clientArrivalDirtPoints);
        hunterDepartureDirtPoints = Mathf.Max(0, hunterDepartureDirtPoints);
        maxRewardPenaltyPercent = Mathf.Max(0f, maxRewardPenaltyPercent);
        baseWashSeconds = Mathf.Max(0f, baseWashSeconds);
        extraWashSecondsPerDirtPoint = Mathf.Max(0f, extraWashSecondsPerDirtPoint);
        if (visualThresholds != null)
        {
            foreach (var threshold in visualThresholds)
            {
                if (threshold == null) continue;
                threshold.minDirtPoints = Mathf.Max(0, threshold.minDirtPoints);
                threshold.rewardPenaltyPercent = Mathf.Max(0f, threshold.rewardPenaltyPercent);
            }
        }

        RefreshVisuals();
    }

    public void AddClientArrivalDirt()
    {
        AddDirt(clientArrivalDirtPoints);
    }

    public void AddHunterDepartureDirt(int hunterCount)
    {
        AddDirt(Mathf.Max(0, hunterCount) * hunterDepartureDirtPoints);
    }

    public void AddDirt(int amount)
    {
        if (amount <= 0 || maxDirtPoints <= 0) return;
        int previous = dirtPoints;
        dirtPoints = Mathf.Clamp(dirtPoints + amount, 0, maxDirtPoints);
        if (dirtPoints == previous) return;

        SaveState();
        RefreshVisuals();
        OnDirtChanged?.Invoke();
    }

    public bool CanClean()
    {
        if (dirtPoints <= 0) return false;
        TimeManager timeManager = GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null;
        return timeManager != null && timeManager.GetDayState() == TimeManager.DayState.Active;
    }

    public bool TryCleanFloor()
    {
        if (!CanClean()) return false;

        TimeManager timeManager = GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null;
        timeManager?.AdvanceTime(GetCleanTimeSeconds());

        dirtPoints = 0;
        SaveState();
        RefreshVisuals();
        OnDirtChanged?.Invoke();
        return true;
    }

    public float GetCleanTimeSeconds()
    {
        float baseSeconds = baseWashSeconds;
        if (useConfigBaseWashTime)
        {
            GameConfig config = GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null;
            if (config != null)
            {
                baseSeconds = Mathf.Max(0f, config.actionTimeSettings.washFloorSeconds);
            }
        }

        return Mathf.Max(0f, baseSeconds + dirtPoints * extraWashSecondsPerDirtPoint);
    }

    public void ApplyRewardPenalty(Order order)
    {
        if (order == null || dirtPoints <= 0) return;
        float penalty = CalculateRewardPenaltyPercent();
        if (penalty <= 0f) return;

        float multiplier = Mathf.Clamp01(1f - penalty / 100f);
        order.goldReward = Mathf.Max(0, Mathf.RoundToInt(order.goldReward * multiplier));
    }

    private float CalculateRewardPenaltyPercent()
    {
        if (visualThresholds == null || visualThresholds.Count == 0) return 0f;

        float penalty = 0f;
        int activeMinDirt = -1;
        foreach (var threshold in visualThresholds)
        {
            if (threshold == null) continue;
            if (dirtPoints < threshold.minDirtPoints) continue;
            if (threshold.minDirtPoints < activeMinDirt) continue;

            activeMinDirt = threshold.minDirtPoints;
            penalty = threshold.rewardPenaltyPercent;
        }

        return Mathf.Min(maxRewardPenaltyPercent, penalty);
    }

    private void RefreshVisuals()
    {
        if (visualThresholds == null || visualThresholds.Count == 0) return;

        DirtVisualThreshold active = null;
        foreach (var threshold in visualThresholds)
        {
            if (threshold == null || threshold.visualRoot == null) continue;
            if (dirtPoints >= threshold.minDirtPoints && (active == null || threshold.minDirtPoints >= active.minDirtPoints))
            {
                active = threshold;
            }
        }

        foreach (var threshold in visualThresholds)
        {
            if (threshold == null || threshold.visualRoot == null) continue;
            threshold.visualRoot.SetActive(threshold == active);
        }
    }

    private void LoadState()
    {
        if (!PlayerPrefs.HasKey(SaveKey)) return;

        try
        {
            DirtSaveData data = JsonUtility.FromJson<DirtSaveData>(PlayerPrefs.GetString(SaveKey));
            dirtPoints = Mathf.Clamp(data != null ? data.dirtPoints : 0, 0, Mathf.Max(0, maxDirtPoints));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"MainHallFloorDirtManager: Failed to load dirt state. {ex.Message}", this);
            dirtPoints = 0;
        }
    }

    private void SaveState()
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(new DirtSaveData { dirtPoints = dirtPoints }));
        PlayerPrefs.Save();
    }
}
