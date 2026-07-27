using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardGameCardView : MonoBehaviour
{
    [SerializeField] private Image cardImage;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private Button button;
    [SerializeField] private GameObject selectedHighlight;
    [SerializeField] private string positiveFormat = "+{0}";

    private int value;
    private Action<int> onClicked;

    public void Initialize(int cardValue, Sprite sprite, bool faceUp, Action<int> clickCallback = null)
    {
        value = cardValue;
        onClicked = clickCallback;

        if (cardImage == null) cardImage = GetComponent<Image>();
        if (valueText == null) valueText = GetComponentInChildren<TMP_Text>(true);
        if (button == null) button = GetComponent<Button>();

        if (cardImage != null)
        {
            cardImage.sprite = sprite;
            cardImage.enabled = sprite != null;
            cardImage.preserveAspect = true;
        }

        if (valueText != null)
        {
            valueText.text = faceUp ? FormatValue(cardValue) : string.Empty;
            valueText.gameObject.SetActive(faceUp);
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.interactable = onClicked != null;
            if (onClicked != null)
            {
                button.onClick.AddListener(() => onClicked?.Invoke(value));
            }
        }

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (selectedHighlight != null)
        {
            selectedHighlight.SetActive(selected);
        }
    }

    private string FormatValue(int cardValue)
    {
        return cardValue > 0 ? string.Format(positiveFormat, cardValue) : cardValue.ToString();
    }
}
