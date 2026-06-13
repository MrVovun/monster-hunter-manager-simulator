using TMPro;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// UI panel for advancing time with +/- controls.
/// Wire buttons to OnIncrease/OnDecrease/OnConfirm/OnCancel.
/// </summary>
public class PassTimeUI : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private float minSeconds = 0f;
    [SerializeField] private float maxSeconds = 36000f;
    [SerializeField] private bool clampToRemainingDay = true;
    [SerializeField] private bool useConfigStep = true;
    [SerializeField] private float stepSeconds = 60f;

    public UnityEvent OnShown;
    public UnityEvent OnHidden;

    private float currentSeconds;
    private TimeManager timeManager;
    private Action onClosed;
    private bool cursorCaptured;
    private CursorLockMode previousLockState;
    private bool previousCursorVisible;

    private void Awake()
    {
        timeManager = GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null;
        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirm);
        }
        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(OnCancel);
        }
    }

    private float GetStep()
    {
        if (!useConfigStep) return Mathf.Max(1f, stepSeconds);
        var config = GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null;
        if (config != null)
        {
            return Mathf.Max(1f, config.actionTimeSettings.passTimeStepSeconds);
        }
        return Mathf.Max(1f, stepSeconds);
    }

    public void Show(float initialSeconds = -1f, Action closedCallback = null)
    {
        timeManager = timeManager != null ? timeManager : (GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null);
        onClosed = closedCallback;
        float maxAllowed = GetMaxAllowedSeconds();
        float defaultSeconds = initialSeconds > 0f ? initialSeconds : GetStep();
        currentSeconds = Mathf.Clamp(defaultSeconds, minSeconds, maxAllowed);
        SetActive(true);
        CaptureCursor();
        Refresh();
        OnShown?.Invoke();
    }

    public void OnIncrease()
    {
        currentSeconds = Mathf.Clamp(currentSeconds + GetStep(), minSeconds, GetMaxAllowedSeconds());
        Refresh();
    }

    public void OnDecrease()
    {
        currentSeconds = Mathf.Clamp(currentSeconds - GetStep(), minSeconds, GetMaxAllowedSeconds());
        Refresh();
    }

    public void OnConfirm()
    {
        if (timeManager == null)
        {
            timeManager = GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null;
        }
        if (timeManager != null)
        {
            var state = timeManager.GetDayState();
            if (state != TimeManager.DayState.PreBell && state != TimeManager.DayState.Evening)
            {
                float clamped = Mathf.Clamp(currentSeconds, minSeconds, GetMaxAllowedSeconds());
                timeManager.AdvanceTime(Mathf.Max(0f, clamped));
            }
        }
        Hide();
    }

    public void OnCancel()
    {
        Hide();
    }

    private void Hide()
    {
        SetActive(false);
        ReleaseCursor();
        OnHidden?.Invoke();
        var callback = onClosed;
        onClosed = null;
        callback?.Invoke();
    }

    private void Refresh()
    {
        float maxAllowed = GetMaxAllowedSeconds();
        currentSeconds = Mathf.Clamp(currentSeconds, minSeconds, maxAllowed);

        if (amountText != null)
        {
            string selectedText = FormatDuration(currentSeconds);
            if (clampToRemainingDay)
            {
                float remaining = timeManager != null ? Mathf.Max(0f, timeManager.GetSecondsRemainingInDay()) : maxAllowed;
                amountText.text = $"{selectedText} / {FormatDuration(remaining)}";
            }
            else
            {
                amountText.text = selectedText;
            }
        }

        if (confirmButton != null)
        {
            confirmButton.interactable = currentSeconds > 0f;
        }
    }

    private void SetActive(bool value)
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(value);
        }
        else
        {
            gameObject.SetActive(value);
        }
    }

    private float GetMaxAllowedSeconds()
    {
        float capped = Mathf.Max(minSeconds, maxSeconds);
        if (!clampToRemainingDay) return capped;

        if (timeManager == null)
        {
            timeManager = GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null;
        }

        if (timeManager == null) return capped;

        float remainingDay = Mathf.Max(0f, timeManager.GetSecondsRemainingInDay());
        return Mathf.Min(capped, remainingDay);
    }

    private static string FormatDuration(float totalSeconds)
    {
        int minutes = Mathf.FloorToInt(totalSeconds / 60f);
        int seconds = Mathf.FloorToInt(totalSeconds % 60f);
        return minutes > 0 ? $"{minutes}m {seconds}s" : $"{seconds}s";
    }

    private void CaptureCursor()
    {
        if (cursorCaptured) return;
        previousLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        cursorCaptured = true;
    }

    private void ReleaseCursor()
    {
        if (!cursorCaptured) return;
        Cursor.lockState = previousLockState;
        Cursor.visible = previousCursorVisible;
        cursorCaptured = false;
    }
}
