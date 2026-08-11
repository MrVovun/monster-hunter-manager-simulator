using UnityEngine;

public class KitchenDirtyPlate : Interactable
{
    private KitchenManager kitchenManager;
    private HunterSeat seat;

    private void Reset()
    {
        interactionPrompt = "[E] Clean Plate";
        interactionType = InteractionType.Trigger;
        locksPlayer = false;
    }

    public void Initialize(KitchenManager manager, HunterSeat ownerSeat)
    {
        kitchenManager = manager != null ? manager : KitchenManager.Instance;
        seat = ownerSeat;
        interactionPrompt = "[E] Clean Plate";
        interactionType = InteractionType.Trigger;
        locksPlayer = false;
        seat?.SetDirtyPlate(this);
    }

    public HunterSeat GetSeat()
    {
        return seat;
    }

    public override bool IsInteractionAvailable()
    {
        var tm = GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null;
        return base.IsInteractionAvailable() && tm != null && tm.GetDayState() == TimeManager.DayState.Active;
    }

    public override bool TryGetUnavailableReason(out string reason)
    {
        if (base.TryGetUnavailableReason(out reason)) return true;

        var tm = GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null;
        if (tm == null || tm.GetDayState() != TimeManager.DayState.Active)
        {
            reason = "Plates can only be cleaned during the workday.";
            return true;
        }

        return false;
    }

    public override void Interact(PlayerInteraction player)
    {
        if (kitchenManager == null)
        {
            kitchenManager = KitchenManager.Instance;
        }

        kitchenManager?.TryCleanPlate(this);
    }

    private void OnDestroy()
    {
        seat?.ClearDirtyPlate(this);
    }
}
