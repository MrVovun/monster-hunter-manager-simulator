using UnityEngine;

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
    private PlayerFloorCleaning floorCleaning;

    private void Awake()
    {
        fpsController = GetComponent<FirstPersonController>();
        floorCleaning = GetComponent<PlayerFloorCleaning>();
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
        // Cleaning can allow walking/looking, but other interactions must wait until it finishes.
        if (floorCleaning != null && floorCleaning.IsCleaning)
        {
            currentInteractable?.OnPlayerExit();
            currentInteractable = null;
            InteractionPromptUI.Instance?.HidePrompt();
            return;
        }
        UpdateFocus();

        if (fpsController != null && fpsController.IsMovementLocked())
        {
            return;
        }

        if (WasInteractPressed() && currentInteractable != null)
        {
            if (!currentInteractable.IsInteractionAvailable())
            {
                InteractionPromptUI.Instance?.ShowPrompt(currentInteractable.GetUnavailablePrompt());
                return;
            }

            currentInteractable.Interact(this);
            if (InteractionPromptUI.Instance != null)
            {
                InteractionPromptUI.Instance.HidePrompt();
            }
        }
    }

    private bool WasInteractPressed()
    {
        return InputKeyUtility.WasPressed(interactKey);
    }

    private void UpdateFocus()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        Interactable nextInteractable = null;
        float focusDistance = interactionRange;
        if (Physics.Raycast(ray, out RaycastHit hit, interactionRange, interactionMask, QueryTriggerInteraction.Collide))
        {
            focusDistance = hit.distance;
            nextInteractable = hit.collider.GetComponentInParent<Interactable>();
            if (nextInteractable != null && !nextInteractable.isActiveAndEnabled) nextInteractable = null;
        }

        // Prefer an existing interactable (for example a plate or a bell) over dirt.
        var dirt = MainHallFloorDirtManager.Instance;
        if (nextInteractable == null && dirt != null &&
            dirt.TryGetCleaningTarget(ray, focusDistance, interactionMask, out Interactable cleaningTarget))
        {
            nextInteractable = cleaningTarget;
        }

        if (nextInteractable != currentInteractable)
        {
            currentInteractable?.OnPlayerExit();
            currentInteractable = nextInteractable;
            currentInteractable?.OnPlayerEnter();
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
                InteractionPromptUI.Instance.ShowPrompt(currentInteractable.GetUnavailablePrompt());
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
