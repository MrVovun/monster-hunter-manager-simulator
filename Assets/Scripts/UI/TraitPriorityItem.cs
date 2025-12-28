using System;
using UnityEngine;
using UnityEngine.UI;

public class TraitPriorityItem : MonoBehaviour
{
    [SerializeField] private Toggle toggle;
    [SerializeField] private Image icon;
    [SerializeField] private GameObject selectedIndicator;
    [SerializeField] private TraitTooltipTrigger tooltipTrigger;

    private string traitId;
    private HunterTrait traitData;
    private Action<string, bool> callback;

    public void Initialize(HunterTrait trait, bool enabled, Action<string, bool> onToggled, TraitTooltipPanel tooltipPanel)
    {
        traitId = trait != null ? trait.traitId : null;
        traitData = trait;
        callback = onToggled;

        if (icon != null)
        {
            icon.sprite = trait != null ? trait.icon : null;
            icon.enabled = icon.sprite != null;
        }

        if (toggle != null)
        {
            toggle.onValueChanged.RemoveAllListeners();
            toggle.SetIsOnWithoutNotify(enabled);
            toggle.onValueChanged.AddListener(HandleToggleChanged);
        }

        UpdateSelectedIndicator(enabled);
        SetupTooltip(tooltipPanel);
    }

    private void HandleToggleChanged(bool value)
    {
        UpdateSelectedIndicator(value);
        callback?.Invoke(traitId, value);
    }

    private void UpdateSelectedIndicator(bool active)
    {
        if (selectedIndicator != null)
        {
            selectedIndicator.SetActive(active);
        }
    }

    private void SetupTooltip(TraitTooltipPanel panel)
    {
        if (panel == null) return;

        if (tooltipTrigger == null)
        {
            tooltipTrigger = GetComponentInChildren<TraitTooltipTrigger>(true);
            if (tooltipTrigger == null)
            {
                tooltipTrigger = gameObject.AddComponent<TraitTooltipTrigger>();
            }
        }

        RectTransform anchor = tooltipTrigger.GetComponent<RectTransform>();
        if (anchor == null)
        {
            anchor = GetComponent<RectTransform>();
        }

        string name = traitData != null ? traitData.displayName : "Trait";
        string description = traitData != null ? traitData.description : string.Empty;
        tooltipTrigger.Initialize(panel, anchor, name, description);
    }
}
