using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Linq;
using UnityEngine.AI;

public class HunterManager : MonoBehaviour
{
    [System.Serializable]
    public class HunterSaveState
    {
        public string hunterId;
        public int level;
        public int xp;
        public int weaponId = -1;
        public bool hasWeaponOverride;
        public int morningDialogueDay = -1;
        public string morningDialogueLineId;
        public bool morningDialogueAsked;
    }

    [System.Serializable]
    private class HunterEquipmentSaveData
    {
        public List<HunterEquipmentSaveState> equipment = new List<HunterEquipmentSaveState>();
    }

    [System.Serializable]
    private class HunterEquipmentSaveState
    {
        public string hunterId;
        public int weaponId;
    }

    [System.Serializable]
    private class MorningDialogueSaveData
    {
        public List<MorningDialogueSaveEntry> entries = new List<MorningDialogueSaveEntry>();
    }

    [System.Serializable]
    private class MorningDialogueSaveEntry
    {
        public string hunterId;
        public int day;
        public string lineId;
        public bool asked;
    }

    private const string MorningDialogueSaveKey = "HunterMorningDialogueState";

    [Header("Hunter Database")]
    [SerializeField] private List<HunterData> allHunterData = new List<HunterData>();
    [SerializeField] private List<HunterData> initialHunters = new List<HunterData>();
    
    [Header("Prefabs")]
    [SerializeField] private GameObject hunterPrefab;
    
    [Header("Spawn Points")]
    [SerializeField] private Transform hunterSpawnPoint;
    [SerializeField] private List<HunterSeat> hunterSeats = new List<HunterSeat>();
    
    [Header("Door Points")]
    [SerializeField] private Transform doorEntryPoint;
    [SerializeField] private Transform doorExitPoint;
    [SerializeField] private Transform returnSpawnPoint;

    private readonly List<Hunter> activeHunters = new List<Hunter>();
    private readonly Dictionary<string, HunterData> hunterLookup = new Dictionary<string, HunterData>();
    private readonly HashSet<string> hiredHunterIds = new HashSet<string>();
    private readonly Dictionary<string, HunterSaveState> hunterSaveStates = new Dictionary<string, HunterSaveState>();
    private readonly Dictionary<string, int> savedWeaponOverrides = new Dictionary<string, int>();
    private readonly Dictionary<Hunter, bool> idleAllDayCandidates = new Dictionary<Hunter, bool>();
    private readonly HashSet<HunterSeat> briefingRoomSeats = new HashSet<HunterSeat>();
    private int nextSeatIndex = 0;
    private bool navMeshChecked = false;
    private bool navMeshAvailable = false;
    private string equipmentSavePath;
    
    public event System.Action OnHuntersChanged;
    public event System.Action<Hunter> OnHunterLeveledUp;
    public void OnDayStarted(int dayIndex)
    {
        if (dayIndex > 0)
        {
            HealWoundedHuntersOvernight(dayIndex);
        }

        ApplyMentorBonuses();

        // reset idle tracking for the new day
        idleAllDayCandidates.Clear();
        foreach (var hunter in activeHunters)
        {
            if (hunter == null) continue;
            idleAllDayCandidates[hunter] = hunter.GetState() == HunterState.Idle;
        }
    }

    private void HealWoundedHuntersOvernight(int dayIndex)
    {
        bool changed = false;
        foreach (var hunter in activeHunters)
        {
            if (hunter == null || hunter.GetState() == HunterState.Dead) continue;

            var state = hunter.GetComponent<HunterInteractionState>();
            if (state == null || (!state.IsWounded && !state.IsHealing)) continue;
            if (!DormitoryManager.CanHunterRecoverOvernight(hunter, dayIndex)) continue;

            state.CompleteHealing();
            var interactable = hunter.GetComponent<HunterInteractable>();
            interactable?.SetHealVfxActive(false);

            if (hunter.GetState() == HunterState.Healing)
            {
                hunter.FinishInfirmaryTreatment();
            }

            changed = true;
        }

        if (changed)
        {
            NotifyHuntersChanged();
        }
    }
    
    private void Awake()
    {
        equipmentSavePath = GameSaveUtility.GetSavePath("hunter_equipment_state.json");
        LoadEquipmentOverrides();
        LoadMorningDialogueState();
        BuildHunterLookup();

        if (hunterSpawnPoint == null)
        {
            // Try to find spawn point
            GameObject spawnObj = GameObject.Find("HunterSpawnPoint");
            if (spawnObj != null)
            {
                hunterSpawnPoint = spawnObj.transform;
            }
        }
        
        SanitizeIdleSeats();
        if (hunterSeats.Count == 0)
        {
            FindIdleSeats();
        }

        // Cache whether we have a NavMesh to avoid repeated warnings
        navMeshAvailable = CheckNavMeshAvailable();
        navMeshChecked = true;
    }
    
    private void Start()
    {
        EnsureInitialHunters();
    }
    
    private void FindIdleSeats()
    {
        RefreshBriefingSeatCache();
        hunterSeats.Clear();
        HunterSeat[] seats = FindObjectsOfType<HunterSeat>(true);
        foreach (var seat in seats)
        {
            if (IsValidIdleSeat(seat))
            {
                hunterSeats.Add(seat);
            }
        }
        
        if (hunterSeats.Count == 0)
        {
            Debug.LogWarning("HunterManager: No guild hall HunterSeat components found in the scene. Hunters will remain standing.");
        }
    }

    private void SanitizeIdleSeats()
    {
        RefreshBriefingSeatCache();
        if (hunterSeats == null)
        {
            hunterSeats = new List<HunterSeat>();
            return;
        }

        hunterSeats = hunterSeats.Where(IsValidIdleSeat).ToList();
        if (nextSeatIndex >= hunterSeats.Count)
        {
            nextSeatIndex = 0;
        }
    }

    private bool IsValidIdleSeat(HunterSeat seat)
    {
        if (seat == null || !seat.CanUseForGuildHall) return false;
        return !IsBriefingRoomChair(seat);
    }

    private bool IsBriefingRoomChair(HunterSeat seat)
    {
        return seat != null && briefingRoomSeats.Contains(seat);
    }

    private void RefreshBriefingSeatCache()
    {
        briefingRoomSeats.Clear();
        var briefingRooms = FindObjectsOfType<BriefingRoomManager>(true);
        foreach (var room in briefingRooms)
        {
            if (room == null) continue;
            var seats = room.GetBriefingChairs();
            if (seats == null) continue;
            foreach (var seat in seats)
            {
                if (seat != null)
                {
                    briefingRoomSeats.Add(seat);
                }
            }
        }
    }
    
    private void BuildHunterLookup()
    {
        hunterLookup.Clear();
        foreach (var data in allHunterData)
        {
            if (data == null || string.IsNullOrEmpty(data.hunterId))
            {
                continue;
            }

            if (!hunterLookup.ContainsKey(data.hunterId))
            {
                hunterLookup.Add(data.hunterId, data);
            }
        }
    }

    private void EnsureInitialHunters()
    {
        if (activeHunters.Count > 0 || hiredHunterIds.Count > 0)
        {
            return;
        }

        var starterList = initialHunters != null && initialHunters.Count > 0
            ? initialHunters
            : GetDefaultInitialHunters();

        foreach (var data in starterList)
        {
            TryHireHunter(data);
        }
    }

    private IEnumerable<HunterData> GetDefaultInitialHunters()
    {
        int desiredCount = GlobalHunterConfig.GetGlobalConfig()?.GetDefaultInitialHunterCount() ?? 3;
        desiredCount = Mathf.Max(0, desiredCount);
        int count = Mathf.Min(desiredCount, allHunterData.Count);
        for (int i = 0; i < count; i++)
        {
            if (allHunterData[i] != null)
            {
                yield return allHunterData[i];
            }
        }
    }
    
    private bool IsHunterSpawned(HunterData data)
    {
        return activeHunters.Any(h => h != null && h.Data == data);
    }
    
    public Hunter SpawnHunter(HunterData data)
    {
        if (IsAtHunterLimit())
        {
            Debug.LogWarning("HunterManager: Hunter limit reached. Cannot spawn more hunters.");
            return null;
        }

        Hunter hunter = InstantiateHunter(data, hunterSpawnPoint);
        if (hunter == null)
        {
            return null;
        }

        activeHunters.Add(hunter);
        AssignHunterToSeat(hunter);
        NotifyHuntersChanged();
        return hunter;
    }

    private Hunter InstantiateHunter(HunterData data, Transform spawnOverride)
    {
        if (data == null) return null;

        Vector3 spawnPosition = spawnOverride != null
            ? spawnOverride.position
            : (hunterSpawnPoint != null ? hunterSpawnPoint.position : Vector3.zero);
        Quaternion spawnRotation = spawnOverride != null
            ? spawnOverride.rotation
            : Quaternion.identity;

        GameObject hunterObj;
        if (hunterPrefab != null)
        {
            hunterObj = Instantiate(hunterPrefab, spawnPosition, spawnRotation);
        }
        else
        {
            hunterObj = new GameObject(data.hunterName);
            hunterObj.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            hunterObj.AddComponent<UnityEngine.AI.NavMeshAgent>();
        }

        Hunter hunter = hunterObj.GetComponent<Hunter>();
        if (hunter == null)
        {
            hunter = hunterObj.AddComponent<Hunter>();
        }

        if (hunterObj.TryGetComponent<NavMeshAgent>(out var agent))
        {
            if (!navMeshChecked)
            {
                navMeshAvailable = CheckNavMeshAvailable();
                navMeshChecked = true;
            }

            agent.enabled = navMeshAvailable;
        }

        hunter.Initialize(data);
        ApplySavedHunterState(hunter);
        hunterObj.name = data.hunterName;
        return hunter;
    }

    public Hunter CreateCandidateInstance(HunterData data, Transform spawnPoint)
    {
        Hunter hunter = InstantiateHunter(data, spawnPoint);
        if (hunter == null)
        {
            return null;
        }
        hunter.SetState(HunterState.Candidate);
        return hunter;
    }

    public bool TryHireCandidate(Hunter candidate)
    {
        if (candidate == null) return false;
        HunterData data = candidate.Data;
        if (data == null) return false;
        if (hiredHunterIds.Contains(data.hunterId)) return false;

        hiredHunterIds.Add(data.hunterId);
        ApplySavedHunterState(candidate);
        if (!activeHunters.Contains(candidate))
        {
            activeHunters.Add(candidate);
        }

        candidate.SetState(HunterState.Idle);
        AssignHunterToSeat(candidate);
        NotifyHuntersChanged();
        return true;
    }

    public void DestroyCandidateInstance(Hunter candidate)
    {
        if (candidate == null) return;
        if (activeHunters.Contains(candidate))
        {
            return;
        }
        Destroy(candidate.gameObject);
    }
    
    public void AssignHunterToSeat(Hunter hunter)
    {
        if (hunter == null) return;
        if (hunterSeats.Count == 0) return;
        
        HunterSeat seat = FindAvailableSeat();
        if (seat == null)
        {
            Debug.LogWarning("HunterManager: No available HunterSeat to assign.");
            return;
        }

        if (!seat.TryAssign(hunter))
        {
            return;
        }

        hunter.WalkToSeat(seat);
    }

    private HunterSeat FindAvailableSeat()
    {
        if (hunterSeats.Count == 0) return null;

        for (int i = 0; i < hunterSeats.Count; i++)
        {
            int index = (nextSeatIndex + i) % hunterSeats.Count;
            HunterSeat seat = hunterSeats[index];
            if (IsValidIdleSeat(seat) && !seat.IsOccupied)
            {
                nextSeatIndex = (index + 1) % hunterSeats.Count;
                return seat;
            }
        }

        return null;
    }
    
    public List<Hunter> GetAvailableHunters()
    {
        return activeHunters.FindAll(h => h != null && h.GetState() == HunterState.Idle);
    }
    
    public List<Hunter> GetAllHunters()
    {
        return new List<Hunter>(activeHunters);
    }
    
    public Hunter GetHunterByName(string name)
    {
        return activeHunters.Find(h => h != null && h.Data != null && h.Data.hunterName == name);
    }
    
    public void RemoveHunter(Hunter hunter)
    {
        if (hunter != null)
        {
            activeHunters.Remove(hunter);
            idleAllDayCandidates.Remove(hunter);
            hunter.ReleaseCurrentSeat();
            Destroy(hunter.gameObject);
            NotifyHuntersChanged();
        }
    }

    public bool CanFireHunter(Hunter hunter)
    {
        if (hunter == null || hunter.Data == null) return false;
        HunterState state = hunter.GetState();
        return state == HunterState.Idle;
    }

    public bool FireHunter(Hunter hunter)
    {
        if (!CanFireHunter(hunter)) return false;

        CaptureHunterState(hunter);
        string hunterId = hunter.Data.hunterId;
        if (!string.IsNullOrEmpty(hunterId))
        {
            hiredHunterIds.Remove(hunterId);
        }

        RemoveHunter(hunter);
        return true;
    }

    public int DismissHuntersUntilUpkeepAtOrBelow(int targetDailyUpkeep, int minimumHuntersToKeep = 1)
    {
        targetDailyUpkeep = Mathf.Max(0, targetDailyUpkeep);
        minimumHuntersToKeep = Mathf.Max(0, minimumHuntersToKeep);
        int dismissed = 0;

        while (CalculateDailyUpkeep() > targetDailyUpkeep && CountDebtDismissibleHunters() > minimumHuntersToKeep)
        {
            Hunter hunter = FindDebtDismissalCandidate();
            if (hunter == null) break;
            DismissHunterForDebt(hunter);
            dismissed++;
        }

        return dismissed;
    }

    private int CountDebtDismissibleHunters()
    {
        int count = 0;
        foreach (var hunter in activeHunters)
        {
            if (IsDebtDismissible(hunter))
            {
                count++;
            }
        }

        return count;
    }

    private Hunter FindDebtDismissalCandidate()
    {
        Hunter best = null;
        int bestUpkeep = int.MinValue;
        foreach (var hunter in activeHunters)
        {
            if (!IsDebtDismissible(hunter)) continue;

            int upkeep = hunter.GetUpkeepCost();
            if (best == null || upkeep > bestUpkeep)
            {
                best = hunter;
                bestUpkeep = upkeep;
            }
        }

        return best;
    }

    private bool IsDebtDismissible(Hunter hunter)
    {
        if (hunter == null || hunter.Data == null) return false;
        HunterState state = hunter.GetState();
        return state != HunterState.Dead
            && state != HunterState.OnMission
            && state != HunterState.Candidate
            && state != HunterState.Armory;
    }

    private void DismissHunterForDebt(Hunter hunter)
    {
        if (hunter == null || hunter.Data == null) return;

        string hunterName = !string.IsNullOrWhiteSpace(hunter.Data.hunterName) ? hunter.Data.hunterName : hunter.name;
        CaptureHunterState(hunter);
        if (!string.IsNullOrEmpty(hunter.Data.hunterId))
        {
            hiredHunterIds.Remove(hunter.Data.hunterId);
        }

        RemoveHunter(hunter);
        var notificationManager = GameManager.Instance != null ? GameManager.Instance.GetNotificationManager() : null;
        notificationManager?.NotifyHunterLeft(hunterName);
    }
    
    public void OnReputationChanged(float newReputation)
    {
        // No automatic spawning; recruitment manager handles availability.
    }

    public void NotifyHunterStateChanged(Hunter hunter, HunterState newState)
    {
        if (hunter == null) return;
        if (idleAllDayCandidates.ContainsKey(hunter))
        {
            if (newState != HunterState.Idle)
            {
                idleAllDayCandidates[hunter] = false;
            }
        }

        NotifyHuntersChanged();
    }

    public int CalculateDailyUpkeep()
    {
        int total = 0;
        foreach (var hunter in activeHunters)
        {
            if (hunter != null && hunter.GetState() != HunterState.Dead && hunter.Data != null)
            {
                total += hunter.GetUpkeepCost();
            }
        }
        return total;
    }

    public bool PayUpkeep(GoldManager goldManager)
    {
        if (goldManager == null) return false;
        int cost = CalculateDailyUpkeep();
        return goldManager.SpendGold(cost);
    }

    public bool TryPayLevelUp(Hunter hunter, GoldManager goldManager)
    {
        if (hunter == null || goldManager == null) return false;
        if (!hunter.CanLevelUp()) return false;
        int cost = hunter.GetLevelUpCost();
        if (!goldManager.SpendGold(cost)) return false;
        bool leveled = hunter.LevelUp();
        if (leveled)
        {
            CaptureHunterState(hunter);
            NotifyHuntersChanged();
            OnHunterLeveledUp?.Invoke(hunter);
            var config = GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null;
            var tm = GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null;
            float timeCost = config != null ? config.actionTimeSettings.levelUpSeconds : 0f;
            tm?.AdvanceTime(timeCost);
        }
        return leveled;
    }

    public Transform GetDoorEntryTransform()
    {
        if (doorEntryPoint != null) return doorEntryPoint;
        if (doorExitPoint != null) return doorExitPoint;
        return null;
    }

    public Vector3 GetMissionDeparturePosition(Hunter hunter)
    {
        Transform entry = GetDoorEntryTransform();
        if (entry == null)
        {
            return hunter != null ? hunter.transform.position : Vector3.zero;
        }

        int missionIndex = 0;
        int missionCount = 0;
        for (int i = 0; i < activeHunters.Count; i++)
        {
            Hunter activeHunter = activeHunters[i];
            if (activeHunter == null || activeHunter.GetState() != HunterState.OnMission) continue;
            if (activeHunter == hunter)
            {
                missionIndex = missionCount;
            }
            missionCount++;
        }

        float spacing = 0.45f;
        float centeredOffset = missionCount > 1
            ? (missionIndex - ((missionCount - 1) * 0.5f)) * spacing
            : 0f;
        Vector3 basePosition = entry.position;
        Vector3 offsetPosition = basePosition + entry.right * centeredOffset;
        if (NavMesh.SamplePosition(offsetPosition, out NavMeshHit hit, 0.75f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return basePosition;
    }

    public Transform GetDoorExitTransform()
    {
        if (doorExitPoint != null) return doorExitPoint;
        if (doorEntryPoint != null) return doorEntryPoint;
        return null;
    }

    public Transform GetReturnSpawnTransform()
    {
        if (returnSpawnPoint != null) return returnSpawnPoint;
        if (doorEntryPoint != null) return doorEntryPoint;
        if (doorExitPoint != null) return doorExitPoint;
        return null;
    }

    private bool CheckNavMeshAvailable()
    {
        // Try to sample near origin to see if a navmesh exists
        return NavMesh.SamplePosition(Vector3.zero, out NavMeshHit _, 1000f, NavMesh.AllAreas);
    }

    private void NotifyHuntersChanged()
    {
        OnHuntersChanged?.Invoke();
    }

    public void NotifyRosterChanged()
    {
        NotifyHuntersChanged();
    }

    public void NotifyHunterEquipmentChanged(Hunter hunter)
    {
        CaptureHunterState(hunter);
        SaveEquipmentOverride(hunter);
        NotifyHuntersChanged();
    }

    public IReadOnlyList<HunterData> GetAllHunterData() => allHunterData;

    public List<HunterData> GetRecruitableHunters(int reputation)
    {
        List<HunterData> result = new List<HunterData>();
        foreach (var data in allHunterData)
        {
            if (data == null) continue;
            if (data.minReputation > reputation) continue;
            if (hiredHunterIds.Contains(data.hunterId)) continue;
            result.Add(data);
        }
        return result;
    }

    public bool IsHunterHired(HunterData data)
    {
        return data != null && hiredHunterIds.Contains(data.hunterId);
    }

    public bool TryHireHunter(HunterData data)
    {
        if (data == null || hiredHunterIds.Contains(data.hunterId))
        {
            return false;
        }
        if (IsAtHunterLimit())
        {
            Debug.LogWarning("HunterManager: Hunter limit reached. Cannot hire new hunter.");
            return false;
        }

        var hunter = SpawnHunter(data);
        if (hunter == null) return false;

        hiredHunterIds.Add(data.hunterId);
        idleAllDayCandidates[hunter] = hunter.GetState() == HunterState.Idle;
        return true;
    }

    public void LoadHiredHunters(IEnumerable<string> ids)
    {
        if (ids == null) return;
        foreach (var id in ids)
        {
            if (string.IsNullOrEmpty(id) || hiredHunterIds.Contains(id)) continue;
            if (!hunterLookup.TryGetValue(id, out var data) || data == null) continue;
            if (IsAtHunterLimit()) break;
            var hunter = SpawnHunter(data);
            if (hunter == null) continue;
            hiredHunterIds.Add(id);
        }
    }

    public List<string> GetHiredHunterIds()
    {
        return new List<string>(hiredHunterIds);
    }

    public void LoadHunterSaveStates(IEnumerable<HunterSaveState> states)
    {
        hunterSaveStates.Clear();
        if (states != null)
        {
            foreach (var state in states)
            {
                if (state == null || string.IsNullOrEmpty(state.hunterId)) continue;
                hunterSaveStates[state.hunterId] = new HunterSaveState
                {
                    hunterId = state.hunterId,
                    level = Mathf.Max(1, state.level),
                    xp = Mathf.Max(0, state.xp),
                    weaponId = state.weaponId,
                    hasWeaponOverride = state.hasWeaponOverride,
                    morningDialogueDay = state.morningDialogueDay,
                    morningDialogueLineId = state.morningDialogueLineId,
                    morningDialogueAsked = state.morningDialogueAsked
                };
            }
        }

        LoadMorningDialogueState();
    }

    public List<HunterSaveState> GetHunterSaveStates()
    {
        foreach (var hunter in activeHunters)
        {
            CaptureHunterState(hunter);
        }

        return hunterSaveStates.Values
            .Where(state => state != null && !string.IsNullOrEmpty(state.hunterId))
            .Select(state => new HunterSaveState
            {
                hunterId = state.hunterId,
                level = state.level,
                xp = state.xp,
                weaponId = state.weaponId,
                hasWeaponOverride = state.hasWeaponOverride,
                morningDialogueDay = state.morningDialogueDay,
                morningDialogueLineId = state.morningDialogueLineId,
                morningDialogueAsked = state.morningDialogueAsked
            })
            .ToList();
    }

    private void CaptureHunterState(Hunter hunter)
    {
        if (hunter == null || hunter.Data == null || string.IsNullOrEmpty(hunter.Data.hunterId)) return;
        hunterSaveStates.TryGetValue(hunter.Data.hunterId, out var existingState);
        hunterSaveStates[hunter.Data.hunterId] = new HunterSaveState
        {
            hunterId = hunter.Data.hunterId,
            level = Mathf.Max(1, hunter.GetLevel()),
            xp = Mathf.Max(0, hunter.GetXP()),
            weaponId = hunter.GetSavedWeaponOverride(),
            hasWeaponOverride = hunter.GetSavedWeaponOverride() >= 0,
            morningDialogueDay = existingState != null ? existingState.morningDialogueDay : -1,
            morningDialogueLineId = existingState != null ? existingState.morningDialogueLineId : null,
            morningDialogueAsked = existingState != null && existingState.morningDialogueAsked
        };
    }

    public string GetMorningDialogueAnswer(Hunter hunter, string repeatPrefix, out bool alreadyAsked)
    {
        alreadyAsked = false;
        if (hunter == null || hunter.Data == null) return string.Empty;

        HunterSaveState state = GetOrCreateHunterSaveState(hunter);
        int dayIndex = GetCurrentDayIndex();
        if (state.morningDialogueDay != dayIndex || string.IsNullOrWhiteSpace(state.morningDialogueLineId))
        {
            RollMorningDialogueLine(hunter, state, dayIndex);
        }

        alreadyAsked = state.morningDialogueAsked;
        string line = ResolveMorningDialogueText(hunter.Data, state.morningDialogueLineId);
        if (string.IsNullOrWhiteSpace(line))
        {
            line = "Nothing new today.";
        }

        return alreadyAsked && !string.IsNullOrWhiteSpace(repeatPrefix)
            ? $"{repeatPrefix} {line}"
            : line;
    }

    public void MarkMorningDialogueAsked(Hunter hunter)
    {
        if (hunter == null || hunter.Data == null) return;
        HunterSaveState state = GetOrCreateHunterSaveState(hunter);
        int dayIndex = GetCurrentDayIndex();
        if (state.morningDialogueDay != dayIndex || string.IsNullOrWhiteSpace(state.morningDialogueLineId))
        {
            RollMorningDialogueLine(hunter, state, dayIndex);
        }

        state.morningDialogueAsked = true;
        CaptureHunterState(hunter);
        SaveMorningDialogueState();
    }

    private HunterSaveState GetOrCreateHunterSaveState(Hunter hunter)
    {
        string hunterId = hunter.Data.hunterId;
        if (!hunterSaveStates.TryGetValue(hunterId, out var state) || state == null)
        {
            state = new HunterSaveState
            {
                hunterId = hunterId,
                level = Mathf.Max(1, hunter.GetLevel()),
                xp = Mathf.Max(0, hunter.GetXP()),
                weaponId = hunter.GetSavedWeaponOverride(),
                hasWeaponOverride = hunter.GetSavedWeaponOverride() >= 0,
                morningDialogueDay = -1
            };
            hunterSaveStates[hunterId] = state;
        }

        return state;
    }

    private void RollMorningDialogueLine(Hunter hunter, HunterSaveState state, int dayIndex)
    {
        state.morningDialogueDay = dayIndex;
        state.morningDialogueAsked = false;
        state.morningDialogueLineId = PickMorningDialogueLineId(hunter);
        SaveMorningDialogueState();
    }

    private string PickMorningDialogueLineId(Hunter hunter)
    {
        var lines = hunter?.Data?.morningDialogueLines;
        if (lines == null || lines.Count == 0) return string.Empty;

        List<int> eligible = new List<int>();
        int totalWeight = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line == null || string.IsNullOrWhiteSpace(line.responseText)) continue;
            if (!IsMorningDialogueLineEligible(hunter, line)) continue;

            eligible.Add(i);
            totalWeight += Mathf.Max(1, line.weight);
        }

        if (eligible.Count == 0)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line == null || string.IsNullOrWhiteSpace(line.responseText)) continue;
                if (line.condition != HunterData.MorningDialogueCondition.Always) continue;

                eligible.Add(i);
                totalWeight += Mathf.Max(1, line.weight);
            }
        }

        if (eligible.Count == 0) return string.Empty;

        int roll = Random.Range(0, Mathf.Max(1, totalWeight));
        foreach (int index in eligible)
        {
            var line = lines[index];
            roll -= Mathf.Max(1, line.weight);
            if (roll < 0)
            {
                return GetMorningDialogueLineKey(line, index);
            }
        }

        int fallbackIndex = eligible[0];
        return GetMorningDialogueLineKey(lines[fallbackIndex], fallbackIndex);
    }

    private bool IsMorningDialogueLineEligible(Hunter hunter, HunterData.HunterMorningDialogueLine line)
    {
        switch (line.condition)
        {
            case HunterData.MorningDialogueCondition.Always:
                return true;
            case HunterData.MorningDialogueCondition.Unpaid:
                return GameManager.Instance != null && GameManager.Instance.GetUnpaidUpkeepStreak() > 0;
            case HunterData.MorningDialogueCondition.NotFed:
                return KitchenManager.Instance != null && !KitchenManager.Instance.IsHunterFed(hunter);
            case HunterData.MorningDialogueCondition.Wounded:
                var state = hunter != null ? hunter.GetComponent<HunterInteractionState>() : null;
                return state != null && (state.IsWounded || state.IsHealing);
            case HunterData.MorningDialogueCondition.ReadyToLevelUp:
                return hunter != null && hunter.CanLevelUp();
            case HunterData.MorningDialogueCondition.OrderWithMonsterPresent:
                return IsOrderWithMonsterPresent(line.monster);
            default:
                return false;
        }
    }

    private bool IsOrderWithMonsterPresent(MonsterData monster)
    {
        if (monster == null || GameManager.Instance == null) return false;
        OrderManager orderManager = GameManager.Instance.GetOrderManager();
        if (orderManager == null) return false;

        foreach (var order in orderManager.GetActiveOrders())
        {
            if (OrderMatchesMonster(order, monster)) return true;
        }

        foreach (var order in orderManager.GetOfferedOrders())
        {
            if (OrderMatchesMonster(order, monster)) return true;
        }

        return false;
    }

    private static bool OrderMatchesMonster(Order order, MonsterData monster)
    {
        if (order == null || monster == null) return false;
        return order.monsterData == monster || order.declaredMonster == monster;
    }

    private string ResolveMorningDialogueText(HunterData data, string lineId)
    {
        if (data == null || data.morningDialogueLines == null || string.IsNullOrWhiteSpace(lineId)) return string.Empty;

        for (int i = 0; i < data.morningDialogueLines.Count; i++)
        {
            var line = data.morningDialogueLines[i];
            if (line == null) continue;
            if (string.Equals(GetMorningDialogueLineKey(line, i), lineId, System.StringComparison.OrdinalIgnoreCase))
            {
                return line.responseText;
            }
        }

        return string.Empty;
    }

    private static string GetMorningDialogueLineKey(HunterData.HunterMorningDialogueLine line, int index)
    {
        if (line != null && !string.IsNullOrWhiteSpace(line.lineId)) return line.lineId;
        return $"line_{index}";
    }

    private int GetCurrentDayIndex()
    {
        TimeManager timeManager = GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null;
        return timeManager != null ? timeManager.GetCurrentDayIndex() : 0;
    }

    private void LoadMorningDialogueState()
    {
        if (!PlayerPrefs.HasKey(MorningDialogueSaveKey)) return;

        try
        {
            MorningDialogueSaveData data = JsonUtility.FromJson<MorningDialogueSaveData>(PlayerPrefs.GetString(MorningDialogueSaveKey));
            if (data?.entries == null) return;

            foreach (var entry in data.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.hunterId)) continue;
                if (!hunterSaveStates.TryGetValue(entry.hunterId, out var state) || state == null)
                {
                    state = new HunterSaveState
                    {
                        hunterId = entry.hunterId,
                        level = 1,
                        xp = 0,
                        weaponId = -1,
                        morningDialogueDay = -1
                    };
                    hunterSaveStates[entry.hunterId] = state;
                }

                state.morningDialogueDay = entry.day;
                state.morningDialogueLineId = entry.lineId;
                state.morningDialogueAsked = entry.asked;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"HunterManager: Failed to load morning dialogue state. {ex.Message}", this);
        }
    }

    private void SaveMorningDialogueState()
    {
        MorningDialogueSaveData data = new MorningDialogueSaveData();
        foreach (var state in hunterSaveStates.Values)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.hunterId)) continue;
            if (state.morningDialogueDay < 0 || string.IsNullOrWhiteSpace(state.morningDialogueLineId)) continue;

            data.entries.Add(new MorningDialogueSaveEntry
            {
                hunterId = state.hunterId,
                day = state.morningDialogueDay,
                lineId = state.morningDialogueLineId,
                asked = state.morningDialogueAsked
            });
        }

        PlayerPrefs.SetString(MorningDialogueSaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    private void ApplySavedHunterState(Hunter hunter)
    {
        if (hunter == null || hunter.Data == null || string.IsNullOrEmpty(hunter.Data.hunterId)) return;
        if (hunterSaveStates.TryGetValue(hunter.Data.hunterId, out var state) && state != null)
        {
            hunter.DebugSetLevelAndXP(state.level, state.xp);
            if (state.hasWeaponOverride && state.weaponId >= 0)
            {
                hunter.SetEquippedWeaponId(state.weaponId);
                return;
            }
        }

        if (savedWeaponOverrides.TryGetValue(hunter.Data.hunterId, out int weaponId) && weaponId >= 0)
        {
            hunter.SetEquippedWeaponId(weaponId);
        }
    }

    private void SaveEquipmentOverride(Hunter hunter)
    {
        if (hunter == null || hunter.Data == null || string.IsNullOrEmpty(hunter.Data.hunterId)) return;

        savedWeaponOverrides[hunter.Data.hunterId] = hunter.GetSavedWeaponOverride();
        SaveEquipmentOverrides();
    }

    private void LoadEquipmentOverrides()
    {
        savedWeaponOverrides.Clear();
        if (string.IsNullOrEmpty(equipmentSavePath) || !File.Exists(equipmentSavePath)) return;

        try
        {
            string json = File.ReadAllText(equipmentSavePath);
            if (string.IsNullOrWhiteSpace(json)) return;

            HunterEquipmentSaveData data = JsonUtility.FromJson<HunterEquipmentSaveData>(json);
            if (data?.equipment == null) return;

            foreach (var entry in data.equipment)
            {
                if (entry == null || string.IsNullOrEmpty(entry.hunterId)) continue;
                savedWeaponOverrides[entry.hunterId] = Mathf.Max(-1, entry.weaponId);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"HunterManager: Failed to load equipment state. {ex.Message}", this);
        }
    }

    private void SaveEquipmentOverrides()
    {
        if (string.IsNullOrEmpty(equipmentSavePath)) return;

        try
        {
            HunterEquipmentSaveData data = new HunterEquipmentSaveData();
            foreach (var kvp in savedWeaponOverrides)
            {
                if (string.IsNullOrEmpty(kvp.Key) || kvp.Value < 0) continue;
                data.equipment.Add(new HunterEquipmentSaveState { hunterId = kvp.Key, weaponId = kvp.Value });
            }

            File.WriteAllText(equipmentSavePath, JsonUtility.ToJson(data, true));
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"HunterManager: Failed to save equipment state. {ex.Message}", this);
        }
    }

    private void ApplyMentorBonuses()
    {
        if (activeHunters == null || activeHunters.Count == 0) return;

        List<HunterTrait.BonusEffect> mentorEffects = new List<HunterTrait.BonusEffect>();
        foreach (var kvp in idleAllDayCandidates)
        {
            if (!kvp.Value) continue; // not idle all day
            var hunter = kvp.Key;
            if (hunter == null || hunter.Data == null || hunter.Data.traits == null) continue;
            foreach (var trait in hunter.Data.traits)
            {
                if (trait == null || trait.bonusEffects == null) continue;
                foreach (var effect in trait.bonusEffects)
                {
                    if (effect == null) continue;
                    if (effect.bonusType != HunterTrait.BonusEffectType.MentorGrantXP) continue;
                    // Conditions use monster=null, partySize=1
                    if (!MissionOutcomeCalculator.DoesConditionPass(effect.condition, null, 1)) continue;
                    mentorEffects.Add(effect);
                }
            }
        }

        if (mentorEffects.Count == 0) return;

        Hunter target = null;
        int lowestLevel = int.MaxValue;
        int lowestXP = int.MaxValue;
        foreach (var hunter in activeHunters)
        {
            if (hunter == null || hunter.GetState() == HunterState.Dead) continue;
            int lvl = hunter.GetLevel();
            int xp = hunter.GetXP();
            if (lvl < lowestLevel || (lvl == lowestLevel && xp < lowestXP))
            {
                lowestLevel = lvl;
                lowestXP = xp;
                target = hunter;
            }
        }

        if (target == null) return;

        foreach (var effect in mentorEffects)
        {
            int xpGain = Mathf.Max(0, Mathf.RoundToInt(effect.value));
            target.GainXP(xpGain);
        }
    }

    public HunterData GetHunterDataById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        hunterLookup.TryGetValue(id, out var data);
        return data;
    }

    public bool IsAtHunterLimit()
    {
        int limit = GetHunterLimit();
        if (limit <= 0) return false;
        int aliveCount = activeHunters.Count(h => h != null && h.GetState() != HunterState.Dead);
        return aliveCount >= limit;
    }

    public int GetHunterLimit()
    {
        var constructionManager = GameManager.Instance != null ? GameManager.Instance.GetConstructionManager() : null;
        return constructionManager != null ? constructionManager.GetBuiltHunterCapacityIncrease() : 0;
    }

    public float GetRecruitmentRarityWeightMultiplier(GlobalHunterConfig.RarityType rarity)
    {
        if (rarity == GlobalHunterConfig.RarityType.Common) return 1f;

        float multiplier = 1f;
        foreach (var hunter in activeHunters)
        {
            if (hunter == null) continue;
            HunterState state = hunter.GetState();
            if (state == HunterState.Dead || state == HunterState.OnMission || state == HunterState.Candidate || state == HunterState.Armory) continue;

            var data = hunter.Data;
            if (data == null || data.traits == null) continue;
            foreach (var trait in data.traits)
            {
                if (trait == null || trait.bonusEffects == null) continue;
                foreach (var effect in trait.bonusEffects)
                {
                    if (effect == null || effect.bonusType != HunterTrait.BonusEffectType.RecruitmentRarityWeightMultiplier) continue;
                    if (!MissionOutcomeCalculator.DoesConditionPass(effect.condition, null, 1)) continue;
                    multiplier *= effect.value <= 0f ? 1f : effect.value;
                }
            }
        }

        return Mathf.Max(0.01f, multiplier);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void DebugResetRoster()
    {
        foreach (var hunter in activeHunters)
        {
            if (hunter != null)
            {
                Destroy(hunter.gameObject);
            }
        }
        activeHunters.Clear();
        hiredHunterIds.Clear();
        hunterSaveStates.Clear();
        savedWeaponOverrides.Clear();
        SaveEquipmentOverrides();
        nextSeatIndex = 0;
        EnsureInitialHunters();
        var graveyardManager = GameManager.Instance != null ? GameManager.Instance.GetGraveyardManager() : null;
        graveyardManager?.RemoveGravesForHunters(GetHiredHunterIds());
        NotifyHuntersChanged();
    }
#endif
}
