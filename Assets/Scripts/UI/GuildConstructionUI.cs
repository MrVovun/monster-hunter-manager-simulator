using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuildConstructionUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button buildButton;

    [Header("List")]
    [SerializeField] private Transform listParent;
    [SerializeField] private GuildConstructionListItem listItemPrefab;
    [SerializeField] private Color availableColor = new Color(0.2f, 0.8f, 0.2f, 1f);
    [SerializeField] private Color unavailableColor = new Color(0.8f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color builtColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    [Header("Details")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text goldRequirementText;
    [SerializeField] private TMP_Text reputationRequirementText;
    [SerializeField] private TMP_Text statusText;

    [Header("Plan View")]
    [SerializeField] private Image planBaseImage;
    [SerializeField] private Image overlayTemplate;

    private GuildConstructionManager manager;
    private readonly List<GuildConstructionListItem> spawnedItems = new List<GuildConstructionListItem>();
    private readonly Dictionary<GuildConstructionDefinition, Image> overlayLookup = new Dictionary<GuildConstructionDefinition, Image>();

    private GuildConstructionDefinition selectedDefinition;
    private Action onClosed;
    private bool cursorCaptured;
    private CursorLockMode previousLockMode;
    private bool previousCursorVisible;

    public void Show(GuildConstructionManager targetManager, Action closedCallback)
    {
        if (targetManager == null || listItemPrefab == null || listParent == null)
        {
            Debug.LogWarning("GuildConstructionUI: Missing manager or UI references.");
            return;
        }

        manager = targetManager;
        onClosed = closedCallback;
        manager.OnStateChanged += HandleManagerStateChanged;

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }
        if (buildButton != null)
        {
            buildButton.onClick.RemoveAllListeners();
            buildButton.onClick.AddListener(HandleBuildPressed);
        }

        EnsureOverlayTemplateHidden();
        SetRootActive(true);
        CaptureCursor();
        RefreshList();
    }

    public void Hide()
    {
        if (manager != null)
        {
            manager.OnStateChanged -= HandleManagerStateChanged;
        }
        ClearList();
        SetRootActive(false);
        ReleaseCursor();
        var callback = onClosed;
        onClosed = null;
        callback?.Invoke();
    }

    private void HandleManagerStateChanged()
    {
        RefreshList();
    }

    private void RefreshList()
    {
        var definitions = manager != null ? manager.GetDefinitionsForDisplay() : null;
        ClearList();

        if (definitions == null || definitions.Count == 0)
        {
            selectedDefinition = null;
            UpdateDetails();
            UpdateOverlayVisuals();
            return;
        }

        if (selectedDefinition == null || !definitions.Contains(selectedDefinition))
        {
            selectedDefinition = definitions[0];
        }

        foreach (var def in definitions)
        {
            var item = Instantiate(listItemPrefab, listParent);
            item.Initialize(def, HandleItemSelected);
            var status = manager.GetStatus(def);
            item.SetStatusColors(GetColorForStatus(status));
            item.SetSelected(def == selectedDefinition);
            spawnedItems.Add(item);
        }

        UpdateDetails();
        UpdateOverlayVisuals();
    }

    private void HandleItemSelected(GuildConstructionDefinition definition)
    {
        if (definition == null) return;
        selectedDefinition = definition;
        foreach (var item in spawnedItems)
        {
            if (item == null) continue;
            item.SetSelected(item.GetDefinition() == selectedDefinition);
        }
        UpdateDetails();
        UpdateOverlayVisuals();
    }

    private void HandleBuildPressed()
    {
        if (manager == null || selectedDefinition == null) return;
        if (manager.TryBuild(selectedDefinition))
        {
            RefreshList();
        }
    }

    private void UpdateDetails()
    {
        if (selectedDefinition == null)
        {
            if (titleText != null) titleText.text = "Select a construction";
            if (descriptionText != null) descriptionText.text = string.Empty;
            if (goldRequirementText != null) goldRequirementText.text = string.Empty;
            if (reputationRequirementText != null) reputationRequirementText.text = string.Empty;
            if (statusText != null) statusText.text = string.Empty;
            if (buildButton != null) buildButton.interactable = false;
            return;
        }

        if (titleText != null) titleText.text = selectedDefinition.displayName;
        if (descriptionText != null) descriptionText.text = selectedDefinition.description;
        if (goldRequirementText != null) goldRequirementText.text = $"Gold: {selectedDefinition.goldCost}";
        if (reputationRequirementText != null) reputationRequirementText.text = $"Reputation: {selectedDefinition.requiredReputation}";

        var status = manager.GetStatus(selectedDefinition);
        if (statusText != null)
        {
            statusText.text = status.ToString();
            statusText.color = GetColorForStatus(status);
        }

        if (buildButton != null)
        {
            buildButton.interactable = status == GuildConstructionManager.ConstructionStatus.Available;
        }
    }

    private void UpdateOverlayVisuals()
    {
        if (manager == null) return;
        var allDefs = manager.GetAllDefinitions();
        foreach (var def in allDefs)
        {
            if (def == null || def.planOverlay == null) continue;
            var overlay = GetOrCreateOverlay(def);
            if (overlay == null) continue;

            bool built = manager.IsBuilt(def);
            bool isSelected = def == selectedDefinition;
            if (built)
            {
                overlay.color = builtColor;
                overlay.gameObject.SetActive(true);
            }
            else if (isSelected)
            {
                overlay.color = GetColorForStatus(manager.GetStatus(def));
                overlay.gameObject.SetActive(true);
            }
            else
            {
                overlay.gameObject.SetActive(false);
            }
        }
    }

    private Image GetOrCreateOverlay(GuildConstructionDefinition definition)
    {
        if (definition == null) return null;
        if (overlayLookup.TryGetValue(definition, out var cached) && cached != null)
        {
            cached.sprite = definition.planOverlay;
            return cached;
        }

        if (overlayTemplate == null || definition.planOverlay == null) return null;
        var instance = Instantiate(overlayTemplate, overlayTemplate.transform.parent);
        instance.gameObject.SetActive(false);
        instance.sprite = definition.planOverlay;
        overlayLookup[definition] = instance;
        return instance;
    }

    private void ClearList()
    {
        foreach (var item in spawnedItems)
        {
            if (item != null)
            {
                Destroy(item.gameObject);
            }
        }
        spawnedItems.Clear();
    }

    private void EnsureOverlayTemplateHidden()
    {
        if (overlayTemplate != null)
        {
            overlayTemplate.gameObject.SetActive(false);
        }
    }

    private void SetRootActive(bool value)
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(value);
        }
        else
        {
            gameObject.SetActive(value);
        }
    }

    private Color GetColorForStatus(GuildConstructionManager.ConstructionStatus status)
    {
        switch (status)
        {
            case GuildConstructionManager.ConstructionStatus.Available:
                return availableColor;
            case GuildConstructionManager.ConstructionStatus.Unavailable:
                return unavailableColor;
            default:
                return builtColor;
        }
    }

    private void CaptureCursor()
    {
        if (cursorCaptured) return;
        previousLockMode = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        cursorCaptured = true;
    }

    private void ReleaseCursor()
    {
        if (!cursorCaptured) return;
        Cursor.lockState = previousLockMode;
        Cursor.visible = previousCursorVisible;
        cursorCaptured = false;
    }
}
