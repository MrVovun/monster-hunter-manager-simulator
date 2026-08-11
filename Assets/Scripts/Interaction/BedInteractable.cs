using UnityEngine;

public class BedInteractable : Interactable
{
    private OrderManager orderManager;
    private TimeManager timeManager;
    private GameManager gm;

    private void Reset()
    {
        interactionPrompt = "[E] Sleep";
        interactionType = InteractionType.Trigger;
        locksPlayer = false;
    }

    public override bool IsInteractionAvailable()
    {
        return base.IsInteractionAvailable() && !HasBlockingState();
    }

    public override bool TryGetUnavailableReason(out string reason)
    {
        if (base.TryGetUnavailableReason(out reason)) return true;

        ResolveReferences();
        if (timeManager == null || timeManager.GetDayState() != TimeManager.DayState.Evening)
        {
            reason = "You can only sleep in the evening.";
            return true;
        }

        if (orderManager != null && orderManager.HasInProgressOrders())
        {
            reason = "Cannot sleep while orders are still in progress.";
            return true;
        }

        return false;
    }

    public override void Interact(PlayerInteraction player)
    {
        ResolveReferences();

        if (timeManager == null || timeManager.GetDayState() != TimeManager.DayState.Evening)
        {
            Debug.LogWarning("Bed: You can only sleep in the evening after the workday ends.");
            return;
        }

        if (orderManager != null)
        {
            if (orderManager.HasInProgressOrders())
            {
                Debug.LogWarning("Bed: Cannot sleep while orders are still in progress.");
                return;
            }
        }

        OnInteractionStart(player);
        FirstPersonController controller = player != null ? player.GetComponent<FirstPersonController>() : null;
        DayTransitionUI transition = DayTransitionUI.Instance;
        if (transition != null && transition.Play(EndDayAndStartNext, controller))
        {
            OnInteractionEnd(player);
            return;
        }

        EndDayAndStartNext();
        OnInteractionEnd(player);
    }

    private void EndDayAndStartNext()
    {
        if (gm == null) gm = GameManager.Instance;
        if (gm != null)
        {
            gm.HandleEndOfDaySleep();
        }
    }

    private void ResolveReferences()
    {
        gm = GameManager.Instance;
        orderManager = gm != null ? gm.GetOrderManager() : null;
        timeManager = gm != null ? gm.GetTimeManager() : null;
    }

    private bool HasBlockingState()
    {
        return TryGetUnavailableReason(out _);
    }
}
