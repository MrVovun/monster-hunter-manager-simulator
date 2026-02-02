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
    [SerializeField] private UnityEngine.UI.Image stateIcon;
    [SerializeField] private Sprite preBellSprite;
    [SerializeField] private Sprite activeSprite;
    [SerializeField] private Sprite eveningSprite;

    private void OnEnable()
    {
        EnsureTimeManager();
        if (timeManager != null)
        {
            timeManager.OnTimeUpdate += HandleTimeUpdate;
            timeManager.OnDayStarted += HandleDayStarted;
            timeManager.OnDayStateChanged += HandleDayStateChanged;
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
