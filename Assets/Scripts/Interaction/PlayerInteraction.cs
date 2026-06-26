using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private LayerMask interactionMask = ~0;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [Header("Visuals")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private GameObject[] visualRoots;

    private Interactable currentInteractable;
    private FirstPersonController fpsController;

    private void Awake()
    {
        fpsController = GetComponent<FirstPersonController>();
        if (playerCamera == null)
        {
            var fpsCam = fpsController != null ? fpsController.GetPlayerCamera() : null;
            playerCamera = fpsCam != null ? fpsCam : Camera.main;
        }
    }

    private void OnDisable()
    {
        InteractionPromptUI.Instance?.HidePrompt();
    }

    private void Update()
    {
        UpdateFocus();

        if (fpsController != null && fpsController.IsMovementLocked())
        {
            return;
        }

        if (WasInteractPressed() && currentInteractable != null)
        {
            currentInteractable.Interact(this);
            if (InteractionPromptUI.Instance != null)
            {
                InteractionPromptUI.Instance.HidePrompt();
            }
        }
    }

    private bool WasInteractPressed()
    {
        // Prefer new Input System if available
        if (Keyboard.current != null)
        {
            return Keyboard.current[Key.E].wasPressedThisFrame;
        }

        // Fallback to old Input Manager
        return Input.GetKeyDown(interactKey);
    }

    private void UpdateFocus()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactionRange, interactionMask, QueryTriggerInteraction.Collide))
        {
            Interactable interactable = hit.collider.GetComponentInParent<Interactable>();
            if (interactable != null && interactable.IsInteractionAvailable())
            {
                if (interactable != currentInteractable)
                {
                    currentInteractable?.OnPlayerExit();
                    currentInteractable = interactable;
                    currentInteractable.OnPlayerEnter();
                }
            }
            else if (currentInteractable != null)
            {
                currentInteractable.OnPlayerExit();
                currentInteractable = null;
            }
        }
        else if (currentInteractable != null)
        {
            currentInteractable.OnPlayerExit();
            currentInteractable = null;
        }

        UpdatePrompt();
    }

    private void UpdatePrompt()
    {
        if (InteractionPromptUI.Instance == null) return;
        if (fpsController != null && fpsController.IsMovementLocked())
        {
            InteractionPromptUI.Instance.HidePrompt();
            return;
        }
        if (currentInteractable != null)
        {
            if (currentInteractable.IsInteractionAvailable())
            {
                InteractionPromptUI.Instance.ShowPrompt(currentInteractable.GetInteractionPrompt());
            }
            else
            {
                currentInteractable.OnPlayerExit();
                currentInteractable = null;
                InteractionPromptUI.Instance.HidePrompt();
            }
        }
        else
        {
            InteractionPromptUI.Instance.HidePrompt();
        }
    }

    public FirstPersonController GetFirstPersonController()
    {
        return fpsController;
    }

    public Camera GetPlayerCamera()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
        return playerCamera;
    }

    public void SetPlayerVisualsActive(bool value)
    {
        if (visualRoots == null) return;
        foreach (var root in visualRoots)
        {
            if (root != null)
            {
                root.SetActive(value);
            }
        }
    }
}
