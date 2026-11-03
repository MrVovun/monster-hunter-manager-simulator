using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Runtime view for the case-building portion of OrderOfferPanel. The order panel
/// remains the single owner of accept/refer/back behavior while this view owns the
/// interactive clue board and filtered monster grid.
/// </summary>
public sealed class CaseBuilderView
{
    private static readonly Color BackgroundColor = new Color(0.075f, 0.055f, 0.04f, 0.99f);
    private static readonly Color PanelColor = new Color(0.16f, 0.115f, 0.075f, 0.98f);
    private static readonly Color SlotColor = new Color(0.23f, 0.17f, 0.105f, 1f);
    private static readonly Color ClueColor = new Color(0.48f, 0.32f, 0.13f, 1f);
    private static readonly Color CandidateColor = new Color(0.19f, 0.14f, 0.095f, 1f);
    private static readonly Color SelectedColor = new Color(0.72f, 0.48f, 0.12f, 1f);
    private static readonly Color TextColor = new Color(0.96f, 0.9f, 0.77f, 1f);
    private static readonly Color MutedTextColor = new Color(0.74f, 0.68f, 0.56f, 1f);

    private readonly OrderOfferPanel owner;
    private readonly RectTransform root;
    private readonly RectTransform slotsGrid;
    private readonly RectTransform cluesGrid;
    private readonly RectTransform candidatesContent;
    private readonly TMP_Text titleText;
    private readonly TMP_Text descriptionText;
    private readonly TMP_Text orderStatsText;
    private readonly TMP_Text traitsText;
    private readonly TMP_Text selectionText;
    private readonly Button backButton;
    private readonly Button assignButton;
    private readonly Button referButton;
    private readonly TMP_Text referButtonText;

    private Order order;
    private InvestigationManager investigation;
    private EvidenceTagLibrary tagLibrary;
    private bool visible;
    private bool deferCaseRefresh;
    private string transientStatus;

    public Transform DragRoot => root;

    private CaseBuilderView(OrderOfferPanel panelOwner, RectTransform parent)
    {
        owner = panelOwner;
        root = CreateRect("CaseBuilderView", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        AddImage(root.gameObject, BackgroundColor, true);
        root.SetAsLastSibling();

        titleText = CreateText("Title", root, new Vector2(0.03f, 0.935f), new Vector2(0.97f, 0.985f), 31f, FontStyles.Bold, TextAlignmentOptions.Center);
        descriptionText = CreateText("Description", root, new Vector2(0.04f, 0.865f), new Vector2(0.96f, 0.935f), 17f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        orderStatsText = CreateText("OrderStats", root, new Vector2(0.04f, 0.82f), new Vector2(0.96f, 0.865f), 17f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        traitsText = CreateText("Traits", root, new Vector2(0.04f, 0.785f), new Vector2(0.96f, 0.825f), 15f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        traitsText.color = MutedTextColor;

        RectTransform boardPanel = CreatePanel("Board", root, new Vector2(0.025f, 0.105f), new Vector2(0.57f, 0.78f));
        CreateText("BoardLabel", boardPanel, new Vector2(0.04f, 0.92f), new Vector2(0.96f, 0.985f), 22f, FontStyles.Bold, TextAlignmentOptions.Center).text = "CASE BOARD";
        slotsGrid = CreateRect("CategorySlots", boardPanel, new Vector2(0.04f, 0.47f), new Vector2(0.96f, 0.91f), Vector2.zero, Vector2.zero);
        GridLayoutGroup slotsLayout = slotsGrid.gameObject.AddComponent<GridLayoutGroup>();
        slotsLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        slotsLayout.constraintCount = 2;
        slotsLayout.cellSize = new Vector2(215f, 76f);
        slotsLayout.spacing = new Vector2(10f, 10f);
        slotsLayout.childAlignment = TextAnchor.UpperCenter;

        CreateText("CluesLabel", boardPanel, new Vector2(0.04f, 0.40f), new Vector2(0.96f, 0.46f), 19f, FontStyles.Bold, TextAlignmentOptions.Center).text = "REVEALED CLUES";
        cluesGrid = CreateRect("RevealedClues", boardPanel, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.39f), Vector2.zero, Vector2.zero);
        GridLayoutGroup cluesLayout = cluesGrid.gameObject.AddComponent<GridLayoutGroup>();
        cluesLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        cluesLayout.constraintCount = 2;
        cluesLayout.cellSize = new Vector2(215f, 62f);
        cluesLayout.spacing = new Vector2(10f, 9f);
        cluesLayout.childAlignment = TextAnchor.UpperCenter;

        RectTransform monsterPanel = CreatePanel("Monsters", root, new Vector2(0.585f, 0.105f), new Vector2(0.975f, 0.78f));
        CreateText("MonsterLabel", monsterPanel, new Vector2(0.04f, 0.92f), new Vector2(0.96f, 0.985f), 22f, FontStyles.Bold, TextAlignmentOptions.Center).text = "MONSTER CANDIDATES";
        RectTransform viewport = CreateRect("Viewport", monsterPanel, new Vector2(0.035f, 0.17f), new Vector2(0.965f, 0.91f), Vector2.zero, Vector2.zero);
        Image viewportImage = AddImage(viewport.gameObject, new Color(0.08f, 0.055f, 0.035f, 0.85f), true);
        Mask mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;
        candidatesContent = CreateRect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        candidatesContent.pivot = new Vector2(0.5f, 1f);
        candidatesContent.anchoredPosition = Vector2.zero;
        GridLayoutGroup candidatesLayout = candidatesContent.gameObject.AddComponent<GridLayoutGroup>();
        candidatesLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        candidatesLayout.constraintCount = 3;
        candidatesLayout.cellSize = new Vector2(102f, 116f);
        candidatesLayout.spacing = new Vector2(8f, 8f);
        candidatesLayout.padding = new RectOffset(5, 5, 5, 5);
        candidatesLayout.childAlignment = TextAnchor.UpperCenter;
        ContentSizeFitter fitter = candidatesContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect scrollRect = monsterPanel.gameObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewport;
        scrollRect.content = candidatesContent;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 25f;

        selectionText = CreateText("Selection", monsterPanel, new Vector2(0.04f, 0.025f), new Vector2(0.96f, 0.16f), 16f, FontStyles.Bold, TextAlignmentOptions.Center);

        backButton = CreateButton("BackButton", root, new Vector2(0.025f, 0.025f), new Vector2(0.205f, 0.085f), "Back", new Color(0.28f, 0.22f, 0.17f, 1f));
        backButton.onClick.AddListener(owner.RequestCaseBuilderBack);
        referButton = CreateButton("ReferButton", root, new Vector2(0.61f, 0.025f), new Vector2(0.785f, 0.085f), "Refer", new Color(0.34f, 0.25f, 0.15f, 1f));
        referButtonText = referButton.GetComponentInChildren<TMP_Text>(true);
        referButton.onClick.AddListener(owner.ReferOrder);
        assignButton = CreateButton("AssignButton", root, new Vector2(0.795f, 0.025f), new Vector2(0.975f, 0.085f), "Assign the Monster", SelectedColor);
        assignButton.onClick.AddListener(owner.AcceptOrder);
    }

    public static CaseBuilderView Create(OrderOfferPanel owner, Transform parent)
    {
        if (owner == null || parent == null) return null;
        RectTransform parentRect = parent as RectTransform;
        return parentRect != null ? new CaseBuilderView(owner, parentRect) : null;
    }

    public void Show(Order boundOrder, InvestigationManager manager)
    {
        Unsubscribe();
        order = boundOrder;
        investigation = manager;
        tagLibrary = investigation != null ? investigation.GetEvidenceTagLibrary() : null;
        if (investigation != null)
        {
            investigation.OnCaseUpdated += HandleCaseUpdated;
        }
        visible = true;
        root.gameObject.SetActive(true);
        root.SetAsLastSibling();
        Refresh();
    }

    public void Hide()
    {
        visible = false;
        Unsubscribe();
        if (root != null)
        {
            root.gameObject.SetActive(false);
        }
        order = null;
        investigation = null;
        tagLibrary = null;
        transientStatus = null;
    }

    public void Refresh()
    {
        if (!visible || root == null || order == null) return;
        RefreshOrderSummary();
        RefreshSlotsAndClues();
        RefreshCandidates();
        RefreshActions();
    }

    public void ToggleClue(string categoryName, bool currentlyAssigned)
    {
        if (investigation == null) return;
        transientStatus = null;
        investigation.SetCaseClueAssigned(categoryName, !currentlyAssigned);
    }

    public bool TryDropClue(string clueCategory, string slotCategory)
    {
        if (!string.Equals(clueCategory, slotCategory, StringComparison.OrdinalIgnoreCase))
        {
            transientStatus = $"That clue belongs in the {GetCategoryLabel(clueCategory)} slot.";
            return false;
        }

        transientStatus = null;
        if (investigation == null) return false;

        // Keep the dragged token alive until OnEndDrag has restored its canvas
        // state. Rebuilding the board during OnDrop would destroy that token.
        deferCaseRefresh = true;
        try
        {
            return investigation.SetCaseClueAssigned(clueCategory, true);
        }
        finally
        {
            deferCaseRefresh = false;
        }
    }

    private void HandleCaseUpdated()
    {
        if (deferCaseRefresh) return;
        Refresh();
    }

    private void RefreshOrderSummary()
    {
        titleText.text = string.IsNullOrWhiteSpace(order.orderTitle) ? "BUILD CASE" : $"BUILD CASE — {order.orderTitle}";
        descriptionText.text = order.GetDescriptionFor(Order.DescriptionAudience.DeclaredMonster);
        orderStatsText.text = $"Difficulty {order.difficulty}   •   {order.goldReward} gold   •   {order.xpReward} XP   •   Party {order.minPartySize}–{order.maxPartySize}";

        List<string> revealedTraitNames = new List<string>();
        InvestigationCase caseData = order.investigationCase;
        if (caseData?.confirmedTraitIds != null && caseData.truthTraits != null)
        {
            foreach (string traitId in caseData.confirmedTraitIds)
            {
                MonsterTrait trait = caseData.truthTraits.FirstOrDefault(item => item != null && string.Equals(item.traitId, traitId, StringComparison.OrdinalIgnoreCase));
                if (trait != null)
                {
                    revealedTraitNames.Add(string.IsNullOrWhiteSpace(trait.displayName) ? trait.name : trait.displayName);
                }
            }
        }
        traitsText.text = revealedTraitNames.Count > 0
            ? $"Revealed traits: {string.Join(", ", revealedTraitNames)}"
            : "Revealed traits: ???";
    }

    private void RefreshSlotsAndClues()
    {
        ClearChildren(slotsGrid);
        ClearChildren(cluesGrid);
        InvestigationCase caseData = order.investigationCase;
        IReadOnlyList<EvidenceTagLibrary.TagCategory> categories = tagLibrary != null ? tagLibrary.Categories : null;

        if (categories != null)
        {
            foreach (EvidenceTagLibrary.TagCategory category in categories)
            {
                if (category == null || string.IsNullOrWhiteSpace(category.categoryName)) continue;
                CreateCategorySlot(category, caseData);
            }
        }

        if (caseData?.knownTags == null || caseData.knownTags.Count == 0)
        {
            TMP_Text emptyText = CreateText("NoClues", cluesGrid, Vector2.zero, Vector2.one, 16f, FontStyles.Italic, TextAlignmentOptions.Center);
            emptyText.text = "Ask the client questions to reveal clues.";
            emptyText.color = MutedTextColor;
            return;
        }

        foreach (InvestigationCase.TagKnowledge clue in caseData.knownTags)
        {
            if (clue == null || string.IsNullOrWhiteSpace(clue.categoryName) || caseData.IsTagAssigned(clue.categoryName)) continue;
            CreateClueToken(cluesGrid, clue, false);
        }
    }

    private void CreateCategorySlot(EvidenceTagLibrary.TagCategory category, InvestigationCase caseData)
    {
        RectTransform slot = CreateRect($"Slot_{category.categoryName}", slotsGrid, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        AddImage(slot.gameObject, SlotColor, true);
        CaseBuilderCategoryDropTarget dropTarget = slot.gameObject.AddComponent<CaseBuilderCategoryDropTarget>();
        dropTarget.Configure(this, category.categoryName);
        TMP_Text categoryText = CreateText("Category", slot, new Vector2(0.03f, 0.58f), new Vector2(0.97f, 0.96f), 14f, FontStyles.Bold, TextAlignmentOptions.Center);
        categoryText.text = GetCategoryLabel(category.categoryName).ToUpperInvariant();
        categoryText.color = MutedTextColor;

        InvestigationCase.TagKnowledge assigned = caseData?.assignedTags?.FirstOrDefault(tag => tag != null && string.Equals(tag.categoryName, category.categoryName, StringComparison.OrdinalIgnoreCase));
        if (assigned != null)
        {
            RectTransform tokenParent = CreateRect("AssignedClue", slot, new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.57f), Vector2.zero, Vector2.zero);
            CreateClueToken(tokenParent, assigned, true);
        }
        else
        {
            TMP_Text empty = CreateText("Empty", slot, new Vector2(0.04f, 0.07f), new Vector2(0.96f, 0.55f), 14f, FontStyles.Italic, TextAlignmentOptions.Center);
            empty.text = "Drop clue here";
            empty.color = MutedTextColor;
        }
    }

    private void CreateClueToken(RectTransform parent, InvestigationCase.TagKnowledge clue, bool assigned)
    {
        RectTransform token = CreateRect($"Clue_{clue.categoryName}", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        AddImage(token.gameObject, assigned ? SelectedColor : ClueColor, true);
        CanvasGroup canvasGroup = token.gameObject.AddComponent<CanvasGroup>();
        CaseBuilderClueItem item = token.gameObject.AddComponent<CaseBuilderClueItem>();
        item.Configure(this, clue.categoryName, assigned, canvasGroup);
        TMP_Text clueText = CreateText("Label", token, new Vector2(0.035f, 0.08f), new Vector2(0.965f, 0.92f), 15f, FontStyles.Bold, TextAlignmentOptions.Center);
        clueText.text = $"{GetCategoryLabel(clue.categoryName)}: {GetValueLabel(clue.categoryName, clue.valueName)}";
    }

    private void RefreshCandidates()
    {
        ClearChildren(candidatesContent);
        if (investigation == null) return;

        List<InvestigationManager.MonsterCandidate> candidates = investigation.GetMonsterCandidates();
        MonsterData forcedMonster = TutorialManager.GetForcedMonsterSelection();
        if (forcedMonster != null)
        {
            candidates = candidates.Where(candidate => candidate.monster == forcedMonster).ToList();
        }

        if (order.declaredMonster != null && candidates.All(candidate => candidate.monster != order.declaredMonster))
        {
            order.declaredMonster = null;
        }

        if (candidates.Count == 0)
        {
            TMP_Text empty = CreateText("NoCandidates", candidatesContent, Vector2.zero, Vector2.one, 16f, FontStyles.Italic, TextAlignmentOptions.Center);
            empty.text = "No matches";
            empty.color = MutedTextColor;
        }

        foreach (InvestigationManager.MonsterCandidate candidate in candidates)
        {
            if (candidate.monster == null) continue;
            CreateMonsterCandidate(candidate.monster);
        }

        string selectedName = order.declaredMonster != null
            ? (string.IsNullOrWhiteSpace(order.declaredMonster.displayName) ? order.declaredMonster.name : order.declaredMonster.displayName)
            : null;
        if (!string.IsNullOrWhiteSpace(transientStatus))
        {
            selectionText.text = transientStatus;
        }
        else if (candidates.Count == 0)
        {
            selectionText.text = "No monster matches the assigned clues. Remove a clue and try another combination.";
        }
        else
        {
            selectionText.text = string.IsNullOrWhiteSpace(selectedName)
                ? "Select a suspected monster. Click it again to unselect."
                : $"Locked selection: {selectedName}";
        }
    }

    private void CreateMonsterCandidate(MonsterData monster)
    {
        bool selected = order.declaredMonster == monster;
        RectTransform entry = CreateRect($"Monster_{monster.name}", candidatesContent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image background = AddImage(entry.gameObject, selected ? SelectedColor : CandidateColor, true);
        Button button = entry.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.interactable = TutorialManager.IsActionAllowed(TutorialIds.SelectMonster);
        button.onClick.AddListener(() =>
        {
            if (!TutorialManager.IsActionAllowed(TutorialIds.SelectMonster)) return;

            bool selecting = order.declaredMonster != monster;
            order.declaredMonster = selecting ? monster : null;
            transientStatus = null;
            InteractionFeedbackManager.PlayUIClick();
            if (selecting)
            {
                TutorialManager.ReportEvent(TutorialIds.EventMonsterSelected);
            }
            owner.NotifyCaseBuilderSelectionChanged();
            Refresh();
        });

        RectTransform portraitRect = CreateRect("Portrait", entry, new Vector2(0.12f, 0.27f), new Vector2(0.88f, 0.93f), Vector2.zero, Vector2.zero);
        Image portrait = AddImage(portraitRect.gameObject, Color.white, false);
        portrait.sprite = monster.portrait;
        portrait.enabled = monster.portrait != null;
        portrait.preserveAspect = true;
        TMP_Text name = CreateText("Name", entry, new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.26f), 13f, FontStyles.Bold, TextAlignmentOptions.Center);
        name.text = string.IsNullOrWhiteSpace(monster.displayName) ? monster.name : monster.displayName;
    }

    private void RefreshActions()
    {
        bool selected = order.declaredMonster != null;
        backButton.interactable = TutorialManager.IsActionAllowed(TutorialIds.DeclineOrder);
        assignButton.interactable = selected && owner.CanAssignCaseMonster();
        referButton.interactable = selected && TutorialManager.IsActionAllowed(TutorialIds.ReferOrder);

        if (selected && !assignButton.interactable && string.IsNullOrWhiteSpace(transientStatus))
        {
            selectionText.text = owner.GetCaseAssignUnavailableReason();
        }

        OrderManager orderManager = GameManager.Instance != null ? GameManager.Instance.GetOrderManager() : null;
        int referralGold = orderManager != null && selected ? orderManager.CalculateReferralFee(order) : 0;
        float multiplier = orderManager != null ? orderManager.GetDailyReferralMultiplier() : 1f;
        referButtonText.text = selected
            ? $"Refer ({Mathf.RoundToInt(multiplier * 100f)}%)\n{referralGold} gold"
            : "Refer";
    }

    private string GetCategoryLabel(string categoryName)
    {
        EvidenceTagLibrary.TagCategory category = tagLibrary?.GetCategory(categoryName);
        return category != null && !string.IsNullOrWhiteSpace(category.displayName) ? category.displayName : categoryName;
    }

    private string GetValueLabel(string categoryName, string valueName)
    {
        EvidenceTagLibrary.TagCategory.TagValue value = tagLibrary?.GetValue(categoryName, valueName);
        return value != null && !string.IsNullOrWhiteSpace(value.displayName) ? value.displayName : valueName;
    }

    private void Unsubscribe()
    {
        if (investigation != null)
        {
            investigation.OnCaseUpdated -= HandleCaseUpdated;
        }
    }

    private static void ClearChildren(Transform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            child.SetActive(false);
            UnityEngine.Object.Destroy(child);
        }
    }

    private static RectTransform CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        RectTransform rect = CreateRect(name, parent, anchorMin, anchorMax, new Vector2(5f, 5f), new Vector2(-5f, -5f));
        AddImage(rect.gameObject, PanelColor, true);
        return rect;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
        return rect;
    }

    private static Image AddImage(GameObject target, Color color, bool raycastTarget)
    {
        Image image = target.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    private static TMP_Text CreateText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, float fontSize, FontStyles style, TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(name, parent, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = TextColor;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
        {
            text.font = TMP_Settings.defaultFontAsset;
        }
        return text;
    }

    private static Button CreateButton(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, string label, Color color)
    {
        RectTransform rect = CreateRect(name, parent, anchorMin, anchorMax, new Vector2(3f, 3f), new Vector2(-3f, -3f));
        Image image = AddImage(rect.gameObject, color, true);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
        colors.disabledColor = new Color(color.r, color.g, color.b, 0.4f);
        button.colors = colors;
        TMP_Text text = CreateText("Label", rect, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.95f), 16f, FontStyles.Bold, TextAlignmentOptions.Center);
        text.text = label;
        return button;
    }
}

internal sealed class CaseBuilderClueItem : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private CaseBuilderView view;
    private string categoryName;
    private bool assigned;
    private CanvasGroup canvasGroup;

    public string CategoryName => categoryName;

    public void Configure(CaseBuilderView owner, string category, bool isAssigned, CanvasGroup group)
    {
        view = owner;
        categoryName = category;
        assigned = isAssigned;
        canvasGroup = group;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.dragging) return;
        InteractionFeedbackManager.PlayUIClick();
        view?.ToggleClue(categoryName, assigned);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (assigned) return;
        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
        transform.SetParent(view.DragRoot, true);
        transform.SetAsLastSibling();
        transform.position = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (assigned) return;
        transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
        view?.Refresh();
    }
}

internal sealed class CaseBuilderCategoryDropTarget : MonoBehaviour, IDropHandler
{
    private CaseBuilderView view;
    private string categoryName;

    public void Configure(CaseBuilderView owner, string category)
    {
        view = owner;
        categoryName = category;
    }

    public void OnDrop(PointerEventData eventData)
    {
        CaseBuilderClueItem clue = eventData.pointerDrag != null
            ? eventData.pointerDrag.GetComponent<CaseBuilderClueItem>()
            : null;
        if (clue == null) return;
        view?.TryDropClue(clue.CategoryName, categoryName);
    }
}
