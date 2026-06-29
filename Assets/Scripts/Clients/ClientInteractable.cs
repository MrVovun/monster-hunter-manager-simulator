using UnityEngine;

public class ClientInteractable : Interactable
{
    [SerializeField] private ClientCharacter clientCharacter;
    [SerializeField] private Camera dialogueCamera;
    private InvestigationManager investigationManager;
    private InvestigationCase linkedCase;
    private bool awaitingRelease;
    private PlayerInteraction activePlayer;
    private bool interactionDisabled;

    private void Reset()
    {
        interactionPrompt = "[E] Speak to client";
        interactionType = InteractionType.Trigger;
        locksPlayer = true;
        useCustomCamera = false;
    }

    public void Initialize(InvestigationManager manager, InvestigationCase investigationCase)
    {
        if (clientCharacter == null)
        {
            clientCharacter = GetComponent<ClientCharacter>();
        }
        investigationManager = manager;
        linkedCase = investigationCase;
        dialogueCamera = manager != null ? manager.GetDialogueCamera() : dialogueCamera;
        interactionDisabled = false;
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionDisabled = !enabled;
        if (!enabled)
        {
            ReleasePlayerLock();
        }
    }

    public override bool IsInteractionAvailable()
    {
        return base.IsInteractionAvailable() && !interactionDisabled && !awaitingRelease;
    }

    public override void Interact(PlayerInteraction player)
    {
        if (interactionDisabled) return;
        if (awaitingRelease) return;

        if (linkedCase == null || investigationManager == null)
        {
            Debug.LogWarning("ClientInteractable: Missing linked case or investigation manager.");
            return;
        }

        OnInteractionStart(player);
        activePlayer = player;
        awaitingRelease = true;
        investigationManager.BeginInvestigationUI(linkedCase, ReleasePlayerLock);
        TutorialManager.ReportEvent(TutorialIds.EventClientDialogueOpened);
    }

    public override string GetTutorialActionId()
    {
        return string.IsNullOrWhiteSpace(tutorialActionId) ? TutorialIds.TalkClient : tutorialActionId;
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
        dialogueCamera = null;
        interactionDisabled = false;
    }

    protected override void HandleCameraSwitch(PlayerInteraction player, bool entered)
    {
        if (dialogueCamera == null || investigationManager == null)
        {
            base.HandleCameraSwitch(player, entered);
            return;
        }

        investigationManager.ToggleDialogueCamera(entered, player != null ? player.GetPlayerCamera() : null);
    }

    public void DisableInteraction()
    {
        SetInteractionEnabled(false);
    }
}
