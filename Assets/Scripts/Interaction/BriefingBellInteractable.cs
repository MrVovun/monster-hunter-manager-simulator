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
            briefingRoomManager = FindObjectOfType<BriefingRoomManager>();
        }
        return briefingRoomManager;
    }
}
