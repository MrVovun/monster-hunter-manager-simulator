using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class ClientSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform waitingSpot;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private float approachThreshold = 0.25f;

    [Header("Prefabs")]
    [SerializeField] private ClientCharacter clientPrefab;

    private ClientCharacter activeClient;
    private ClientProfile activeProfile;
    private NavMeshAgent clientAgent;
    private SharedCharacterAnimator animatorController;
    private InvestigationManager investigationManager;
    private Coroutine arrivalRoutine;
    private Coroutine departureRoutine;

    public bool HasActiveClient => activeClient != null;

    /// <summary>
    /// Returns the currently spawned client's shared animator (if any).
    /// Used by systems that want to drive facial/talking animations during dialogue.
    /// </summary>
    public SharedCharacterAnimator GetActiveAnimator()
    {
        return animatorController;
    }

    public NavMeshAgent GetActiveClientAgent()
    {
        return clientAgent;
    }

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

        if (arrivalRoutine != null)
        {
            StopCoroutine(arrivalRoutine);
        }
        arrivalRoutine = StartCoroutine(WaitForArrival());
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
        arrivalRoutine = null;
    }

    public void DespawnCurrentClient()
    {
        if (arrivalRoutine != null)
        {
            StopCoroutine(arrivalRoutine);
            arrivalRoutine = null;
        }

        if (departureRoutine != null)
        {
            StopCoroutine(departureRoutine);
            departureRoutine = null;
        }

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

    public void DismissCurrentClient(Action onComplete = null)
    {
        if (activeClient == null)
        {
            onComplete?.Invoke();
            return;
        }

        activeClient.DisableInteraction();

        if (arrivalRoutine != null)
        {
            StopCoroutine(arrivalRoutine);
            arrivalRoutine = null;
        }

        if (clientAgent == null || !clientAgent.enabled)
        {
            DespawnCurrentClient();
            onComplete?.Invoke();
            return;
        }

        Vector3 targetPos = GetExitPosition();
        Quaternion targetRot = GetExitRotation();

        clientAgent.isStopped = false;
        clientAgent.ResetPath();
        clientAgent.stoppingDistance = approachThreshold;
        clientAgent.SetDestination(targetPos);
        animatorController?.SetMoving(true);

        if (departureRoutine != null)
        {
            StopCoroutine(departureRoutine);
        }
        departureRoutine = StartCoroutine(WaitForDeparture(targetPos, targetRot, onComplete));
    }

    private IEnumerator WaitForDeparture(Vector3 targetPos, Quaternion targetRot, Action onComplete)
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

        if (activeClient != null)
        {
            activeClient.transform.position = targetPos;
            activeClient.transform.rotation = targetRot;
        }

        animatorController?.SetMoving(false);
        departureRoutine = null;
        DespawnCurrentClient();
        onComplete?.Invoke();
    }

    private Vector3 GetExitPosition()
    {
        if (exitPoint != null) return exitPoint.position;
        if (spawnPoint != null) return spawnPoint.position;
        return transform.position;
    }

    private Quaternion GetExitRotation()
    {
        if (exitPoint != null) return exitPoint.rotation;
        if (spawnPoint != null) return spawnPoint.rotation;
        return transform.rotation;
    }

    private void OnDisable()
    {
        DespawnCurrentClient();
    }
}
