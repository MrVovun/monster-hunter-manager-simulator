using System.Linq;
using TMPro;
using UnityEngine;

public class StatisticsTab : MonoBehaviour
{
    [SerializeField] private TMP_Text statsText;
    [SerializeField] private TMP_Text missionSummaryText;
    [SerializeField] private TMP_Text casualtiesText;
    [SerializeField] private TMP_Text goldEarnedText;
    [SerializeField] private TMP_Text reputationGainedText;
    [SerializeField] private TMP_Text currentReputationText;
    [SerializeField] private TMP_Text reputationProgressText;

    public void Refresh()
    {
        OrderManager orderManager = GameManager.Instance != null ? GameManager.Instance.GetOrderManager() : null;
        ReputationManager reputationManager = GameManager.Instance != null ? GameManager.Instance.GetReputationManager() : null;
        if (orderManager == null)
        {
            SetText(statsText, "No statistics available.");
            ClearSplitTexts();
            return;
        }

        var history = orderManager.GetMissionHistory();
        int total = history.Count;
        int successes = history.Count(r => r.success);
        int failures = total - successes;
        int injuries = history.Sum(r => r.GetInjuriesCount());
        int deaths = history.Sum(r => r.GetDeathsCount());
        int goldEarned = history.Sum(r => r.goldEarned);
        float repGained = history.Sum(r => r.reputationGained);

        string missionSummary = $"Missions: {successes} success / {failures} failed (Total {total})";
        string casualties = $"Casualties: {injuries} injured / {deaths} dead";
        string gold = $"Gold earned: {goldEarned}";
        string reputationGained = $"Reputation gained: {repGained:0.##}";
        string currentReputation = reputationManager != null
            ? $"Current reputation: {reputationManager.GetReputation()}"
            : "Current reputation: -";
        string reputationProgress = reputationManager != null
            ? reputationManager.GetProgressText()
            : string.Empty;

        SetText(missionSummaryText, missionSummary);
        SetText(casualtiesText, casualties);
        SetText(goldEarnedText, gold);
        SetText(reputationGainedText, reputationGained);
        SetText(currentReputationText, currentReputation);
        SetText(reputationProgressText, reputationProgress);

        if (statsText != null)
        {
            statsText.text =
                $"{missionSummary}\n" +
                $"{casualties}\n" +
                $"{gold}\n" +
                $"{reputationGained}\n" +
                $"{currentReputation}\n" +
                $"{reputationProgress}";
        }
    }

    private void ClearSplitTexts()
    {
        SetText(missionSummaryText, string.Empty);
        SetText(casualtiesText, string.Empty);
        SetText(goldEarnedText, string.Empty);
        SetText(reputationGainedText, string.Empty);
        SetText(currentReputationText, string.Empty);
        SetText(reputationProgressText, string.Empty);
    }

    private void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }
}
