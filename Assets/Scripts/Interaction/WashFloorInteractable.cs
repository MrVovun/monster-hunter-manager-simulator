using UnityEngine;

public class WashFloorInteractable : Interactable
{
    [SerializeField] private MainHallFloorDirtManager dirtManager;

    private void Reset()
    {
        interactionPrompt = "[E] Wash Floor";
        interactionType = InteractionType.Trigger;
        locksPlayer = false;
    }

    public override bool IsInteractionAvailable()
    {
        ResolveReferences();
        return base.IsInteractionAvailable() && dirtManager != null && dirtManager.CanClean();
    }

    public override bool TryGetUnavailableReason(out string reason)
    {
        if (base.TryGetUnavailableReason(out reason)) return true;

        ResolveReferences();
        if (dirtManager == null)
        {
            reason = "There is no floor to wash here.";
            return true;
        }

        if (dirtManager.DirtPoints <= 0)
        {
            reason = "The floor is already clean.";
            return true;
        }

        TimeManager timeManager = GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null;
        if (timeManager == null || timeManager.GetDayState() != TimeManager.DayState.Active)
        {
            reason = "The floor can only be washed during the workday.";
            return true;
        }

        return false;
    }

    public override void Interact(PlayerInteraction player)
    {
        ResolveReferences();
        if (dirtManager == null || !dirtManager.CanClean()) return;

        OnInteractionStart(player);
        dirtManager.TryCleanFloor();
        OnInteractionEnd(player);
    }

    private void ResolveReferences()
    {
        if (dirtManager == null)
        {
            dirtManager = MainHallFloorDirtManager.Instance != null
                ? MainHallFloorDirtManager.Instance
                : SceneLookup.Find<MainHallFloorDirtManager>();
        }
    }
}
