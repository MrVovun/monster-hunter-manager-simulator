using UnityEngine;

public class BriefingBellInteractable : Interactable
{
    [SerializeField] private BriefingRoomManager briefingRoomManager;
    [SerializeField] private AudioSource bellAudio;

    private void Reset()
    {
        interactionPrompt = "[E] Ring Briefing Bell";
        interactionType = InteractionType.Trigger;
        locksPlayer = false;
    }

    public override bool IsInteractionAvailable()
    {
        var manager = ResolveManager();
        return base.IsInteractionAvailable() && manager != null && manager.CanCallHunters();
    }

    public override bool TryGetUnavailableReason(out string reason)
    {
        if (base.TryGetUnavailableReason(out reason)) return true;

        var manager = ResolveManager();
        if (manager == null)
        {
            reason = "The briefing room is not ready.";
            return true;
        }

        if (!manager.CanCallHunters())
        {
            var timeManager = GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null;
            if (timeManager != null && timeManager.GetDayState() != TimeManager.DayState.PreBell)
            {
                reason = "Briefings can only be called before the workday starts.";
                return true;
            }

            reason = "The briefing bell was already used today.";
            return true;
        }

        return false;
    }

    public override void Interact(PlayerInteraction player)
    {
        var manager = ResolveManager();
        if (manager == null || !manager.CanCallHunters()) return;

        OnInteractionStart(player);

        if (bellAudio != null)
        {
            bellAudio.Play();
        }

        manager.CallHuntersToBriefing();
        OnInteractionEnd(player);
    }

    private BriefingRoomManager ResolveManager()
    {
        if (briefingRoomManager != null) return briefingRoomManager;
        briefingRoomManager = BriefingRoomManager.Instance;
        if (briefingRoomManager == null)
        {
            briefingRoomManager = SceneLookup.Find<BriefingRoomManager>();
        }
        return briefingRoomManager;
    }
}
