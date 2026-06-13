using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BestiaryUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button selectButton;

    [Header("List")]
    [SerializeField] private RectTransform monsterListParent;
    [SerializeField] private GameObject familyHeaderPrefab;
    [SerializeField] private GameObject monsterListItemPrefab;

    [Header("Details")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text familyText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Transform possibleTraitsParent;
    [SerializeField] private TMP_Text possibleTraitsFallbackText;
    [SerializeField] private TMP_Text completionText;
    [SerializeField] private Image portraitImage;

    [Header("Investigation Context")]
    [SerializeField] private GameObject contextPanel;
    [SerializeField] private TMP_Text knownTagsText;
    [SerializeField] private Transform knownTraitsParent;
    [SerializeField] private TMP_Text knownTraitsFallbackText;
    [Header("Trait Items")]
    [SerializeField] private GameObject traitItemPrefab;
    [SerializeField] private TraitTooltipPanel traitTooltipPanel;
    [SerializeField] private Image traitIconPrototype;

    private readonly List<GameObject> spawnedEntries = new List<GameObject>();
    private readonly List<GameObject> spawnedPossibleTraits = new List<GameObject>();
    private readonly List<GameObject> spawnedKnownTraits = new List<GameObject>();
    private readonly Dictionary<GameObject, Image> listEntryPortraitLookup = new Dictionary<GameObject, Image>();
    private List<MonsterData> availableMonsters = new List<MonsterData>();
    private MonsterData currentSelection;
    private System.Action<MonsterData> onSelection;
    private System.Action onClosed;
    private bool selectionEnabled;
    private InvestigationCase contextCase;
    private OrderManager orderManager;
    public bool IsVisible => panelRoot != null ? panelRoot.gameObject.activeInHierarchy : gameObject.activeInHierarchy;
    private CursorLockMode previousLockState;
    private bool previousCursorVisible;
    private bool cursorManaged;

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(() => Hide());
        }

        if (selectButton != null)
        {
            selectButton.onClick.AddListener(HandleSelectPressed);
        }

        SetActive(false);
    }

    public void Show(List<MonsterData> monsters, bool allowSelection, InvestigationCase context, System.Action<MonsterData> selectionCallback, System.Action closedCallback)
    {
        availableMonsters = monsters ?? new List<MonsterData>();
        selectionEnabled = allowSelection;
        onSelection = selectionCallback;
        onClosed = closedCallback;
        contextCase = context;
        if (selectButton != null)
        {
            EnsureButtonVisualFeedback(selectButton);
            selectButton.gameObject.SetActive(selectionEnabled);
            selectButton.interactable = selectionEnabled && currentSelection != null;
            RefreshButtonVisual(selectButton);
        }

        if (orderManager == null && GameManager.Instance != null)
        {
            orderManager = GameManager.Instance.GetOrderManager();
        }

        SetActive(true);
        EnsureCursor();
        currentSelection = null;
        MonsterData defaultMonster = BuildList();
        bool appliedDefault = TryApplyDefaultSelection(defaultMonster);
        if (!appliedDefault)
        {
            ShowDetails(null);
        }
        RefreshContext();
    }

    public void Hide()
    {
        SetActive(false);
        RestoreCursorIfNeeded();
        onClosed?.Invoke();
        onClosed = null;
        onSelection = null;
        contextCase = null;
        currentSelection = null;
    }

    private MonsterData BuildList()
    {
        foreach (var go in spawnedEntries)
        {
            if (go != null) Destroy(go);
        }
        spawnedEntries.Clear();
        listEntryPortraitLookup.Clear();

        if (monsterListParent == null || monsterListItemPrefab == null) return null;

        var grouped = availableMonsters
            .Where(m => m != null)
            .GroupBy(m =>
            {
                string family = m.GetTagValue("family");
                return string.IsNullOrWhiteSpace(family) ? "Unknown" : family;
            })
            .OrderBy(g => g.Key);

        MonsterData firstSelectable = null;

        foreach (var group in grouped)
        {
            if (familyHeaderPrefab != null)
            {
                var header = Instantiate(familyHeaderPrefab, monsterListParent);
                var headerText = header.GetComponentInChildren<TMP_Text>();
                if (headerText != null)
                {
                    headerText.text = group.Key;
                }
                spawnedEntries.Add(header);
            }

            var sortedMonsters = group.OrderBy(m => m.displayName).ToList();
            foreach (var monster in sortedMonsters)
            {
                var entry = Instantiate(monsterListItemPrefab, monsterListParent);
                spawnedEntries.Add(entry);
                ConfigureMonsterListEntry(entry, monster);
                Button entryButton = EnsureButton(entry);
                EnsureButtonVisualFeedback(entryButton);
                entryButton?.onClick.AddListener(() =>
                {
                    InteractionFeedbackManager.PlayUIClick();
                    ShowDetails(monster);
                });
                if (firstSelectable == null)
                {
                    firstSelectable = monster;
                }
            }
        }

        return firstSelectable;
    }

    private Button EnsureButton(GameObject entry)
    {
        if (entry == null) return null;
        Button button = entry.GetComponent<Button>();
        if (button != null) return button;

        var image = entry.GetComponent<Image>();
        if (image == null)
        {
            image = entry.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
        }

        button = entry.AddComponent<Button>();
        return button;
    }

    private bool TryApplyDefaultSelection(MonsterData monster)
    {
        if (!selectionEnabled || monster == null)
        {
            return false;
        }

        ShowDetails(monster);
        return true;
    }

    private void ShowDetails(MonsterData monster)
    {
        currentSelection = monster;
        if (selectButton != null)
        {
            selectButton.interactable = selectionEnabled && monster != null;
            RefreshButtonVisual(selectButton);
        }

        if (monster == null)
        {
            if (nameText != null) nameText.text = "Select a Monster";
            if (familyText != null) familyText.text = string.Empty;
            if (descriptionText != null) descriptionText.text = string.Empty;
            if (completionText != null) completionText.text = string.Empty;
            if (portraitImage != null)
            {
                portraitImage.sprite = null;
                portraitImage.enabled = false;
            }
            PopulatePossibleTraits(null);
            PopulateKnownTraits();
            return;
        }

        if (nameText != null) nameText.text = monster.displayName;
        if (familyText != null)
        {
            string family = monster.GetTagValue("family") ?? "Unknown";
            familyText.text = $"Family: {family}";
        }
        if (descriptionText != null) descriptionText.text = monster.description;
        PopulatePossibleTraits(monster.possibleTraits);
        if (completionText != null)
        {
            int count = orderManager != null ? orderManager.GetMonsterCompletionCount(monster) : 0;
            completionText.text = $"{count}";
        }
        if (portraitImage != null)
        {
            portraitImage.sprite = monster.portrait;
            portraitImage.enabled = monster.portrait != null;
        }

        RefreshContext();
    }

    private void RefreshContext()
    {
        if (contextPanel != null)
        {
            contextPanel.SetActive(contextCase != null);
        }

        if (contextCase == null)
        {
            if (knownTagsText != null) knownTagsText.text = string.Empty;
            return;
        }

        if (knownTagsText != null)
        {
            if (contextCase.knownTags != null && contextCase.knownTags.Count > 0)
            {
                var lines = contextCase.knownTags.Select(tag => $"{tag.categoryName}: {(!string.IsNullOrEmpty(tag.valueName) ? tag.valueName : "???")}");
                knownTagsText.text = string.Join("\n", lines);
            }
            else
            {
                knownTagsText.text = "Tags: ???";
            }
        }

        PopulateKnownTraits();
    }

    private void HandleSelectPressed()
    {
        if (!selectionEnabled || currentSelection == null) return;
        InteractionFeedbackManager.PlayUIClick();
        onSelection?.Invoke(currentSelection);
        Hide();
    }

    private void RefreshButtonVisual(Button button)
    {
        if (button == null) return;
        var visualFeedback = button.GetComponent<UIButtonVisualFeedback>();
        if (visualFeedback != null)
        {
            visualFeedback.RefreshVisualState(true);
        }
    }

    private void EnsureButtonVisualFeedback(Button button)
    {
        if (button == null) return;
        var visualFeedback = button.GetComponent<UIButtonVisualFeedback>();
        if (visualFeedback == null)
        {
            visualFeedback = button.gameObject.AddComponent<UIButtonVisualFeedback>();
        }

        visualFeedback.Configure(
            colorEnabled: true,
            scaleEnabled: true,
            hover: new Color(0.82f, 0.82f, 0.82f, 1f),
            pressedState: new Color(0.65f, 0.65f, 0.65f, 1f),
            disabled: new Color(0.45f, 0.45f, 0.45f, 0.7f),
            hoverScaleValue: 1.01f,
            pressedScaleValue: 0.99f,
            duration: 0.08f);
    }

    private void SetActive(bool value)
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

    private void EnsureCursor()
    {
        if (cursorManaged) return;
        previousLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        cursorManaged = true;
    }

    private void RestoreCursorIfNeeded()
    {
        if (!cursorManaged) return;
        Cursor.lockState = previousLockState;
        Cursor.visible = previousCursorVisible;
        cursorManaged = false;
    }

    private void PopulatePossibleTraits(IEnumerable<MonsterTrait> traits)
    {
        ClearTraitEntries(spawnedPossibleTraits, possibleTraitsParent, possibleTraitsFallbackText);
        var list = traits?.Where(t => t != null).ToList();
        if (list == null || list.Count == 0)
        {
            if (possibleTraitsFallbackText != null)
            {
                possibleTraitsFallbackText.text = "Traits: ???";
            }
            return;
        }

        if (possibleTraitsFallbackText != null)
        {
            possibleTraitsFallbackText.text = string.Empty;
        }

        foreach (var trait in list)
        {
            var item = CreateTraitItem(trait);
            item.transform.SetParent(possibleTraitsParent, false);
            spawnedPossibleTraits.Add(item);
        }
    }

    private void PopulateKnownTraits()
    {
        ClearTraitEntries(spawnedKnownTraits, knownTraitsParent, knownTraitsFallbackText);
        if (contextCase == null || contextCase.confirmedTraitIds == null || contextCase.confirmedTraitIds.Count == 0)
        {
            if (knownTraitsFallbackText != null)
            {
                knownTraitsFallbackText.text = "Traits: ???";
            }
            return;
        }

        List<MonsterTrait> traits = new List<MonsterTrait>();
        foreach (var traitId in contextCase.confirmedTraitIds)
        {
            var trait = contextCase.truthTraits?.FirstOrDefault(t => t != null && t.traitId == traitId);
            if (trait != null)
            {
                traits.Add(trait);
            }
        }

        if (traits.Count == 0)
        {
            if (knownTraitsFallbackText != null)
            {
                knownTraitsFallbackText.text = "Traits: ???";
            }
            return;
        }

        if (knownTraitsFallbackText != null)
        {
            knownTraitsFallbackText.text = string.Empty;
        }

        foreach (var trait in traits)
        {
            var item = CreateTraitItem(trait);
            item.transform.SetParent(knownTraitsParent, false);
            spawnedKnownTraits.Add(item);
        }
    }

    private void ClearTraitEntries(List<GameObject> list, Transform parent, TMP_Text fallback)
    {
        foreach (var go in list)
        {
            if (go != null) Destroy(go);
        }
        list.Clear();

        if (parent != null)
        {
            foreach (Transform child in parent)
            {
                Destroy(child.gameObject);
            }
        }

        if (fallback != null)
        {
            fallback.text = string.Empty;
        }
    }

    private GameObject CreateTraitItem(MonsterTrait trait)
    {
        GameObject item = traitItemPrefab != null ? Instantiate(traitItemPrefab) : new GameObject("Trait");
        RectTransform rect = item.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = item.AddComponent<RectTransform>();
        }

        TMP_Text text = item.GetComponentInChildren<TMP_Text>();
        if (text != null)
        {
            text.text = string.Empty;
            text.gameObject.SetActive(false);
        }

        Image icon = FindOrCreateTraitIcon(item);
        if (icon != null)
        {
            icon.sprite = trait != null ? trait.icon : null;
            icon.enabled = icon.sprite != null;
        }

        if (traitTooltipPanel != null)
        {
            var tooltip = item.GetComponent<TraitTooltipTrigger>();
            if (tooltip == null)
            {
                tooltip = item.AddComponent<TraitTooltipTrigger>();
            }
            tooltip.Initialize(traitTooltipPanel, rect, trait != null ? trait.displayName : "Trait", trait != null ? trait.description : string.Empty);
        }

        return item;
    }

    private Image FindOrCreateTraitIcon(GameObject item)
    {
        if (item == null) return null;

        Image icon = null;
        var images = item.GetComponentsInChildren<Image>(true);
        foreach (var candidate in images)
        {
            if (candidate == null) continue;
            if (candidate.transform == item.transform && item.GetComponent<Button>() != null)
            {
                continue;
            }
            icon = candidate;
            break;
        }

        if (icon == null && traitIconPrototype != null)
        {
            icon = Instantiate(traitIconPrototype, item.transform);
        }

        if (icon == null)
        {
            icon = item.GetComponent<Image>();
            if (icon == null)
            {
                icon = item.AddComponent<Image>();
            }
        }

        return icon;
    }

    private void ConfigureMonsterListEntry(GameObject entry, MonsterData monster)
    {
        if (entry == null) return;

        var label = entry.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.text = monster != null ? monster.displayName : string.Empty;
        }

        UpdateMonsterListPortrait(entry, monster);
    }

    private void UpdateMonsterListPortrait(GameObject entry, MonsterData monster)
    {
        if (entry == null) return;

        if (!listEntryPortraitLookup.TryGetValue(entry, out var portrait) || portrait == null)
        {
            portrait = FindListItemPortraitImage(entry);
            listEntryPortraitLookup[entry] = portrait;
        }

        if (portrait != null)
        {
            Sprite sprite = monster != null ? monster.portrait : null;
            portrait.sprite = sprite;
            portrait.enabled = sprite != null;
        }
    }

    private Image FindListItemPortraitImage(GameObject entry)
    {
        if (entry == null) return null;

        Image rootImage = entry.GetComponent<Image>();
        Image fallback = null;
        var images = entry.GetComponentsInChildren<Image>(true);
        foreach (var image in images)
        {
            if (image == null || image == rootImage) continue;

            string imageName = image.gameObject.name;
            if (!string.IsNullOrEmpty(imageName) && imageName.IndexOf("portrait", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return image;
            }

            if (fallback == null && image.transform != entry.transform)
            {
                fallback = image;
            }
        }

        return fallback;
    }
}
