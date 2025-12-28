using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Hunter))]
public class HunterCandidateController : MonoBehaviour
{
    [SerializeField] private float arrivalThreshold = 0.15f;

    private NavMeshAgent agent;
    private HunterRecruitmentManager recruitmentManager;
    private HunterRecruitmentManager.RecruitmentCandidate candidate;
    private Transform waitSpot;
    private Transform exitPoint;
    private bool leaving;

    public void Initialize(
        HunterRecruitmentManager manager,
        HunterRecruitmentManager.RecruitmentCandidate linkedCandidate,
        Transform spawnPoint,
        Transform waitSpot,
        Transform exitPoint)
    {
        recruitmentManager = manager;
        candidate = linkedCandidate;
        this.waitSpot = waitSpot;
        this.exitPoint = exitPoint;
        agent = GetComponent<NavMeshAgent>();

        if (spawnPoint != null)
        {
            transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
            if (agent != null && agent.enabled)
            {
                agent.Warp(spawnPoint.position);
            }
        }

        MoveToWaitSpot();
    }

    public void MoveToWaitSpot()
    {
        leaving = false;
        SetDestination(waitSpot != null ? waitSpot.position : transform.position);
    }

    public void LeaveGuild()
    {
        leaving = true;
        if (exitPoint == null)
        {
            recruitmentManager?.HandleCandidateExited(candidate);
            return;
        }
        SetDestination(exitPoint.position);
    }

    public void CancelNavigation()
    {
        if (agent != null && agent.enabled)
        {
            agent.ResetPath();
            agent.isStopped = true;
        }
    }

    private void SetDestination(Vector3 destination)
    {
        if (agent == null || !agent.enabled)
        {
            return;
        }

        agent.isStopped = false;
        agent.SetDestination(destination);
    }

    private void Update()
    {
        if (agent == null || !agent.enabled || agent.pathPending)
        {
            return;
        }

        if (!leaving && waitSpot != null && agent.remainingDistance <= arrivalThreshold)
        {
            agent.isStopped = true;
            transform.position = waitSpot.position;
            transform.rotation = waitSpot.rotation;
        }
        else if (leaving && (exitPoint == null || agent.remainingDistance <= arrivalThreshold))
        {
            recruitmentManager?.HandleCandidateExited(candidate);
        }
    }
}
