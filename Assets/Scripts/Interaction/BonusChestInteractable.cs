using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class BonusChestInteractable : Interactable
{
    [System.Serializable]
    public struct Settings
    {
        public bool IsMimic;
        public string InteractionPrompt;
        public string InitialState;
        public string OpenTrigger;
        public string ScaredTrigger;
        public string RunTrigger;
        public float ScaredDelay;
        public float FleeSeconds;
        public float FleeDistance;
        public float FleeSpeed;
        public bool HoldInitialStateUntilInteraction;
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

    [Header("Mimic Flee")]
    [SerializeField] private float scaredDelay = 0.75f;
    [SerializeField] private float fleeSeconds = 4f;
    [SerializeField] private float fleeDistance = 8f;
    [SerializeField] private float fleeSpeed = 3.5f;
    [SerializeField] private float fleeTurnSpeed = 720f;

    private bool interacted;

    private void Reset()
    {
        interactionPrompt = "[E] Open Chest";
        interactionType = InteractionType.Trigger;
        locksPlayer = false;
    }

    private void Awake()
    {
        CacheComponents();
        interactionType = InteractionType.Trigger;
        locksPlayer = false;
    }

    public void Initialize(Settings settings)
    {
        CacheComponents();
        isMimic = settings.IsMimic;
        interactionPrompt = string.IsNullOrWhiteSpace(settings.InteractionPrompt) ? "[E] Open Chest" : settings.InteractionPrompt;
        initialState = settings.InitialState;
        openTrigger = settings.OpenTrigger;
        scaredTrigger = settings.ScaredTrigger;
        runTrigger = settings.RunTrigger;
        scaredDelay = Mathf.Max(0f, settings.ScaredDelay);
        fleeSeconds = Mathf.Max(0.1f, settings.FleeSeconds);
        fleeDistance = Mathf.Max(0f, settings.FleeDistance);
        fleeSpeed = Mathf.Max(0.1f, settings.FleeSpeed);
        holdInitialStateUntilInteraction = settings.HoldInitialStateUntilInteraction;
        interactionType = InteractionType.Trigger;
        locksPlayer = false;

        if (isMimic)
        {
            PlayStateImmediate(initialState);
        }
    }

    private void Update()
    {
        if (!isMimic || interacted || !holdInitialStateUntilInteraction) return;
        HoldInitialState();
    }

    public override bool IsInteractionAvailable()
    {
        return !interacted && base.IsInteractionAvailable();
    }

    public override void Interact(PlayerInteraction player)
    {
        if (interacted) return;
        interacted = true;

        OnInteractionStart(player);

        if (isMimic)
        {
            StartCoroutine(PlayMimicRoutine(player));
        }
        else
        {
            PlayAnimation(openTrigger);
            OnInteractionEnd(player);
        }
    }

    private IEnumerator PlayMimicRoutine(PlayerInteraction player)
    {
        PlayAnimation(scaredTrigger);

        if (scaredDelay > 0f)
        {
            yield return new WaitForSeconds(scaredDelay);
        }

        PlayAnimation(runTrigger);
        FleeFrom(player != null ? player.transform : null);
        OnInteractionEnd(player);
    }

    private void FleeFrom(Transform playerTransform)
    {
        Vector3 away = playerTransform != null
            ? transform.position - playerTransform.position
            : transform.forward;

        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)
        {
            away = -transform.forward;
        }
        away.Normalize();

        Vector3 target = transform.position + away * fleeDistance;

        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.speed = fleeSpeed;
            navAgent.updateRotation = true;
            navAgent.angularSpeed = Mathf.Max(navAgent.angularSpeed, fleeTurnSpeed);
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, fleeDistance, NavMesh.AllAreas))
            {
                FaceDirection(hit.position - transform.position, true);
                navAgent.SetDestination(hit.position);
                return;
            }
        }

        StartCoroutine(TranslateFleeRoutine(target));
    }

    private IEnumerator TranslateFleeRoutine(Vector3 target)
    {
        Vector3 start = transform.position;
        float elapsed = 0f;

        while (elapsed < fleeSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fleeSeconds);
            Vector3 nextPosition = Vector3.Lerp(start, target, t);
            FaceDirection(nextPosition - transform.position, false);
            transform.position = nextPosition;
            yield return null;
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
