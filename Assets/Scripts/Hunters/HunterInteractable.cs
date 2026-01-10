using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class HunterInteractable : Interactable
{
    [SerializeField] private HunterRecruitmentManager recruitmentManager;
    [SerializeField] private InvestigationManager investigationManager;
    [SerializeField] private Camera dialogueCameraOverride;
    [SerializeField] private float cameraTransitionDuration = 0.5f;
    [SerializeField] private bool debugLogs = false;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 1.6f, -1.8f);
    [SerializeField] private Vector3 cameraLookOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private GameObject healVfx;

    private PlayerInteraction activePlayer;
    private Hunter ownerHunter;
    private HunterInteractionState interactionState;
    private bool awaitingRelease;
    private InvestigationCase tempCase;
    private NavMeshAgent pausedAgent;
    private bool navWasStopped;
    private bool navPaused;
    private Quaternion originalHunterRotation;
    private bool healPending;
    private string pendingHealLine;
    private float pendingHealDuration;

    private void Reset()
    {
        interactionPrompt = "[E] Speak";
        interactionType = InteractionType.Trigger;
        locksPlayer = true;
        useCustomCamera = false;
    }

    public void Initialize(HunterRecruitmentManager manager, HunterRecruitmentManager.RecruitmentCandidate candidate, Camera overrideCamera)
    {
        recruitmentManager = manager;
        ownerHunter = candidate != null ? candidate.spawnedHunter : null;
        dialogueCameraOverride = overrideCamera;
        locksPlayer = true;
        useCustomCamera = false;
    }

    public void Initialize(Hunter hunter)
    {
        ownerHunter = hunter;
    }

    public override void Interact(PlayerInteraction player)
    {
        if (awaitingRelease) return;

        ResolveManagers();
        if (ownerHunter == null)
        {
            ownerHunter = GetComponent<Hunter>();
        }
        if (ownerHunter == null || investigationManager == null)
        {
            if (debugLogs) Debug.LogWarning("HunterInteractable: Missing owner or InvestigationManager.");
            return;
        }

        if (ownerHunter.GetState() == HunterState.OnMission || ownerHunter.GetState() == HunterState.Dead)
        {
            return;
        }

        interactionState = ownerHunter.GetComponent<HunterInteractionState>();
        if (interactionState == null)
        {
            interactionState = ownerHunter.gameObject.AddComponent<HunterInteractionState>();
        }

        OnInteractionStart(player);
        activePlayer = player;
        awaitingRelease = true;
        PauseMovementIfNeeded();
        CacheHunterRotation();
        AimAtHunter(player);
        FacePlayer();

        BuildTempCaseAndShowDialogue();
    }

    private void BuildTempCaseAndShowDialogue()
    {
        tempCase = new InvestigationCase();
        tempCase.truthMonster = null;
        tempCase.clientProfile = null;
        tempCase.truthTraits = new List<MonsterTrait>();

        // Build a pseudo-question list from hunter data
        var hunterData = ownerHunter != null ? ownerHunter.Data : null;
        List<InvestigationQuestion> questionList = new List<InvestigationQuestion>();
        Dictionary<string, string> answers = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        if (hunterData != null && hunterData.dialogueQuestions != null)
        {
            foreach (var hq in hunterData.dialogueQuestions)
            {
                if (hq == null) continue;
                var q = ScriptableObject.CreateInstance<InvestigationQuestion>();
                q.questionId = hq.questionId;
                q.promptText = hq.questionText;
                answers[q.questionId] = hq.answerText;
                questionList.Add(q);
            }
        }

        // Heal entry
        bool canHeal = interactionState != null && interactionState.IsWounded && !interactionState.IsHealing;
        InvestigationQuestion healQuestion = null;
        if (canHeal)
        {
            healQuestion = ScriptableObject.CreateInstance<InvestigationQuestion>();
            healQuestion.questionId = "heal";
            healQuestion.promptText = "Heal wounds";
            answers[healQuestion.questionId] = hunterData != null ? hunterData.healLine : "Hold still while we patch you up.";
            questionList.Add(healQuestion);
        }

        // Goodbye entry
        var goodbye = ScriptableObject.CreateInstance<InvestigationQuestion>();
        goodbye.questionId = "goodbye";
        string goodbyeLine = hunterData != null && !string.IsNullOrWhiteSpace(hunterData.goodbyeLine)
            ? hunterData.goodbyeLine
            : "That's all, thanks.";
        goodbye.promptText = goodbyeLine;
        answers[goodbye.questionId] = goodbyeLine;
        questionList.Add(goodbye);

        ConfigureDialogueCameraTarget();
        investigationManager.BeginHunterDialogue(questionList, answers, ownerHunter, null, cameraTransitionDuration, HandleQuestionSelected, ReleaseInteraction, useDialogueCamera: false, onResponseFinished: HandleResponseFinished);
    }

    private void ConfigureDialogueCameraTarget()
    {
        if (investigationManager == null || ownerHunter == null) return;
        Vector3 forward = ownerHunter.transform.forward;
        Vector3 targetPos = ownerHunter.transform.position - forward * Mathf.Abs(cameraOffset.z) + Vector3.up * cameraOffset.y;
        Vector3 lookTarget = ownerHunter.transform.position + Vector3.up * cameraLookOffset.y;
        Quaternion targetRot = Quaternion.LookRotation((lookTarget - targetPos).normalized, Vector3.up);
        investigationManager.SetDialogueCameraHome(targetPos, targetRot);
    }

    private void HandleQuestionSelected(InvestigationQuestion question)
    {
        if (question == null) return;
        if (question.questionId == "heal")
        {
            PrepareHeal();
        }
        else if (question.questionId == "goodbye")
        {
            investigationManager?.CompleteInvestigation();
        }
    }

    private void PrepareHeal()
    {
        if (interactionState == null) return;
        float duration = GetHealDuration();
        pendingHealDuration = duration;
        pendingHealLine = ownerHunter != null && ownerHunter.Data != null && !string.IsNullOrWhiteSpace(ownerHunter.Data.healLine)
            ? ownerHunter.Data.healLine
            : "Hold still while we patch you up.";
        healPending = true;
    }

    private void HandleResponseFinished(InvestigationQuestion question, string responseText)
    {
        if (question == null) return;
        if (question.questionId == "heal" && healPending)
        {
            StartCoroutine(HealRoutine());
        }
    }

    private System.Collections.IEnumerator HealRoutine()
    {
        healPending = false;
        interactionState?.StartHealing(pendingHealDuration);
        SetHealVfxActive(true);
        investigationManager?.HideDialoguePanel();
        float wait = Mathf.Max(0f, pendingHealDuration);
        float elapsed = 0f;
        while (elapsed < wait)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        SetHealVfxActive(false);
        interactionState?.SetWounded(false);
        if (investigationManager != null)
        {
            investigationManager.RemoveHunterQuestion("heal");
        }
        if (investigationManager != null)
        {
            investigationManager.ShowDialogueResponse(pendingHealLine, refreshQuestions: true);
        }
    }

    private float GetHealDuration()
    {
        float duration = 10f;
        var config = GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null;
        if (config != null)
        {
            duration = Mathf.Max(0.5f, config.hunterHealDurationSeconds);
        }
        // Trait modifier: fast healer halves time
        var data = ownerHunter != null ? ownerHunter.Data : null;
        if (data != null && data.traits != null)
        {
            foreach (var trait in data.traits)
            {
                if (trait != null && string.Equals(trait.traitId, "fast_healer", System.StringComparison.OrdinalIgnoreCase))
                {
                    duration *= 0.5f;
                    break;
                }
            }
        }
        return duration;
    }

    private void ReleaseInteraction()
    {
        if (!awaitingRelease) return;
        awaitingRelease = false;
        if (activePlayer != null)
        {
            OnInteractionEnd(activePlayer);
            activePlayer = null;
        }
        ResumeMovementIfNeeded();
        RestoreHunterRotation();
        SetHealVfxActive(false);
    }

    private void ResolveManagers()
    {
        if (investigationManager == null && GameManager.Instance != null)
        {
            investigationManager = GameManager.Instance.GetInvestigationManager();
        }
        if (recruitmentManager == null)
        {
            recruitmentManager = FindObjectOfType<HunterRecruitmentManager>();
        }
    }

    private void PauseMovementIfNeeded()
    {
        if (ownerHunter == null) return;
        var agent = ownerHunter.GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            pausedAgent = agent;
            navWasStopped = agent.isStopped;
            if (!agent.isStopped)
            {
                agent.isStopped = true;
                navPaused = true;
            }
        }
    }

    private void ResumeMovementIfNeeded()
    {
        if (pausedAgent == null) return;
        if (pausedAgent.enabled && pausedAgent.isOnNavMesh && navPaused)
        {
            pausedAgent.isStopped = navWasStopped;
        }
        pausedAgent = null;
        navPaused = false;
        navWasStopped = false;
    }

    private void CacheHunterRotation()
    {
        if (ownerHunter != null)
        {
            originalHunterRotation = ownerHunter.transform.rotation;
        }
    }

    private void RestoreHunterRotation()
    {
        if (ownerHunter != null && originalHunterRotation != Quaternion.identity)
        {
            ownerHunter.transform.rotation = originalHunterRotation;
        }
    }

    private void FacePlayer()
    {
        if (ownerHunter == null || activePlayer == null) return;
        var playerCam = activePlayer.GetPlayerCamera();
        if (playerCam == null) return;
        Vector3 direction = playerCam.transform.position - ownerHunter.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;
        Quaternion desired = Quaternion.LookRotation(direction.normalized, Vector3.up);
        ownerHunter.transform.rotation = Quaternion.RotateTowards(ownerHunter.transform.rotation, desired, 45f);
    }

    private void AimAtHunter(PlayerInteraction player)
    {
        if (player == null) return;
        var cam = player.GetPlayerCamera();
        if (cam == null) return;
        Vector3 target = ownerHunter != null
            ? ownerHunter.transform.position + Vector3.up * cameraLookOffset.y
            : cam.transform.position + cam.transform.forward;
        Vector3 dir = (target - cam.transform.position).normalized;
        cam.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }

    private void SetHealVfxActive(bool value)
    {
        if (healVfx != null)
        {
            healVfx.SetActive(value);
        }
    }

}
