using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

public class BonusChestInteractable : Interactable
{
    private enum RewardChestState
    {
        Ready,
        NormalOpening,
        MimicDisguised,
        MimicScaring,
        MimicFleeing,
        MimicNotCaught,
        MimicCaught
    }

    [Header("Chest Reward")]
    [SerializeField] private bool isMimic;
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent navAgent;

    [Header("Animation")]
    [SerializeField] private string initialState;
    [SerializeField] private bool holdInitialStateUntilInteraction = true;
    [SerializeField] private string openTrigger = "Open";
    [SerializeField] private string scaredTrigger = "SenseSomethingST";
    [SerializeField] private string runTrigger = "Run";
    [SerializeField] private string notCaughtTrigger;
    [SerializeField] private string caughtTrigger;

    [Header("VFX")]
    [SerializeField] private GameObject openVfxObject;
    [SerializeField] private GameObject caughtVfxObject;
    [SerializeField] private float chestDestroyDelay = 1f;
    [SerializeField] private bool destroyMimicOnCatch = true;
    [SerializeField] private float mimicCaughtDestroyDelay = 0.25f;

    [Header("Simple Lid Animation")]
    [SerializeField] private Transform lidTransform;
    [SerializeField] private Vector3 lidOpenEulerOffset = new Vector3(-75f, 0f, 0f);
    [SerializeField] private float lidOpenSeconds = 0.45f;

    [Header("Mimic Flee")]
    [SerializeField] private string mimicCatchInteractionPrompt = "[E] Catch Mimic";
    [SerializeField] private float scaredDelay = 0.75f;
    [SerializeField] private float fleeSeconds = 4f;
    [SerializeField] private float catchGraceSeconds = 1.5f;
    [FormerlySerializedAs("notCaughtReturnDelay")]
    [SerializeField] private float notCaughtDestroyDelay = 0.75f;
    [SerializeField] private float fleeSpeed = 3.5f;
    [SerializeField] private float fleeTurnSpeed = 720f;
    [SerializeField] private float waypointSearchRadius = 8f;
    [SerializeField] private float minWaypointDistance = 3f;
    [SerializeField] private int targetSearchAttempts = 16;
    [SerializeField] private float repathInterval = 0.75f;
    [SerializeField] private float stuckSeconds = 0.5f;
    [SerializeField] private float stuckMoveThreshold = 0.05f;

    [Header("Mimic Door Boundaries")]
    [FormerlySerializedAs("doorOpenRadius")]
    [SerializeField] private float closedDoorBlockRadius = 0.8f;
    [Tooltip("Doors the mimic should treat as boundaries. Closed doors block flee paths; open doors are allowed.")]
    [FormerlySerializedAs("routeDoorsToOpen")]
    [SerializeField] private List<GuildDoorController> routeDoorsToCheck = new List<GuildDoorController>();
    [FormerlySerializedAs("autoFindAvailableRouteDoors")]
    [SerializeField] private bool autoFindRouteDoorsToCheck = true;

    [Header("Mimic Audio")]
    [Tooltip("AudioSource on this mimic prefab. Leave empty to create one automatically at runtime.")]
    [FormerlySerializedAs("runningAudioSource")]
    [SerializeField] private AudioSource mimicLoopAudioSource;
    [SerializeField] private AudioClip runningLoopClip;
    [SerializeField, Range(0f, 1f)] private float runningLoopVolume = 1f;
    [SerializeField] private MusicManager musicManager;
    [SerializeField] private bool switchMusicDuringMimicFlee = true;

    private RewardChestState state = RewardChestState.Ready;
    private Coroutine mimicRoutine;
    private Coroutine lidRoutine;
    private bool catchAvailable;
    private string defaultInteractionPrompt;
    private GuildDoorController[] cachedAutoRouteDoors;
    private Quaternion closedLidRotation;

    private void Reset()
    {
        interactionPrompt = "[E] Open Chest";
        defaultInteractionPrompt = interactionPrompt;
        interactionType = InteractionType.Trigger;
        locksPlayer = false;
    }

    private void Awake()
    {
        CacheComponents();
        defaultInteractionPrompt = interactionPrompt;
        if (lidTransform != null)
        {
            closedLidRotation = lidTransform.localRotation;
        }
        interactionType = InteractionType.Trigger;
        locksPlayer = false;
        ResetInteractionState();
    }

    private void OnDisable()
    {
        StopMimicAudioAndMusic();
    }

    private void ResetInteractionState()
    {
        interactionPrompt = defaultInteractionPrompt;
        catchAvailable = false;

        if (isMimic)
        {
            state = RewardChestState.MimicDisguised;
            PlayStateImmediate(initialState);
        }
        else
        {
            state = RewardChestState.Ready;
        }
    }

    private void Update()
    {
        if (!isMimic || state != RewardChestState.MimicDisguised || !holdInitialStateUntilInteraction) return;
        HoldInitialState();
    }

    public override bool IsInteractionAvailable()
    {
        if (!base.IsInteractionAvailable()) return false;

        if (!isMimic)
        {
            return state == RewardChestState.Ready;
        }

        if (state == RewardChestState.MimicFleeing)
        {
            return catchAvailable;
        }

        return state == RewardChestState.MimicDisguised || state == RewardChestState.Ready;
    }

    public override void Interact(PlayerInteraction player)
    {
        if (!IsInteractionAvailable()) return;

        OnInteractionStart(player);

        if (!isMimic)
        {
            OpenNormalChest(player);
            return;
        }

        if (state == RewardChestState.MimicFleeing)
        {
            CatchMimic(player);
            return;
        }

        StartMimicFlee(player);
    }

    private void OpenNormalChest(PlayerInteraction player)
    {
        state = RewardChestState.NormalOpening;
        PlayAnimation(openTrigger);
        PlaySimpleLidAnimation();
        PlayOpenVfx(openVfxObject);
        OnInteractionEnd(player);
        Destroy(gameObject, chestDestroyDelay);
    }

    private void StartMimicFlee(PlayerInteraction player)
    {
        if (mimicRoutine != null)
        {
            StopCoroutine(mimicRoutine);
        }

        state = RewardChestState.MimicScaring;
        catchAvailable = false;
        interactionPrompt = defaultInteractionPrompt;
        StartMimicChaseMusic();
        mimicRoutine = StartCoroutine(MimicFleeRoutine(player));
    }

    private IEnumerator MimicFleeRoutine(PlayerInteraction player)
    {
        PlayAnimation(scaredTrigger);

        if (scaredDelay > 0f)
        {
            yield return new WaitForSeconds(scaredDelay);
        }

        state = RewardChestState.MimicFleeing;
        PlayAnimation(runTrigger);
        PrepareNavAgent();
        Transform playerTransform = player != null ? player.transform : null;
        SetNewFleeDestination(playerTransform);
        StartMimicRunningAudio();
        OnInteractionEnd(player);

        Vector3 lastPosition = transform.position;
        float elapsed = 0f;
        float repathTimer = repathInterval;
        float stuckTimer = 0f;

        while (elapsed < fleeSeconds)
        {
            float deltaTime = Time.deltaTime;
            elapsed += deltaTime;

            if (!catchAvailable && elapsed >= catchGraceSeconds)
            {
                catchAvailable = true;
                interactionPrompt = mimicCatchInteractionPrompt;
            }

            repathTimer += deltaTime;

            if (HasMovedEnough(lastPosition))
            {
                lastPosition = transform.position;
                stuckTimer = 0f;
            }
            else
            {
                stuckTimer += deltaTime;
            }

            if (ShouldRepath(repathTimer, stuckTimer))
            {
                repathTimer = 0f;
                stuckTimer = 0f;
                SetNewFleeDestination(playerTransform);
            }

            yield return null;
        }

        FinishMimicFlee(catchFail: true);
    }

    private void CatchMimic(PlayerInteraction player)
    {
        if (mimicRoutine != null)
        {
            StopCoroutine(mimicRoutine);
            mimicRoutine = null;
        }

        state = RewardChestState.MimicCaught;
        catchAvailable = false;
        StopMovement();
        StopMimicAudioAndMusic();
        PlayAnimation(caughtTrigger);
        PlayOpenVfx(caughtVfxObject);
        OnInteractionEnd(player);

        if (destroyMimicOnCatch)
        {
            Destroy(gameObject, mimicCaughtDestroyDelay);
        }
        else
        {
            ReturnToDisguise();
        }
    }

    private void FinishMimicFlee(bool catchFail)
    {
        mimicRoutine = null;
        StopMovement();
        StopMimicAudioAndMusic();

        if (catchFail)
        {
            StartCoroutine(DestroyAfterNotCaught());
        }
    }

    private IEnumerator DestroyAfterNotCaught()
    {
        state = RewardChestState.MimicNotCaught;
        catchAvailable = false;
        interactionPrompt = defaultInteractionPrompt;
        PlayAnimation(notCaughtTrigger);

        if (notCaughtDestroyDelay > 0f)
        {
            yield return new WaitForSeconds(notCaughtDestroyDelay);
        }

        Destroy(gameObject);
    }

    private void ReturnToDisguise()
    {
        state = RewardChestState.MimicDisguised;
        catchAvailable = false;
        interactionPrompt = defaultInteractionPrompt;
        PlayStateImmediate(initialState);
    }

    private void PrepareNavAgent()
    {
        if (navAgent == null || !navAgent.enabled || !navAgent.isOnNavMesh) return;

        navAgent.isStopped = false;
        navAgent.speed = fleeSpeed;
        navAgent.updateRotation = true;
        navAgent.angularSpeed = Mathf.Max(navAgent.angularSpeed, fleeTurnSpeed);
    }

    private bool ShouldRepath(float repathTimer, float stuckTimer)
    {
        if (navAgent == null || !navAgent.enabled || !navAgent.isOnNavMesh) return false;
        if (stuckTimer >= stuckSeconds) return true;
        if (repathTimer < repathInterval) return false;
        if (navAgent.pathPending) return false;
        if (!navAgent.hasPath) return true;
        if (navAgent.pathStatus != NavMeshPathStatus.PathComplete) return true;
        return navAgent.remainingDistance <= Mathf.Max(navAgent.stoppingDistance + 0.25f, 0.35f);
    }

    private bool HasMovedEnough(Vector3 lastPosition)
    {
        Vector3 flatDelta = transform.position - lastPosition;
        flatDelta.y = 0f;
        return flatDelta.sqrMagnitude >= stuckMoveThreshold * stuckMoveThreshold;
    }

    private bool SetNewFleeDestination(Transform playerTransform)
    {
        if (navAgent == null || !navAgent.enabled || !navAgent.isOnNavMesh) return false;

        Vector3 away = playerTransform != null
            ? transform.position - playerTransform.position
            : transform.forward;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)
        {
            away = transform.forward;
        }
        away.Normalize();

        for (int i = 0; i < targetSearchAttempts; i++)
        {
            Vector3 direction = GetFleeSearchDirection(away, i);
            float distance = Random.Range(minWaypointDistance, waypointSearchRadius);
            Vector3 candidate = transform.position + direction * distance;

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas)) continue;

            NavMeshPath path = new NavMeshPath();
            if (!navAgent.CalculatePath(hit.position, path)) continue;
            if (path.status != NavMeshPathStatus.PathComplete) continue;
            if (PathPassesThroughClosedDoor(path)) continue;

            FaceDirection(hit.position - transform.position, true);
            navAgent.SetDestination(hit.position);
            return true;
        }

        return false;
    }

    private Vector3 GetFleeSearchDirection(Vector3 away, int attemptIndex)
    {
        if (attemptIndex == 0) return away;

        Vector2 randomCircle = Random.insideUnitCircle.normalized;
        if (randomCircle.sqrMagnitude < 0.01f)
        {
            float angle = Random.Range(0f, 360f);
            return Quaternion.AngleAxis(angle, Vector3.up) * away;
        }

        return new Vector3(randomCircle.x, 0f, randomCircle.y);
    }

    private void StopMovement()
    {
        if (navAgent == null || !navAgent.enabled || !navAgent.isOnNavMesh) return;

        navAgent.ResetPath();
        navAgent.isStopped = true;
    }

    private bool PathPassesThroughClosedDoor(NavMeshPath path)
    {
        if (closedDoorBlockRadius <= 0f || path == null || path.corners == null || path.corners.Length < 2)
        {
            return false;
        }

        if (routeDoorsToCheck != null)
        {
            foreach (var door in routeDoorsToCheck)
            {
                if (PathPassesThroughClosedDoor(path, door)) return true;
            }
        }

        if (!autoFindRouteDoorsToCheck) return false;

        if (cachedAutoRouteDoors == null)
        {
            cachedAutoRouteDoors = SceneLookup.FindAll<GuildDoorController>(true);
        }

        foreach (var door in cachedAutoRouteDoors)
        {
            if (PathPassesThroughClosedDoor(path, door)) return true;
        }

        return false;
    }

    private bool PathPassesThroughClosedDoor(NavMeshPath path, GuildDoorController door)
    {
        if (door == null || door.IsOpen) return false;

        Vector3 doorPosition = door.transform.position;
        float radiusSquared = closedDoorBlockRadius * closedDoorBlockRadius;
        for (int i = 0; i < path.corners.Length - 1; i++)
        {
            Vector3 segmentStart = path.corners[i];
            Vector3 segmentEnd = path.corners[i + 1];
            if (DistanceSquaredToPathSegment(doorPosition, segmentStart, segmentEnd) > radiusSquared)
            {
                continue;
            }

            if (SegmentCrossesDoorPlane(door, segmentStart, segmentEnd))
            {
                return true;
            }
        }

        return false;
    }

    private bool SegmentCrossesDoorPlane(GuildDoorController door, Vector3 segmentStart, Vector3 segmentEnd)
    {
        Vector3 doorPosition = door.transform.position;
        Vector3 forward = door.transform.forward;
        forward.y = 0f;
        Vector3 right = door.transform.right;
        right.y = 0f;

        bool crossesForwardPlane = CrossesPlaneAtPoint(segmentStart, segmentEnd, doorPosition, forward);
        bool crossesRightPlane = CrossesPlaneAtPoint(segmentStart, segmentEnd, doorPosition, right);
        return crossesForwardPlane || crossesRightPlane;
    }

    private bool CrossesPlaneAtPoint(Vector3 segmentStart, Vector3 segmentEnd, Vector3 planePoint, Vector3 planeNormal)
    {
        if (planeNormal.sqrMagnitude < 0.001f) return false;

        planeNormal.Normalize();
        segmentStart.y = 0f;
        segmentEnd.y = 0f;
        planePoint.y = 0f;

        float startSide = Vector3.Dot(segmentStart - planePoint, planeNormal);
        float endSide = Vector3.Dot(segmentEnd - planePoint, planeNormal);
        const float sideEpsilon = 0.05f;
        return startSide < -sideEpsilon && endSide > sideEpsilon
            || startSide > sideEpsilon && endSide < -sideEpsilon;
    }

    private float DistanceSquaredToPathSegment(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd)
    {
        point.y = 0f;
        segmentStart.y = 0f;
        segmentEnd.y = 0f;

        Vector3 segment = segmentEnd - segmentStart;
        float segmentLengthSquared = segment.sqrMagnitude;
        if (segmentLengthSquared <= 0.0001f)
        {
            return (point - segmentStart).sqrMagnitude;
        }

        float t = Vector3.Dot(point - segmentStart, segment) / segmentLengthSquared;
        t = Mathf.Clamp01(t);
        Vector3 closestPoint = segmentStart + segment * t;
        return (point - closestPoint).sqrMagnitude;
    }

    private void StartMimicRunningAudio()
    {
        if (runningLoopClip != null)
        {
            if (mimicLoopAudioSource == null)
            {
                mimicLoopAudioSource = gameObject.AddComponent<AudioSource>();
            }

            mimicLoopAudioSource.clip = runningLoopClip;
            mimicLoopAudioSource.loop = true;
            mimicLoopAudioSource.volume = runningLoopVolume;
            mimicLoopAudioSource.playOnAwake = false;
            mimicLoopAudioSource.Play();
        }
    }

    private void StartMimicChaseMusic()
    {
        if (!switchMusicDuringMimicFlee) return;

        ResolveMusicManager()?.PlayMimicChaseMusic();
    }

    private void StopMimicAudioAndMusic()
    {
        if (mimicLoopAudioSource != null)
        {
            mimicLoopAudioSource.Stop();
        }

        if (switchMusicDuringMimicFlee && musicManager != null)
        {
            musicManager.ClearTemporaryOverride();
        }
    }

    private MusicManager ResolveMusicManager()
    {
        if (musicManager != null) return musicManager;

        musicManager = SceneLookup.Find<MusicManager>(true);
        if (musicManager == null)
        {
            Debug.LogWarning("BonusChestInteractable: Cannot switch to mimic chase music because no MusicManager was found in the scene.", this);
        }

        return musicManager;
    }

    private void PlaySimpleLidAnimation()
    {
        if (lidTransform == null || lidOpenSeconds <= 0f) return;

        if (lidRoutine != null)
        {
            StopCoroutine(lidRoutine);
        }

        lidRoutine = StartCoroutine(OpenLidRoutine());
    }

    private IEnumerator OpenLidRoutine()
    {
        Quaternion startRotation = lidTransform.localRotation;
        Quaternion targetRotation = closedLidRotation * Quaternion.Euler(lidOpenEulerOffset);
        float duration = Mathf.Max(0.01f, lidOpenSeconds);

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            t = Mathf.SmoothStep(0f, 1f, t);
            lidTransform.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        lidTransform.localRotation = targetRotation;
        lidRoutine = null;
    }

    private void PlayOpenVfx(GameObject vfxObject)
    {
        if (vfxObject != null)
        {
            vfxObject.SetActive(true);
            PlayParticleSystems(vfxObject);
        }
    }

    private void PlayParticleSystems(GameObject root)
    {
        if (root == null) return;

        var particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var particleSystem in particleSystems)
        {
            if (particleSystem == null) continue;
            particleSystem.Clear(true);
            particleSystem.Play(true);
        }
    }

    private void FaceDirection(Vector3 direction, bool instant)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = instant
            ? targetRotation
            : Quaternion.RotateTowards(transform.rotation, targetRotation, fleeTurnSpeed * Time.deltaTime);
    }

    private void CacheComponents()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (navAgent == null)
        {
            navAgent = GetComponent<NavMeshAgent>();
        }
    }

    private void PlayAnimation(string animationName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(animationName)) return;

        if (HasTriggerParameter(animationName))
        {
            animator.ResetTrigger(animationName);
            animator.SetTrigger(animationName);
            return;
        }

        CrossFadeState(animationName, 0.1f);
    }

    private void PlayStateImmediate(string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName)) return;

        int stateHash = Animator.StringToHash(stateName);
        int baseLayerStateHash = Animator.StringToHash($"Base Layer.{stateName}");
        if (animator.HasState(0, stateHash))
        {
            animator.Play(stateHash, 0, 0f);
        }
        else if (animator.HasState(0, baseLayerStateHash))
        {
            animator.Play(baseLayerStateHash, 0, 0f);
        }
    }

    private void HoldInitialState()
    {
        if (animator == null || string.IsNullOrWhiteSpace(initialState)) return;
        if (IsCurrentOrNextState(initialState)) return;

        PlayStateImmediate(initialState);
    }

    private bool IsCurrentOrNextState(string stateName)
    {
        int stateHash = Animator.StringToHash(stateName);
        int baseLayerStateHash = Animator.StringToHash($"Base Layer.{stateName}");

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (current.shortNameHash == stateHash || current.fullPathHash == baseLayerStateHash)
        {
            return true;
        }

        if (!animator.IsInTransition(0)) return false;

        AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
        return next.shortNameHash == stateHash || next.fullPathHash == baseLayerStateHash;
    }

    private void CrossFadeState(string stateName, float transitionSeconds)
    {
        int stateHash = Animator.StringToHash(stateName);
        int baseLayerStateHash = Animator.StringToHash($"Base Layer.{stateName}");
        if (animator.HasState(0, stateHash))
        {
            animator.CrossFadeInFixedTime(stateHash, transitionSeconds, 0);
        }
        else if (animator.HasState(0, baseLayerStateHash))
        {
            animator.CrossFadeInFixedTime(baseLayerStateHash, transitionSeconds, 0);
        }
    }

    private bool HasTriggerParameter(string parameterName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName)) return false;

        foreach (var parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == parameterName)
            {
                return true;
            }
        }

        return false;
    }
}
