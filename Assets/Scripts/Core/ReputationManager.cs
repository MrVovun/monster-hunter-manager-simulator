using UnityEngine;

public class ReputationManager : MonoBehaviour
{
    private float currentReputation;
    public event System.Action<float> OnReputationChanged;

    public void Initialize(float startingValue)
    {
        currentReputation = Mathf.Max(0f, startingValue);
        NotifyReputationChanged();
    }

    public int GetReputation()
    {
        return Mathf.FloorToInt(currentReputation);
    }

    public float GetReputationPrecise()
    {
        return currentReputation;
    }

    public void AddReputation(float amount)
    {
        currentReputation = Mathf.Max(0f, currentReputation + amount);
        NotifyReputationChanged();
    }

    public void AddReputation(int amount)
    {
        AddReputation((float)amount);
    }

    private void NotifyReputationChanged()
    {
        OnReputationChanged?.Invoke(currentReputation);
    }
}
