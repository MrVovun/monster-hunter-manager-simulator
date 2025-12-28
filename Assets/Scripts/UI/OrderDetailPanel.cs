using System;
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
    [SerializeField] private TraitTooltipPanel traitTooltipPanel;
    [Header("Revealed Traits")]
    [SerializeField] private Transform revealedTraitsParent;
    [SerializeField] private GameObject traitItemPrefab;
    [SerializeField] private Image traitIconPrototype;
    [Header("Monster Visuals")]
    [SerializeField] private Image declaredMonsterPortrait;

    [Header("Systems")]
    [SerializeField] private PartyFormation partyFormation;

    [Header("Party Slots")]
    [SerializeField] private Transform partySlotsParent;
    [SerializeField] private OrderPartySlot partySlotPrefab;
    
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

    private void Awake()
    {
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
                $"Reward: {order.goldReward}g / {order.xpReward}xp\n" +
                $"Mission: {order.missionDuration:0}s";
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
            missionTimeText.text = $"{order.missionDuration:0}s";
        }

        if (partyInfoText != null && partyFormation != null)
        {
            var chance = partyFormation.CalculateSuccessChance();
            var riskLabel = partyFormation.GetRiskLevel();
            partyInfoText.text = $"Party: {partyFormation.GetPartySize()}/{partyFormation.GetMaxPartySize()}  Success: {chance:0}% ({riskLabel})";
        }

        UpdateTimerText();
    }

    public void OnSendParty()
    {
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
                timerLine = $"Mission: {currentOrder.missionTimer.GetFormattedRemainingTime()}";
            }
        }

        timerText.text = timerLine;
    }

    public bool TryAssignHunterToSlot(int slotIndex, Hunter hunter)
    {
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
        return hunter.GetState() == HunterState.Idle;
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

    private void ClearDetails()
    {
        if (titleText != null) titleText.text = "Select an Order";
        if (descriptionText != null) descriptionText.text = string.Empty;
        if (statsText != null) statsText.text = string.Empty;
        if (monsterText != null) monsterText.text = "-";
        if (difficultyText != null) difficultyText.text = "-";
        if (rewardGoldText != null) rewardGoldText.text = "-";
        if (rewardXPText != null) rewardXPText.text = "-";
        if (missionTimeText != null) missionTimeText.text = "-";
        if (partyInfoText != null) partyInfoText.text = string.Empty;
        if (timerText != null) timerText.text = string.Empty;
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

        var caseData = currentOrder?.investigationCase;
        if (caseData == null || caseData.confirmedTraitIds == null || caseData.confirmedTraitIds.Count == 0)
        {
            revealedTraitsParent.gameObject.SetActive(false);
            return;
        }

        List<MonsterTrait> traits = new List<MonsterTrait>();
        foreach (var traitId in caseData.confirmedTraitIds)
        {
            if (string.IsNullOrEmpty(traitId)) continue;
            var trait = caseData.truthTraits?.Find(t => t != null && string.Equals(t.traitId, traitId, StringComparison.OrdinalIgnoreCase));
            if (trait != null)
            {
                traits.Add(trait);
            }
        }

        if (traits.Count == 0)
        {
            revealedTraitsParent.gameObject.SetActive(false);
            return;
        }

        revealedTraitsParent.gameObject.SetActive(true);
        foreach (var trait in traits)
        {
            var item = CreateTraitItem(trait);
            if (item == null) continue;
            item.transform.SetParent(revealedTraitsParent, false);
            spawnedTraitItems.Add(item);
        }
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

    private GameObject CreateTraitItem(MonsterTrait trait)
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
}
