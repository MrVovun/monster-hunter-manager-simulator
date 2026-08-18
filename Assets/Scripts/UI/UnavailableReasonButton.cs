using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UnavailableReasonButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private Button button;
    [SerializeField] private TraitTooltipPanel tooltipPanel;
    [SerializeField] private string tooltipTitle = "Unavailable";
    [TextArea(2, 4)]
    [SerializeField] private string unavailableReason;
    [SerializeField] private bool showOnHover = true;
    [SerializeField] private bool showOnClick = true;
    [SerializeField] private bool autoFindTooltipPanel = true;

    private RectTransform rectTransform;
    private bool pointerInside;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        if (pointerInside)
        {
            tooltipPanel?.Hide();
            pointerInside = false;
        }
    }

    public void SetReason(string reason)
    {
        unavailableReason = reason;
        if (pointerInside)
        {
            ShowReason();
        }
    }

    public void ClearReason()
    {
        unavailableReason = string.Empty;
        if (pointerInside)
        {
            tooltipPanel?.Hide();
        }
    }

    public static void SetReason(Button targetButton, string reason)
    {
        if (targetButton == null) return;

        var reasonButton = targetButton.GetComponent<UnavailableReasonButton>();
        if (reasonButton == null)
        {
            reasonButton = targetButton.gameObject.AddComponent<UnavailableReasonButton>();
        }

        reasonButton.SetReason(reason);
    }

    public static void ClearReason(Button targetButton)
    {
        if (targetButton == null) return;
        var reasonButton = targetButton.GetComponent<UnavailableReasonButton>();
        if (reasonButton != null)
        {
            reasonButton.ClearReason();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        if (showOnHover)
        {
            ShowReason();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        tooltipPanel?.Hide();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (showOnClick)
        {
            ShowReason();
        }
    }

    private void ShowReason()
    {
        ResolveReferences();
        if (tooltipPanel == null || rectTransform == null) return;
        if (button != null && button.interactable) return;
        if (string.IsNullOrWhiteSpace(unavailableReason)) return;

        tooltipPanel.Show(rectTransform, tooltipTitle, unavailableReason);
    }

    private void ResolveReferences()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        if (tooltipPanel == null && autoFindTooltipPanel)
        {
            tooltipPanel = SceneLookup.Find<TraitTooltipPanel>(true);
        }
    }
}
