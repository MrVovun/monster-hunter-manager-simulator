using UnityEngine;
using UnityEngine.EventSystems;

public class TraitTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private TraitTooltipPanel tooltipPanel;
    private RectTransform anchor;
    private string traitName;
    private string traitDescription;

    public void Initialize(TraitTooltipPanel panel, RectTransform anchorTransform, string name, string description)
    {
        tooltipPanel = panel;
        anchor = anchorTransform;
        traitName = name;
        traitDescription = description;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        tooltipPanel?.Show(anchor, traitName, traitDescription);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        tooltipPanel?.Hide();
    }
}
