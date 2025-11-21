using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private LayerMask interactionMask = ~0;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    private Interactable currentInteractable;
    private FirstPersonController fpsController;

    private void Awake()
    {
        fpsController = GetComponent<FirstPersonController>();
    }

    private void Update()
    {
        UpdateFocus();

        if (Input.GetKeyDown(interactKey) && currentInteractable != null)
        {
            currentInteractable.Interact(this);
        }
    }

    private void UpdateFocus()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactionRange, interactionMask, QueryTriggerInteraction.Collide))
        {
            Interactable interactable = hit.collider.GetComponentInParent<Interactable>();

            if (interactable != null && interactable != currentInteractable)
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

    public FirstPersonController GetFirstPersonController()
    {
        return fpsController;
    }
}
