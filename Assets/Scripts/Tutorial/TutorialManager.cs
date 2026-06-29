using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class TutorialManager : MonoBehaviour
{
    private const string DisabledKey = "tutorial.disabled";
    private const string CompletedKeyPrefix = "tutorial.completed.";

    public static TutorialManager Instance { get; private set; }
    public static event Action OnTutorialGateChanged;

    [SerializeField] private TutorialSequence firstSessionSequence;
    [SerializeField] private TutorialPopupUI popupUI;
    [SerializeField] private InputActionReference manualContinueAction;
    [SerializeField] private string manualContinueFallbackLabel = "R";
    [SerializeField] private bool startAutomatically = true;
    [SerializeField] private bool disableTutorial;

    private int currentStepIndex = -1;
    private int currentEventCount;
    private TutorialStep CurrentStep =>
        firstSessionSequence != null
        && currentStepIndex >= 0
        && currentStepIndex < firstSessionSequence.steps.Count
            ? firstSessionSequence.steps[currentStepIndex]
            : null;

    public bool IsRunning => CurrentStep != null && !IsTutorialDisabled();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        disableTutorial = IsTutorialDisabled();
    }

    private void OnEnable()
    {
        if (manualContinueAction != null && manualContinueAction.action != null)
        {
            manualContinueAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (manualContinueAction != null && manualContinueAction.action != null)
        {
            manualContinueAction.action.Disable();
        }
    }

    private void Start()
    {
        if (startAutomatically)
        {
            TryStartFirstSessionTutorial();
        }
    }

    private void Update()
    {
        TutorialStep step = CurrentStep;
        if (step == null || !step.allowManualContinue) return;

        bool pressed = WasManualContinuePressed();

        if (pressed)
        {
            AdvanceStep();
        }
    }

    public void TryStartFirstSessionTutorial()
    {
        if (firstSessionSequence == null) return;
        if (IsTutorialDisabled()) return;
        if (IsSequenceCompleted(firstSessionSequence.sequenceId)) return;

        currentStepIndex = -1;
        AdvanceStep();
    }

    public void SkipTutorial()
    {
        if (firstSessionSequence != null)
        {
            PlayerPrefs.SetInt(GetCompletedKey(firstSessionSequence.sequenceId), 1);
        }

        currentStepIndex = -1;
        popupUI?.Hide();
        OnTutorialGateChanged?.Invoke();
    }

    public void ResetTutorialProgress()
    {
        if (firstSessionSequence != null)
        {
            PlayerPrefs.DeleteKey(GetCompletedKey(firstSessionSequence.sequenceId));
        }

        SetTutorialDisabled(false);
        currentStepIndex = -1;
        TryStartFirstSessionTutorial();
    }

    public void SetTutorialDisabled(bool disabled)
    {
        disableTutorial = disabled;
        PlayerPrefs.SetInt(DisabledKey, disabled ? 1 : 0);
        if (disabled)
        {
            currentStepIndex = -1;
            popupUI?.Hide();
        }
        else
        {
            TryStartFirstSessionTutorial();
        }

        OnTutorialGateChanged?.Invoke();
    }

    public bool IsTutorialDisabled()
    {
        return disableTutorial || PlayerPrefs.GetInt(DisabledKey, 0) == 1;
    }

    public static bool IsActionAllowed(string actionId)
    {
        var manager = Instance;
        if (manager == null || !manager.IsRunning) return true;

        TutorialStep step = manager.CurrentStep;
        if (step == null || !step.HasAllowedActions()) return true;
        if (string.IsNullOrWhiteSpace(actionId)) return false;

        return step.AllowsAction(actionId);
    }

    public static void ReportEvent(string eventId)
    {
        Instance?.HandleEvent(eventId);
    }

    public static bool TryGetForcedPassTimeSeconds(out float seconds)
    {
        seconds = 0f;
        var manager = Instance;
        if (manager == null || !manager.IsRunning) return false;

        TutorialStep step = manager.CurrentStep;
        if (step == null || !step.forcePassTimeToFirstActiveMissionRemaining) return false;

        OrderManager orderManager = GameManager.Instance != null ? GameManager.Instance.GetOrderManager() : null;
        if (orderManager == null) return false;

        foreach (var order in orderManager.GetActiveOrders())
        {
            if (order == null || order.state != OrderState.InProgress || order.missionTimer == null) continue;
            seconds = Mathf.Max(0f, order.missionTimer.GetRemainingTime());
            return seconds > 0f;
        }

        return false;
    }

    public static bool TryCreateForcedOrder(out Order order)
    {
        order = null;
        var manager = Instance;
        if (manager == null || !manager.IsRunning) return false;

        TutorialStep step = manager.CurrentStep;
        if (step == null || step.forcedOrder == null) return false;

        order = step.forcedOrder.CreateOrder();
        return order != null;
    }

    public static MonsterData GetForcedMonsterSelection()
    {
        var manager = Instance;
        if (manager == null || !manager.IsRunning) return null;
        return manager.CurrentStep != null ? manager.CurrentStep.forcedMonsterSelection : null;
    }

    public static bool TryGetForcedHiringAd(out float durationSeconds, out bool free)
    {
        durationSeconds = 0f;
        free = false;
        var manager = Instance;
        if (manager == null || !manager.IsRunning) return false;

        TutorialStep step = manager.CurrentStep;
        if (step == null) return false;

        free = step.forceHiringAdFree;
        if (step.forceHiringAdDuration)
        {
            durationSeconds = Mathf.Max(1f, step.forcedHiringAdDurationSeconds);
            return true;
        }

        return free;
    }

    private void HandleEvent(string eventId)
    {
        TutorialStep step = CurrentStep;
        string completionEvent = step != null ? step.GetCompletionEventId() : null;
        if (step == null || string.IsNullOrWhiteSpace(completionEvent)) return;
        if (!string.Equals(completionEvent, eventId, StringComparison.OrdinalIgnoreCase)) return;

        currentEventCount++;
        if (currentEventCount >= Mathf.Max(1, step.requiredEventCount))
        {
            AdvanceStep();
        }
    }

    private void AdvanceStep()
    {
        currentStepIndex++;
        currentEventCount = 0;

        if (firstSessionSequence == null || currentStepIndex >= firstSessionSequence.steps.Count)
        {
            CompleteSequence();
            return;
        }

        popupUI?.Show(CurrentStep, GetManualContinueBindingLabel());
        OnTutorialGateChanged?.Invoke();
    }

    private bool WasManualContinuePressed()
    {
        if (manualContinueAction != null && manualContinueAction.action != null)
        {
            return manualContinueAction.action.WasPressedThisFrame();
        }

        return Keyboard.current != null
            ? Keyboard.current[Key.R].wasPressedThisFrame
            : Input.GetKeyDown(KeyCode.R);
    }

    private string GetManualContinueBindingLabel()
    {
        if (manualContinueAction != null && manualContinueAction.action != null)
        {
            string display = manualContinueAction.action.GetBindingDisplayString();
            if (!string.IsNullOrWhiteSpace(display))
            {
                return display;
            }
        }

        return string.IsNullOrWhiteSpace(manualContinueFallbackLabel)
            ? "R"
            : manualContinueFallbackLabel;
    }

    private void CompleteSequence()
    {
        if (firstSessionSequence != null)
        {
            PlayerPrefs.SetInt(GetCompletedKey(firstSessionSequence.sequenceId), 1);
        }

        currentStepIndex = -1;
        popupUI?.Hide();
        OnTutorialGateChanged?.Invoke();
    }

    private bool IsSequenceCompleted(string sequenceId)
    {
        return PlayerPrefs.GetInt(GetCompletedKey(sequenceId), 0) == 1;
    }

    private static string GetCompletedKey(string sequenceId)
    {
        return CompletedKeyPrefix + (string.IsNullOrWhiteSpace(sequenceId) ? "default" : sequenceId);
    }
}
