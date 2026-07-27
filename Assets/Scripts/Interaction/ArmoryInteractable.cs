using UnityEngine;

public class ArmoryInteractable : Interactable
{
    [SerializeField] private ArmoryManager armoryManager;
    [SerializeField] private ArmoryUI armoryUI;

    private PlayerInteraction activePlayer;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Reset()
    {
        interactionPrompt = "[E] Open Armory";
        interactionType = InteractionType.Trigger;
        locksPlayer = true;
        useCustomCamera = false;
    }

    public override bool IsInteractionAvailable()
    {
        ResolveReferences();
        return base.IsInteractionAvailable()
            && armoryManager != null
            && armoryUI != null
            && armoryManager.CanOpenArmory();
    }

    public override void Interact(PlayerInteraction player)
    {
        ResolveReferences();
        if (armoryManager == null || armoryUI == null)
        {
            Debug.LogWarning("ArmoryInteractable: Missing armory manager or armory UI.", this);
            return;
        }

        OnInteractionStart(player);
        activePlayer = player;

        if (!armoryManager.Open(armoryUI, player, ReleasePlayerLock))
        {
            ReleasePlayerLock();
            return;
        }

        armoryUI.Show(armoryManager);
    }

    private void ReleasePlayerLock()
    {
        if (activePlayer == null) return;
        OnInteractionEnd(activePlayer);
        activePlayer = null;
    }

    private void ResolveReferences()
    {
        if (armoryManager == null)
        {
            armoryManager = FindObjectOfType<ArmoryManager>(true);
        }
    }
}
