using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class HunterManager : MonoBehaviour
{
    [Header("Hunter Database")]
    [SerializeField] private List<HunterData> allHunterData = new List<HunterData>();
    
    [Header("Prefabs")]
    [SerializeField] private GameObject hunterPrefab;
    
    [Header("Spawn Points")]
    [SerializeField] private Transform hunterSpawnPoint;
    [SerializeField] private List<Transform> idleSeats = new List<Transform>();
    
    private List<Hunter> activeHunters = new List<Hunter>();
    private int nextSeatIndex = 0;
    
    private void Awake()
    {
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
        if (idleSeats.Count == 0)
        {
            FindIdleSeats();
        }
    }
    
    private void Start()
    {
        // Initialize available hunters based on reputation
        UpdateAvailableHunters();
    }
    
    private void FindIdleSeats()
    {
        // Find all objects tagged as "HunterSeat" or named "HunterSeat"
        GameObject[] seats = GameObject.FindGameObjectsWithTag("HunterSeat");
        foreach (GameObject seat in seats)
        {
            idleSeats.Add(seat.transform);
        }
        
        // Also search by name
        Transform[] allTransforms = FindObjectsOfType<Transform>();
        foreach (Transform t in allTransforms)
        {
            bool looksLikeSeat = t.name.Contains("HunterSeat") || t.name.Contains("Sofa");
            if (looksLikeSeat && !idleSeats.Contains(t))
            {
                idleSeats.Add(t);
            }
        }
    }
    
    public void UpdateAvailableHunters()
    {
        int currentReputation = GameManager.Instance != null ? 
            GameManager.Instance.GetReputation() : 0;
        
        // Spawn hunters that are unlocked
        foreach (var hunterData in allHunterData)
        {
            if (hunterData.minReputation <= currentReputation)
            {
                // Check if already spawned
                if (!IsHunterSpawned(hunterData))
                {
                    SpawnHunter(hunterData);
                }
            }
        }
    }
    
    private bool IsHunterSpawned(HunterData data)
    {
        return activeHunters.Any(h => h != null && h.GetHunterData() == data);
    }
    
    public Hunter SpawnHunter(HunterData data)
    {
        if (data == null) return null;
        
        Vector3 spawnPosition = hunterSpawnPoint != null ? 
            hunterSpawnPoint.position : Vector3.zero;
        
        GameObject hunterObj;
        if (hunterPrefab != null)
        {
            hunterObj = Instantiate(hunterPrefab, spawnPosition, Quaternion.identity);
        }
        else
        {
            // Create basic hunter object
            hunterObj = new GameObject(data.hunterName);
            hunterObj.transform.position = spawnPosition;
            hunterObj.AddComponent<UnityEngine.AI.NavMeshAgent>();
        }
        
        Hunter hunter = hunterObj.GetComponent<Hunter>();
        if (hunter == null)
        {
            hunter = hunterObj.AddComponent<Hunter>();
        }
        
        hunter.Initialize(data);
        activeHunters.Add(hunter);
        
        // Assign to a seat
        AssignHunterToSeat(hunter);
        
        return hunter;
    }
    
    private void AssignHunterToSeat(Hunter hunter)
    {
        if (idleSeats.Count == 0) return;
        
        Transform seat = idleSeats[nextSeatIndex % idleSeats.Count];
        hunter.WalkToSeat(seat);
        nextSeatIndex++;
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
        }
    }
    
    public void OnReputationChanged(int newReputation)
    {
        UpdateAvailableHunters();
    }
}
