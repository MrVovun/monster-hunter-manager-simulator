using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class GuildConstructionListItem : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image stateIndicator;
    [SerializeField] private GameObject selectionHighlight;

    private GuildConstructionDefinition definition;
    private Action<GuildConstructionDefinition> onSelected;

    public void Initialize(GuildConstructionDefinition def, Action<GuildConstructionDefinition> selectedCallback)
    {
        definition = def;
        onSelected = selectedCallback;
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

    public void SetSelected(bool selected)
    {
        if (selectionHighlight != null)
        {
            selectionHighlight.SetActive(selected);
        }
    }

    public GuildConstructionDefinition GetDefinition() => definition;
}
