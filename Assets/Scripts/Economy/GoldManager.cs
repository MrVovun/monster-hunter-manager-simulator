using UnityEngine;

public class GoldManager : MonoBehaviour
{
    private int currentGold;
    private int debt;
    private int currentDayGrossIncome;
    private int previousDayGrossIncome;
    
    public event System.Action<int> OnGoldChanged;
    public event System.Action<int, int> OnSpendFailed;
    public event System.Action<int> OnDebtChanged;
    
    public void Initialize(int startingGold)
    {
        currentGold = startingGold;
        debt = 0;
        currentDayGrossIncome = 0;
        previousDayGrossIncome = 0;
        NotifyGoldChanged();
        NotifyDebtChanged();
    }
    
    public int GetGold()
    {
        return currentGold;
    }

    public int GetDebt()
    {
        return debt;
    }

    public int GetPreviousDayGrossIncome()
    {
        return previousDayGrossIncome;
    }

    public int GetCurrentDayGrossIncome()
    {
        return currentDayGrossIncome;
    }
    
    public void AddGold(int amount)
    {
        if (amount <= 0)
        {
            currentGold = Mathf.Max(0, currentGold + amount);
            NotifyGoldChanged();
            return;
        }

        currentDayGrossIncome += amount;
        if (debt > 0)
        {
            int debtPayment = Mathf.Min(debt, amount);
            debt -= debtPayment;
            amount -= debtPayment;
            NotifyDebtChanged();
        }

        // Clamp to avoid going negative from bad inputs
        currentGold = Mathf.Max(0, currentGold + amount);
        NotifyGoldChanged();
    }

    public void AddDebt(int amount)
    {
        if (amount <= 0) return;
        debt += amount;
        NotifyDebtChanged();
    }

    public int BeginNewDayAndGetPreviousGrossIncome()
    {
        previousDayGrossIncome = currentDayGrossIncome;
        currentDayGrossIncome = 0;
        return previousDayGrossIncome;
    }
    
    public bool SpendGold(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

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

    private void NotifyDebtChanged()
    {
        OnDebtChanged?.Invoke(debt);
    }
}

