using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class Hunter : MonoBehaviour
{
    [Header("Hunter Data")]
    [SerializeField] private HunterData hunterData;
    
    [Header("Runtime State")]
    private int currentLevel;
    private int currentXP;
    private HunterState state = HunterState.Idle;
    
    [Header("Components")]
    private NavMeshAgent navAgent;
    private Animator animator;
    
    [Header("Seating")]
    private Transform assignedSeat;
    private bool isSeated = false;
    
    private HunterStats stats;
    private HunterLevelSystem levelSystem;
    
    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
        if (navAgent == null)
        {
            navAgent = gameObject.AddComponent<NavMeshAgent>();
        }
        
        animator = GetComponent<Animator>();
        stats = GetComponent<HunterStats>();
        if (stats == null)
        {
            stats = gameObject.AddComponent<HunterStats>();
        }
        
        levelSystem = GetComponent<HunterLevelSystem>();
        if (levelSystem == null)
        {
            levelSystem = gameObject.AddComponent<HunterLevelSystem>();
        }
    }
    
    private void Start()
    {
        if (hunterData != null)
        {
            Initialize(hunterData);
        }
    }
    
    public void Initialize(HunterData data)
    {
        hunterData = data;
        currentLevel = data.startingLevel;
        currentXP = data.startingXP;
        state = HunterState.Idle;
        
        stats?.Initialize(data, currentLevel);
        levelSystem?.Initialize(data);
        
        // Set name
        gameObject.name = data.hunterName;
    }
    
    public void SetState(HunterState newState)
    {
        state = newState;
        
        if (newState == HunterState.OnMission)
        {
            // Walk to door and despawn (optional visual)
            WalkToDoor();
        }
        else if (newState == HunterState.Idle)
        {
            // Return to guild
            ReturnToGuild();
        }
    }
    
    public HunterState GetState()
    {
        return state;
    }
    
    public void WalkToSeat(Transform seat)
    {
        if (navAgent != null && seat != null)
        {
            assignedSeat = seat;
            navAgent.SetDestination(seat.position);
            isSeated = false;
        }
    }
    
    public void SitAtSeat()
    {
        if (assignedSeat != null)
        {
            transform.position = assignedSeat.position;
            transform.rotation = assignedSeat.rotation;
            isSeated = true;
            
            if (navAgent != null)
            {
                navAgent.enabled = false;
            }
        }
    }
    
    private void WalkToDoor()
    {
        // Find door spawn point
        Transform door = GameObject.Find("ClientDoor")?.transform;
        if (door != null && navAgent != null)
        {
            navAgent.SetDestination(door.position);
        }
    }
    
    private void ReturnToGuild()
    {
        // Spawn at door and walk to idle spot
        Transform door = GameObject.Find("ClientDoor")?.transform;
        if (door != null)
        {
            transform.position = door.position;
            if (navAgent != null)
            {
                navAgent.enabled = true;
            }
        }
    }
    
    public void GainXP(int amount)
    {
        if (levelSystem != null)
        {
            levelSystem.AddXP(amount);
            currentXP = levelSystem.GetCurrentXP();
            currentLevel = levelSystem.GetCurrentLevel();
            stats?.UpdateLevel(currentLevel);
        }
    }
    
    public int GetLevel()
    {
        return currentLevel;
    }
    
    public int GetXP()
    {
        return currentXP;
    }
    
    public HunterData GetHunterData()
    {
        return hunterData;
    }
    
    public HunterStats GetStats()
    {
        return stats;
    }
    
    public bool CanLevelUp()
    {
        return levelSystem != null && levelSystem.CanLevelUp();
    }
    
    public int GetLevelUpCost()
    {
        return levelSystem != null ? levelSystem.GetLevelUpCost() : 0;
    }
    
    public bool LevelUp()
    {
        if (levelSystem != null && levelSystem.LevelUp())
        {
            currentLevel = levelSystem.GetCurrentLevel();
            stats?.UpdateLevel(currentLevel);
            return true;
        }
        return false;
    }
    
    private void Update()
    {
        // Check if reached seat
        if (!isSeated && assignedSeat != null && navAgent != null)
        {
            if (!navAgent.pathPending && navAgent.remainingDistance < 0.5f)
            {
                SitAtSeat();
            }
        }
    }
}

