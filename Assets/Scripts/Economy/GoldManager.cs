using UnityEngine;

public class GoldManager : MonoBehaviour
{
    private int currentGold;
    
    public event System.Action<int> OnGoldChanged;
    public event System.Action<int, int> OnSpendFailed;
    
    public void Initialize(int startingGold)
    {
        currentGold = startingGold;
        NotifyGoldChanged();
    }
    
    public int GetGold()
    {
        return currentGold;
    }
    
    public void AddGold(int amount)
    {
        // Clamp to avoid going negative from bad inputs
        currentGold = Mathf.Max(0, currentGold + amount);
        NotifyGoldChanged();
    }
    
    public bool SpendGold(int amount)
    {
        if (currentGold >= amount)
        {
            currentGold -= amount;
            NotifyGoldChanged();
            return true;
        }

        OnSpendFailed?.Invoke(amount, currentGold);
        return false;
    }

    private void NotifyGoldChanged()
    {
        OnGoldChanged?.Invoke(currentGold);
    }
}

