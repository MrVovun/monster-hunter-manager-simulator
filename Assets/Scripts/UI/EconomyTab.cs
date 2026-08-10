using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EconomyTab : MonoBehaviour
{
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private TMP_Text reputationText;
    [SerializeField] private TMP_Text reputationProgressText;
    [SerializeField] private Image reputationProgressFillImage;
    [SerializeField] private Slider reputationProgressSlider;
    [SerializeField] private TMP_Text upkeepText;
    [SerializeField] private TMP_Text debtText;
    [SerializeField] private TMP_Text previousIncomeText;
    [SerializeField] private TMP_Text upkeepStateText;
    [SerializeField] private TMP_Text upkeepStatusText;
    [SerializeField] private TMP_Text missionPenaltyText;
    [SerializeField] private TMP_Text hiringStatusText;

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

        if (rep != null)
        {
            float progress = rep.GetProgressToNextReputationLevel01();
            if (reputationProgressFillImage != null)
            {
                reputationProgressFillImage.fillAmount = progress;
            }

            if (reputationProgressSlider != null)
            {
                reputationProgressSlider.value = progress;
            }
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

        if (upkeepStateText != null)
        {
            upkeepStateText.text = GameManager.Instance != null
                ? $"Upkeep state: {GameManager.Instance.GetUpkeepCrisisLabel()}"
                : "Upkeep state: -";
        }

        if (upkeepStatusText != null)
        {
            upkeepStatusText.text = GameManager.Instance != null
                ? GameManager.Instance.GetUpkeepCrisisDescription()
                : string.Empty;
        }

        if (missionPenaltyText != null)
        {
            float penalty = GameManager.Instance != null ? GameManager.Instance.GetDebtSuccessPenaltyPercent() : 0f;
            missionPenaltyText.text = penalty > 0.01f
                ? $"Mission success penalty: -{penalty:0.#}%"
                : "Mission success penalty: none";
        }

        if (hiringStatusText != null)
        {
            bool blocked = GameManager.Instance != null && GameManager.Instance.IsHiringBlockedByDebt();
            hiringStatusText.text = blocked
                ? "Hiring campaigns blocked by upkeep debt"
                : "Hiring campaigns available during the workday";
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
