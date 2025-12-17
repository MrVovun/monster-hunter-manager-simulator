using UnityEngine;
using UnityEngine.InputSystem;

public class BestiaryHotkey : MonoBehaviour
{
    [SerializeField] private InputActionReference bestiaryAction;
    [SerializeField] private InvestigationManager investigationManager;

    private void OnEnable()
    {
        if (bestiaryAction == null) return;

        bestiaryAction.action.performed += HandlePerformed;
        if (!bestiaryAction.action.enabled)
        {
            bestiaryAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (bestiaryAction == null) return;
        bestiaryAction.action.performed -= HandlePerformed;
    }

    private void HandlePerformed(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        ResolveManager()?.ShowBestiaryFree();
    }

    private InvestigationManager ResolveManager()
    {
        if (investigationManager != null) return investigationManager;
        return GameManager.Instance != null ? GameManager.Instance.GetInvestigationManager() : null;
    }
}
