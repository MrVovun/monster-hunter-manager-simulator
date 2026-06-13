using UnityEngine;

public class GraveMarker : Interactable
{
    private GraveyardManager graveyardManager;
    private GraveRecord record;
    private PlayerInteraction activePlayer;

    private void Reset()
    {
        interactionPrompt = "[E] Read Plaque";
        interactionType = InteractionType.Trigger;
        locksPlayer = true;
        useCustomCamera = false;
    }

    public void Initialize(GraveyardManager manager, GraveRecord graveRecord)
    {
        graveyardManager = manager;
        record = graveRecord;
        interactionPrompt = "[E] Read Plaque";
        interactionType = InteractionType.Trigger;
        locksPlayer = true;
        useCustomCamera = false;
    }

    public override void Interact(PlayerInteraction player)
    {
        if (graveyardManager == null || record == null || activePlayer != null) return;

        activePlayer = player;
        OnInteractionStart(player);
        RegisterLockRelease(ReleasePlayer);
        graveyardManager.ShowPlaque(record, ReleasePlayer);
    }

    private void ReleasePlayer()
    {
        if (activePlayer != null)
        {
            OnInteractionEnd(activePlayer);
        }
        activePlayer = null;
        ClearLockRelease(ReleasePlayer);
    }
}
