using UnityEngine;

public class BestiaryHotkey : MonoBehaviour
{
    [SerializeField] private KeyCode hotkey = KeyCode.B;
    [SerializeField] private InvestigationManager investigationManager;

    private void Update()
    {
        if (!Input.GetKeyDown(hotkey)) return;

        var manager = ResolveManager();
        manager?.ShowBestiaryFree();
    }

    private InvestigationManager ResolveManager()
    {
        if (investigationManager != null) return investigationManager;
        return GameManager.Instance != null ? GameManager.Instance.GetInvestigationManager() : null;
    }
}
