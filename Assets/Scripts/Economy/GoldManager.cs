using UnityEngine;

public class GoldManager : MonoBehaviour
{
    private int currentGold;
    
    public void Initialize(int startingGold)
    {
        currentGold = startingGold;
    }
    
    public int GetGold()
    {
        return currentGold;
    }
    
    public void AddGold(int amount)
    {
        // Clamp to avoid going negative from bad inputs
        currentGold = Mathf.Max(0, currentGold + amount);
    }
    
    public bool SpendGold(int amount)
    {
        if (currentGold >= amount)
        {
            currentGold -= amount;
            return true;
        }
        return false;
    }
}

