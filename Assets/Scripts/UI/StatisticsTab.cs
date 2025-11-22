using System.Linq;
using TMPro;
using UnityEngine;

public class StatisticsTab : MonoBehaviour
{
    [SerializeField] private TMP_Text statsText;

    public void Refresh()
    {
        if (statsText == null) return;

        OrderManager orderManager = GameManager.Instance != null ? GameManager.Instance.GetOrderManager() : null;
        if (orderManager == null)
        {
            statsText.text = "No statistics available.";
            return;
        }

        var history = orderManager.GetMissionHistory();
        int total = history.Count;
        int successes = history.Count(r => r.success);
        int failures = total - successes;
        int injuries = history.Sum(r => r.GetInjuriesCount());
        int deaths = history.Sum(r => r.GetDeathsCount());
        int goldEarned = history.Sum(r => r.goldEarned);
        int repGained = history.Sum(r => r.reputationGained);

        statsText.text =
            $"Missions: {successes} success / {failures} failed (Total {total})\n" +
            $"Casualties: {injuries} injured / {deaths} dead\n" +
            $"Gold earned: {goldEarned}\n" +
            $"Reputation gained: {repGained}";
    }
}
