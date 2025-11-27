using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class OrderDetailPanel : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text statsText;
    [SerializeField] private TMP_Text partyInfoText;
    [SerializeField] private TMP_Text timerText;

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

    public void ShowOrder(Order order)
    {
        currentOrder = order;
        if (partyFormation != null)
        {
            partyFormation.Initialize(order);
        }
        BuildPartySlots(order);
        SyncSlotsWithParty();
        UpdateUI();
        NotifyPartyChanged();
    }

    public void UpdateUI()
    {
        if (currentOrder == null) return;
        
        RefreshSlotVisuals();

        if (titleText != null) titleText.text = currentOrder.orderTitle;
        if (descriptionText != null) descriptionText.text = currentOrder.description;
        if (statsText != null)
        {
            statsText.text =
                $"Monster: {currentOrder.monsterType}\n" +
                $"Difficulty: {currentOrder.difficulty}\n" +
                $"Reward: {currentOrder.goldReward}g / {currentOrder.xpReward}xp\n" +
                $"Prep: {currentOrder.prepTimeLimit:0}s  Mission: {currentOrder.missionDuration:0}s";
        }

        if (partyInfoText != null && partyFormation != null)
        {
            var chance = partyFormation.CalculateSuccessChance();
            partyInfoText.text = $"Party: {partyFormation.GetPartySize()}/{partyFormation.GetMaxPartySize()}  Success: {chance:0}%";
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
            else if (currentOrder.prepTimer != null)
            {
                timerLine = $"Prep: {currentOrder.prepTimer.GetFormattedRemainingTime()}";
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
}
