using TMPro;
using UnityEngine;

/// <summary>
/// Keeps HUD labels in sync with in-game day timer.
/// Assign optional text fields in the inspector; if left null, nothing is written.
/// </summary>
public class DayTimeHUD : MonoBehaviour
{
    [SerializeField] private TimeManager timeManager;
    [SerializeField] private TMP_Text timeRemainingText;
    [SerializeField] private TMP_Text dayCounterText;
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private TMP_Text upkeepText;
    [SerializeField] private TMP_Text debtStatusText;
    [SerializeField] private TMP_Text reputationText;
    [SerializeField] private TMP_Text reputationProgressText;
    [SerializeField] private UnityEngine.UI.Image stateIcon;
    [SerializeField] private Sprite preBellSprite;
    [SerializeField] private Sprite activeSprite;
    [SerializeField] private Sprite eveningSprite;

    private ReputationManager reputationManager;
    private GoldManager goldManager;
    private HunterManager hunterManager;

    private void OnEnable()
    {
        EnsureTimeManager();
        if (timeManager != null)
        {
            timeManager.OnTimeUpdate += HandleTimeUpdate;
            timeManager.OnDayStarted += HandleDayStarted;
            timeManager.OnDayStateChanged += HandleDayStateChanged;
        }

        EnsureReputationManager();
        if (reputationManager != null)
        {
            reputationManager.OnReputationChanged += HandleReputationChanged;
        }

        EnsureEconomyManagers();
        if (goldManager != null)
        {
            goldManager.OnGoldChanged += HandleGoldChanged;
            goldManager.OnDebtChanged += HandleDebtChanged;
        }

        if (hunterManager != null)
        {
            hunterManager.OnHuntersChanged += HandleHuntersChanged;
        }

        RefreshTexts();
    }

    private void OnDisable()
    {
        if (timeManager != null)
        {
            timeManager.OnTimeUpdate -= HandleTimeUpdate;
            timeManager.OnDayStarted -= HandleDayStarted;
            timeManager.OnDayStateChanged -= HandleDayStateChanged;
        }

        if (reputationManager != null)
        {
            reputationManager.OnReputationChanged -= HandleReputationChanged;
        }

        if (goldManager != null)
        {
            goldManager.OnGoldChanged -= HandleGoldChanged;
            goldManager.OnDebtChanged -= HandleDebtChanged;
        }

        if (hunterManager != null)
        {
            hunterManager.OnHuntersChanged -= HandleHuntersChanged;
        }
    }

    private void HandleTimeUpdate(float _)
    {
        RefreshTexts();
    }

    private void HandleDayStarted(int _)
    {
        RefreshTexts();
    }

    private void HandleDayStateChanged(TimeManager.DayState state)
    {
        RefreshStateIcon(state);
        RefreshTexts();
    }

    private void HandleReputationChanged(float _)
    {
        RefreshReputationTexts();
    }

    private void HandleGoldChanged(int _)
    {
        RefreshEconomyTexts();
    }

    private void HandleDebtChanged(int _)
    {
        RefreshEconomyTexts();
    }

    private void HandleHuntersChanged()
    {
        RefreshEconomyTexts();
    }

    private void RefreshTexts()
    {
        EnsureTimeManager();
        if (timeManager == null) return;

        if (timeRemainingText != null)
        {
            float remaining = timeManager.GetSecondsRemainingInDay();
            int hours = Mathf.FloorToInt(remaining / 3600f);
            int minutes = Mathf.FloorToInt((remaining % 3600f) / 60f);
            int seconds = Mathf.FloorToInt(remaining % 60f);
            // mm:ss if under an hour, otherwise hh:mm:ss
            if (hours > 0)
                timeRemainingText.text = $"{hours:00}:{minutes:00}:{seconds:00}";
            else
                timeRemainingText.text = $"{minutes:00}:{seconds:00}";
        }

        if (dayCounterText != null)
        {
            // convert 0-based day index to 1-based for display
            dayCounterText.text = $"Day {timeManager.GetCurrentDayIndex() + 1}";
        }

        RefreshStateIcon(timeManager.GetDayState());
        RefreshReputationTexts();
        RefreshEconomyTexts();
    }

    private void RefreshReputationTexts()
    {
        EnsureReputationManager();
        if (reputationManager == null) return;

        if (reputationText != null)
        {
            reputationText.text = $"Reputation {reputationManager.GetReputation()}";
        }

        if (reputationProgressText != null)
        {
            reputationProgressText.text = reputationManager.GetProgressText();
        }
    }

    private void EnsureTimeManager()
    {
        if (timeManager == null && GameManager.Instance != null)
        {
            timeManager = GameManager.Instance.GetTimeManager();
        }
        if (timeManager == null)
        {
            timeManager = FindObjectOfType<TimeManager>();
        }
    }

    private void EnsureReputationManager()
    {
        if (reputationManager == null && GameManager.Instance != null)
        {
            reputationManager = GameManager.Instance.GetReputationManager();
        }
    }

    private void EnsureEconomyManagers()
    {
        if (GameManager.Instance == null) return;

        if (goldManager == null)
        {
            goldManager = GameManager.Instance.GetGoldManager();
        }

        if (hunterManager == null)
        {
            hunterManager = GameManager.Instance.GetHunterManager();
        }
    }

    private void RefreshEconomyTexts()
    {
        EnsureEconomyManagers();

        if (goldText != null)
        {
            goldText.text = goldManager != null ? $"Gold {goldManager.GetGold()}" : "Gold -";
        }

        if (upkeepText != null)
        {
            int upkeep = GameManager.Instance != null ? GameManager.Instance.GetTodayUpkeepCost() : 0;
            upkeepText.text = $"Upkeep {upkeep}";
        }

        if (debtStatusText != null)
        {
            debtStatusText.text = GameManager.Instance != null
                ? GameManager.Instance.GetUpkeepCrisisLabel()
                : string.Empty;
        }
    }

    private void RefreshStateIcon(TimeManager.DayState state)
    {
        if (stateIcon == null) return;
        switch (state)
        {
            case TimeManager.DayState.PreBell:
                stateIcon.sprite = preBellSprite;
                break;
            case TimeManager.DayState.Active:
                stateIcon.sprite = activeSprite;
                break;
            case TimeManager.DayState.Evening:
                stateIcon.sprite = eveningSprite;
                break;
        }
        stateIcon.enabled = stateIcon.sprite != null;
    }
}
