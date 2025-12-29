using UnityEngine;

public class WorkbenchInteractable : Interactable
{
    [SerializeField] private GuildConstructionManager constructionManager;
    [SerializeField] private GuildConstructionUI constructionUI;

    private PlayerInteraction activePlayer;

    private void Awake()
    {
        if (constructionManager == null && GameManager.Instance != null)
        {
            constructionManager = GameManager.Instance.GetConstructionManager();
        }
    }

    private void Reset()
    {
        interactionPrompt = "[E] Open Workbench";
        interactionType = InteractionType.Trigger;
        locksPlayer = true;
        useCustomCamera = false;
    }

    public override void Interact(PlayerInteraction player)
    {
        if (constructionUI == null || constructionManager == null)
        {
            Debug.LogWarning("WorkbenchInteractable: Missing references.");
            return;
        }

        OnInteractionStart(player);
        activePlayer = player;
        constructionUI.Show(constructionManager, ReleasePlayerLock);
    }

    private void ReleasePlayerLock()
    {
        if (activePlayer != null)
        {
            OnInteractionEnd(activePlayer);
            activePlayer = null;
        }
    }
}
