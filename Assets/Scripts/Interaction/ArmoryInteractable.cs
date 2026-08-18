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
        return base.IsInteractionAvailable() && !HasBlockingState();
    }

    public override bool TryGetUnavailableReason(out string reason)
    {
        if (base.TryGetUnavailableReason(out reason)) return true;

        ResolveReferences();
        if (armoryManager == null || armoryUI == null)
        {
            reason = "The armory is not ready.";
            return true;
        }

        if (!armoryManager.CanOpenArmory())
        {
            var timeManager = GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null;
            if (timeManager != null && timeManager.GetDayState() == TimeManager.DayState.Evening)
            {
                reason = "The armory is closed in the evening.";
                return true;
            }

            reason = "The armory has not been built yet.";
            return true;
        }

        return false;
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
            armoryManager = SceneLookup.Find<ArmoryManager>(true);
        }
    }

    private bool HasBlockingState()
    {
        return TryGetUnavailableReason(out _);
    }
}
