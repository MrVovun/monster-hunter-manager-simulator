using UnityEngine;
using UnityEngine.AI;

public class ClientCharacter : MonoBehaviour
{
    [SerializeField] private Transform visualParent;
    [SerializeField] private NavMeshAgent navAgent;
    [SerializeField] private SharedCharacterAnimator sharedAnimator;
    [SerializeField] private ClientInteractable interactable;

    private GameObject visualInstance;

    public NavMeshAgent Agent => navAgent;
    public SharedCharacterAnimator AnimatorController => sharedAnimator;
    public ClientInteractable Interactable => interactable;

    private void Awake()
    {
        if (navAgent == null)
        {
            navAgent = GetComponentInChildren<NavMeshAgent>();
        }

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
        sharedAnimator.SetAnimationSpeed(1f);
    }

    public void SetVisualPrefab(GameObject prefab)
    {
        SetVisual(prefab, null);
    }

    public void SetVisualPreset(P09HumanoidPreset preset)
    {
        GameObject prefab = preset != null ? preset.baseVisualPrefab : null;
        SetVisual(prefab, preset);
    }

    private void SetVisual(GameObject prefab, P09HumanoidPreset p09Preset)
    {
        if (visualInstance != null)
        {
            Destroy(visualInstance);
            visualInstance = null;
        }

        if (prefab == null) return;

        Transform parent = visualParent != null ? visualParent : transform;
        visualInstance = Instantiate(prefab, parent);
        visualInstance.transform.localPosition = Vector3.zero;
        visualInstance.transform.localRotation = Quaternion.identity;
        visualInstance.transform.localScale = Vector3.one;

        Animator animator;
        if (p09Preset != null)
        {
            var applier = visualInstance.GetComponent<P09HumanoidVisualApplier>();
            if (applier == null)
            {
                applier = visualInstance.AddComponent<P09HumanoidVisualApplier>();
            }

            applier.ApplyPreset(p09Preset);
            animator = applier.Animator != null ? applier.Animator : visualInstance.GetComponentInChildren<Animator>();
        }
        else
        {
            animator = visualInstance.GetComponentInChildren<Animator>();
        }

        sharedAnimator.SetAnimatorReference(animator);
        sharedAnimator.SetAnimationSpeed(1f);
    }

    public void AssignInvestigation(InvestigationManager manager, InvestigationCase caseData)
    {
        if (interactable != null)
        {
            interactable.Initialize(manager, caseData);
        }
    }

    public void DisableInteraction()
    {
        if (interactable != null)
        {
            interactable.DisableInteraction();
        }
    }

    public void SetInteractionEnabled(bool enabled)
    {
        if (interactable != null)
        {
            interactable.SetInteractionEnabled(enabled);
        }
    }

    public void Cleanup()
    {
        if (visualInstance != null)
        {
            Destroy(visualInstance);
            visualInstance = null;
        }

        if (interactable != null)
        {
            interactable.Clear();
        }
    }
}
