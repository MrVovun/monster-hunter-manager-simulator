using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ArmoryHunterOptionUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Image portraitImage;
    [SerializeField] private Image background;
    [SerializeField] private GameObject selectedHighlight;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Color normalBackgroundColor = new Color(0f, 0f, 0f, 0f);
    [SerializeField] private Color hoverBackgroundColor = new Color(1f, 1f, 1f, 0.08f);
    [SerializeField] private Color selectedBackgroundColor = new Color(1f, 1f, 1f, 0.16f);

    private Hunter hunter;
    private Action<Hunter> onSelected;
    private bool selected;
    private bool hovered;
    private bool interactable = true;

    public void Initialize(Hunter targetHunter, Action<Hunter> selectedCallback)
    {
        hunter = targetHunter;
        onSelected = selectedCallback;

        if (background == null) background = GetComponent<Image>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

        if (nameText != null)
        {
            nameText.text = hunter != null && hunter.Data != null ? hunter.Data.hunterName : hunter != null ? hunter.name : "Hunter";
        }

        if (statusText != null)
        {
            statusText.text = hunter != null ? HunterStatusFormatter.GetStatus(hunter) : string.Empty;
        }

        if (portraitImage != null)
        {
            Sprite portrait = hunter != null ? hunter.Data?.portrait : null;
            portraitImage.sprite = portrait;
            portraitImage.enabled = portrait != null;
        }

        RefreshVisuals();
    }

    public void SetSelected(bool value)
    {
        selected = value;
        if (selectedHighlight != null)
        {
            selectedHighlight.SetActive(selected);
        }
        RefreshVisuals();
    }

    public void SetInteractable(bool value)
    {
        interactable = value;
        RefreshVisuals();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!interactable || eventData.button != PointerEventData.InputButton.Left) return;
        onSelected?.Invoke(hunter);
    }

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
                : hovered && interactable ? hoverBackgroundColor : normalBackgroundColor;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = interactable ? 1f : 0.45f;
            canvasGroup.interactable = interactable;
            canvasGroup.blocksRaycasts = interactable;
        }
    }
}
