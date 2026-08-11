using UnityEngine;

/// <summary>
/// Simple interactable to advance time by a fixed step (action-based clock).
/// </summary>
public class PassTimeInteractable : Interactable
{
    [SerializeField] private PassTimeUI passTimeUI;
    [SerializeField] private bool useConfigStep = true;
    [SerializeField] private float stepSeconds = 60f;
    private PlayerInteraction activePlayer;

    private void Reset()
    {
        interactionPrompt = "[E] Pass Time";
        interactionType = InteractionType.Trigger;
        locksPlayer = true;
    }

    public override bool IsInteractionAvailable()
    {
        return base.IsInteractionAvailable() && !HasBlockingState();
    }

    public override bool TryGetUnavailableReason(out string reason)
    {
        if (base.TryGetUnavailableReason(out reason)) return true;

        var tm = GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null;
        if (tm == null)
        {
            reason = "The clock is not ready.";
            return true;
        }

        TimeManager.DayState state = tm.GetDayState();
        if (state == TimeManager.DayState.PreBell)
        {
            reason = "Time starts after the first client is called.";
            return true;
        }

        if (state == TimeManager.DayState.Evening)
        {
            reason = "The workday is over.";
            return true;
        }

        return false;
    }

    public override void Interact(PlayerInteraction player)
    {
        if (passTimeUI != null)
        {
            OnInteractionStart(player);
            activePlayer = player;
            float amount = GetStepAmount();
            passTimeUI.Show(amount, ReleasePlayerLock);
            return;
        }

        var tm = GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null;
        if (tm == null) return;
        // Do not pass time before day starts or after evening
        var state = tm.GetDayState();
        if (state == TimeManager.DayState.PreBell || state == TimeManager.DayState.Evening) return;

        OnInteractionStart(player);
        tm.AdvanceTime(Mathf.Max(0f, GetStepAmount()));
        TutorialManager.ReportEvent(TutorialIds.EventPassTimeConfirmed);
        OnInteractionEnd(player);
    }

    private void ReleasePlayerLock()
    {
        if (activePlayer == null) return;
        OnInteractionEnd(activePlayer);
        activePlayer = null;
    }

    private float GetStepAmount()
    {
        if (!useConfigStep) return Mathf.Max(0f, stepSeconds);
        var config = GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null;
        if (config != null)
        {
            return Mathf.Max(0f, config.actionTimeSettings.passTimeStepSeconds);
        }
        return Mathf.Max(0f, stepSeconds);
    }

    public override string GetTutorialActionId()
    {
        return string.IsNullOrWhiteSpace(tutorialActionId) ? TutorialIds.PassTime : tutorialActionId;
    }

    private bool HasBlockingState()
    {
        return TryGetUnavailableReason(out _);
    }
}
