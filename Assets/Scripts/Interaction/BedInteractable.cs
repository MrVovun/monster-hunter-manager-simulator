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

    public override void Interact(PlayerInteraction player)
    {
        gm = GameManager.Instance;
        orderManager = gm != null ? gm.GetOrderManager() : null;
        timeManager = gm != null ? gm.GetTimeManager() : null;

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
}
