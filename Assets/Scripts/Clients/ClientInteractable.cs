using UnityEngine;

public class ClientInteractable : Interactable
{
    [SerializeField] private ClientCharacter clientCharacter;
    private InvestigationManager investigationManager;
    private InvestigationCase linkedCase;
    private bool awaitingRelease;
    private PlayerInteraction activePlayer;

    private void Reset()
    {
        interactionPrompt = "[E] Speak to client";
        interactionType = InteractionType.Trigger;
        locksPlayer = true;
    }

    public void Initialize(InvestigationManager manager, InvestigationCase investigationCase)
    {
        if (clientCharacter == null)
        {
            clientCharacter = GetComponent<ClientCharacter>();
        }
        investigationManager = manager;
        linkedCase = investigationCase;
    }

    public override void Interact(PlayerInteraction player)
    {
        if (linkedCase == null || investigationManager == null)
        {
            Debug.LogWarning("ClientInteractable: Missing linked case or investigation manager.");
            return;
        }

        OnInteractionStart(player);
        activePlayer = player;
        awaitingRelease = true;
        investigationManager.BeginInvestigationUI(linkedCase, ReleasePlayerLock);
    }

    private void ReleasePlayerLock()
    {
        if (!awaitingRelease) return;
        awaitingRelease = false;
        if (activePlayer != null)
        {
            OnInteractionEnd(activePlayer);
            activePlayer = null;
        }
    }

    public void Clear()
    {
        ReleasePlayerLock();
        investigationManager = null;
        linkedCase = null;
    }
}
