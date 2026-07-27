using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ArmoryWeaponOptionUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image background;
    [SerializeField] private GameObject selectedHighlight;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Color normalBackgroundColor = new Color(0f, 0f, 0f, 0f);
    [SerializeField] private Color hoverBackgroundColor = new Color(1f, 1f, 1f, 0.08f);
    [SerializeField] private Color selectedBackgroundColor = new Color(1f, 1f, 1f, 0.16f);

    private P09HumanoidLibrary.PartOption option;
    private Action<P09HumanoidLibrary.PartOption> onSelected;
    private bool selected;
    private bool hovered;
    private bool interactable = true;

    public void Initialize(P09HumanoidLibrary.PartOption targetOption, Action<P09HumanoidLibrary.PartOption> selectedCallback)
    {
        option = targetOption;
        onSelected = selectedCallback;

        if (background == null) background = GetComponent<Image>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

        if (nameText != null)
        {
            nameText.text = option != null ? option.displayName : "Weapon";
        }

        if (iconImage != null)
        {
            Sprite icon = option != null ? option.icon : null;
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
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
        onSelected?.Invoke(option);
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
