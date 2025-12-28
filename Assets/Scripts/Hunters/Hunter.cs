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

    [Header("Visuals")]
    [SerializeField] private Transform visualParent;
    private GameObject visualInstance;

    [Header("Components")]
    private NavMeshAgent navAgent;
    private Animator animator;
    [SerializeField] private SharedCharacterAnimator sharedAnimator;

    [Header("Seating")]
    private HunterSeat assignedSeat;
    private bool isSeated = false;
    private bool playSitEntry = false;

    [Header("Navigation")]
    [SerializeField] private float doorApproachOffset = 0.8f;
    [SerializeField] private float doorArrivalThreshold = 0.3f;
    [SerializeField] private float standUpDuration = 1.3f;
    private Transform doorTransform;
    private bool isDepartingForMission;
    private bool isStandingUp;
    private float standUpTimer;

    private bool baseLayerInitialized = false;
    private HunterStats stats;
    private HunterLevelSystem levelSystem;
    private HunterManager hunterManager;
    private int debugUpkeepOverride = -1;

    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
        if (navAgent == null)
        {
            navAgent = gameObject.AddComponent<NavMeshAgent>();
        }

        animator = GetComponentInChildren<Animator>();
        if (sharedAnimator == null)
        {
            sharedAnimator = GetComponent<SharedCharacterAnimator>();
            if (sharedAnimator == null)
            {
                sharedAnimator = gameObject.AddComponent<SharedCharacterAnimator>();
            }
        }
        sharedAnimator.SetNavAgent(navAgent);
        sharedAnimator.AutoUpdateVelocity = true;
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

        CacheHunterManager();
        CacheDoorTransform();
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
        SetupVisual(data.visualPrefab);
    }

    public void SetState(HunterState newState)
    {
        if (state == newState) return;
        state = newState;

        if (newState == HunterState.OnMission)
        {
            bool wasSeated = isSeated;
            isSeated = false;
            playSitEntry = false;
            ReleaseSeat();
            if (wasSeated)
            {
                BeginStandUpSequence();
            }
            else
            {
                WalkToDoor();
            }
        }
        else if (newState == HunterState.Idle)
        {
            ReturnToGuild();
            isSeated = false;
            playSitEntry = false;
            RequestSeatAssignment();
        }
        else if (newState == HunterState.Dead)
        {
            ReleaseSeat();
            if (navAgent != null)
            {
                navAgent.enabled = false;
            }
        }
    }

    public HunterState GetState()
    {
        return state;
    }

    public void WalkToSeat(HunterSeat seat)
    {
        if (navAgent == null || seat == null) return;

        ReleaseSeat();
        assignedSeat = seat;
        assignedSeat.TryAssign(this);
        isSeated = false;

        if (!navAgent.enabled)
        {
            navAgent.enabled = true;
        }

        navAgent.isStopped = false;
        navAgent.SetDestination(seat.ApproachPosition);
    }

    public void SitAtSeat()
    {
        if (assignedSeat != null)
        {
            assignedSeat.TryAssign(this);
            Transform anchor = assignedSeat.Anchor;
            transform.position = anchor.position;
            transform.rotation = anchor.rotation;
            isSeated = true;
            playSitEntry = true;
            sharedAnimator?.PlaySitSequence();

            if (navAgent != null)
            {
                navAgent.enabled = false;
            }
        }
    }

    private void ReleaseSeat()
    {
        if (assignedSeat != null)
        {
            assignedSeat.Release(this);
            assignedSeat = null;
        }
    }

    private void BeginStandUpSequence()
    {
        isStandingUp = true;
        standUpTimer = Mathf.Max(0.1f, standUpDuration);

        if (animator != null)
        {
            animator.SetInteger("TriggerNumber", 2);
            animator.SetTrigger("Trigger");
            animator.SetInteger("Action", 9);
            animator.SetBool("Moving", false);
        }

        if (navAgent != null)
        {
            navAgent.enabled = false;
        }
    }

    private void CompleteStandUpSequence()
    {
        isStandingUp = false;
        standUpTimer = 0f;

        if (navAgent != null && !navAgent.enabled)
        {
            navAgent.enabled = true;
        }

        WalkToDoor();
    }

    private void WalkToDoor()
    {
        if (navAgent == null)
        {
            CompleteDeparture();
            return;
        }

        Vector3 target = GetDoorInsidePosition();

        if (!navAgent.enabled)
        {
            navAgent.enabled = true;
        }

        if (!navAgent.isOnNavMesh)
        {
            navAgent.Warp(transform.position);
        }

        navAgent.isStopped = false;
        bool pathSet = navAgent.SetDestination(target);
        if (!pathSet)
        {
            Debug.LogWarning($"Hunter {name}: Unable to find path to door. Teleporting out.");
            CompleteDeparture();
            return;
        }

        isDepartingForMission = true;
    }

    private void ReturnToGuild()
    {
        isStandingUp = false;
        standUpTimer = 0f;
        CacheDoorTransform();
        Vector3 spawnPos = GetReturnSpawnPosition();

        if (navAgent != null)
        {
            navAgent.enabled = true;
            navAgent.Warp(spawnPos);
            navAgent.ResetPath();
            navAgent.isStopped = false;
        }
        else
        {
            transform.position = spawnPos;
        }

        if (visualInstance != null)
        {
            visualInstance.SetActive(true);
        }

        isDepartingForMission = false;
    }

    private void CompleteDeparture()
    {
        isDepartingForMission = false;

        Vector3 outside = GetDoorOutsidePosition();
        transform.position = outside;

        if (navAgent != null)
        {
            if (navAgent.enabled && navAgent.isOnNavMesh)
            {
                navAgent.ResetPath();
            }
            navAgent.enabled = false;
        }

        if (visualInstance != null)
        {
            visualInstance.SetActive(false);
        }
    }

    private void RequestSeatAssignment()
    {
        CacheHunterManager();
        hunterManager?.AssignHunterToSeat(this);
    }

    private void CacheHunterManager()
    {
        if (hunterManager == null && GameManager.Instance != null)
        {
            hunterManager = GameManager.Instance.GetHunterManager();
        }
    }

    private void CacheDoorTransform()
    {
        if (doorTransform != null) return;

        GameObject doorObj = GameObject.Find("ClientDoor");
        if (doorObj != null)
        {
            doorTransform = doorObj.transform;
        }
    }

    private Vector3 GetDoorInsidePosition()
    {
        CacheHunterManager();
        var entry = hunterManager?.GetDoorEntryTransform();
        if (entry != null)
        {
            return entry.position;
        }

        CacheDoorTransform();
        if (doorTransform == null)
        {
            return transform.position;
        }

        return doorTransform.position + doorTransform.forward * Mathf.Max(0f, doorApproachOffset);
    }

    private Vector3 GetDoorOutsidePosition()
    {
        CacheHunterManager();
        var exit = hunterManager?.GetDoorExitTransform();
        if (exit != null)
        {
            return exit.position;
        }

        CacheDoorTransform();
        if (doorTransform == null)
        {
            return transform.position;
        }

        return doorTransform.position - doorTransform.forward * Mathf.Max(0.1f, doorApproachOffset);
    }

    private Vector3 GetReturnSpawnPosition()
    {
        CacheHunterManager();
        var spawn = hunterManager?.GetReturnSpawnTransform();
        if (spawn != null)
        {
            return spawn.position;
        }

        return GetDoorInsidePosition();
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

    public int GetXPToNextLevel()
    {
        if (levelSystem == null) return int.MaxValue;
        return levelSystem.GetXPForNextLevel();
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

    public void DebugSetLevelAndXP(int level, int xp)
    {
        if (levelSystem == null) return;
        levelSystem.DebugSetLevelAndXP(level, xp);
        currentLevel = levelSystem.GetCurrentLevel();
        currentXP = levelSystem.GetCurrentXP();
        stats?.UpdateLevel(currentLevel);
    }

    public void SetDebugUpkeep(int value)
    {
        debugUpkeepOverride = Mathf.Max(0, value);
    }

    public void ClearDebugUpkeep()
    {
        debugUpkeepOverride = -1;
    }

    public bool HasDebugUpkeepOverride()
    {
        return debugUpkeepOverride >= 0;
    }

    public int GetUpkeepCost()
    {
        if (debugUpkeepOverride >= 0) return debugUpkeepOverride;
        return hunterData != null ? hunterData.dailyUpkeepCost : 0;
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
        if (isStandingUp)
        {
            standUpTimer -= Time.deltaTime;
            if (standUpTimer <= 0f)
            {
                CompleteStandUpSequence();
            }
        }

        // Check if reached seat
        if (!isSeated && assignedSeat != null && navAgent != null)
        {
            if (!navAgent.pathPending && navAgent.remainingDistance < 0.5f)
            {
                SitAtSeat();
            }
        }

        if (isDepartingForMission && navAgent != null && navAgent.enabled)
        {
            if (!navAgent.pathPending && navAgent.remainingDistance < doorArrivalThreshold)
            {
                CompleteDeparture();
            }
        }

        UpdateAnimationParameters();
    }

    private void SetupVisual(GameObject prefab)
    {
        if (visualInstance != null)
        {
            if (Application.isPlaying)
            {
                Destroy(visualInstance);
            }
            else
            {
                DestroyImmediate(visualInstance);
            }
            visualInstance = null;
        }

        if (prefab != null)
        {
            Transform parent = visualParent != null ? visualParent : transform;
            visualInstance = Instantiate(prefab, parent);
            visualInstance.transform.localPosition = Vector3.zero;
            visualInstance.transform.localRotation = Quaternion.identity;
            visualInstance.transform.localScale = Vector3.one;
            animator = visualInstance.GetComponentInChildren<Animator>();
        }
        else
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            sharedAnimator?.SetAnimatorReference(animator);
            sharedAnimator?.SetAnimationSpeed(1f);
        }
        else
        {
            Debug.LogWarning($"Hunter '{name}' has no Animator assigned after visual setup.", this);
        }
    }

    private void UpdateAnimationParameters()
    {
        if (animator == null) return;

        float speed = 0f;
        if (!baseLayerInitialized)
        {
            animator.SetInteger("Weapon", -1);
            animator.SetInteger("TriggerNumber", 25);
            animator.SetTrigger("Trigger");
            baseLayerInitialized = true;
        }

        bool navEnabled = navAgent != null && navAgent.enabled;
        if (navEnabled)
        {
            speed = navAgent.velocity.magnitude;
        }

        sharedAnimator?.SetAnimationSpeed(1f);

        bool moving = speed > 0.05f && !isSeated;
        sharedAnimator?.SetMoving(moving);
        if (!moving && !isSeated && !isStandingUp)
        {
            animator.SetInteger("Action", 0);
            animator.SetInteger("Talking", 0);
        }
        animator.SetBool("Crouch", false);
        animator.SetBool("Injured", false);

        if (playSitEntry)
        {
            animator.SetInteger("TriggerNumber", 2);
            animator.SetTrigger("Trigger");
            animator.SetInteger("Action", 0);
            animator.SetBool("Moving", false);
            playSitEntry = false;
        }
    }
}
