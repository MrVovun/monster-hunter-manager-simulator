using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Playables;
using UnityEngine.Animations;

/// <summary>
/// Shared animator driver used by any character that relies on the Relax animator controller.
/// Handles base-layer initialization, movement/talking booleans, and velocity feed.
/// </summary>
public class SharedCharacterAnimator : MonoBehaviour
{
    [Header("Parameter Names")]
    [SerializeField] private string movingBoolParameter = "Moving";
    [SerializeField] private string talkingIntParameter = "Talking";
    [SerializeField] private string animationSpeedParameter = "AnimationSpeed";
    [SerializeField] private string velocityXParameter = "Velocity X";
    [SerializeField] private string velocityZParameter = "Velocity Z";
    [SerializeField] private string weaponParameter = "Weapon";
    [SerializeField] private string triggerNumberParameter = "TriggerNumber";
    [SerializeField] private string triggerParameter = "Trigger";
    [SerializeField] private string actionIntParameter = "Action";

    [Header("Optional Clip Playback (designer friendly)")]
    [Tooltip("If true, will try to play these clips directly via Playables. Falls back to state names/parameters if missing.")]
    [SerializeField] private bool useClipPlayback = true;
    [SerializeField] private AnimationClip thinkingClip;
    [SerializeField] private float thinkingClipSpeed = 1f;
    [SerializeField] private bool thinkingClipLoop = false;

    [System.Serializable]
    public class ClipEntry
    {
        public AnimationClip clip;
        public float speed = 1f;
        public bool loop = false;
    }

    [SerializeField] private ClipEntry[] speakingClips;

    [Header("Dependencies")]
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent navAgent;

    [Tooltip("When true, the component will automatically write velocity values every Update.")]
    [SerializeField] private bool autoUpdateVelocity = true;

    private bool baseLayerInitialized;
    private bool parameterCacheBuilt;
    private bool hasMoving;
    private bool hasTalking;
    private bool hasAnimSpeed;
    private bool hasVelocityX;
    private bool hasVelocityZ;
    private bool hasWeapon;
    private bool hasTriggerNumber;
    private bool hasTrigger;
    private bool hasAction;

    // Playable-driven clip playback
    private UnityEngine.Playables.PlayableGraph dialogueGraph;

    public bool AutoUpdateVelocity
    {
        get => autoUpdateVelocity;
        set => autoUpdateVelocity = value;
    }

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        if (navAgent == null)
        {
            navAgent = GetComponentInChildren<NavMeshAgent>();
        }

        CacheParameters();
        InitializeBaseLayer();
    }

    private void OnEnable()
    {
        InitializeBaseLayer();
    }

    private void OnDisable()
    {
        StopDialogueClip();
    }

    private void OnDestroy()
    {
        StopDialogueClip();
    }

    private void Update()
    {
        if (autoUpdateVelocity)
        {
            UpdateVelocityParameters();
        }
    }

    public void SetAnimatorReference(Animator newAnimator)
    {
        animator = newAnimator;
        parameterCacheBuilt = false;
        baseLayerInitialized = false;
        if (animator == null)
        {
            Debug.LogWarning($"SharedCharacterAnimator on '{name}' received a null Animator reference.", this);
            return;
        }
        CacheParameters();
        InitializeBaseLayer();
    }

    public void SetNavAgent(NavMeshAgent agent)
    {
        navAgent = agent;
    }

    private void CacheParameters()
    {
        if (animator == null || parameterCacheBuilt) return;

        hasMoving = HasParameter(movingBoolParameter, AnimatorControllerParameterType.Bool);
        hasTalking = HasParameter(talkingIntParameter, AnimatorControllerParameterType.Int);
        hasAnimSpeed = HasParameter(animationSpeedParameter, AnimatorControllerParameterType.Float);
        hasVelocityX = HasParameter(velocityXParameter, AnimatorControllerParameterType.Float);
        hasVelocityZ = HasParameter(velocityZParameter, AnimatorControllerParameterType.Float);
        hasWeapon = HasParameter(weaponParameter, AnimatorControllerParameterType.Int);
        hasTriggerNumber = HasParameter(triggerNumberParameter, AnimatorControllerParameterType.Int);
        hasTrigger = HasParameter(triggerParameter, AnimatorControllerParameterType.Trigger);
        hasAction = HasParameter(actionIntParameter, AnimatorControllerParameterType.Int);

        parameterCacheBuilt = true;
    }

    private bool HasParameter(string name, AnimatorControllerParameterType type)
    {
        if (animator == null || string.IsNullOrEmpty(name)) return false;
        foreach (var param in animator.parameters)
        {
            if (param.type == type && param.name == name)
            {
                return true;
            }
        }
        return false;
    }

    private void InitializeBaseLayer()
    {
        if (animator == null || baseLayerInitialized) return;
        CacheParameters();

        if (hasWeapon)
        {
            animator.SetInteger(weaponParameter, -1);
        }

        if (hasTriggerNumber)
        {
            animator.SetInteger(triggerNumberParameter, 25);
        }

        if (hasTrigger)
        {
            animator.SetTrigger(triggerParameter);
        }

        baseLayerInitialized = true;
    }

    public void SetMoving(bool moving)
    {
        if (animator == null) return;

        if (hasMoving)
        {
            animator.SetBool(movingBoolParameter, moving);
        }

        if (hasTalking)
        {
            animator.SetInteger(talkingIntParameter, 0);
        }
    }

    public void PlaySitSequence()
    {
        if (animator == null) return;
        CacheParameters();

        if (hasTriggerNumber)
        {
            animator.SetInteger(triggerNumberParameter, 2);
        }
        if (hasTalking)
        {
            animator.SetInteger(talkingIntParameter, 0);
        }
        if (hasMoving)
        {
            animator.SetBool(movingBoolParameter, false);
        }
        if (hasTrigger)
        {
            animator.SetTrigger(triggerParameter);
        }
    }

    public void SetAnimationSpeed(float value)
    {
        if (animator == null || !hasAnimSpeed) return;
        animator.SetFloat(animationSpeedParameter, value);
    }

    public void SetTalkingValue(int value)
    {
        if (animator == null || !hasTalking)
        {
            if (animator != null)
            {
                Debug.LogWarning($"SharedCharacterAnimator: Animator '{animator.runtimeAnimatorController?.name}' has no int parameter named '{talkingIntParameter}'.", this);
            }
            return;
        }
        animator.SetInteger(talkingIntParameter, value);
    }

    public void SetActionValue(int value)
    {
        if (animator == null || !hasAction)
        {
            if (animator != null)
            {
                Debug.LogWarning($"SharedCharacterAnimator: Animator '{animator.runtimeAnimatorController?.name}' has no int parameter named '{actionIntParameter}'.", this);
            }
            return;
        }
        animator.SetInteger(actionIntParameter, value);
    }

    public bool HasTalkingParameter() => hasTalking;
    public bool HasActionParameter() => hasAction;
    public string GetControllerName() => animator != null ? animator.runtimeAnimatorController?.name : "<none>";

    /// <summary>
    /// Crossfades to a named state if provided. Returns true if a play was issued.
    /// </summary>
    #region Clip Playback
    private void StopDialogueClip()
    {
        if (dialogueGraph.IsValid())
        {
            dialogueGraph.Destroy();
        }
    }

    private bool PlayClip(AnimationClip clip, float duration = -1f, float speedOverride = 1f, bool loop = false)
    {
        if (clip == null || animator == null) return false;
        StopDialogueClip();

        dialogueGraph = PlayableGraph.Create($"DialogueClipGraph_{name}");
        var output = AnimationPlayableOutput.Create(dialogueGraph, "DialogueOutput", animator);
        var playable = AnimationClipPlayable.Create(dialogueGraph, clip);
        playable.SetApplyFootIK(false);
        playable.SetApplyPlayableIK(false);

        float targetDuration = duration > 0f ? duration : clip.length;
        float baseSpeed = Mathf.Approximately(targetDuration, 0f) ? 1f : clip.length / targetDuration;

        playable.SetDuration(loop ? double.PositiveInfinity : clip.length);
        playable.SetTime(0);
        playable.SetSpeed(baseSpeed * Mathf.Max(0.01f, speedOverride));

        output.SetSourcePlayable(playable);
        dialogueGraph.Play();
        if (!loop)
        {
            StartCoroutine(StopDialogueClipAfter(targetDuration / Mathf.Max(0.01f, (float)playable.GetSpeed())));
        }
        return true;
    }

    private System.Collections.IEnumerator StopDialogueClipAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        StopDialogueClip();
    }

    public bool PlayThinkingClip(float duration = -1f)
    {
        if (!useClipPlayback) return false;
        return PlayClip(thinkingClip, duration, thinkingClipSpeed, thinkingClipLoop);
    }

    public bool PlayRandomSpeakingClip()
    {
        if (!useClipPlayback || speakingClips == null || speakingClips.Length == 0) return false;
        int idx = Random.Range(0, speakingClips.Length);
        var entry = speakingClips[idx];
        if (entry == null || entry.clip == null) return false;
        return PlayClip(entry.clip, -1f, entry.speed <= 0f ? 1f : entry.speed, entry.loop);
    }
    #endregion

    public void ManualVelocityUpdate(Vector3 worldVelocity, Transform reference)
    {
        if (animator == null) return;
        if (!hasVelocityX && !hasVelocityZ) return;

        Vector3 localVel = reference != null ? reference.InverseTransformDirection(worldVelocity) : worldVelocity;
        float maxSpeed = Mathf.Max(0.01f, worldVelocity.magnitude);
        float normX = Mathf.Clamp(localVel.x / maxSpeed, -1f, 1f);
        float normZ = Mathf.Clamp(localVel.z / maxSpeed, -1f, 1f);

        if (hasVelocityX)
        {
            animator.SetFloat(velocityXParameter, normX);
        }
        if (hasVelocityZ)
        {
            animator.SetFloat(velocityZParameter, normZ);
        }
    }

    private void UpdateVelocityParameters()
    {
        if (navAgent == null || animator == null) return;
        if (!hasVelocityX && !hasVelocityZ) return;

        Vector3 localVel = transform.InverseTransformDirection(navAgent.velocity);
        float maxSpeed = Mathf.Max(0.01f, navAgent.speed);
        float normX = Mathf.Clamp(localVel.x / maxSpeed, -1f, 1f);
        float normZ = Mathf.Clamp(localVel.z / maxSpeed, -1f, 1f);

        if (hasVelocityX) animator.SetFloat(velocityXParameter, normX);
        if (hasVelocityZ) animator.SetFloat(velocityZParameter, normZ);
    }
}
