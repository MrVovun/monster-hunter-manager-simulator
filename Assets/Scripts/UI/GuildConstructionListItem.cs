using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class GuildConstructionListItem : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private Image stateIndicator;
    [SerializeField] private Image background;
    [SerializeField] private GameObject selectionHighlight;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Color normalBackgroundColor = new Color(0f, 0f, 0f, 0f);
    [SerializeField] private Color hoverBackgroundColor = new Color(1f, 1f, 1f, 0.08f);
    [SerializeField] private Color selectedBackgroundColor = new Color(1f, 1f, 1f, 0.16f);
    [SerializeField] private Color normalTextColor = Color.white;
    [SerializeField] private Color disabledTextColor = new Color(0.65f, 0.65f, 0.65f, 1f);

    private GuildConstructionDefinition definition;
    private Action<GuildConstructionDefinition> onSelected;
    private GuildConstructionManager.ConstructionStatus status;
    private bool selected;
    private bool hovered;

    public void Initialize(GuildConstructionDefinition def, Action<GuildConstructionDefinition> selectedCallback)
    {
        definition = def;
        onSelected = selectedCallback;
        if (background == null)
        {
            background = GetComponent<Image>();
        }
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
        if (label != null)
        {
            label.text = def != null ? def.displayName : "Construction";
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        onSelected?.Invoke(definition);
    }

    public void SetStatusColors(Color color)
    {
        if (stateIndicator != null)
        {
            stateIndicator.color = color;
        }
    }

    public void SetStatus(GuildConstructionManager.ConstructionStatus newStatus, Color color)
    {
        status = newStatus;
        SetStatusColors(color);
        if (statusLabel != null)
        {
            statusLabel.text = FormatStatus(status);
            statusLabel.color = color;
        }
        RefreshVisuals();
    }

    public void SetSelected(bool selected)
    {
        this.selected = selected;
        if (selectionHighlight != null)
        {
            selectionHighlight.SetActive(selected);
        }
        RefreshVisuals();
    }

    public GuildConstructionDefinition GetDefinition() => definition;

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        RefreshVisuals();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        RefreshVisuals();
    }

    private void RefreshVisuals()
    {
        if (background != null)
        {
            background.color = selected
                ? selectedBackgroundColor
                : hovered ? hoverBackgroundColor : normalBackgroundColor;
        }

        if (label != null)
        {
            label.color = status == GuildConstructionManager.ConstructionStatus.Unavailable
                ? disabledTextColor
                : normalTextColor;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = status == GuildConstructionManager.ConstructionStatus.Unavailable ? 0.75f : 1f;
        }
    }

    private static string FormatStatus(GuildConstructionManager.ConstructionStatus value)
    {
        switch (value)
        {
            case GuildConstructionManager.ConstructionStatus.Available:
                return "Available";
            case GuildConstructionManager.ConstructionStatus.Built:
                return "Built";
            default:
                return "Locked";
        }
    }
}
