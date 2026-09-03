using UnityEngine;
using System;
using System.IO;

public class ReputationManager : MonoBehaviour
{
    [Serializable]
    private class ReputationSaveData
    {
        public float reputationPoints;
        public float highestEarnedReputationPoints;
        public int reputationRank;
        public bool hasManualReputationRank;
        public int trustStreak;
    }

    [SerializeField] private float currentReputationPoints;
    [SerializeField] private int currentReputationRank;
    [SerializeField] private float highestEarnedReputationPoints;
    [SerializeField] private int trustStreak;
    public event System.Action<float> OnReputationChanged; // passes reputation level
    public event System.Action<int, int> OnReputationRankIncreased; // previous rank, new rank

    private GameConfig cachedConfig;
    private float defaultReputationPoints;
    private int defaultReputationRank;
    private string savePath;

    public void Initialize(float startingValue)
    {
        cachedConfig = GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null;
        defaultReputationPoints = Mathf.Max(0f, startingValue);
        defaultReputationRank = Mathf.Max(GetMinimumReputationRank(), ComputeReputationLevel(defaultReputationPoints));
        savePath = Path.Combine(Application.persistentDataPath, "reputation_state.json");
        currentReputationPoints = defaultReputationPoints;
        currentReputationRank = defaultReputationRank;
        highestEarnedReputationPoints = defaultReputationPoints;
        LoadState();
        NotifyReputationChanged();
    }

    public int GetReputation()
    {
        return currentReputationRank;
    }

    public float GetReputationPrecise()
    {
        return GetReputation();
    }

    public float GetReputationPointsPrecise()
    {
        return currentReputationPoints;
    }

    public int GetCurrentReputationLevelRequiredPoints()
    {
        int currentLevel = GetReputation();
        return GetRequiredPointsForLevel(currentLevel);
    }

    public bool TryGetNextReputationLevel(out int nextLevel, out int requiredPoints)
    {
        nextLevel = 0;
        requiredPoints = 0;
        if (cachedConfig == null || cachedConfig.orderLimitByReputation == null)
        {
            return false;
        }

        int currentLevel = GetReputation();
        bool found = false;
        foreach (var tier in cachedConfig.orderLimitByReputation)
        {
            if (tier == null) continue;
            int tierLevel = tier.requiredReputation;
            int tierRequiredPoints = Mathf.Max(0, tier.requiredReputationPoints);
            if (tierLevel <= currentLevel) continue;
            if (!found || tierLevel < nextLevel || tierLevel == nextLevel && tierRequiredPoints < requiredPoints)
            {
                found = true;
                nextLevel = tierLevel;
                requiredPoints = tierRequiredPoints;
            }
        }

        return found;
    }

    public bool CanUpgradeReputation(out int nextLevel, out int requiredPoints, out string unavailableReason)
    {
        unavailableReason = string.Empty;
        if (!TryGetNextReputationLevel(out nextLevel, out requiredPoints))
        {
            unavailableReason = "Maximum reputation";
            return false;
        }

        if (!IsPreBell())
        {
            unavailableReason = "Available before ringing the bell.";
            return false;
        }

        if (highestEarnedReputationPoints < requiredPoints)
        {
            float missing = Mathf.Max(0f, requiredPoints - currentReputationPoints);
            unavailableReason = $"Need {missing:0.##} more reputation points.";
            return false;
        }

        return true;
    }

    public bool TryUpgradeReputation()
    {
        if (!CanUpgradeReputation(out int nextLevel, out _, out _))
        {
            return false;
        }

        int previousRank = GetReputation();
        currentReputationRank = Mathf.Max(currentReputationRank, nextLevel);
        SaveState();
        NotifyReputationChanged(previousRank);
        return currentReputationRank > previousRank;
    }

    public string GetProgressText()
    {
        if (TryGetNextReputationLevel(out int nextLevel, out int nextRequiredPoints))
        {
            float points = GetReputationPointsPrecise();
            return $"{points:0.##} / {nextRequiredPoints} to Reputation {nextLevel}";
        }

        return "Maximum reputation";
    }

    public float GetProgressToNextReputationLevel01()
    {
        if (!TryGetNextReputationLevel(out _, out int nextRequiredPoints))
        {
            return 1f;
        }

        int currentRequiredPoints = GetCurrentReputationLevelRequiredPoints();
        float span = Mathf.Max(1f, nextRequiredPoints - currentRequiredPoints);
        return Mathf.Clamp01((currentReputationPoints - currentRequiredPoints) / span);
    }

    public void AddReputation(float amount)
    {
        AddReputationPoints(amount);
    }

    public void AddReputation(int amount)
    {
        AddReputationPoints((float)amount);
    }

    public void AddReputationPoints(float amount)
    {
        int previousRank = GetReputation();
        currentReputationPoints = Mathf.Max(0f, currentReputationPoints + amount);
        highestEarnedReputationPoints = Mathf.Max(highestEarnedReputationPoints, currentReputationPoints);
        SaveState();
        NotifyReputationChanged(previousRank);
    }

    public float LoseCurrentReputationPointsPercent(float percent)
    {
        percent = Mathf.Clamp(percent, 0f, 100f);
        if (percent <= 0f || currentReputationPoints <= 0f)
        {
            return 0f;
        }

        int previousRank = GetReputation();
        float pointsLost = currentReputationPoints * (percent / 100f);
        currentReputationPoints = Mathf.Max(0f, currentReputationPoints - pointsLost);
        SaveState();
        NotifyReputationChanged(previousRank);
        return pointsLost;
    }

    public float ApplyMissionTrustAndCalculateReputation(MissionReport report)
    {
        if (report == null || report.order == null)
        {
            return 0f;
        }

        float baseReward = Mathf.Max(0f, report.order.reputationPointsReward);
        bool success = report.success;
        bool hunterDied = HasHunterDeath(report);
        bool cleanSuccess = success && IsCleanSuccess(report);
        bool trustEligible = IsOrderTrustEligible(report.order);
        int trustBefore = ClampTrust(trustStreak);
        float qualityMultiplier = success
            ? (cleanSuccess ? GetCleanSuccessReputationMultiplier() : GetMessySuccessReputationMultiplier())
            : GetFailureReputationMultiplier();

        int trustForReward = hunterDied ? 0 : trustBefore;
        float finalReward = baseReward * qualityMultiplier * (1f + trustForReward * GetTrustReputationBonusPerStreak());

        if (hunterDied)
        {
            trustStreak = 0;
        }
        else if (success)
        {
            if (cleanSuccess && trustEligible)
            {
                trustStreak = ClampTrust(trustStreak + GetCleanSuccessTrustGain());
            }
        }
        else
        {
            trustStreak = ResetTrustOnFailedOrder()
                ? 0
                : ClampTrust(trustStreak - GetFailedOrderTrustLoss());
        }

        SaveState();
        return Mathf.Max(0f, finalReward);
    }

    public int GetTrustStreak()
    {
        return ClampTrust(trustStreak);
    }

    public float GetTrustReputationMultiplier()
    {
        return 1f + GetTrustStreak() * GetTrustReputationBonusPerStreak();
    }

    public int LoseReputationRanks(int ranksToLose)
    {
        ranksToLose = Mathf.Max(0, ranksToLose);
        if (ranksToLose <= 0) return GetReputation();

        int currentLevel = GetReputation();
        int targetLevel = Mathf.Max(GetMinimumReputationRank(), currentLevel - ranksToLose);

        int previousRank = GetReputation();
        currentReputationRank = targetLevel;
        SaveState();
        NotifyReputationChanged(previousRank);
        return targetLevel;
    }

    public void ResetToDefault()
    {
        int previousRank = GetReputation();
        currentReputationPoints = defaultReputationPoints;
        currentReputationRank = defaultReputationRank;
        highestEarnedReputationPoints = defaultReputationPoints;
        trustStreak = 0;
        SaveState();
        NotifyReputationChanged(previousRank);
    }

    private int ComputeReputationLevel(float reputationPoints)
    {
        // Reputation level is derived from thresholds, not directly from raw points.
        int level = 0;
        if (cachedConfig == null || cachedConfig.orderLimitByReputation == null || cachedConfig.orderLimitByReputation.Count == 0)
        {
            return level;
        }

        foreach (var tier in cachedConfig.orderLimitByReputation)
        {
            if (tier == null) continue;
            int requiredPoints = Mathf.Max(0, tier.requiredReputationPoints);
            int tierLevel = tier.requiredReputation;
            if (reputationPoints >= requiredPoints)
            {
                level = Mathf.Max(level, tierLevel);
            }
        }

        return level;
    }

    private int GetMinimumReputationRank()
    {
        int minimum = int.MaxValue;
        if (cachedConfig != null && cachedConfig.orderLimitByReputation != null)
        {
            foreach (var tier in cachedConfig.orderLimitByReputation)
            {
                if (tier == null) continue;
                minimum = Mathf.Min(minimum, tier.requiredReputation);
            }
        }

        return minimum == int.MaxValue ? 1 : Mathf.Max(1, minimum);
    }

    private void NotifyReputationChanged()
    {
        NotifyReputationChanged(GetReputation());
    }

    private void NotifyReputationChanged(int previousRank)
    {
        int currentRank = GetReputation();
        OnReputationChanged?.Invoke(currentRank);
        if (currentRank > previousRank)
        {
            OnReputationRankIncreased?.Invoke(previousRank, currentRank);
        }
    }

    private int GetRequiredPointsForLevel(int level)
    {
        int requiredPoints = 0;
        if (cachedConfig == null || cachedConfig.orderLimitByReputation == null)
        {
            return requiredPoints;
        }

        foreach (var tier in cachedConfig.orderLimitByReputation)
        {
            if (tier == null) continue;
            if (tier.requiredReputation == level)
            {
                requiredPoints = Mathf.Max(requiredPoints, tier.requiredReputationPoints);
            }
        }

        return requiredPoints;
    }

    private void LoadState()
    {
        if (string.IsNullOrEmpty(savePath) || !File.Exists(savePath))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(savePath);
            if (string.IsNullOrWhiteSpace(json)) return;
            ReputationSaveData data = JsonUtility.FromJson<ReputationSaveData>(json);
            if (data == null) return;
            currentReputationPoints = Mathf.Max(0f, data.reputationPoints);
            currentReputationRank = data.hasManualReputationRank
                ? data.reputationRank
                : ComputeReputationLevel(currentReputationPoints);
            currentReputationRank = Mathf.Max(GetMinimumReputationRank(), currentReputationRank);
            highestEarnedReputationPoints = Mathf.Max(
                currentReputationPoints,
                data.highestEarnedReputationPoints);
            trustStreak = ClampTrust(data.trustStreak);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"ReputationManager: Failed to load reputation state. {ex.Message}");
        }
    }

    private void SaveState()
    {
        if (string.IsNullOrEmpty(savePath)) return;

        try
        {
            ReputationSaveData data = new ReputationSaveData
            {
                reputationPoints = currentReputationPoints,
                highestEarnedReputationPoints = highestEarnedReputationPoints,
                reputationRank = currentReputationRank,
                hasManualReputationRank = true,
                trustStreak = ClampTrust(trustStreak)
            };
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(savePath, json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"ReputationManager: Failed to save reputation state. {ex.Message}");
        }
    }

    private bool IsCleanSuccess(MissionReport report)
    {
        if (report == null || report.order == null || !report.success) return false;
        if (!SameMonster(report.order.declaredMonster, report.order.monsterData)) return false;
        if (HasHunterDeath(report)) return false;
        if (HasHunterInjury(report)) return false;
        return true;
    }

    private bool IsOrderTrustEligible(Order order)
    {
        if (order == null) return false;
        int currentRank = GetReputation();
        int allowedTierDelta = cachedConfig != null ? Mathf.Max(0, cachedConfig.trustEligibleTierBelowCurrentReputation) : 1;
        int minimumEligibleTier = Mathf.Max(0, currentRank - allowedTierDelta);
        return order.reputationTier >= minimumEligibleTier;
    }

    private bool HasHunterDeath(MissionReport report)
    {
        if (report?.hunterResults == null) return false;
        foreach (var result in report.hunterResults)
        {
            if (result != null && result.died)
            {
                return true;
            }
        }
        return false;
    }

    private bool HasHunterInjury(MissionReport report)
    {
        if (report?.hunterResults == null) return false;
        foreach (var result in report.hunterResults)
        {
            if (result != null && result.injured)
            {
                return true;
            }
        }
        return false;
    }

    private bool SameMonster(MonsterData a, MonsterData b)
    {
        if (a == null || b == null) return false;
        if (!string.IsNullOrWhiteSpace(a.monsterId) && !string.IsNullOrWhiteSpace(b.monsterId))
        {
            return string.Equals(a.monsterId, b.monsterId, StringComparison.OrdinalIgnoreCase);
        }
        return a == b;
    }

    private int GetCleanSuccessTrustGain()
    {
        return cachedConfig != null ? Mathf.Max(0, cachedConfig.cleanSuccessTrustGain) : 1;
    }

    private int GetFailedOrderTrustLoss()
    {
        return cachedConfig != null ? Mathf.Max(0, cachedConfig.failedOrderTrustLoss) : 2;
    }

    private bool ResetTrustOnFailedOrder()
    {
        return cachedConfig == null || cachedConfig.resetTrustOnFailedOrder;
    }

    private float GetTrustReputationBonusPerStreak()
    {
        return cachedConfig != null ? Mathf.Max(0f, cachedConfig.trustReputationBonusPerStreak) : 0.15f;
    }

    private float GetCleanSuccessReputationMultiplier()
    {
        return cachedConfig != null ? Mathf.Clamp01(cachedConfig.cleanSuccessReputationMultiplier) : 1f;
    }

    private float GetMessySuccessReputationMultiplier()
    {
        return cachedConfig != null ? Mathf.Clamp01(cachedConfig.messySuccessReputationMultiplier) : 0.65f;
    }

    private float GetFailureReputationMultiplier()
    {
        return cachedConfig != null ? Mathf.Clamp01(cachedConfig.failureReputationMultiplier) : 0f;
    }

    private int GetMaxTrustStreak()
    {
        return cachedConfig != null ? Mathf.Max(0, cachedConfig.maxTrustStreak) : 5;
    }

    private int ClampTrust(int value)
    {
        return Mathf.Clamp(value, 0, GetMaxTrustStreak());
    }

    private bool IsPreBell()
    {
        TimeManager timeManager = GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null;
        return timeManager == null || timeManager.GetDayState() == TimeManager.DayState.PreBell;
    }
}
