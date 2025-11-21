using UnityEngine;

public class ReputationManager : MonoBehaviour
{
    [SerializeField] private int startingReputation = 0;
    private int currentReputation;

    public void Initialize(int startingValue)
    {
        currentReputation = startingValue;
    }

    private void Awake()
    {
        currentReputation = startingReputation;
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
