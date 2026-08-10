using UnityEngine;
using System;
using System.IO;

public class ReputationManager : MonoBehaviour
{
    [Serializable]
    private class ReputationSaveData
    {
        public float reputationPoints;
    }

    [SerializeField] private float currentReputationPoints;
    public event System.Action<float> OnReputationChanged; // passes reputation level
    public event System.Action<int, int> OnReputationRankIncreased; // previous rank, new rank

    private GameConfig cachedConfig;
    private float defaultReputationPoints;
    private string savePath;

    public void Initialize(float startingValue)
    {
        cachedConfig = GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null;
        defaultReputationPoints = Mathf.Max(0f, startingValue);
        savePath = Path.Combine(Application.persistentDataPath, "reputation_state.json");
        currentReputationPoints = defaultReputationPoints;
        LoadState();
        NotifyReputationChanged();
    }

    public int GetReputation()
    {
        return ComputeReputationLevel(currentReputationPoints);
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
            if (!found || tierRequiredPoints < requiredPoints || tierRequiredPoints == requiredPoints && tierLevel < nextLevel)
            {
                found = true;
                nextLevel = tierLevel;
                requiredPoints = tierRequiredPoints;
            }
        }

        return found;
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
        SaveState();
        NotifyReputationChanged(previousRank);
    }

    public int LoseReputationRanks(int ranksToLose)
    {
        ranksToLose = Mathf.Max(0, ranksToLose);
        if (ranksToLose <= 0) return GetReputation();

        int currentLevel = GetReputation();
        int targetLevel = Mathf.Max(0, currentLevel - ranksToLose);
        float targetPoints = GetRequiredPointsForLevel(targetLevel);

        int previousRank = GetReputation();
        currentReputationPoints = Mathf.Min(currentReputationPoints, targetPoints);
        SaveState();
        NotifyReputationChanged(previousRank);
        return targetLevel;
    }

    public void ResetToDefault()
    {
        int previousRank = GetReputation();
        currentReputationPoints = defaultReputationPoints;
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
                reputationPoints = currentReputationPoints
            };
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(savePath, json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"ReputationManager: Failed to save reputation state. {ex.Message}");
        }
    }
}
