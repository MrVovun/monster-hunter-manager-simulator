using System;
using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] protected InteractionType interactionType = InteractionType.Trigger;
    [SerializeField] protected bool locksPlayer = false;
    [SerializeField] protected bool useCustomCamera = false;
    [SerializeField] protected string interactionPrompt = "[E] Interact";
    private static Action pendingLockRelease;

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

        HandleCameraSwitch(player, true);
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

        HandleCameraSwitch(player, false);
    }

    protected virtual void HandleCameraSwitch(PlayerInteraction player, bool entered)
    {
        if (!useCustomCamera || customCamera == null) return;

        if (entered)
        {
            var main = Camera.main;
            if (main != null)
            {
                main.enabled = false;
            }
            customCamera.enabled = true;
        }
        else
        {
            customCamera.enabled = false;
            var main = Camera.main;
            if (main != null)
            {
                main.enabled = true;
            }
        }
    }

    protected void RegisterLockRelease(Action releaseAction)
    {
        pendingLockRelease = releaseAction;
    }

    protected void ClearLockRelease(Action releaseAction)
    {
        if (pendingLockRelease == releaseAction)
        {
            pendingLockRelease = null;
        }
    }

    public static void ReleaseActiveLock()
    {
        Action releaseAction = pendingLockRelease;
        pendingLockRelease = null;
        releaseAction?.Invoke();
    }
}
