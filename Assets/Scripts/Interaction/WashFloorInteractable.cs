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

    public void SetDirtManager(MainHallFloorDirtManager manager)
    {
        dirtManager = manager;
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
        if (player == null || !IsInteractionAvailable()) return;
        var cleaning = player.GetComponent<PlayerFloorCleaning>();
        if (cleaning == null || !cleaning.TryStartCleaning(dirtManager, locksPlayer)) return;
        InteractionFeedbackManager.PlayInteraction(player.transform.position);
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
