using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class ClientSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform waitingSpot;
    [SerializeField] private float approachThreshold = 0.25f;

    [Header("Prefabs")]
    [SerializeField] private ClientCharacter clientPrefab;

    private ClientCharacter activeClient;
    private ClientProfile activeProfile;
    private NavMeshAgent clientAgent;
    private SharedCharacterAnimator animatorController;
    private InvestigationManager investigationManager;

    public bool HasActiveClient => activeClient != null;

    public void SpawnClientForCase(InvestigationCase investigationCase)
    {
        if (investigationCase == null) return;
        if (investigationManager == null && GameManager.Instance != null)
        {
            investigationManager = GameManager.Instance.GetInvestigationManager();
        }
        if (activeClient != null)
        {
            DespawnCurrentClient();
        }

        activeProfile = investigationCase.clientProfile;
        GameObject prefab = activeProfile != null ? activeProfile.GetNextVisualPrefab() : null;
        if (prefab == null)
        {
            Debug.LogWarning("ClientSpawner: No visual prefab assigned for client profile.");
            return;
        }

        if (clientPrefab == null)
        {
            Debug.LogWarning("ClientSpawner: No client prefab assigned.");
            return;
        }

        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion spawnRot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        activeClient = Instantiate(clientPrefab, spawnPos, spawnRot);
        activeClient.SetVisualPrefab(prefab);
        activeClient.AssignInvestigation(investigationManager, investigationCase);
        clientAgent = activeClient.Agent;
        animatorController = activeClient.AnimatorController;

        if (clientAgent == null)
        {
            clientAgent = activeClient.gameObject.AddComponent<NavMeshAgent>();
        }
        clientAgent.enabled = true;
        clientAgent.ResetPath();
        clientAgent.isStopped = false;
        clientAgent.stoppingDistance = approachThreshold;

        animatorController?.SetNavAgent(clientAgent);
        animatorController?.SetAnimationSpeed(1f);
        animatorController?.SetMoving(true);
        MoveTowardsWaitingSpot();
    }

    private void MoveTowardsWaitingSpot()
    {
        if (clientAgent == null) return;

        Vector3 target = waitingSpot != null ? waitingSpot.position : transform.position;
        clientAgent.SetDestination(target);
        StartCoroutine(WaitForArrival());
    }

    private IEnumerator WaitForArrival()
    {
        while (clientAgent != null && clientAgent.enabled)
        {
            if (!clientAgent.pathPending && clientAgent.remainingDistance <= approachThreshold)
            {
                break;
            }
            yield return null;
        }

        if (clientAgent != null)
        {
            clientAgent.isStopped = true;
        }

        if (waitingSpot != null)
        {
            activeClient.transform.position = waitingSpot.position;
            activeClient.transform.rotation = waitingSpot.rotation;
        }

        animatorController?.SetMoving(false);
    }

    public void DespawnCurrentClient()
    {
        if (activeClient != null)
        {
            activeClient.Cleanup();
            Destroy(activeClient.gameObject);
            activeClient = null;
        }
        clientAgent = null;
        animatorController = null;
        activeProfile = null;
    }

    private void OnDisable()
    {
        DespawnCurrentClient();
    }
}
