using UnityEngine;
using UnityEngine.AI;

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
        if (animator == null || !hasTalking) return;
        animator.SetInteger(talkingIntParameter, value);
    }

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
