using System.Collections.Generic;
using UnityEngine;

public class InfirmaryManager : MonoBehaviour
{
    private class TreatmentSlot
    {
        public Transform point;
        public Hunter hunter;
        public HunterInteractionState state;
        public bool treatmentStarted;
        public float remainingSeconds;
    }

    [Header("Unlock")]
    [SerializeField] private GuildConstructionManager constructionManager;
    [SerializeField] private GuildConstructionDefinition infirmaryConstruction;

    [Header("Treatment")]
    [SerializeField] private List<Transform> treatmentPoints = new List<Transform>();
    [SerializeField] private float automaticHealDurationSeconds = 10f;
    [SerializeField] private float scanIntervalSeconds = 0.75f;
    [SerializeField] private bool notifyWhenTreated = true;

    [Header("Route")]
    [Tooltip("Optional doors to open before hunters path to the infirmary.")]
    [SerializeField] private List<GuildDoorController> routeDoorsToOpen = new List<GuildDoorController>();

    private readonly List<TreatmentSlot> slots = new List<TreatmentSlot>();
    private HunterManager hunterManager;
    private TimeManager timeManager;
    private TimeManager subscribedTimeManager;
    private float scanTimer;

    private void Awake()
    {
        ResolveReferences();
        RebuildSlots();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToTimeManager();
    }

    private void OnDisable()
    {
        if (subscribedTimeManager != null)
        {
            subscribedTimeManager.OnTimeAdvanced -= HandleTimeAdvanced;
            subscribedTimeManager = null;
        }
    }

    private void Update()
    {
        if (!IsUnlocked()) return;

        scanTimer -= Time.deltaTime;
        if (scanTimer > 0f) return;
        scanTimer = Mathf.Max(0.1f, scanIntervalSeconds);

        ReleaseRecoveredSlots();
        TryAssignWaitingHunters();
    }

    private void ResolveReferences()
    {
        if (GameManager.Instance != null)
        {
            if (constructionManager == null) constructionManager = GameManager.Instance.GetConstructionManager();
            if (hunterManager == null) hunterManager = GameManager.Instance.GetHunterManager();
            if (timeManager == null) timeManager = GameManager.Instance.GetTimeManager();
        }

        SubscribeToTimeManager();
    }

    private void SubscribeToTimeManager()
    {
        if (!isActiveAndEnabled) return;
        if (timeManager == null) return;
        if (subscribedTimeManager == timeManager) return;

        if (subscribedTimeManager != null)
        {
            subscribedTimeManager.OnTimeAdvanced -= HandleTimeAdvanced;
        }

        subscribedTimeManager = timeManager;
        subscribedTimeManager.OnTimeAdvanced += HandleTimeAdvanced;
    }

    private void RebuildSlots()
    {
        slots.Clear();
        if (treatmentPoints == null) return;

        foreach (var point in treatmentPoints)
        {
            if (point == null) continue;
            slots.Add(new TreatmentSlot { point = point });
        }
    }

    private bool IsUnlocked()
    {
        if (infirmaryConstruction == null) return false;
        ResolveReferences();
        return constructionManager != null && constructionManager.IsBuilt(infirmaryConstruction);
    }

    private void TryAssignWaitingHunters()
    {
        ResolveReferences();
        if (hunterManager == null || slots.Count == 0) return;

        foreach (var hunter in hunterManager.GetAllHunters())
        {
            if (hunter == null) continue;
            if (!hunter.IsAvailableForOrders()) continue;

            var state = hunter.GetComponent<HunterInteractionState>();
            if (state == null || !state.IsWounded || state.IsHealing) continue;
            if (IsHunterAlreadyAssigned(hunter)) continue;

            TreatmentSlot slot = FindFreeSlot();
            if (slot == null) return;

            AssignHunterToSlot(hunter, state, slot);
        }
    }

    private bool IsHunterAlreadyAssigned(Hunter hunter)
    {
        foreach (var slot in slots)
        {
            if (slot != null && slot.hunter == hunter)
            {
                return true;
            }
        }

        return false;
    }

    private TreatmentSlot FindFreeSlot()
    {
        foreach (var slot in slots)
        {
            if (slot != null && slot.point != null && slot.hunter == null)
            {
                return slot;
            }
        }

        return null;
    }

    private void AssignHunterToSlot(Hunter hunter, HunterInteractionState state, TreatmentSlot slot)
    {
        OpenRouteDoors();

        bool startedWalking = hunter.WalkToInfirmary(slot.point, HandleHunterArrived);
        if (!startedWalking) return;

        slot.hunter = hunter;
        slot.state = state;
        slot.treatmentStarted = false;
        slot.remainingSeconds = Mathf.Max(0.1f, automaticHealDurationSeconds);
        hunterManager?.NotifyRosterChanged();
    }

    private void HandleHunterArrived(Hunter hunter)
    {
        TreatmentSlot slot = FindSlotForHunter(hunter);
        if (slot == null || slot.state == null)
        {
            hunter?.FinishInfirmaryTreatment();
            return;
        }

        if (!slot.state.IsWounded)
        {
            CompleteTreatment(slot);
            return;
        }

        slot.remainingSeconds = Mathf.Max(0.1f, automaticHealDurationSeconds);
        slot.treatmentStarted = true;
        slot.state.StartHealing(slot.remainingSeconds, realTimeHealing: false);
        SetHunterHealVfxActive(hunter, true);
    }

    private TreatmentSlot FindSlotForHunter(Hunter hunter)
    {
        foreach (var slot in slots)
        {
            if (slot != null && slot.hunter == hunter)
            {
                return slot;
            }
        }

        return null;
    }

    private void HandleTimeAdvanced(float deltaSeconds)
    {
        if (deltaSeconds <= 0f) return;

        for (int i = slots.Count - 1; i >= 0; i--)
        {
            TreatmentSlot slot = slots[i];
            if (slot == null || slot.hunter == null || !slot.treatmentStarted) continue;

            if (slot.hunter.GetState() == HunterState.Dead)
            {
                ClearSlot(slot);
                continue;
            }

            slot.remainingSeconds = Mathf.Max(0f, slot.remainingSeconds - deltaSeconds);
            slot.state?.AdvanceHealing(deltaSeconds);

            if (slot.remainingSeconds <= 0f || slot.state == null || !slot.state.IsWounded)
            {
                CompleteTreatment(slot);
            }
        }
    }

    private void CompleteTreatment(TreatmentSlot slot)
    {
        Hunter hunter = slot.hunter;
        HunterInteractionState state = slot.state;

        state?.CompleteHealing();
        SetHunterHealVfxActive(hunter, false);
        ClearSlot(slot);

        if (notifyWhenTreated && hunter != null)
        {
            var notifications = GameManager.Instance != null ? GameManager.Instance.GetNotificationManager() : null;
            notifications?.NotifyHunterTreated(hunter);
        }

        hunter?.FinishInfirmaryTreatment();
        hunterManager?.NotifyRosterChanged();
    }

    private void ClearSlot(TreatmentSlot slot)
    {
        if (slot == null) return;
        SetHunterHealVfxActive(slot.hunter, false);
        slot.hunter = null;
        slot.state = null;
        slot.treatmentStarted = false;
        slot.remainingSeconds = 0f;
    }

    private void ReleaseRecoveredSlots()
    {
        foreach (var slot in slots)
        {
            if (slot == null || slot.hunter == null || slot.state == null) continue;
            if (slot.state.IsWounded || slot.state.IsHealing) continue;

            slot.hunter.FinishInfirmaryTreatment();
            ClearSlot(slot);
        }
    }

    private void SetHunterHealVfxActive(Hunter hunter, bool active)
    {
        if (hunter == null) return;
        var interactable = hunter.GetComponent<HunterInteractable>();
        interactable?.SetHealVfxActive(active);
    }

    private void OpenRouteDoors()
    {
        if (routeDoorsToOpen == null) return;
        foreach (var door in routeDoorsToOpen)
        {
            if (door == null) continue;
            door.OpenForRoute();
        }
    }
}
