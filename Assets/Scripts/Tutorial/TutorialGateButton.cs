using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class TutorialGateButton : MonoBehaviour
{
    [SerializeField] private string actionId;

    private Button button;
    private bool baseInteractable;

    private void Awake()
    {
        button = GetComponent<Button>();
        baseInteractable = button != null && button.interactable;
    }

    private void OnEnable()
    {
        TutorialManager.OnTutorialGateChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        TutorialManager.OnTutorialGateChanged -= Refresh;
    }

    public void SetBaseInteractable(bool interactable)
    {
        baseInteractable = interactable;
        Refresh();
    }

    private void Refresh()
    {
        if (button == null) return;
        button.interactable = baseInteractable && TutorialManager.IsActionAllowed(actionId);
        var visualFeedback = button.GetComponent<UIButtonVisualFeedback>();
        if (visualFeedback != null)
        {
            visualFeedback.RefreshVisualState(true);
        }
    }
}
