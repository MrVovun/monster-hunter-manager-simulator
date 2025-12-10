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

        var animator = visualInstance.GetComponentInChildren<Animator>();
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
