using UnityEngine;
using UnityEngine.Events;
using System;

public class TriggerInteraction : Interactable
{
    [Header("Trigger Settings")]
    public UnityEvent onTriggered;
    public Action<PlayerInteraction> onTriggeredAction; // For script-based triggers
    
    private void Awake()
    {
        interactionType = InteractionType.Trigger;
        locksPlayer = false; // Triggers usually don't lock player
        useCustomCamera = false;
    }
    
    public override void Interact(PlayerInteraction player)
    {
        OnInteractionStart(player);
        
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
        
        OnInteractionEnd(player);
    }
    
    protected virtual void OnTriggered(PlayerInteraction player)
    {
        // Override in derived classes for specific trigger behavior
    }
}

