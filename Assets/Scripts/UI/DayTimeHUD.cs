using TMPro;
using System.Collections;
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
    [Header("State Icon Rotation")]
    [SerializeField] private bool rotateStateIconWithDayState = true;
    [SerializeField] private RectTransform stateIconRotatingRoot;
    [Tooltip("Z rotation used while planning/pre-bell. 0 keeps the sprite as authored.")]
    [SerializeField] private float preBellRotationZ = 0f;
    [Tooltip("Z rotation used during the workday. Negative values rotate clockwise in UI space.")]
    [SerializeField] private float activeRotationZ = -120f;
    [Tooltip("Z rotation used in evening. Negative values rotate clockwise in UI space.")]
    [SerializeField] private float eveningRotationZ = -240f;
    [SerializeField] private float stateIconRotationDuration = 0.35f;
    [Header("State Icon Audio")]
    [SerializeField] private AudioSource stateIconAudioSource;
    [SerializeField] private AudioClip stateIconRotationClip;
    [SerializeField] private float stateIconRotationVolume = 1f;
    [SerializeField] private bool playStateIconSoundOnInitialRefresh = false;

    private ReputationManager reputationManager;
    private GoldManager goldManager;
    private HunterManager hunterManager;
    private Coroutine stateIconRotationRoutine;
    private bool hasRefreshedStateIcon;

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
        if (stateIconRotationRoutine != null)
        {
            StopCoroutine(stateIconRotationRoutine);
            stateIconRotationRoutine = null;
        }

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
            reputationText.text = $"{reputationManager.GetReputation()}";
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
            timeManager = SceneLookup.Find<TimeManager>();
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
            goldText.text = goldManager != null ? $"{goldManager.GetGold()}" : "Gold -";
        }

        if (upkeepText != null)
        {
            int upkeep = GameManager.Instance != null ? GameManager.Instance.GetTodayUpkeepCost() : 0;
            upkeepText.text = $"{upkeep}";
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

        Sprite stateSprite = null;
        switch (state)
        {
            case TimeManager.DayState.PreBell:
                stateSprite = preBellSprite;
                break;
            case TimeManager.DayState.Active:
                stateSprite = activeSprite;
                break;
            case TimeManager.DayState.Evening:
                stateSprite = eveningSprite;
                break;
        }

        if (stateSprite != null)
        {
            stateIcon.sprite = stateSprite;
        }

        stateIcon.enabled = stateIcon.sprite != null;

        if (rotateStateIconWithDayState)
        {
            RotateStateIcon(GetStateIconRotationZ(state), Application.isPlaying);
        }

        PlayStateIconRotationSoundIfNeeded();
        hasRefreshedStateIcon = true;
    }

    private void PlayStateIconRotationSoundIfNeeded()
    {
        if (!Application.isPlaying) return;
        if (!hasRefreshedStateIcon && !playStateIconSoundOnInitialRefresh) return;
        if (stateIconRotationClip == null) return;

        if (stateIconAudioSource != null)
        {
            stateIconAudioSource.PlayOneShot(stateIconRotationClip, Mathf.Clamp01(stateIconRotationVolume));
        }
        else
        {
            AudioSource.PlayClipAtPoint(stateIconRotationClip, Vector3.zero, Mathf.Clamp01(stateIconRotationVolume));
        }
    }

    private float GetStateIconRotationZ(TimeManager.DayState state)
    {
        switch (state)
        {
            case TimeManager.DayState.Active:
                return activeRotationZ;
            case TimeManager.DayState.Evening:
                return eveningRotationZ;
            default:
                return preBellRotationZ;
        }
    }

    private void RotateStateIcon(float targetZ, bool animate)
    {
        RectTransform target = stateIconRotatingRoot != null
            ? stateIconRotatingRoot
            : stateIcon != null ? stateIcon.rectTransform : null;
        if (target == null) return;

        if (stateIconRotationRoutine != null)
        {
            StopCoroutine(stateIconRotationRoutine);
            stateIconRotationRoutine = null;
        }

        if (!animate || stateIconRotationDuration <= 0f || !isActiveAndEnabled)
        {
            SetIconRotation(target, targetZ);
            return;
        }

        stateIconRotationRoutine = StartCoroutine(AnimateStateIconRotation(target, targetZ));
    }

    private IEnumerator AnimateStateIconRotation(RectTransform target, float targetZ)
    {
        float startZ = NormalizeAngle(target.localEulerAngles.z);
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, stateIconRotationDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            float z = Mathf.LerpAngle(startZ, targetZ, t);
            SetIconRotation(target, z);
            yield return null;
        }

        SetIconRotation(target, targetZ);
        stateIconRotationRoutine = null;
    }

    private static void SetIconRotation(RectTransform target, float z)
    {
        target.localRotation = Quaternion.Euler(0f, 0f, z);
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        return angle;
    }
}
