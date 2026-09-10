using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

public class BonusChestInteractable : Interactable
{
    [System.Serializable]
    public struct Settings
    {
        public bool IsMimic;
        public string InteractionPrompt;
        public string MimicCatchInteractionPrompt;
        public string InitialState;
        public string OpenTrigger;
        public string ScaredTrigger;
        public string RunTrigger;
        public string CaughtTrigger;
        public float ScaredDelay;
        public float FleeSeconds;
        public float CatchGraceSeconds;
        public float FleeSpeed;
        public float WaypointSearchRadius;
        public float MinWaypointDistance;
        public float RepathInterval;
        public float StuckSeconds;
        public float DoorOpenRadius;
        public GameObject OpenVfxPrefab;
        public GameObject CaughtVfxPrefab;
        public float ChestDestroyDelay;
        public AudioClip RunningLoopClip;
        public float RunningLoopVolume;
        public bool SwitchMusicDuringMimicFlee;
        public List<GuildDoorController> RouteDoorsToOpen;
        public bool AutoFindAvailableRouteDoors;
        public bool HoldInitialStateUntilInteraction;
    }

    private enum RewardChestState
    {
        Ready,
        NormalOpening,
        MimicDisguised,
        MimicScaring,
        MimicFleeing,
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
    [SerializeField] private string caughtTrigger;

    [Header("VFX")]
    [SerializeField] private Transform vfxAnchor;
    [SerializeField] private GameObject openVfxPrefab;
    [SerializeField] private GameObject caughtVfxPrefab;
    [SerializeField] private float chestDestroyDelay = 1f;
    [SerializeField] private bool destroyMimicOnCatch = true;
    [SerializeField] private float mimicCaughtDestroyDelay = 0.25f;

    [Header("Mimic Flee")]
    [SerializeField] private string mimicCatchInteractionPrompt = "[E] Catch Mimic";
    [SerializeField] private float scaredDelay = 0.75f;
    [SerializeField] private float fleeSeconds = 4f;
    [SerializeField] private float catchGraceSeconds = 1.5f;
    [SerializeField] private float fleeSpeed = 3.5f;
    [SerializeField] private float fleeTurnSpeed = 720f;
    [SerializeField] private float waypointSearchRadius = 8f;
    [SerializeField] private float minWaypointDistance = 3f;
    [SerializeField] private int targetSearchAttempts = 16;
    [SerializeField] private float repathInterval = 0.75f;
    [SerializeField] private float stuckSeconds = 0.5f;
    [SerializeField] private float stuckMoveThreshold = 0.05f;

    [Header("Mimic Doors")]
    [SerializeField] private float doorOpenRadius = 2.5f;
    [SerializeField] private List<GuildDoorController> routeDoorsToOpen = new List<GuildDoorController>();
    [SerializeField] private bool autoFindAvailableRouteDoors = true;

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
    private bool catchAvailable;
    private string defaultInteractionPrompt;
    private GuildDoorController[] cachedAutoRouteDoors;

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
        interactionType = InteractionType.Trigger;
        locksPlayer = false;
    }

    private void OnDisable()
    {
        StopMimicAudioAndMusic();
    }

    public void Initialize(Settings settings)
    {
        CacheComponents();
        isMimic = settings.IsMimic;
        defaultInteractionPrompt = string.IsNullOrWhiteSpace(settings.InteractionPrompt) ? "[E] Open Chest" : settings.InteractionPrompt;
        interactionPrompt = defaultInteractionPrompt;
        mimicCatchInteractionPrompt = string.IsNullOrWhiteSpace(settings.MimicCatchInteractionPrompt) ? mimicCatchInteractionPrompt : settings.MimicCatchInteractionPrompt;
        initialState = settings.InitialState;
        openTrigger = settings.OpenTrigger;
        scaredTrigger = settings.ScaredTrigger;
        runTrigger = settings.RunTrigger;
        caughtTrigger = settings.CaughtTrigger;
        scaredDelay = Mathf.Max(0f, settings.ScaredDelay);
        fleeSeconds = Mathf.Max(0.1f, settings.FleeSeconds);
        catchGraceSeconds = Mathf.Max(0f, settings.CatchGraceSeconds);
        fleeSpeed = Mathf.Max(0.1f, settings.FleeSpeed);
        waypointSearchRadius = Mathf.Max(0.5f, settings.WaypointSearchRadius);
        minWaypointDistance = Mathf.Clamp(settings.MinWaypointDistance, 0.1f, waypointSearchRadius);
        repathInterval = Mathf.Max(0.1f, settings.RepathInterval);
        stuckSeconds = Mathf.Max(0.1f, settings.StuckSeconds);
        doorOpenRadius = Mathf.Max(0.1f, settings.DoorOpenRadius);
        openVfxPrefab = settings.OpenVfxPrefab;
        caughtVfxPrefab = settings.CaughtVfxPrefab;
        chestDestroyDelay = Mathf.Max(0f, settings.ChestDestroyDelay);
        runningLoopClip = settings.RunningLoopClip;
        runningLoopVolume = Mathf.Clamp01(settings.RunningLoopVolume);
        switchMusicDuringMimicFlee = settings.SwitchMusicDuringMimicFlee;
        routeDoorsToOpen = settings.RouteDoorsToOpen ?? routeDoorsToOpen;
        autoFindAvailableRouteDoors = settings.AutoFindAvailableRouteDoors;
        holdInitialStateUntilInteraction = settings.HoldInitialStateUntilInteraction;
        interactionType = InteractionType.Trigger;
        locksPlayer = false;

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
        SpawnVfx(openVfxPrefab);
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
        OpenAvailableRouteDoors();
        StartMimicAudioAndMusic();
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

            OpenAvailableRouteDoors();
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
        SpawnVfx(caughtVfxPrefab);
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
            ReturnToDisguise();
        }
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
            float angle = i == 0 ? 0f : Random.Range(-140f, 140f);
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * away;
            float distance = Random.Range(minWaypointDistance, waypointSearchRadius);
            Vector3 candidate = transform.position + direction * distance;

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas)) continue;

            NavMeshPath path = new NavMeshPath();
            if (!navAgent.CalculatePath(hit.position, path)) continue;
            if (path.status != NavMeshPathStatus.PathComplete) continue;

            FaceDirection(hit.position - transform.position, true);
            navAgent.SetDestination(hit.position);
            return true;
        }

        return false;
    }

    private void StopMovement()
    {
        if (navAgent == null || !navAgent.enabled || !navAgent.isOnNavMesh) return;

        navAgent.ResetPath();
        navAgent.isStopped = true;
    }

    private void OpenAvailableRouteDoors()
    {
        if (routeDoorsToOpen != null)
        {
            foreach (var door in routeDoorsToOpen)
            {
                OpenDoorIfNear(door);
            }
        }

        if (!autoFindAvailableRouteDoors) return;

        if (cachedAutoRouteDoors == null)
        {
            cachedAutoRouteDoors = SceneLookup.FindAll<GuildDoorController>(true);
        }

        foreach (var door in cachedAutoRouteDoors)
        {
            OpenDoorIfNear(door);
        }
    }

    private void OpenDoorIfNear(GuildDoorController door)
    {
        if (door == null) return;

        Vector3 delta = door.transform.position - transform.position;
        delta.y = 0f;
        if (delta.sqrMagnitude > doorOpenRadius * doorOpenRadius) return;

        door.OpenForAvailableRoute();
    }

    private void StartMimicAudioAndMusic()
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

        if (!switchMusicDuringMimicFlee) return;

        if (musicManager == null)
        {
            musicManager = SceneLookup.Find<MusicManager>();
        }

        musicManager?.PlayMimicChaseMusic();
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

    private void SpawnVfx(GameObject prefab)
    {
        if (prefab == null) return;

        Transform anchor = vfxAnchor != null ? vfxAnchor : transform;
        Instantiate(prefab, anchor.position, anchor.rotation);
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

        if (vfxAnchor == null)
        {
            vfxAnchor = transform;
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
