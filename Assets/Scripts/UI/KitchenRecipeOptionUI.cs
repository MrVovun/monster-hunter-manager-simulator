using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class KitchenRecipeOptionUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Image iconImage;
    [SerializeField] private Button chooseButton;
    [SerializeField] private Image background;
    [SerializeField] private GameObject selectedHighlight;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Color normalBackgroundColor = new Color(0f, 0f, 0f, 0f);
    [SerializeField] private Color hoverBackgroundColor = new Color(1f, 1f, 1f, 0.08f);
    [SerializeField] private Color selectedBackgroundColor = new Color(1f, 1f, 1f, 0.16f);

    private KitchenRecipe recipe;
    private Action<KitchenRecipe> onSelected;
    private Action<KitchenRecipe> onChosen;
    private bool selected;
    private bool hovered;
    private bool interactable = true;
    private bool canChoose = true;

    public void Initialize(KitchenRecipe targetRecipe, Action<KitchenRecipe> selectedCallback, Action<KitchenRecipe> chosenCallback, MonsterTrait rolledCounterTrait = null)
    {
        recipe = targetRecipe;
        onSelected = selectedCallback;
        onChosen = chosenCallback;

        if (background == null) background = GetComponent<Image>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (chooseButton != null)
        {
            chooseButton.onClick.RemoveAllListeners();
            chooseButton.onClick.AddListener(HandleChoosePressed);
        }

        if (nameText != null)
        {
            nameText.text = recipe != null ? recipe.GetDisplayName() : "Recipe";
        }
        if (descriptionText != null)
        {
            descriptionText.text = recipe != null ? recipe.description : string.Empty;
        }
        if (summaryText != null)
        {
            summaryText.text = recipe != null ? recipe.BuildEffectSummary(rolledCounterTrait) : string.Empty;
        }
        if (iconImage != null)
        {
            iconImage.sprite = recipe != null ? recipe.icon : null;
            iconImage.enabled = iconImage.sprite != null;
        }

        RefreshVisuals();
    }

    public void RefreshFromRecipe(MonsterTrait rolledCounterTrait = null)
    {
        if (nameText != null)
        {
            nameText.text = recipe != null ? recipe.GetDisplayName() : "Recipe";
        }
        if (descriptionText != null)
        {
            descriptionText.text = recipe != null ? recipe.description : string.Empty;
        }
        if (summaryText != null)
        {
            summaryText.text = recipe != null ? recipe.BuildEffectSummary(rolledCounterTrait) : string.Empty;
        }
        if (iconImage != null)
        {
            iconImage.sprite = recipe != null ? recipe.icon : null;
            iconImage.enabled = iconImage.sprite != null;
        }
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

    public void SetCanChoose(bool value)
    {
        canChoose = value;
        RefreshVisuals();
    }

    public void SetStatus(string value)
    {
        if (statusText != null)
        {
            statusText.text = value;
        }
    }

    public KitchenRecipe GetRecipe()
    {
        return recipe;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!interactable || eventData.button != PointerEventData.InputButton.Left) return;
        onSelected?.Invoke(recipe);
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
            canvasGroup.alpha = interactable ? 1f : 0.55f;
            canvasGroup.interactable = interactable;
            canvasGroup.blocksRaycasts = interactable;
        }

        if (chooseButton != null)
        {
            chooseButton.interactable = interactable && canChoose;
        }
    }

    private void HandleChoosePressed()
    {
        if (!interactable || !canChoose) return;
        onSelected?.Invoke(recipe);
        onChosen?.Invoke(recipe);
    }
}
