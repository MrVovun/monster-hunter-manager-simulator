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
    [SerializeField] private float infirmaryNavMeshSampleRadius = 2f;
    private Transform doorTransform;
    private bool isDepartingForMission;
    private bool isStandingUp;
    private float standUpTimer;
    private System.Action standUpCompletedAction;
    private bool isWalkingToInfirmary;
    private Transform infirmaryTarget;
    private System.Action<Hunter> infirmaryArrivalCallback;
    private bool isWalkingToDormitory;
    private Transform dormitoryBedTarget;
    private System.Action<Hunter> dormitoryArrivalCallback;
    private bool isWakingFromDormitory;
    private bool isWalkingToKitchenPoint;
    private Transform kitchenPointTarget;
    private System.Action<Hunter> kitchenPointArrivalCallback;
    private bool isWalkingToTemporarySeat;
    private System.Action<Hunter> temporarySeatArrivalCallback;

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
        HunterState previousState = state;
        state = newState;

        CacheHunterManager();
        hunterManager?.NotifyHunterStateChanged(this, state);

        if (newState == HunterState.OnMission)
        {
            bool wasSeated = isSeated;
            isSeated = false;
            playSitEntry = false;
            ReleaseSeat();
            if (wasSeated)
            {
                BeginStandUpSequence(WalkToDoor);
            }
            else
            {
                WalkToDoor();
            }
        }
        else if (newState == HunterState.Idle)
        {
            isWalkingToInfirmary = false;
            infirmaryTarget = null;
            infirmaryArrivalCallback = null;
            isWalkingToDormitory = false;
            dormitoryBedTarget = null;
            dormitoryArrivalCallback = null;
            isWakingFromDormitory = false;
            isWalkingToKitchenPoint = false;
            kitchenPointTarget = null;
            kitchenPointArrivalCallback = null;
            isWalkingToTemporarySeat = false;
            temporarySeatArrivalCallback = null;
            standUpCompletedAction = null;
            sharedAnimator?.StopClipPlayback();
            if (previousState == HunterState.Healing || previousState == HunterState.Sleeping)
            {
                PrepareForIndoorNavigation();
            }
            else
            {
                ReturnToGuild();
            }
            isSeated = false;
            playSitEntry = false;
            RequestSeatAssignment();
        }
        else if (newState == HunterState.Healing)
        {
            isSeated = false;
            playSitEntry = false;
            ReleaseSeat();
        }
        else if (newState == HunterState.Sleeping)
        {
            isSeated = false;
            playSitEntry = false;
            ReleaseSeat();
        }
        else if (newState == HunterState.Dead)
        {
            isWalkingToInfirmary = false;
            infirmaryTarget = null;
            infirmaryArrivalCallback = null;
            isWalkingToDormitory = false;
            dormitoryBedTarget = null;
            dormitoryArrivalCallback = null;
            isWakingFromDormitory = false;
            isWalkingToKitchenPoint = false;
            kitchenPointTarget = null;
            kitchenPointArrivalCallback = null;
            isWalkingToTemporarySeat = false;
            temporarySeatArrivalCallback = null;
            standUpCompletedAction = null;
            sharedAnimator?.StopClipPlayback();
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

        if (isSeated)
        {
            BeginStandUpSequence(() => StartWalkToSeat(seat));
            return;
        }

        StartWalkToSeat(seat);
    }

    private void StartWalkToSeat(HunterSeat seat)
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

    public bool WalkToTemporarySeat(HunterSeat seat, System.Action<Hunter> onArrived = null)
    {
        if (seat == null) return false;
        if (GetState() == HunterState.Dead || GetState() == HunterState.OnMission || GetState() == HunterState.Candidate || GetState() == HunterState.Healing || GetState() == HunterState.Sleeping) return false;
        if (!seat.TryAssign(this)) return false;

        temporarySeatArrivalCallback = onArrived;
        if (isSeated)
        {
            BeginStandUpSequence(() => StartWalkToTemporarySeat(seat));
            return true;
        }

        StartWalkToTemporarySeat(seat);
        return true;
    }

    private void StartWalkToTemporarySeat(HunterSeat seat)
    {
        if (navAgent == null || seat == null)
        {
            SitAtSeat();
            return;
        }

        ReleaseSeat();
        assignedSeat = seat;
        assignedSeat.TryAssign(this);
        isSeated = false;
        isWalkingToTemporarySeat = true;

        if (!navAgent.enabled)
        {
            navAgent.enabled = true;
        }

        if (!navAgent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit selfHit, Mathf.Max(0.1f, infirmaryNavMeshSampleRadius), NavMesh.AllAreas))
            {
                navAgent.Warp(selfHit.position);
            }
        }

        if (!navAgent.isOnNavMesh)
        {
            Debug.LogWarning($"Hunter {name}: Unable to reach temporary seat because the hunter is not on the NavMesh.", this);
            ReturnToGuildSeat();
            return;
        }

        Vector3 destination = seat.ApproachPosition;
        if (NavMesh.SamplePosition(seat.ApproachPosition, out NavMeshHit targetHit, Mathf.Max(0.1f, infirmaryNavMeshSampleRadius), NavMesh.AllAreas))
        {
            destination = targetHit.position;
        }
        else
        {
            Debug.LogWarning($"Hunter {name}: Unable to reach temporary seat because approach point for '{seat.name}' is not near the NavMesh.", this);
            ReturnToGuildSeat();
            return;
        }

        navAgent.isStopped = false;
        NavMeshPath path = new NavMeshPath();
        bool hasCompletePath = navAgent.CalculatePath(destination, path) && path.status == NavMeshPathStatus.PathComplete;
        bool pathSet = hasCompletePath && navAgent.SetDestination(destination);
        if (!pathSet)
        {
            Debug.LogWarning($"Hunter {name}: Unable to find complete path to temporary seat '{seat.name}'. Check NavMesh bake, briefing route doors, walls, and chair approach point.", this);
            ReturnToGuildSeat();
        }
    }

    public void ReturnToGuildSeat()
    {
        if (GetState() == HunterState.Dead || GetState() == HunterState.OnMission || GetState() == HunterState.Candidate) return;

        if (isSeated)
        {
            BeginStandUpSequence(StartReturnToGuildSeat);
            return;
        }

        StartReturnToGuildSeat();
    }

    private void StartReturnToGuildSeat()
    {
        isWalkingToTemporarySeat = false;
        temporarySeatArrivalCallback = null;
        isSeated = false;
        playSitEntry = false;
        ReleaseSeat();
        RequestSeatAssignment();
    }

    public void PlayBriefingReaction(SharedCharacterAnimator.ClipEntry reactionClip)
    {
        sharedAnimator?.PlayCustomClip(reactionClip);
    }

    public bool PlayCustomAnimation(SharedCharacterAnimator.ClipEntry clip, System.Action onComplete = null)
    {
        return sharedAnimator != null && sharedAnimator.PlayCustomClip(clip, onComplete);
    }

    public void StopCustomAnimation()
    {
        sharedAnimator?.StopClipPlayback();
    }

    public void PlayBriefingReactionThenReturn(SharedCharacterAnimator.ClipEntry reactionClip, float fallbackDelay)
    {
        if (GetState() == HunterState.Dead || GetState() == HunterState.OnMission || GetState() == HunterState.Candidate) return;

        if (isSeated)
        {
            BeginStandUpSequence(() => PlayBriefingReactionAndReturn(reactionClip, fallbackDelay));
            return;
        }

        PlayBriefingReactionAndReturn(reactionClip, fallbackDelay);
    }

    private void PlayBriefingReactionAndReturn(SharedCharacterAnimator.ClipEntry reactionClip, float fallbackDelay)
    {
        bool played = sharedAnimator != null && sharedAnimator.PlayCustomClip(reactionClip, StartReturnToGuildSeat);
        if (!played)
        {
            StartCoroutine(ReturnToGuildSeatAfterDelay(fallbackDelay));
        }
    }

    private System.Collections.IEnumerator ReturnToGuildSeatAfterDelay(float delay)
    {
        float wait = Mathf.Max(0f, delay);
        if (wait > 0f)
        {
            yield return new WaitForSeconds(wait);
        }

        StartReturnToGuildSeat();
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

    public HunterSeat GetAssignedSeat()
    {
        return assignedSeat;
    }

    public bool IsSeated()
    {
        return isSeated;
    }

    public bool WalkToKitchenPoint(Transform target, System.Action<Hunter> onArrived)
    {
        if (target == null) return false;
        if (GetState() == HunterState.Dead || GetState() == HunterState.OnMission || GetState() == HunterState.Candidate || GetState() == HunterState.Healing || GetState() == HunterState.Sleeping) return false;

        bool wasSeated = isSeated;
        isSeated = false;
        playSitEntry = false;
        ReleaseSeat();
        kitchenPointTarget = target;
        kitchenPointArrivalCallback = onArrived;

        if (wasSeated)
        {
            BeginStandUpSequence(() => StartKitchenPointPath());
            return true;
        }

        return StartKitchenPointPath();
    }

    private bool StartKitchenPointPath()
    {
        if (kitchenPointTarget == null) return false;

        if (navAgent == null)
        {
            CompleteKitchenPointArrival();
            return true;
        }

        if (!navAgent.enabled)
        {
            navAgent.enabled = true;
        }

        if (!navAgent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit selfHit, Mathf.Max(0.1f, infirmaryNavMeshSampleRadius), NavMesh.AllAreas))
            {
                navAgent.Warp(selfHit.position);
            }
        }

        if (!navAgent.isOnNavMesh)
        {
            Debug.LogWarning($"Hunter {name}: Unable to reach kitchen point because the hunter is not on the NavMesh.", this);
            kitchenPointTarget = null;
            kitchenPointArrivalCallback = null;
            ReturnToGuildSeat();
            return false;
        }

        Vector3 destination = kitchenPointTarget.position;
        if (NavMesh.SamplePosition(kitchenPointTarget.position, out NavMeshHit targetHit, Mathf.Max(0.1f, infirmaryNavMeshSampleRadius), NavMesh.AllAreas))
        {
            destination = targetHit.position;
        }
        else
        {
            Debug.LogWarning($"Hunter {name}: Unable to reach kitchen point '{kitchenPointTarget.name}' because it is not near the NavMesh.", this);
            kitchenPointTarget = null;
            kitchenPointArrivalCallback = null;
            ReturnToGuildSeat();
            return false;
        }

        navAgent.isStopped = false;
        NavMeshPath path = new NavMeshPath();
        bool hasCompletePath = navAgent.CalculatePath(destination, path) && path.status == NavMeshPathStatus.PathComplete;
        bool pathSet = hasCompletePath && navAgent.SetDestination(destination);
        if (!pathSet)
        {
            Debug.LogWarning($"Hunter {name}: Unable to find complete path to kitchen point '{kitchenPointTarget.name}'. Check NavMesh bake, route doors, and obstacles.", this);
            kitchenPointTarget = null;
            kitchenPointArrivalCallback = null;
            ReturnToGuildSeat();
            return false;
        }

        isWalkingToKitchenPoint = true;
        return true;
    }

    public bool WalkToInfirmary(Transform treatmentPoint, System.Action<Hunter> onArrived)
    {
        if (treatmentPoint == null) return false;
        if (GetState() == HunterState.Dead || GetState() == HunterState.OnMission) return false;

        bool wasSeated = isSeated;
        SetState(HunterState.Healing);
        infirmaryTarget = treatmentPoint;
        infirmaryArrivalCallback = onArrived;

        if (wasSeated)
        {
            BeginStandUpSequence(() => StartInfirmaryPath());
            return true;
        }

        return StartInfirmaryPath();
    }

    private bool StartInfirmaryPath()
    {
        if (infirmaryTarget == null) return false;

        if (navAgent == null)
        {
            CompleteInfirmaryArrival();
            return true;
        }

        if (!navAgent.enabled)
        {
            navAgent.enabled = true;
        }

        if (!navAgent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit selfHit, Mathf.Max(0.1f, infirmaryNavMeshSampleRadius), NavMesh.AllAreas))
            {
                navAgent.Warp(selfHit.position);
            }
        }

        if (!navAgent.isOnNavMesh)
        {
            Debug.LogWarning($"Hunter {name}: Unable to reach infirmary because the hunter is not on the NavMesh.", this);
            infirmaryTarget = null;
            infirmaryArrivalCallback = null;
            SetState(HunterState.Idle);
            return false;
        }

        Vector3 destination = infirmaryTarget.position;
        if (NavMesh.SamplePosition(infirmaryTarget.position, out NavMeshHit targetHit, Mathf.Max(0.1f, infirmaryNavMeshSampleRadius), NavMesh.AllAreas))
        {
            destination = targetHit.position;
        }
        else
        {
            Debug.LogWarning($"Hunter {name}: Unable to reach infirmary because treatment point '{infirmaryTarget.name}' is not near the NavMesh.", this);
            infirmaryTarget = null;
            infirmaryArrivalCallback = null;
            SetState(HunterState.Idle);
            return false;
        }

        navAgent.isStopped = false;
        NavMeshPath path = new NavMeshPath();
        bool hasCompletePath = navAgent.CalculatePath(destination, path) && path.status == NavMeshPathStatus.PathComplete;
        bool pathSet = hasCompletePath && navAgent.SetDestination(destination);
        if (!pathSet)
        {
            Debug.LogWarning($"Hunter {name}: Unable to find complete path to infirmary treatment point '{infirmaryTarget.name}'. Check NavMesh bake, route doors, and obstacles.", this);
            infirmaryTarget = null;
            infirmaryArrivalCallback = null;
            SetState(HunterState.Idle);
            return false;
        }

        isWalkingToInfirmary = true;
        return true;
    }

    public void FinishInfirmaryTreatment()
    {
        isWalkingToInfirmary = false;
        infirmaryTarget = null;
        infirmaryArrivalCallback = null;
        if (GetState() != HunterState.Dead)
        {
            SetState(HunterState.Idle);
        }
    }

    public bool WalkToDormitoryBed(Transform bedPoint, System.Action<Hunter> onArrived)
    {
        if (bedPoint == null) return false;
        if (GetState() == HunterState.Dead || GetState() == HunterState.OnMission || GetState() == HunterState.Candidate) return false;

        bool wasSeated = isSeated;
        SetState(HunterState.Sleeping);
        dormitoryBedTarget = bedPoint;
        dormitoryArrivalCallback = onArrived;

        if (wasSeated)
        {
            BeginStandUpSequence(() => StartDormitoryPath());
            return true;
        }

        return StartDormitoryPath();
    }

    private bool StartDormitoryPath()
    {
        if (dormitoryBedTarget == null) return false;

        if (navAgent == null)
        {
            CompleteDormitoryArrival();
            return true;
        }

        if (!navAgent.enabled)
        {
            navAgent.enabled = true;
        }

        if (!navAgent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit selfHit, Mathf.Max(0.1f, infirmaryNavMeshSampleRadius), NavMesh.AllAreas))
            {
                navAgent.Warp(selfHit.position);
            }
        }

        if (!navAgent.isOnNavMesh)
        {
            Debug.LogWarning($"Hunter {name}: Unable to reach dormitory because the hunter is not on the NavMesh.", this);
            dormitoryBedTarget = null;
            dormitoryArrivalCallback = null;
            SetState(HunterState.Idle);
            return false;
        }

        Vector3 destination = dormitoryBedTarget.position;
        if (NavMesh.SamplePosition(dormitoryBedTarget.position, out NavMeshHit targetHit, Mathf.Max(0.1f, infirmaryNavMeshSampleRadius), NavMesh.AllAreas))
        {
            destination = targetHit.position;
        }
        else
        {
            Debug.LogWarning($"Hunter {name}: Unable to reach dormitory because bed point '{dormitoryBedTarget.name}' is not near the NavMesh.", this);
            dormitoryBedTarget = null;
            dormitoryArrivalCallback = null;
            SetState(HunterState.Idle);
            return false;
        }

        navAgent.isStopped = false;
        NavMeshPath path = new NavMeshPath();
        bool hasCompletePath = navAgent.CalculatePath(destination, path) && path.status == NavMeshPathStatus.PathComplete;
        bool pathSet = hasCompletePath && navAgent.SetDestination(destination);
        if (!pathSet)
        {
            Debug.LogWarning($"Hunter {name}: Unable to find complete path to dormitory bed point '{dormitoryBedTarget.name}'. Check NavMesh bake, route doors, and obstacles.", this);
            dormitoryBedTarget = null;
            dormitoryArrivalCallback = null;
            SetState(HunterState.Idle);
            return false;
        }

        isWalkingToDormitory = true;
        return true;
    }

    public void WakeFromDormitory()
    {
        if (GetState() != HunterState.Sleeping) return;
        if (isWakingFromDormitory) return;

        isWakingFromDormitory = true;
        isWalkingToDormitory = false;
        dormitoryBedTarget = null;
        dormitoryArrivalCallback = null;

        bool played = sharedAnimator != null && sharedAnimator.PlayGetUpClip(CompleteDormitoryWake);
        if (!played)
        {
            CompleteDormitoryWake();
        }
    }

    private void CompleteDormitoryWake()
    {
        isWakingFromDormitory = false;
        sharedAnimator?.StopClipPlayback();
        SetState(HunterState.Idle);
    }

    private void ReleaseSeat()
    {
        if (assignedSeat != null)
        {
            assignedSeat.Release(this);
            assignedSeat = null;
        }
    }

    private void BeginStandUpSequence(System.Action onCompleted = null)
    {
        isStandingUp = true;
        standUpTimer = Mathf.Max(0.1f, standUpDuration);
        standUpCompletedAction = onCompleted;

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

        var action = standUpCompletedAction;
        standUpCompletedAction = null;
        action?.Invoke();
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

    private void PrepareForIndoorNavigation()
    {
        if (visualInstance != null)
        {
            visualInstance.SetActive(true);
        }

        if (navAgent == null) return;
        if (!navAgent.enabled)
        {
            navAgent.enabled = true;
        }

        if (!navAgent.isOnNavMesh)
        {
            navAgent.Warp(transform.position);
        }

        navAgent.isStopped = false;
    }

    private void CompleteInfirmaryArrival()
    {
        isWalkingToInfirmary = false;

        if (infirmaryTarget != null)
        {
            transform.SetPositionAndRotation(infirmaryTarget.position, infirmaryTarget.rotation);
        }

        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.ResetPath();
            navAgent.isStopped = true;
            navAgent.enabled = false;
        }

        var callback = infirmaryArrivalCallback;
        infirmaryArrivalCallback = null;
        callback?.Invoke(this);
    }

    private void CompleteDormitoryArrival()
    {
        isWalkingToDormitory = false;

        if (dormitoryBedTarget != null)
        {
            transform.SetPositionAndRotation(dormitoryBedTarget.position, dormitoryBedTarget.rotation);
        }

        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.ResetPath();
            navAgent.isStopped = true;
            navAgent.enabled = false;
        }

        sharedAnimator?.SetMoving(false);
        bool playedLayDown = sharedAnimator != null && sharedAnimator.PlayLayDownClip(PlayDormitorySleepLoop);
        if (!playedLayDown)
        {
            PlayDormitorySleepLoop();
        }

        var callback = dormitoryArrivalCallback;
        dormitoryArrivalCallback = null;
        callback?.Invoke(this);
    }

    private void CompleteKitchenPointArrival()
    {
        isWalkingToKitchenPoint = false;

        if (kitchenPointTarget != null)
        {
            transform.rotation = kitchenPointTarget.rotation;
        }

        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.ResetPath();
            navAgent.isStopped = true;
        }

        var callback = kitchenPointArrivalCallback;
        kitchenPointTarget = null;
        kitchenPointArrivalCallback = null;
        callback?.Invoke(this);
    }

    private void PlayDormitorySleepLoop()
    {
        if (GetState() != HunterState.Sleeping) return;
        bool playedSleep = sharedAnimator != null && sharedAnimator.PlaySleepLoopClip();
        if (!playedSleep)
        {
            sharedAnimator?.StopClipPlayback();
        }
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

    public HunterData Data => hunterData;

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
        int baseCost = hunterData != null ? hunterData.dailyUpkeepCost : 0;
        float multiplier = 1f;

        var traits = hunterData != null ? hunterData.traits : null;
        if (traits != null)
        {
            foreach (var trait in traits)
            {
                if (trait == null || trait.bonusEffects == null) continue;
                foreach (var effect in trait.bonusEffects)
                {
                    if (effect == null) continue;
                    if (effect.bonusType != HunterTrait.BonusEffectType.UpkeepCostMultiplier) continue;
                    if (!MissionOutcomeCalculator.DoesConditionPass(effect.condition, null, 1)) continue;
                    float mult = effect.value <= 0f ? 1f : effect.value;
                    multiplier *= mult;
                }
            }
        }

        return Mathf.Max(0, Mathf.RoundToInt(baseCost * multiplier));
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
                if (isWalkingToTemporarySeat)
                {
                    isWalkingToTemporarySeat = false;
                    var callback = temporarySeatArrivalCallback;
                    temporarySeatArrivalCallback = null;
                    callback?.Invoke(this);
                }
            }
        }

        if (isDepartingForMission && navAgent != null && navAgent.enabled)
        {
            if (!navAgent.pathPending && navAgent.remainingDistance < doorArrivalThreshold)
            {
                CompleteDeparture();
            }
        }

        if (isWalkingToInfirmary && navAgent != null && navAgent.enabled)
        {
            if (!navAgent.pathPending && navAgent.remainingDistance < 0.5f)
            {
                CompleteInfirmaryArrival();
            }
        }

        if (isWalkingToDormitory && navAgent != null && navAgent.enabled)
        {
            if (!navAgent.pathPending && navAgent.remainingDistance < 0.5f)
            {
                CompleteDormitoryArrival();
            }
        }

        if (isWalkingToKitchenPoint && navAgent != null && navAgent.enabled)
        {
            if (!navAgent.pathPending && navAgent.remainingDistance < 0.5f)
            {
                CompleteKitchenPointArrival();
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
