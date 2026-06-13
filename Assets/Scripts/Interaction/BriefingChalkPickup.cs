using System;
using UnityEngine;

public class BriefingChalkPickup : Interactable
{
    public static bool HasChalk { get; private set; }
    public static event Action<bool> OnChalkChanged;

    [SerializeField] private GameObject worldChalkVisual;
    [SerializeField] private bool hideWorldChalkWhenTaken;
    [SerializeField] private bool hidePickupPresentationWhenTaken = true;
    [SerializeField] private Renderer[] renderersToHideWhenTaken;
    [SerializeField] private Collider[] collidersToDisableWhenTaken;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        HasChalk = false;
        OnChalkChanged = null;
    }

    private void Reset()
    {
        interactionPrompt = "[E] Take Chalk";
        interactionType = InteractionType.Trigger;
        locksPlayer = false;
        hideWorldChalkWhenTaken = true;
        hidePickupPresentationWhenTaken = true;
    }

    private void Awake()
    {
        if (renderersToHideWhenTaken == null || renderersToHideWhenTaken.Length == 0)
        {
            renderersToHideWhenTaken = GetComponentsInChildren<Renderer>(true);
        }
        if (collidersToDisableWhenTaken == null || collidersToDisableWhenTaken.Length == 0)
        {
            collidersToDisableWhenTaken = GetComponentsInChildren<Collider>(true);
        }
    }

    private void OnEnable()
    {
        ApplyVisualState();
        BriefingChalkHolder.RefreshAll();
        OnChalkChanged += HandleChalkChanged;
    }

    private void OnDisable()
    {
        OnChalkChanged -= HandleChalkChanged;
    }

    public override bool IsInteractionAvailable()
    {
        return base.IsInteractionAvailable() && !HasChalk;
    }

    public override void Interact(PlayerInteraction player)
    {
        if (HasChalk) return;

        OnInteractionStart(player);
        SetHasChalk(true);
        OnInteractionEnd(player);
    }

    public static void SetHasChalk(bool value)
    {
        if (HasChalk == value) return;
        HasChalk = value;
        OnChalkChanged?.Invoke(HasChalk);
        BriefingChalkHolder.RefreshAll();
    }

    private void HandleChalkChanged(bool _)
    {
        ApplyVisualState();
    }

    private void ApplyVisualState()
    {
        if (worldChalkVisual != null && hideWorldChalkWhenTaken)
        {
            if (worldChalkVisual == gameObject)
            {
                ApplyPickupPresentationVisible(!HasChalk);
            }
            else
            {
                worldChalkVisual.SetActive(!HasChalk);
            }
        }
        else if (hidePickupPresentationWhenTaken)
        {
            ApplyPickupPresentationVisible(!HasChalk);
        }
    }

    private void ApplyPickupPresentationVisible(bool visible)
    {
        if (renderersToHideWhenTaken != null)
        {
            foreach (var renderer in renderersToHideWhenTaken)
            {
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
        }

        if (collidersToDisableWhenTaken != null)
        {
            foreach (var collider in collidersToDisableWhenTaken)
            {
                if (collider != null)
                {
                    collider.enabled = visible;
                }
            }
        }
    }
}
