using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EconomyTab : MonoBehaviour
{
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private TMP_Text reputationText;
    [SerializeField] private TMP_Text upkeepText;

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

        if (upkeepText != null && hunters != null)
        {
            upkeepText.text = $"Upkeep per day: {hunters.CalculateDailyUpkeep()}";
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
