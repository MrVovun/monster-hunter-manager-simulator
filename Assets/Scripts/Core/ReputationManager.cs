using UnityEngine;

public class ReputationManager : MonoBehaviour
{
    private int currentReputation;

    public void Initialize(int startingValue)
    {
        currentReputation = startingValue;
    }

    public int GetReputation()
    {
        return currentReputation;
    }

    public void AddReputation(int amount)
    {
        currentReputation = Mathf.Max(0, currentReputation + amount);
    }
}
