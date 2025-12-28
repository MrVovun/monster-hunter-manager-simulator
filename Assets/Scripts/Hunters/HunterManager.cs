using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.AI;

public class HunterManager : MonoBehaviour
{
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
    private int nextSeatIndex = 0;
    private bool navMeshChecked = false;
    private bool navMeshAvailable = false;
    
    public event System.Action OnHuntersChanged;
    
    private void Awake()
    {
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
        
        // Find idle seats
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
        hunterSeats.Clear();
        HunterSeat[] seats = FindObjectsOfType<HunterSeat>(true);
        hunterSeats.AddRange(seats);
        
        if (hunterSeats.Count == 0)
        {
            Debug.LogWarning("HunterManager: No HunterSeat components found in the scene. Hunters will remain standing.");
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
        return activeHunters.Any(h => h != null && h.GetHunterData() == data);
    }
    
    public Hunter SpawnHunter(HunterData data)
    {
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
        HunterData data = candidate.GetHunterData();
        if (data == null) return false;
        if (hiredHunterIds.Contains(data.hunterId)) return false;

        hiredHunterIds.Add(data.hunterId);
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
            if (seat != null && !seat.IsOccupied)
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
        return activeHunters.Find(h => h != null && h.GetHunterData().hunterName == name);
    }
    
    public void RemoveHunter(Hunter hunter)
    {
        if (hunter != null)
        {
            activeHunters.Remove(hunter);
            Destroy(hunter.gameObject);
            NotifyHuntersChanged();
        }
    }
    
    public void OnReputationChanged(float newReputation)
    {
        // No automatic spawning; recruitment manager handles availability.
    }

    public int CalculateDailyUpkeep()
    {
        int total = 0;
        foreach (var hunter in activeHunters)
        {
            if (hunter != null && hunter.GetState() != HunterState.Dead && hunter.GetHunterData() != null)
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
            NotifyHuntersChanged();
        }
        return leveled;
    }

    public Transform GetDoorEntryTransform()
    {
        if (doorEntryPoint != null) return doorEntryPoint;
        if (doorExitPoint != null) return doorExitPoint;
        return null;
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

        hiredHunterIds.Add(data.hunterId);
        SpawnHunter(data);
        return true;
    }

    public void LoadHiredHunters(IEnumerable<string> ids)
    {
        if (ids == null) return;
        foreach (var id in ids)
        {
            if (string.IsNullOrEmpty(id) || hiredHunterIds.Contains(id)) continue;
            if (!hunterLookup.TryGetValue(id, out var data) || data == null) continue;
            hiredHunterIds.Add(id);
            SpawnHunter(data);
        }
    }

    public List<string> GetHiredHunterIds()
    {
        return new List<string>(hiredHunterIds);
    }

    public HunterData GetHunterDataById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        hunterLookup.TryGetValue(id, out var data);
        return data;
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
        nextSeatIndex = 0;
        EnsureInitialHunters();
        NotifyHuntersChanged();
    }
#endif
}
