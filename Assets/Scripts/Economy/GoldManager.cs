using UnityEngine;
using System;
using System.IO;

public class GoldManager : MonoBehaviour
{
    [Serializable]
    private class GoldSaveData
    {
        public int currentGold;
        public int debt;
        public int currentDayGrossIncome;
        public int previousDayGrossIncome;
    }

    private int currentGold;
    private int debt;
    private int currentDayGrossIncome;
    private int previousDayGrossIncome;
    private string savePath;
    
    public event System.Action<int> OnGoldChanged;
    public event System.Action<int, int> OnSpendFailed;
    public event System.Action<int> OnDebtChanged;
    
    public void Initialize(int startingGold)
    {
        currentGold = startingGold;
        debt = 0;
        currentDayGrossIncome = 0;
        previousDayGrossIncome = 0;
        savePath = GameSaveUtility.GetSavePath("gold_state.json");
        LoadState();
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
            SaveState();
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
        SaveState();
        NotifyGoldChanged();
    }

    public void AddDebt(int amount)
    {
        if (amount <= 0) return;
        debt += amount;
        SaveState();
        NotifyDebtChanged();
    }

    public int BeginNewDayAndGetPreviousGrossIncome()
    {
        previousDayGrossIncome = currentDayGrossIncome;
        currentDayGrossIncome = 0;
        SaveState();
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
            SaveState();
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

    private void LoadState()
    {
        if (string.IsNullOrEmpty(savePath) || !File.Exists(savePath)) return;

        try
        {
            string json = File.ReadAllText(savePath);
            if (string.IsNullOrWhiteSpace(json)) return;

            GoldSaveData data = JsonUtility.FromJson<GoldSaveData>(json);
            if (data == null) return;

            currentGold = Mathf.Max(0, data.currentGold);
            debt = Mathf.Max(0, data.debt);
            currentDayGrossIncome = Mathf.Max(0, data.currentDayGrossIncome);
            previousDayGrossIncome = Mathf.Max(0, data.previousDayGrossIncome);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"GoldManager: Failed to load gold state. {ex.Message}");
        }
    }

    private void SaveState()
    {
        if (string.IsNullOrEmpty(savePath)) return;

        try
        {
            GoldSaveData data = new GoldSaveData
            {
                currentGold = currentGold,
                debt = debt,
                currentDayGrossIncome = currentDayGrossIncome,
                previousDayGrossIncome = previousDayGrossIncome
            };
            File.WriteAllText(savePath, JsonUtility.ToJson(data, true));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"GoldManager: Failed to save gold state. {ex.Message}");
        }
    }
}
