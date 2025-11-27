using UnityEngine;

public class ReputationManager : MonoBehaviour
{
    private int currentReputation;
    public event System.Action<int> OnReputationChanged;

    public void Initialize(int startingValue)
    {
        currentReputation = startingValue;
        NotifyReputationChanged();
    }

    public int GetReputation()
    {
        return currentReputation;
    }

    public void AddReputation(int amount)
    {
        currentReputation = Mathf.Max(0, currentReputation + amount);
        NotifyReputationChanged();
    }

    private void NotifyReputationChanged()
    {
        OnReputationChanged?.Invoke(currentReputation);
    }
}
