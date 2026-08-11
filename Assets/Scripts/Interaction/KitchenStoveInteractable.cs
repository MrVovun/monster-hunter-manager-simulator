using UnityEngine;

public class KitchenStoveInteractable : Interactable
{
    [SerializeField] private KitchenManager kitchenManager;
    [SerializeField] private KitchenRecipeUI recipeUI;

    private PlayerInteraction activePlayer;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Reset()
    {
        interactionPrompt = "[E] Open Kitchen";
        interactionType = InteractionType.Trigger;
        locksPlayer = true;
        useCustomCamera = false;
    }

    public override bool IsInteractionAvailable()
    {
        ResolveReferences();
        return base.IsInteractionAvailable() && !HasBlockingState();
    }

    public override bool TryGetUnavailableReason(out string reason)
    {
        if (base.TryGetUnavailableReason(out reason)) return true;

        ResolveReferences();
        if (kitchenManager == null || recipeUI == null)
        {
            reason = "The kitchen is not ready.";
            return true;
        }

        if (!kitchenManager.CanOpenRecipeUI())
        {
            reason = "The kitchen has not been built yet.";
            return true;
        }

        return false;
    }

    public override void Interact(PlayerInteraction player)
    {
        ResolveReferences();
        if (recipeUI == null || kitchenManager == null)
        {
            Debug.LogWarning("KitchenStoveInteractable: Missing kitchen manager or recipe UI.", this);
            return;
        }

        OnInteractionStart(player);
        activePlayer = player;
        recipeUI.Show(kitchenManager, ReleasePlayerLock);
    }

    private void ReleasePlayerLock()
    {
        if (activePlayer == null) return;
        OnInteractionEnd(activePlayer);
        activePlayer = null;
    }

    private void ResolveReferences()
    {
        if (kitchenManager == null)
        {
            kitchenManager = KitchenManager.Instance != null ? KitchenManager.Instance : FindFirstObjectByType<KitchenManager>();
        }
    }

    private bool HasBlockingState()
    {
        return TryGetUnavailableReason(out _);
    }
}
