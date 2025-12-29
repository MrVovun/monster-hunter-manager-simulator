using System.Collections.Generic;
using UnityEngine;

public class GuildDoorController : MonoBehaviour
{
    [Header("Door Components")]
    [SerializeField] private Interactable doorInteractable;
    [SerializeField] private Collider[] colliders;
    [SerializeField] private bool controlColliders = true;
    [SerializeField] private GameObject lockedIndicator;
    [Header("Optional Animation")]
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private string openTriggerName = "Open";
    [SerializeField] private string closeTriggerName;
    [Header("Initial State")]
    [SerializeField] private bool startsOpen = false;

    private readonly HashSet<Object> unlockSources = new HashSet<Object>();
    private bool isOpen;

    private void Awake()
    {
        if (doorInteractable == null)
        {
            doorInteractable = GetComponent<Interactable>();
        }
        if (colliders == null || colliders.Length == 0)
        {
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                colliders = new[] { collider };
            }
        }
        isOpen = startsOpen;
        ApplyDoorState();
        if (isOpen)
        {
            PlayOpenAnimation();
        }
    }

    public void RegisterUnlockSource(Object source)
    {
        if (source == null) return;
        if (unlockSources.Add(source))
        {
            ApplyDoorState();
        }
    }

    public void UnregisterUnlockSource(Object source)
    {
        if (source == null) return;
        if (unlockSources.Remove(source))
        {
            ApplyDoorState();
        }
    }

    private void ApplyDoorState()
    {
        bool unlocked = unlockSources.Count > 0;

        if (doorInteractable != null)
        {
            doorInteractable.enabled = unlocked;
        }

        if (controlColliders && colliders != null)
        {
            foreach (var col in colliders)
            {
                if (col == null) continue;
                col.enabled = unlocked;
            }
        }

        if (lockedIndicator != null)
        {
            lockedIndicator.SetActive(!unlocked);
        }
    }

    public void PlayOpenAnimation()
    {
        if (doorAnimator == null || string.IsNullOrEmpty(openTriggerName)) return;
        if (!string.IsNullOrEmpty(closeTriggerName))
        {
            doorAnimator.ResetTrigger(closeTriggerName);
        }
        doorAnimator.SetTrigger(openTriggerName);
    }

    public void PlayCloseAnimation()
    {
        if (doorAnimator == null || string.IsNullOrEmpty(closeTriggerName)) return;
        doorAnimator.ResetTrigger(openTriggerName);
        doorAnimator.SetTrigger(closeTriggerName);
    }

    public void ToggleDoor()
    {
        if (!IsInteractable()) return;
        if (isOpen)
        {
            CloseDoor();
        }
        else
        {
            OpenDoor();
        }
    }

    public void OpenDoor()
    {
        if (isOpen) return;
        if (!IsInteractable()) return;
        isOpen = true;
        PlayOpenAnimation();
    }

    public void CloseDoor()
    {
        if (!isOpen) return;
        isOpen = false;
        PlayCloseAnimation();
    }

    private bool IsInteractable()
    {
        if (doorInteractable != null)
        {
            return doorInteractable.enabled;
        }
        return unlockSources.Count > 0;
    }
}
