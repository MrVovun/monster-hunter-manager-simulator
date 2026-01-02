using System.Collections.Generic;
using UnityEngine;

public class HunterInteractable : Interactable
{
    [SerializeField] private HunterRecruitmentManager recruitmentManager;
    [SerializeField] private InvestigationManager investigationManager;
    [SerializeField] private Camera dialogueCameraOverride;
    [SerializeField] private float cameraTransitionDuration = 0.5f;
    [SerializeField] private bool debugLogs = false;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 1.6f, -1.8f);
    [SerializeField] private Vector3 cameraLookOffset = new Vector3(0f, 1.5f, 0f);

    private PlayerInteraction activePlayer;
    private Hunter ownerHunter;
    private HunterInteractionState interactionState;
    private bool awaitingRelease;
    private InvestigationCase tempCase;

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

        ConfigureDialogueCameraTarget();
        investigationManager.BeginHunterDialogue(questionList, answers, ownerHunter, dialogueCameraOverride, cameraTransitionDuration, HandleQuestionSelected, ReleaseInteraction);
    }

    private void ConfigureDialogueCameraTarget()
    {
        if (investigationManager == null || ownerHunter == null) return;
        Vector3 targetPos = ownerHunter.transform.TransformPoint(cameraOffset);
        Vector3 lookTarget = ownerHunter.transform.position + ownerHunter.transform.TransformDirection(cameraLookOffset);
        Quaternion targetRot = Quaternion.LookRotation((lookTarget - targetPos).normalized, Vector3.up);
        investigationManager.SetDialogueCameraHome(targetPos, targetRot);
    }

    private void HandleQuestionSelected(InvestigationQuestion question)
    {
        if (question == null) return;
        if (question.questionId == "heal")
        {
            StartHeal();
        }
    }

    private void StartHeal()
    {
        if (interactionState == null) return;
        float duration = GetHealDuration();
        interactionState.StartHealing(duration);
        // Notify UI to show progress / hide dialogue
        investigationManager?.BeginHunterHeal(ownerHunter, duration, ReleaseInteraction);
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
}
