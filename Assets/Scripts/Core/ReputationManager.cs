using UnityEngine;

public class ReputationManager : MonoBehaviour
{
    [SerializeField] private float currentReputationPoints;
    public event System.Action<float> OnReputationChanged; // passes reputation level

    private GameConfig cachedConfig;

    public void Initialize(float startingValue)
    {
        cachedConfig = GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null;
        currentReputationPoints = Mathf.Max(0f, startingValue);
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
        currentReputationPoints = Mathf.Max(0f, currentReputationPoints + amount);
        NotifyReputationChanged();
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
        OnReputationChanged?.Invoke(GetReputation());
    }
}
