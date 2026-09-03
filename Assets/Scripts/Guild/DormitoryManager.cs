using System.Collections.Generic;
using System;
using UnityEngine;

public class DormitoryManager : MonoBehaviour
{
    public static DormitoryManager Instance { get; private set; }

    [Serializable]
    private class DormitorySaveData
    {
        public List<BedSaveData> beds = new List<BedSaveData>();
    }

    [Serializable]
    private class BedSaveData
    {
        public string bedId;
        public int dirtyDayCount;
    }

    [System.Serializable]
    public class DormitoryConstructionGroup
    {
        public GuildConstructionDefinition construction;
        public List<Transform> bedPoints = new List<Transform>();
        [Tooltip("Optional doors to open before hunters path to this dormitory section.")]
        public List<GuildDoorController> routeDoorsToOpen = new List<GuildDoorController>();
    }

    private class BedSlot
    {
        public Transform point;
        public Hunter hunter;
        public DormitoryConstructionGroup group;
        public DormitoryBed bed;
    }

    private const string SaveKey = "GuildDormitoryState";

    [Header("Unlock")]
    [SerializeField] private GuildConstructionManager constructionManager;

    [Header("Dormitory Sections")]
    [Tooltip("Each section contributes beds only when its construction is built. If Construction is empty, the section is always available.")]
    [SerializeField] private List<DormitoryConstructionGroup> dormitoryGroups = new List<DormitoryConstructionGroup>();

    [Header("Dirty Beds")]
    [Tooltip("Dirty day count at which cleaning takes the increased sheet-changing time.")]
    [SerializeField] private int staleDirtyDayCount = 2;
    [Tooltip("Dirty day count at which a bed can no longer be used for sleep.")]
    [SerializeField] private int unusableDirtyDayCount = 3;
    [Tooltip("Flat success chance penalty for hunters who did not sleep last night.")]
    [SerializeField] private float missedSleepSuccessPenaltyPercent = 10f;

    [SerializeField] private bool debugLogs = false;

    private readonly List<BedSlot> slots = new List<BedSlot>();
    private readonly HashSet<string> sleptLastNightHunterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> unrestedHunterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private HunterManager hunterManager;
    private TimeManager timeManager;
    private TimeManager subscribedTimeManager;
    private int eveningHandledDayIndex = -1;
    private int warningLoggedDayIndex = -1;
    private int sleepRecordDayIndex = -1;
    private bool loadedState;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple DormitoryManager instances found. The newest one will replace the static instance.", this);
        }

        Instance = this;
        ResolveReferences();
        RebuildSlots();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToTimeManager();
    }

    private void Start()
    {
        ResolveReferences();
        if (!loadedState)
        {
            LoadState();
        }
        TryHandleCurrentEveningState();
    }

    private void Update()
    {
        if (subscribedTimeManager == null)
        {
            ResolveReferences();
        }

        TryHandleCurrentEveningState();
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (subscribedTimeManager != null)
        {
            subscribedTimeManager.OnDayStateChanged -= HandleDayStateChanged;
            subscribedTimeManager.OnDayStarted -= HandleDayStarted;
            subscribedTimeManager = null;
        }
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
            subscribedTimeManager.OnDayStateChanged -= HandleDayStateChanged;
            subscribedTimeManager.OnDayStarted -= HandleDayStarted;
        }

        subscribedTimeManager = timeManager;
        subscribedTimeManager.OnDayStateChanged += HandleDayStateChanged;
        subscribedTimeManager.OnDayStarted += HandleDayStarted;
    }

    private void RebuildSlots()
    {
        slots.Clear();
        if (dormitoryGroups == null) return;

        foreach (var group in dormitoryGroups)
        {
            if (group == null || group.bedPoints == null) continue;
            foreach (var point in group.bedPoints)
            {
                if (point == null) continue;
                DormitoryBed bed = ResolveBed(point);
                if (bed != null)
                {
                    bed.Initialize(this, staleDirtyDayCount, unusableDirtyDayCount);
                }
                slots.Add(new BedSlot { point = point, group = group, bed = bed });
            }
        }
    }

    private bool IsGroupUnlocked(DormitoryConstructionGroup group)
    {
        if (group == null) return false;
        if (group.construction == null) return true;
        ResolveReferences();
        return constructionManager != null && constructionManager.IsBuilt(group.construction);
    }

    private void HandleDayStateChanged(TimeManager.DayState state)
    {
        if (state == TimeManager.DayState.Evening)
        {
            TrySendHuntersToBedsForCurrentDay();
        }
    }

    private void HandleDayStarted(int _)
    {
        CaptureSleepResults();
        MarkUsedBedsDirty();
        eveningHandledDayIndex = -1;
        warningLoggedDayIndex = -1;
        WakeSleepingHunters();
        SaveState();
    }

    private void TryHandleCurrentEveningState()
    {
        if (timeManager == null) return;
        if (timeManager.GetDayState() != TimeManager.DayState.Evening) return;
        TrySendHuntersToBedsForCurrentDay();
    }

    private void TrySendHuntersToBedsForCurrentDay()
    {
        if (timeManager == null) return;
        int dayIndex = timeManager.GetCurrentDayIndex();
        if (eveningHandledDayIndex == dayIndex) return;

        eveningHandledDayIndex = dayIndex;
        SendHuntersToBeds();
    }

    private void SendHuntersToBeds()
    {
        ResolveReferences();
        if (hunterManager == null)
        {
            LogDormitoryWarningOnce("DormitoryManager: HunterManager is missing.");
            return;
        }

        if (slots.Count == 0)
        {
            LogDormitoryWarningOnce("DormitoryManager: No bed points are assigned.");
            return;
        }

        bool assignedAny = false;
        bool foundIdleHunter = false;
        bool foundUnlockedBed = HasUnlockedFreeSlot();

        foreach (var hunter in hunterManager.GetAllHunters())
        {
            if (hunter == null) continue;
            if (!hunter.IsAvailableForOrders()) continue;
            foundIdleHunter = true;
            if (IsHunterAssigned(hunter)) continue;

            BedSlot slot = FindFreeSlot();
            if (slot == null) return;

            OpenRouteDoors(slot.group);
            bool startedWalking = hunter.WalkToDormitoryBed(slot.point, HandleHunterArrived);
            if (!startedWalking) continue;

            slot.hunter = hunter;
            assignedAny = true;
        }

        if (assignedAny)
        {
            hunterManager.NotifyRosterChanged();
        }
        else if (!foundUnlockedBed)
        {
            LogDormitoryWarningOnce("DormitoryManager: No unlocked free dormitory bed found. Check Dormitory Groups, Construction assignment, and whether the construction is built.");
        }
        else if (!foundIdleHunter && debugLogs)
        {
            Debug.Log("DormitoryManager: Evening started, but no idle hunters were available to send to beds.", this);
        }
    }

    private void HandleHunterArrived(Hunter hunter)
    {
        if (hunter == null) return;
        hunterManager?.NotifyRosterChanged();
    }

    private void WakeSleepingHunters()
    {
        ResolveReferences();
        bool changed = false;

        foreach (var slot in slots)
        {
            if (slot == null || slot.hunter == null) continue;
            slot.hunter.WakeFromDormitory();
            slot.hunter = null;
            changed = true;
        }

        if (hunterManager != null)
        {
            foreach (var hunter in hunterManager.GetAllHunters())
            {
                if (hunter == null || hunter.GetState() != HunterState.Sleeping) continue;
                hunter.WakeFromDormitory();
                changed = true;
            }
        }

        if (changed)
        {
            hunterManager?.NotifyRosterChanged();
        }
    }

    private bool IsHunterAssigned(Hunter hunter)
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

    private BedSlot FindFreeSlot()
    {
        foreach (var slot in slots)
        {
            if (slot != null && slot.point != null && slot.hunter == null)
            {
                if (!IsGroupUnlocked(slot.group)) continue;
                if (slot.bed != null && !slot.bed.IsUsable()) continue;
                return slot;
            }
        }

        return null;
    }

    private bool HasUnlockedFreeSlot()
    {
        return FindFreeSlot() != null;
    }

    private void LogDormitoryWarningOnce(string message)
    {
        int dayIndex = timeManager != null ? timeManager.GetCurrentDayIndex() : -1;
        if (warningLoggedDayIndex == dayIndex) return;
        warningLoggedDayIndex = dayIndex;
        Debug.LogWarning(message, this);
    }

    private void OpenRouteDoors(DormitoryConstructionGroup group)
    {
        if (group?.routeDoorsToOpen == null) return;
        foreach (var door in group.routeDoorsToOpen)
        {
            if (door == null) continue;
            door.OpenForRoute();
        }
    }

    public bool TryCleanBed(DormitoryBed bed)
    {
        if (bed == null || !bed.IsDirty) return false;

        ResolveReferences();
        if (timeManager == null || timeManager.GetDayState() != TimeManager.DayState.Active) return false;

        var config = GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null;
        float cost = config != null
            ? (bed.DirtyDayCount >= staleDirtyDayCount
                ? config.actionTimeSettings.cleanStaleDormitoryBedSeconds
                : config.actionTimeSettings.cleanDormitoryBedSeconds)
            : 0f;
        timeManager.AdvanceTime(cost);

        bed.Clean();
        SaveState();
        return true;
    }

    public bool DidHunterSleepLastNight(Hunter hunter, int dayIndex)
    {
        string id = GetHunterId(hunter);
        if (string.IsNullOrEmpty(id)) return false;

        if (sleepRecordDayIndex == dayIndex)
        {
            return sleptLastNightHunterIds.Contains(id);
        }

        return IsHunterCurrentlyAssignedToSleepingSlot(hunter);
    }

    public float GetMissedSleepPenaltyPercent(Hunter hunter)
    {
        string id = GetHunterId(hunter);
        if (string.IsNullOrEmpty(id)) return 0f;
        return unrestedHunterIds.Contains(id) ? Mathf.Max(0f, missedSleepSuccessPenaltyPercent) : 0f;
    }

    public static bool CanHunterRecoverOvernight(Hunter hunter, int dayIndex)
    {
        return Instance == null || Instance.DidHunterSleepLastNight(hunter, dayIndex);
    }

    public static float GetActiveMissedSleepPenaltyPercent(Hunter hunter)
    {
        return Instance != null ? Instance.GetMissedSleepPenaltyPercent(hunter) : 0f;
    }

    private void CaptureSleepResults()
    {
        ResolveReferences();
        int dayIndex = timeManager != null ? timeManager.GetCurrentDayIndex() : sleepRecordDayIndex + 1;
        sleepRecordDayIndex = dayIndex;
        sleptLastNightHunterIds.Clear();
        unrestedHunterIds.Clear();

        foreach (var slot in slots)
        {
            string id = GetHunterId(slot?.hunter);
            if (!string.IsNullOrEmpty(id))
            {
                sleptLastNightHunterIds.Add(id);
            }
        }

        if (dayIndex <= 0 || hunterManager == null) return;

        foreach (var hunter in hunterManager.GetAllHunters())
        {
            if (hunter == null || hunter.GetState() == HunterState.Dead || hunter.GetState() == HunterState.Candidate) continue;
            string id = GetHunterId(hunter);
            if (string.IsNullOrEmpty(id)) continue;
            if (!sleptLastNightHunterIds.Contains(id))
            {
                unrestedHunterIds.Add(id);
            }
        }
    }

    private void MarkUsedBedsDirty()
    {
        foreach (var slot in slots)
        {
            if (slot == null || slot.hunter == null || slot.bed == null) continue;
            slot.bed.MarkSleptIn();
        }
    }

    private bool IsHunterCurrentlyAssignedToSleepingSlot(Hunter hunter)
    {
        if (hunter == null) return false;
        foreach (var slot in slots)
        {
            if (slot != null && slot.hunter == hunter)
            {
                return true;
            }
        }

        return false;
    }

    private DormitoryBed ResolveBed(Transform point)
    {
        if (point == null) return null;
        DormitoryBed bed = point.GetComponent<DormitoryBed>();
        if (bed != null) return bed;

        bed = point.GetComponentInParent<DormitoryBed>();
        if (bed != null) return bed;

        return point.GetComponentInChildren<DormitoryBed>();
    }

    private void LoadState()
    {
        loadedState = true;
        if (!PlayerPrefs.HasKey(SaveKey)) return;

        try
        {
            DormitorySaveData data = JsonUtility.FromJson<DormitorySaveData>(PlayerPrefs.GetString(SaveKey));
            if (data?.beds == null) return;

            foreach (var savedBed in data.beds)
            {
                if (savedBed == null || string.IsNullOrWhiteSpace(savedBed.bedId)) continue;
                DormitoryBed bed = FindBedById(savedBed.bedId);
                bed?.SetDirtyDayCount(savedBed.dirtyDayCount);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"DormitoryManager: Failed to load saved dormitory state: {ex.Message}", this);
        }
    }

    private void SaveState()
    {
        DormitorySaveData data = new DormitorySaveData();
        foreach (var slot in slots)
        {
            if (slot?.bed == null) continue;
            data.beds.Add(new BedSaveData
            {
                bedId = slot.bed.BedId,
                dirtyDayCount = slot.bed.DirtyDayCount
            });
        }

        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    private DormitoryBed FindBedById(string bedId)
    {
        foreach (var slot in slots)
        {
            if (slot?.bed == null) continue;
            if (string.Equals(slot.bed.BedId, bedId, StringComparison.OrdinalIgnoreCase))
            {
                return slot.bed;
            }
        }

        return null;
    }

    private static string GetHunterId(Hunter hunter)
    {
        return hunter != null && hunter.Data != null ? hunter.Data.hunterId : null;
    }
}
