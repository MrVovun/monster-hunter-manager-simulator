using UnityEngine;
using UnityEngine.Events;
using System;

public class TriggerInteraction : Interactable
{
    [Header("Trigger Settings")]
    public UnityEvent onTriggered;
    public Action<PlayerInteraction> onTriggeredAction; // For script-based triggers

    [Header("Order Offer (optional)")]
    [SerializeField] private bool generateOrderOnTrigger = false;
    [SerializeField] private OrderOfferPanel orderOfferPanel;

    private PlayerInteraction pendingPlayerRelease;
    
    private void Awake()
    {
        interactionType = InteractionType.Trigger;
        useCustomCamera = false;
    }
    
    public override void Interact(PlayerInteraction player)
    {
        OnInteractionStart(player);
        
        if (generateOrderOnTrigger)
        {
            TryGenerateAndShowOrder(player);
        }
        
        // Invoke Unity Event
        if (onTriggered != null)
        {
            onTriggered.Invoke();
        }
        
        // Invoke C# Action
        if (onTriggeredAction != null)
        {
            onTriggeredAction.Invoke(player);
        }
        
        // Call virtual method for derived classes
        OnTriggered(player);

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
    
    protected virtual void OnTriggered(PlayerInteraction player)
    {
        // Override in derived classes for specific trigger behavior
    }

    private void TryGenerateAndShowOrder(PlayerInteraction player)
    {
        OrderManager manager = GameManager.Instance != null ? GameManager.Instance.GetOrderManager() : null;
        if (manager == null)
        {
            Debug.LogWarning("TriggerInteraction: No OrderManager found to generate order.");
            return;
        }

        Order order = manager.GenerateAndOfferOrder();
        if (orderOfferPanel != null)
        {
            orderOfferPanel.Show(order);
            if (locksPlayer)
            {
                BeginWaitingForOrderPanel(player);
            }
        }
    }

    private void BeginWaitingForOrderPanel(PlayerInteraction player)
    {
        pendingPlayerRelease = player;
        orderOfferPanel.OnPanelHidden -= HandleOrderPanelHidden;
        orderOfferPanel.OnPanelHidden += HandleOrderPanelHidden;
        RegisterLockRelease(ReleasePendingPlayerLock);
    }

    private void HandleOrderPanelHidden(OrderOfferPanel panel)
    {
        orderOfferPanel.OnPanelHidden -= HandleOrderPanelHidden;
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

    public void ReleasePlayerLock()
    {
        ReleasePendingPlayerLock();
    }

    private void OnDisable()
    {
        if (orderOfferPanel != null)
        {
            orderOfferPanel.OnPanelHidden -= HandleOrderPanelHidden;
        }
        ReleasePendingPlayerLock();
        ClearLockRelease(ReleasePendingPlayerLock);
    }
}
