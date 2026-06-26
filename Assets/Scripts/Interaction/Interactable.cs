using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] protected InteractionType interactionType = InteractionType.Trigger;
    [SerializeField] protected bool locksPlayer = false;
    [SerializeField] protected bool useCustomCamera = false;
    [SerializeField] protected string interactionPrompt = "[E] Interact";
    [SerializeField] private bool playInteractionFeedback = true;
    [SerializeField] private bool disableHudDuringInteraction = false;
    [Tooltip("Optional HUD roots to hide during this interaction. If empty, common HUD components are found automatically.")]
    [SerializeField] private GameObject[] hudRootsToDisable;
    private static Action pendingLockRelease;

    [Header("Camera Settings")]
    [SerializeField] protected Camera customCamera;
    
    protected bool isPlayerInRange = false;
    private readonly List<HudRootState> hiddenHudRoots = new List<HudRootState>();
    
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

    public virtual bool IsInteractionAvailable()
    {
        return isActiveAndEnabled;
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
        if (playInteractionFeedback)
        {
            InteractionFeedbackManager.PlayInteraction(transform.position);
        }

        if (locksPlayer)
        {
            FirstPersonController controller = player.GetFirstPersonController();
            if (controller != null)
            {
                controller.LockMovement();
            }
        }

        SetHudVisible(false);
        HandleCameraSwitch(player, true);
    }

    protected virtual void OnInteractionEnd(PlayerInteraction player)
    {
        SetHudVisible(true);

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

    private void SetHudVisible(bool visible)
    {
        if (!disableHudDuringInteraction) return;

        if (!visible)
        {
            HideHudRoots();
            return;
        }

        RestoreHudRoots();
    }

    private void HideHudRoots()
    {
        RestoreHudRoots();

        List<GameObject> roots = CollectHudRoots();
        foreach (var root in roots)
        {
            if (root == null) continue;
            hiddenHudRoots.Add(new HudRootState(root, root.activeSelf));
            root.SetActive(false);
        }
    }

    private void RestoreHudRoots()
    {
        foreach (var state in hiddenHudRoots)
        {
            if (state.Root != null)
            {
                state.Root.SetActive(state.WasActive);
            }
        }
        hiddenHudRoots.Clear();
    }

    private List<GameObject> CollectHudRoots()
    {
        List<GameObject> roots = new List<GameObject>();
        HashSet<GameObject> seen = new HashSet<GameObject>();

        if (hudRootsToDisable != null)
        {
            foreach (var root in hudRootsToDisable)
            {
                AddHudRoot(root, roots, seen);
            }
        }

        if (roots.Count > 0)
        {
            return roots;
        }

        AddComponentRoot(FindObjectOfType<DayTimeHUD>(true), roots, seen);
        AddComponentRoot(FindObjectOfType<NotificationFeedUI>(true), roots, seen);
        AddComponentRoot(FindObjectOfType<TimeAdvanceFeedback>(true), roots, seen);
        AddComponentRoot(FindObjectOfType<InteractionPromptUI>(true), roots, seen);
        return roots;
    }

    private void AddComponentRoot(Component component, List<GameObject> roots, HashSet<GameObject> seen)
    {
        if (component == null) return;
        AddHudRoot(component.gameObject, roots, seen);
    }

    private void AddHudRoot(GameObject root, List<GameObject> roots, HashSet<GameObject> seen)
    {
        if (root == null || seen.Contains(root)) return;
        seen.Add(root);
        roots.Add(root);
    }

    private readonly struct HudRootState
    {
        public readonly GameObject Root;
        public readonly bool WasActive;

        public HudRootState(GameObject root, bool wasActive)
        {
            Root = root;
            WasActive = wasActive;
        }
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
