using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EconomyTab : MonoBehaviour
{
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private TMP_Text reputationText;
    [SerializeField] private TMP_Text reputationProgressText;
    [SerializeField] private TMP_Text upkeepText;
    [SerializeField] private TMP_Text debtText;
    [SerializeField] private TMP_Text previousIncomeText;

    public void Refresh()
    {
        GoldManager gold = GameManager.Instance != null ? GameManager.Instance.GetGoldManager() : null;
        ReputationManager rep = GameManager.Instance != null ? GameManager.Instance.GetReputationManager() : null;
        HunterManager hunters = GameManager.Instance != null ? GameManager.Instance.GetHunterManager() : null;

        if (goldText != null && gold != null)
        {
            goldText.text = $"Gold: {gold.GetGold()}";
        }

        if (reputationText != null && rep != null)
        {
            reputationText.text = $"Reputation: {rep.GetReputation()}";
        }

        if (reputationProgressText != null && rep != null)
        {
            reputationProgressText.text = rep.GetProgressText();
        }

        if (upkeepText != null && hunters != null)
        {
            upkeepText.text = $"Upkeep per day: {hunters.CalculateDailyUpkeep()}";
        }

        if (debtText != null && gold != null)
        {
            debtText.text = $"Debt: {gold.GetDebt()}";
        }

        if (previousIncomeText != null && gold != null)
        {
            previousIncomeText.text = $"Previous day income: {gold.GetPreviousDayGrossIncome()}";
        }
    }

    public void PayUpkeep()
    {
        GoldManager gold = GameManager.Instance != null ? GameManager.Instance.GetGoldManager() : null;
        HunterManager hunters = GameManager.Instance != null ? GameManager.Instance.GetHunterManager() : null;
        if (gold == null || hunters == null) return;

        bool paid = hunters.PayUpkeep(gold);
        if (!paid)
        {
            Debug.LogWarning("Not enough gold to pay upkeep.");
        }
        Refresh();
    }
}
