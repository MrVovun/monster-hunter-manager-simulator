using UnityEngine;

public class ClientBell : Interactable
{
    [SerializeField] private OrderOfferPanel orderOfferPanel;
    [SerializeField] private AudioSource bellAudio;

    private PlayerInteraction pendingPlayerRelease;

    private void Reset()
    {
        interactionPrompt = "[E] Ring Bell";
        interactionType = InteractionType.Trigger;
        locksPlayer = false;
    }

    public override void Interact(PlayerInteraction player)
    {
        OnInteractionStart(player);

        if (bellAudio != null)
        {
            bellAudio.Play();
        }

        OrderManager manager = GameManager.Instance != null ? GameManager.Instance.GetOrderManager() : null;
        if (manager != null)
        {
            Order newOrder = manager.GenerateAndOfferOrder();
            if (orderOfferPanel != null)
            {
                orderOfferPanel.Show(newOrder);
                if (locksPlayer)
                {
                    BeginWaitingForOrderPanel(player);
                }
            }
        }
        else
        {
            Debug.LogWarning("ClientBell: No OrderManager found.");
        }

        if (locksPlayer && pendingPlayerRelease == null)
        {
            pendingPlayerRelease = player;
            RegisterLockRelease(ReleasePendingPlayerLock);
        }

        if (pendingPlayerRelease == null)
        {
            OnInteractionEnd(player);
        }
    }

    private void BeginWaitingForOrderPanel(PlayerInteraction player)
    {
        pendingPlayerRelease = player;
        orderOfferPanel.OnPanelHidden -= HandlePanelHidden;
        orderOfferPanel.OnPanelHidden += HandlePanelHidden;
        RegisterLockRelease(ReleasePendingPlayerLock);
    }

    private void HandlePanelHidden(OrderOfferPanel panel)
    {
        orderOfferPanel.OnPanelHidden -= HandlePanelHidden;
        ReleasePendingPlayerLock();
    }

    public void ReleasePlayerLock()
    {
        ReleasePendingPlayerLock();
    }

    private void ReleasePendingPlayerLock()
    {
        if (pendingPlayerRelease != null)
        {
            OnInteractionEnd(pendingPlayerRelease);
            pendingPlayerRelease = null;
            ClearLockRelease(ReleasePendingPlayerLock);
        }
    }

    private void OnDisable()
    {
        if (orderOfferPanel != null)
        {
            orderOfferPanel.OnPanelHidden -= HandlePanelHidden;
        }
        ReleasePendingPlayerLock();
        ClearLockRelease(ReleasePendingPlayerLock);
    }
}
