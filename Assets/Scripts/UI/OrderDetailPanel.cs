using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OrderDetailPanel : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text statsText;
    [SerializeField] private TMP_Text partyInfoText;
    [SerializeField] private TMP_Text timerText;
    [Header("Separated Stats")]
    [SerializeField] private TMP_Text monsterText;
    [SerializeField] private TMP_Text difficultyText;
    [SerializeField] private TMP_Text rewardGoldText;
    [SerializeField] private TMP_Text rewardXPText;
    [SerializeField] private TMP_Text missionTimeText;
    [SerializeField] private TMP_Text successTelemetryText;
    [SerializeField] private TraitTooltipPanel traitTooltipPanel;
    [Header("Revealed Traits")]
    [SerializeField] private Transform revealedTraitsParent;
    [SerializeField] private GameObject traitItemPrefab;
    [SerializeField] private Image traitIconPrototype;
    [SerializeField] private Color counteredTraitIconColor = new Color(0.45f, 0.45f, 0.45f, 0.55f);
    [SerializeField] private Color counterSlashColor = new Color(0.95f, 0.05f, 0.03f, 0.95f);
    [SerializeField] private float counterSlashThickness = 6f;
    [SerializeField] private float counterSlashDurationSeconds = 0.22f;
    [Header("Monster Visuals")]
    [SerializeField] private Image declaredMonsterPortrait;

    [Header("Systems")]
    [SerializeField] private PartyFormation partyFormation;

    [Header("Party Slots")]
    [SerializeField] private Transform partySlotsParent;
    [SerializeField] private OrderPartySlot partySlotPrefab;
    [Header("Action Buttons")]
    [SerializeField] private Button sendPartyButton;
    [SerializeField] private Button autoFillPartyButton;
    [SerializeField] private Button clearPartyButton;
    [SerializeField] private bool autoBindActionButtons = true;
    
    private readonly System.Collections.Generic.List<OrderPartySlot> partySlotInstances =
        new System.Collections.Generic.List<OrderPartySlot>();
    private readonly System.Collections.Generic.List<Hunter> slotAssignments =
        new System.Collections.Generic.List<Hunter>();
    
    [Header("Live Updates")]
    [SerializeField] private float timerRefreshIntervalSeconds = 0.15f;

    private Order currentOrder;
    private float timerRefreshCountdown;
    
    public System.Action OnPartyChanged;
    private readonly List<GameObject> spawnedTraitItems = new List<GameObject>();
    private readonly HashSet<string> previouslyCounteredTraitKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        AutoBindActionButtons();
        ClearSelection();
    }

    public void ShowOrder(Order order)
    {
        if (order == null)
        {
            ClearSelection();
            return;
        }

        currentOrder = order;
        previouslyCounteredTraitKeys.Clear();
        if (partyFormation != null)
        {
            partyFormation.Initialize(order);
        }
        BuildPartySlots(order);
        UpdateUI();
        NotifyPartyChanged();
    }

    public void UpdateUI()
    {
        Order order = currentOrder;
        bool hasOrder = order != null;
        if (panelRoot != null)
        {
            panelRoot.SetActive(hasOrder);
        }

        if (!hasOrder)
        {
            ClearDetails();
            return;
        }
        
        if (partyFormation != null)
        {
            SyncSlotsWithParty();
        }
        RefreshSlotVisuals();

        if (titleText != null) titleText.text = order.orderTitle;
        if (descriptionText != null)
        {
            descriptionText.text = order.GetDescriptionFor(Order.DescriptionAudience.DeclaredMonster);
        }
        string monsterName = order.GetDeclaredOrGenericMonsterName();
        if (statsText != null)
        {
            statsText.text =
                $"Monster: {monsterName}\n" +
                $"Difficulty: {order.difficulty}\n" +
                $"Reward: {order.goldReward}g / {order.xpReward}xp";
        }

        if (monsterText != null)
        {
            monsterText.text = monsterName;
        }

        UpdateMonsterPortrait(order);
        UpdateRevealedTraitsUI();

        if (difficultyText != null)
        {
            difficultyText.text = order.difficulty.ToString();
        }

        if (rewardGoldText != null)
        {
            rewardGoldText.text = $"{order.goldReward} gold";
        }
        if (rewardXPText != null)
        {
            rewardXPText.text = $"{order.xpReward} XP";
        }

        if (missionTimeText != null)
        {
            missionTimeText.text = string.Empty;
        }

        if (partyInfoText != null && partyFormation != null)
        {
            var chance = partyFormation.CalculateSuccessChance();
            var riskLabel = partyFormation.GetRiskLevel();
            partyInfoText.text = $"Party: {partyFormation.GetPartySize()}/{partyFormation.GetMaxPartySize()}  Success: {chance:0}% ({riskLabel})";
        }

        UpdateSuccessTelemetry(order);

        UpdateTimerText();
        UpdateActionButtonStates();
    }

    public void OnSendParty()
    {
        if (!TutorialManager.IsActionAllowed(TutorialIds.SendParty)) return;
        if (currentOrder == null || partyFormation == null) return;

        List<Hunter> party = partyFormation.GetParty();
        OrderManager manager = GameManager.Instance != null ? GameManager.Instance.GetOrderManager() : null;

        if (manager == null || !manager.StartMission(currentOrder, party))
        {
            UnityEngine.Debug.LogWarning("Failed to start mission. Check party size/state.");
            return;
        }
        
        UpdateUI();
        NotifyPartyChanged();
    }

    // Helper: auto-fill with idle hunters up to max party size
    public void AutoFillParty()
    {
        if (!TutorialManager.IsActionAllowed(TutorialIds.AssignHunter)) return;
        if (partyFormation == null || currentOrder == null || !CanEditParty()) return;

        HunterManager hunterManager = GameManager.Instance != null ? GameManager.Instance.GetHunterManager() : null;
        if (hunterManager == null) return;

        var available = hunterManager.GetAvailableHunters();
        int needed = Mathf.Min(currentOrder.maxPartySize, available.Count);
        for (int i = 0; i < slotAssignments.Count; i++)
        {
            slotAssignments[i] = i < needed ? available[i] : null;
        }

        SyncPartyFormationFromSlots();
        RefreshSlotVisuals();
        UpdateUI();
        NotifyPartyChanged();
    }

    public void ClearParty()
    {
        if (partyFormation == null) return;
        ClearAllSlots();
    }

    private void Update()
    {
        if (!isActiveAndEnabled || currentOrder == null || timerText == null) return;

        timerRefreshCountdown -= Time.unscaledDeltaTime;
        if (timerRefreshCountdown <= 0f)
        {
            UpdateTimerText();
            timerRefreshCountdown = Mathf.Max(0.05f, timerRefreshIntervalSeconds);
        }
    }

    private void UpdateTimerText()
    {
        if (timerText == null) return;

        string timerLine = string.Empty;
        if (currentOrder != null)
        {
            if (currentOrder.missionTimer != null)
            {
                timerLine = "Mission in progress";
            }
        }

        timerText.text = timerLine;
        UpdateActionButtonStates();
    }

    public bool TryAssignHunterToSlot(int slotIndex, Hunter hunter)
    {
        if (!TutorialManager.IsActionAllowed(TutorialIds.AssignHunter)) return false;
        if (!CanEditParty()) return false;
        if (!IsHunterSelectable(hunter)) return false;
        if (slotIndex < 0 || slotIndex >= slotAssignments.Count) return false;

        // Remove hunter from any other slot
        for (int i = 0; i < slotAssignments.Count; i++)
        {
            if (slotAssignments[i] == hunter)
            {
                slotAssignments[i] = null;
            }
        }

        slotAssignments[slotIndex] = hunter;
        SyncPartyFormationFromSlots();
        RefreshSlotVisuals();
        UpdateUI();
        NotifyPartyChanged();
        TutorialManager.ReportEvent(TutorialIds.EventHunterAssignedToOrder);
        return true;
    }

    public void RemoveHunterFromSlot(int slotIndex)
    {
        if (!CanEditParty()) return;
        if (slotIndex < 0 || slotIndex >= slotAssignments.Count) return;
        if (slotAssignments[slotIndex] == null) return;
        slotAssignments[slotIndex] = null;
        SyncPartyFormationFromSlots();
        RefreshSlotVisuals();
        UpdateUI();
        NotifyPartyChanged();
    }

    public bool IsHunterAssigned(Hunter hunter)
    {
        if (hunter == null) return false;
        for (int i = 0; i < slotAssignments.Count; i++)
        {
            if (slotAssignments[i] == hunter)
            {
                return true;
            }
        }
        return false;
    }

    public bool IsHunterSelectable(Hunter hunter)
    {
        if (currentOrder == null || hunter == null) return false;
        if (!CanEditParty()) return false;
        return hunter.IsAvailableForOrders();
    }

    private void BuildPartySlots(Order order)
    {
        ClearPartySlots();

        if (partySlotsParent == null || partySlotPrefab == null || order == null) return;

        int slotCount = Mathf.Max(1, order.maxPartySize);
        partySlotInstances.Capacity = Mathf.Max(partySlotInstances.Capacity, slotCount);
        slotAssignments.Capacity = Mathf.Max(slotAssignments.Capacity, slotCount);

        for (int i = 0; i < slotCount; i++)
        {
            OrderPartySlot slot = Instantiate(partySlotPrefab, partySlotsParent);
            slot.Initialize(i, this);
            partySlotInstances.Add(slot);
            slotAssignments.Add(null);
        }
    }

    private void ClearPartySlots()
    {
        foreach (var slot in partySlotInstances)
        {
            if (slot != null)
            {
                Destroy(slot.gameObject);
            }
        }
        partySlotInstances.Clear();
        slotAssignments.Clear();
    }

    private void RefreshSlotVisuals()
    {
        bool canEdit = CanEditParty();
        for (int i = 0; i < partySlotInstances.Count; i++)
        {
            var slot = partySlotInstances[i];
            if (slot != null)
            {
                var hunter = i < slotAssignments.Count ? slotAssignments[i] : null;
                slot.SetHunter(hunter);
                bool showSlot = canEdit || hunter != null;
                slot.gameObject.SetActive(showSlot);
            }
        }
    }

    private void SyncSlotsWithParty()
    {
        if (partyFormation == null) return;

        for (int i = 0; i < slotAssignments.Count; i++)
        {
            slotAssignments[i] = null;
        }

        var existingParty = partyFormation.GetParty();
        for (int i = 0; i < existingParty.Count && i < slotAssignments.Count; i++)
        {
            slotAssignments[i] = existingParty[i];
        }

        RefreshSlotVisuals();
    }

    private void SyncPartyFormationFromSlots()
    {
        if (partyFormation == null) return;

        partyFormation.ClearParty();
        for (int i = 0; i < slotAssignments.Count; i++)
        {
            var hunter = slotAssignments[i];
            if (hunter == null) continue;

            bool added = partyFormation.AddHunter(hunter);
            if (!added)
            {
                slotAssignments[i] = null;
            }
        }
    }

    private void ClearAllSlots()
    {
        if (!CanEditParty()) return;
        for (int i = 0; i < slotAssignments.Count; i++)
        {
            slotAssignments[i] = null;
        }
        SyncPartyFormationFromSlots();
        RefreshSlotVisuals();
        UpdateUI();
        NotifyPartyChanged();
    }

    private void NotifyPartyChanged()
    {
        OnPartyChanged?.Invoke();
    }

    private bool CanEditParty()
    {
        return currentOrder != null && currentOrder.state == OrderState.Accepted;
    }

    private bool CanSendParty()
    {
        if (currentOrder == null || partyFormation == null) return false;
        if (!TutorialManager.IsActionAllowed(TutorialIds.SendParty)) return false;
        if (currentOrder.state != OrderState.Accepted) return false;

        int partySize = partyFormation.GetPartySize();
        if (partySize < currentOrder.minPartySize || partySize > currentOrder.maxPartySize)
        {
            return false;
        }

        TimeManager timeManager = GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null;
        if (timeManager != null)
        {
            var dayState = timeManager.GetDayState();
            if (dayState == TimeManager.DayState.PreBell || dayState == TimeManager.DayState.Evening)
            {
                return false;
            }
        }

        return true;
    }

    private void UpdateActionButtonStates()
    {
        SetButtonState(sendPartyButton, CanSendParty(), GetSendPartyUnavailableReason());
        SetButtonState(autoFillPartyButton, CanEditParty() && TutorialManager.IsActionAllowed(TutorialIds.AssignHunter), GetAssignHunterUnavailableReason());
        SetButtonState(clearPartyButton, CanEditParty() && TutorialManager.IsActionAllowed(TutorialIds.AssignHunter) && partyFormation != null && partyFormation.GetPartySize() > 0, GetClearPartyUnavailableReason());
    }

    private void SetButtonState(Button button, bool interactable, string unavailableReason = null)
    {
        if (button == null) return;
        button.interactable = interactable;
        if (interactable)
        {
            UnavailableReasonButton.ClearReason(button);
        }
        else
        {
            UnavailableReasonButton.SetReason(button, unavailableReason);
        }
        var visualFeedback = button.GetComponent<UIButtonVisualFeedback>();
        if (visualFeedback != null)
        {
            visualFeedback.RefreshVisualState(true);
        }
    }

    private string GetSendPartyUnavailableReason()
    {
        if (currentOrder == null) return "Select an accepted order first.";
        if (partyFormation == null) return "Party formation is not ready.";
        if (!TutorialManager.IsActionAllowed(TutorialIds.SendParty)) return "Unavailable during the current tutorial step.";
        if (currentOrder.state != OrderState.Accepted) return "Only accepted orders can be sent.";

        int partySize = partyFormation.GetPartySize();
        if (partySize < currentOrder.minPartySize) return $"Assign at least {currentOrder.minPartySize} hunter(s).";
        if (partySize > currentOrder.maxPartySize) return $"This order allows at most {currentOrder.maxPartySize} hunter(s).";

        TimeManager timeManager = GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null;
        if (timeManager != null)
        {
            var dayState = timeManager.GetDayState();
            if (dayState == TimeManager.DayState.PreBell) return "Orders can be sent after the workday starts.";
            if (dayState == TimeManager.DayState.Evening) return "No new orders can be sent in the evening.";
        }

        return "Cannot send this party right now.";
    }

    private string GetAssignHunterUnavailableReason()
    {
        if (!TutorialManager.IsActionAllowed(TutorialIds.AssignHunter)) return "Unavailable during the current tutorial step.";
        if (currentOrder == null) return "Select an accepted order first.";
        if (currentOrder.state != OrderState.Accepted) return "Party can only be edited on accepted orders.";
        return "Cannot edit this party right now.";
    }

    private string GetClearPartyUnavailableReason()
    {
        string editReason = GetAssignHunterUnavailableReason();
        if (partyFormation == null || partyFormation.GetPartySize() <= 0) return "No hunters are assigned.";
        return editReason;
    }

    private void AutoBindActionButtons()
    {
        if (!autoBindActionButtons) return;

        Transform searchRoot = transform.root != null ? transform.root : transform;
        if (sendPartyButton == null) sendPartyButton = FindButtonByName(searchRoot, "SendPartyButton");
        if (autoFillPartyButton == null) autoFillPartyButton = FindButtonByName(searchRoot, "AutofillPartyButton");
        if (clearPartyButton == null) clearPartyButton = FindButtonByName(searchRoot, "ClearPartyButton");
    }

    private Button FindButtonByName(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrEmpty(objectName)) return null;

        var buttons = root.GetComponentsInChildren<Button>(true);
        foreach (var candidate in buttons)
        {
            if (candidate != null && candidate.gameObject.name == objectName)
            {
                return candidate;
            }
        }

        return null;
    }

    private void ClearDetails()
    {
        if (titleText != null) titleText.text = "Select an Order";
        if (descriptionText != null) descriptionText.text = string.Empty;
        if (statsText != null) statsText.text = string.Empty;
        if (monsterText != null) monsterText.text = "-";
        if (difficultyText != null) difficultyText.text = "-";
        if (rewardGoldText != null) rewardGoldText.text = "-";
        if (rewardXPText != null) rewardXPText.text = "-";
        if (missionTimeText != null) missionTimeText.text = string.Empty;
        if (partyInfoText != null) partyInfoText.text = string.Empty;
        if (successTelemetryText != null) successTelemetryText.text = string.Empty;
        if (timerText != null) timerText.text = string.Empty;
        UpdateActionButtonStates();
        ClearRevealedTraitItems();
        UpdateMonsterPortrait(null);
        ClearPartySlots();
    }

    public void ClearSelection()
    {
        currentOrder = null;
        ClearDetails();
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    private void UpdateMonsterPortrait(Order order)
    {
        if (declaredMonsterPortrait == null) return;

        Sprite sprite = order?.declaredMonster != null ? order.declaredMonster.portrait : null;
        declaredMonsterPortrait.sprite = sprite;
        declaredMonsterPortrait.enabled = sprite != null;
    }

    private void UpdateRevealedTraitsUI()
    {
        ClearRevealedTraitItems();

        if (revealedTraitsParent == null)
        {
            return;
        }

        var party = partyFormation != null ? partyFormation.GetParty() : null;
        var traitStates = MissionOutcomeCalculator.BuildPreviewTraitCounterStates(currentOrder, party);
        if (traitStates == null || traitStates.Count == 0)
        {
            revealedTraitsParent.gameObject.SetActive(false);
            previouslyCounteredTraitKeys.Clear();
            return;
        }

        revealedTraitsParent.gameObject.SetActive(true);
        var currentCounteredKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var traitState in traitStates)
        {
            var trait = traitState.Trait;
            string traitKey = GetTraitKey(trait);
            bool animateCounter = traitState.IsCountered && !previouslyCounteredTraitKeys.Contains(traitKey);
            if (traitState.IsCountered)
            {
                currentCounteredKeys.Add(traitKey);
            }

            var item = CreateTraitItem(trait, traitState.IsCountered, animateCounter);
            if (item == null) continue;
            item.transform.SetParent(revealedTraitsParent, false);
            spawnedTraitItems.Add(item);
        }

        previouslyCounteredTraitKeys.Clear();
        foreach (string key in currentCounteredKeys)
        {
            previouslyCounteredTraitKeys.Add(key);
        }
    }

    private void UpdateSuccessTelemetry(Order order)
    {
        if (successTelemetryText == null) return;
        var party = partyFormation != null ? partyFormation.GetParty() : null;
        var lines = MissionOutcomeCalculator.BuildPreviewSuccessTelemetryLines(order, party);
        successTelemetryText.text = lines != null && lines.Count > 0
            ? "Modifiers:\n" + string.Join("\n", lines)
            : "Modifiers:\nNo active success modifiers.";
    }

    private void ClearRevealedTraitItems()
    {
        foreach (var item in spawnedTraitItems)
        {
            if (item != null)
            {
                Destroy(item);
            }
        }
        spawnedTraitItems.Clear();

        if (revealedTraitsParent != null)
        {
            foreach (Transform child in revealedTraitsParent)
            {
                Destroy(child.gameObject);
            }
            revealedTraitsParent.gameObject.SetActive(false);
        }
    }

    private GameObject CreateTraitItem(MonsterTrait trait, bool isCountered, bool animateCounter)
    {
        GameObject item = traitItemPrefab != null ? Instantiate(traitItemPrefab) : new GameObject("RevealedTrait");
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
            Sprite sprite = trait != null ? trait.icon : null;
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        var counterVisual = item.GetComponent<OrderTraitCounterVisual>();
        if (counterVisual == null)
        {
            counterVisual = item.AddComponent<OrderTraitCounterVisual>();
        }
        counterVisual.Apply(icon, isCountered, counteredTraitIconColor, counterSlashColor, counterSlashThickness, counterSlashDurationSeconds, animateCounter);

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

    private static string GetTraitKey(MonsterTrait trait)
    {
        if (trait == null) return string.Empty;
        if (!string.IsNullOrEmpty(trait.traitId)) return trait.traitId;
        if (!string.IsNullOrEmpty(trait.displayName)) return trait.displayName;
        return trait.GetInstanceID().ToString();
    }
}

public class OrderTraitCounterVisual : MonoBehaviour
{
    private const string SlashRootName = "CounteredSlash";

    private Image icon;
    private GameObject slashRoot;
    private Image slashA;
    private Image slashB;
    private Coroutine animationRoutine;
    private Color defaultIconColor = Color.white;
    private bool defaultColorCaptured;

    public void Apply(
        Image traitIcon,
        bool isCountered,
        Color counteredIconColor,
        Color slashColor,
        float slashThickness,
        float durationSeconds,
        bool animate)
    {
        icon = traitIcon;
        if (icon != null && !defaultColorCaptured)
        {
            defaultIconColor = icon.color;
            defaultColorCaptured = true;
        }

        EnsureSlash(slashColor, slashThickness);

        if (icon != null)
        {
            icon.color = isCountered ? counteredIconColor : defaultIconColor;
        }

        if (slashRoot == null) return;

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        slashRoot.SetActive(isCountered);
        SetSlashScale(isCountered && !animate ? 1f : 0f);

        if (isCountered && animate && durationSeconds > 0f)
        {
            animationRoutine = StartCoroutine(AnimateSlash(durationSeconds));
        }
    }

    private void EnsureSlash(Color slashColor, float slashThickness)
    {
        if (slashRoot == null)
        {
            Transform existing = transform.Find(SlashRootName);
            slashRoot = existing != null ? existing.gameObject : CreateSlashRoot();
        }

        if (slashA == null || slashB == null)
        {
            slashA = FindOrCreateSlashLine("LineA", 45f);
            slashB = FindOrCreateSlashLine("LineB", -45f);
        }

        ConfigureLine(slashA, slashColor, slashThickness);
        ConfigureLine(slashB, slashColor, slashThickness);
    }

    private GameObject CreateSlashRoot()
    {
        var root = new GameObject(SlashRootName, typeof(RectTransform));
        root.transform.SetParent(transform, false);

        var rect = root.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        return root;
    }

    private Image FindOrCreateSlashLine(string lineName, float rotationZ)
    {
        Transform existing = slashRoot.transform.Find(lineName);
        GameObject line = existing != null ? existing.gameObject : new GameObject(lineName, typeof(RectTransform), typeof(Image));
        line.transform.SetParent(slashRoot.transform, false);

        var rect = line.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localRotation = Quaternion.Euler(0f, 0f, rotationZ);

        return line.GetComponent<Image>();
    }

    private void ConfigureLine(Image line, Color slashColor, float slashThickness)
    {
        if (line == null) return;

        line.color = slashColor;
        line.raycastTarget = false;

        var rect = line.rectTransform;
        float parentSize = 48f;
        if (transform is RectTransform parentRect)
        {
            parentSize = Mathf.Max(parentRect.rect.width, parentRect.rect.height, parentSize);
        }

        rect.sizeDelta = new Vector2(parentSize * 1.35f, Mathf.Max(1f, slashThickness));
    }

    private IEnumerator AnimateSlash(float durationSeconds)
    {
        float elapsed = 0f;
        while (elapsed < durationSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / durationSeconds);
            SetSlashScale(Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        SetSlashScale(1f);
        animationRoutine = null;
    }

    private void SetSlashScale(float scale)
    {
        if (slashRoot == null) return;
        slashRoot.transform.localScale = new Vector3(scale, 1f, 1f);
    }
}
