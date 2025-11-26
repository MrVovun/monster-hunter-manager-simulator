using System.Text;
using TMPro;
using UnityEngine;

public class MissionReportPanel : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text rewardsText;
    [SerializeField] private TMP_Text casualtiesText;

    private OrderManager trackedManager;
    private CursorLockMode previousLockState;
    private bool previousCursorVisible;

    private void OnEnable()
    {
        RememberCursor();
        UnlockCursor();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        RestoreCursor();
    }

    private void Subscribe()
    {
        trackedManager = GameManager.Instance != null ? GameManager.Instance.GetOrderManager() : null;
        if (trackedManager != null)
        {
            trackedManager.OnMissionResolved += HandleMissionResolved;
        }
    }

    private void Unsubscribe()
    {
        if (trackedManager != null)
        {
            trackedManager.OnMissionResolved -= HandleMissionResolved;
            trackedManager = null;
        }
    }

    private void HandleMissionResolved(MissionReport report)
    {
        if (report != null)
        {
            ShowReport(report);
        }
    }

    public void ShowReport(MissionReport report)
    {
        if (report == null) return;

        GameObject root = panelRoot != null ? panelRoot : gameObject;
        if (root != null)
        {
            root.SetActive(true);
        }

        if (titleText != null)
        {
            titleText.text = report.order != null ? report.order.orderTitle : "Mission";
        }

        if (resultText != null)
        {
            resultText.text = report.success ? "Result: Success" : "Result: Failure";
            resultText.color = report.success ? Color.green : Color.red;
        }

        if (rewardsText != null)
        {
            rewardsText.text = $"Gold: {report.goldEarned}\nReputation: {report.reputationGained}";
        }

        if (casualtiesText != null)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var hr in report.hunterResults)
            {
                if (hr == null || hr.hunter == null) continue;
                sb.Append(hr.hunter.name).Append(": ");
                if (hr.died)
                {
                    sb.Append("Died");
                }
                else if (hr.injured)
                {
                    sb.Append("Injured");
                }
                else if (hr.survived)
                {
                    sb.Append("OK");
                }
                sb.Append(" (XP +").Append(hr.xpGained).Append(')');
                if (hr.leveledUp) sb.Append(" LVL UP");
                sb.AppendLine();
            }
            casualtiesText.text = sb.Length > 0 ? sb.ToString() : "No party data.";
        }
    }

    public void Close()
    {
        GameObject root = panelRoot != null ? panelRoot : gameObject;
        if (root != null)
        {
            root.SetActive(false);
        }
        RestoreCursor();
    }

    private void RememberCursor()
    {
        previousLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestoreCursor()
    {
        Cursor.lockState = previousLockState;
        Cursor.visible = previousCursorVisible;
    }
}
