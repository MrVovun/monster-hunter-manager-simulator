using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] protected InteractionType interactionType = InteractionType.Trigger;
    [SerializeField] protected bool locksPlayer = false;
    [SerializeField] protected bool useCustomCamera = false;
    [SerializeField] protected string interactionPrompt = "[E] Interact";
    
    [Header("Camera Settings")]
    [SerializeField] protected Camera customCamera;
    
    protected bool isPlayerInRange = false;
    
    public InteractionType GetInteractionType()
    {
        return interactionType;
    }
    
    public bool LocksPlayer()
    {
        return locksPlayer;
    }
    
    public bool UseCustomCamera()
    {
        return useCustomCamera;
    }
    
    public string GetInteractionPrompt()
    {
        return interactionPrompt;
    }
    
    public virtual void OnPlayerEnter()
    {
        isPlayerInRange = true;
    }
    
    public virtual void OnPlayerExit()
    {
        isPlayerInRange = false;
    }
    
    public abstract void Interact(PlayerInteraction player);
    
    protected virtual void OnInteractionStart(PlayerInteraction player)
    {
        if (locksPlayer)
        {
            FirstPersonController controller = player.GetFirstPersonController();
            if (controller != null)
            {
                controller.LockMovement();
            }
        }
        
        if (useCustomCamera && customCamera != null)
        {
            Camera.main.enabled = false;
            customCamera.enabled = true;
        }
    }
    
    protected virtual void OnInteractionEnd(PlayerInteraction player)
    {
        if (locksPlayer)
        {
            FirstPersonController controller = player.GetFirstPersonController();
            if (controller != null)
            {
                controller.UnlockMovement();
            }
        }
        
        if (useCustomCamera && customCamera != null)
        {
            customCamera.enabled = false;
            Camera.main.enabled = true;
        }
    }
}

