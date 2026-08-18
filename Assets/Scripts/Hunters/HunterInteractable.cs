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
    [SerializeField] private bool focusDialogueCameraOnFace = true;
    [SerializeField] private Transform faceFocusOverride;
    [Tooltip("How far the dialogue camera sits from the face, measured from the player's current side of the hunter.")]
    [SerializeField] private float faceCameraDistance = 1.8f;
    [Tooltip("Vertical offset from the face point to the dialogue camera. Usually small, because the face is already the target.")]
    [SerializeField] private float faceCameraHeightOffset = -0.05f;
    [Tooltip("Small target offset applied to the face point the camera looks at.")]
    [SerializeField] private Vector3 faceLookOffset = new Vector3(0f, 0.02f, 0f);
    [SerializeField] private bool hideNotificationsDuringDialogue = true;
    [SerializeField] private GameObject healVfx;
    [Header("Morning Flavor")]
    [SerializeField] private bool injectMorningDialogueOption = true;
    [SerializeField] private string morningDialogueQuestionId = "morning_whats_new";
    [SerializeField] private string morningDialogueQuestionText = "What's new?";
    [SerializeField] private string repeatedMorningDialoguePrefix = "As I said earlier,";
    [Header("Card Game")]
    [SerializeField] private CardGameUI cardGameUI;
    [SerializeField] private bool injectCardGameDialogueOption = true;
    [SerializeField] private string cardGameQuestionId = "play_cards";
    [SerializeField] private string cardGameQuestionText = "You want to play some cards?";
    [TextArea(2, 4)]
    [SerializeField] private string cardGameAnswerText = "Sure. Let's play.";

    private PlayerInteraction activePlayer;
    private Hunter ownerHunter;
    private HunterInteractionState interactionState;
    private bool awaitingRelease;
    private InvestigationCase tempCase;
    private NavMeshAgent pausedAgent;
    private bool navWasStopped;
    private bool navPaused;
    private Quaternion originalHunterRotation;
    private bool interactionDisabled;

    protected override bool HideNotificationFeedDuringInteraction => hideNotificationsDuringDialogue || base.HideNotificationFeedDuringInteraction;

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

    public void SetInteractionEnabled(bool enabled)
    {
        interactionDisabled = !enabled;
        if (!enabled)
        {
            ReleaseInteraction();
        }
    }

    public void Initialize(Hunter hunter)
    {
        ownerHunter = hunter;
    }

    public override bool IsInteractionAvailable()
    {
        if (!base.IsInteractionAvailable() || interactionDisabled || awaitingRelease)
        {
            return false;
        }

        if (ownerHunter == null)
        {
            ownerHunter = GetComponent<Hunter>();
        }

        if (ownerHunter == null)
        {
            return true;
        }

        var state = ownerHunter.GetState();
        return state != HunterState.OnMission && state != HunterState.Dead && state != HunterState.Healing && state != HunterState.Sleeping && state != HunterState.Armory;
    }

    public override bool TryGetUnavailableReason(out string reason)
    {
        if (base.TryGetUnavailableReason(out reason)) return true;

        if (interactionDisabled)
        {
            reason = "They are not ready to talk yet.";
            return true;
        }

        if (awaitingRelease)
        {
            reason = "You are already talking to them.";
            return true;
        }

        if (ownerHunter == null)
        {
            ownerHunter = GetComponent<Hunter>();
        }

        if (ownerHunter == null) return false;

        switch (ownerHunter.GetState())
        {
            case HunterState.OnMission:
                reason = "They are away on an order.";
                return true;
            case HunterState.Dead:
                reason = "They are dead.";
                return true;
            case HunterState.Healing:
                reason = "They are being treated.";
                return true;
            case HunterState.Sleeping:
                reason = "They are sleeping.";
                return true;
            case HunterState.Armory:
                reason = "They are using the armory.";
                return true;
        }

        return false;
    }

    public override void Interact(PlayerInteraction player)
    {
        if (interactionDisabled) return;
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

        var state = ownerHunter.GetState();
        if (state == HunterState.OnMission || state == HunterState.Dead || state == HunterState.Healing || state == HunterState.Sleeping || state == HunterState.Armory)
        {
            return;
        }

        // If this hunter is a recruitment candidate, show the hiring panel instead of dialogue.
        if (recruitmentManager != null && recruitmentManager.ShowCandidateProfile(ownerHunter, ReleaseInteraction, onlyIfPending: true))
        {
            OnInteractionStart(player);
            activePlayer = player;
            awaitingRelease = true;
            PauseMovementIfNeeded();
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
        TutorialManager.ReportEvent(TutorialIds.EventHunterDialogueOpened);
    }

    public override string GetTutorialActionId()
    {
        return string.IsNullOrWhiteSpace(tutorialActionId) ? TutorialIds.TalkHunter : tutorialActionId;
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

        if (CanInjectMorningDialogue(hunterData))
        {
            var morning = ScriptableObject.CreateInstance<InvestigationQuestion>();
            morning.questionId = morningDialogueQuestionId;
            morning.promptText = morningDialogueQuestionText;
            answers[morning.questionId] = BuildMorningDialogueAnswer();
            questionList.Add(morning);
        }

        if (hunterData != null && hunterData.dialogueQuestions != null)
        {
            foreach (var hq in hunterData.dialogueQuestions)
            {
                if (hq == null) continue;
                if (IsManualMorningDialogueDuplicate(hq)) continue;
                var q = ScriptableObject.CreateInstance<InvestigationQuestion>();
                q.questionId = hq.questionId;
                q.promptText = hq.questionText;
                answers[q.questionId] = hq.answerText;
                questionList.Add(q);
            }
        }

        if (CanOfferCardGame())
        {
            var cards = ScriptableObject.CreateInstance<InvestigationQuestion>();
            cards.questionId = cardGameQuestionId;
            cards.promptText = cardGameQuestionText;
            answers[cards.questionId] = cardGameAnswerText;
            questionList.Add(cards);
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
        Camera playerCamera = activePlayer != null ? activePlayer.GetPlayerCamera() : null;
        investigationManager.BeginHunterDialogue(questionList, answers, ownerHunter, null, cameraTransitionDuration, HandleQuestionSelected, ReleaseInteraction, useDialogueCamera: true, onResponseFinished: HandleResponseFinished, keepConfiguredCameraHome: true, playerCameraOverride: playerCamera);
    }

    private void ConfigureDialogueCameraTarget()
    {
        if (investigationManager == null || ownerHunter == null) return;
        Vector3 lookTarget;
        Vector3 targetPos;

        if (focusDialogueCameraOnFace && TryGetDialogueFacePoint(out Vector3 facePoint))
        {
            lookTarget = facePoint + faceLookOffset;
            Vector3 cameraDirection = GetDialogueCameraDirectionFromFace(facePoint);
            targetPos = facePoint + cameraDirection * Mathf.Max(0.1f, faceCameraDistance) + Vector3.up * faceCameraHeightOffset;
        }
        else
        {
            Vector3 forward = ownerHunter.transform.forward;
            targetPos = ownerHunter.transform.position - forward * Mathf.Abs(cameraOffset.z) + Vector3.up * cameraOffset.y;
            lookTarget = ownerHunter.transform.position + Vector3.up * cameraLookOffset.y;
        }

        Quaternion targetRot = Quaternion.LookRotation((lookTarget - targetPos).normalized, Vector3.up);
        investigationManager.SetDialogueCameraHome(targetPos, targetRot);
    }

    private Vector3 GetDialogueCameraDirectionFromFace(Vector3 facePoint)
    {
        Camera playerCamera = activePlayer != null ? activePlayer.GetPlayerCamera() : null;
        if (playerCamera != null)
        {
            Vector3 fromFaceToPlayerCamera = playerCamera.transform.position - facePoint;
            fromFaceToPlayerCamera.y = 0f;
            if (fromFaceToPlayerCamera.sqrMagnitude > 0.001f)
            {
                return fromFaceToPlayerCamera.normalized;
            }
        }

        Vector3 fromFaceToPlayer = activePlayer != null ? activePlayer.transform.position - facePoint : Vector3.zero;
        fromFaceToPlayer.y = 0f;
        if (fromFaceToPlayer.sqrMagnitude > 0.001f)
        {
            return fromFaceToPlayer.normalized;
        }

        Vector3 fallback = -ownerHunter.transform.forward;
        fallback.y = 0f;
        return fallback.sqrMagnitude > 0.001f ? fallback.normalized : Vector3.back;
    }

    private bool TryGetDialogueFacePoint(out Vector3 facePoint)
    {
        facePoint = Vector3.zero;
        if (ownerHunter == null) return false;

        if (faceFocusOverride != null)
        {
            facePoint = faceFocusOverride.position;
            return true;
        }

        Animator animator = ownerHunter.GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null)
            {
                facePoint = head.position;
                return true;
            }
        }

        Transform namedHead = FindChildByNamePart(ownerHunter.transform, "head");
        if (namedHead != null)
        {
            facePoint = namedHead.position;
            return true;
        }

        if (TryGetRendererBounds(ownerHunter.gameObject, out Bounds bounds))
        {
            facePoint = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.82f, bounds.center.z);
            return true;
        }

        return false;
    }

    private static Transform FindChildByNamePart(Transform root, string namePart)
    {
        if (root == null || string.IsNullOrEmpty(namePart)) return null;
        string lowered = namePart.ToLowerInvariant();
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child == root) continue;
            if (!child.gameObject.activeInHierarchy) continue;
            if (child.name.ToLowerInvariant().Contains(lowered))
            {
                return child;
            }
        }

        return null;
    }

    private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        if (root == null) return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled) continue;
            if (!renderer.gameObject.activeInHierarchy) continue;
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private void HandleQuestionSelected(InvestigationQuestion question)
    {
        if (question == null) return;
        if (IsMorningDialogueQuestion(question.questionId))
        {
            HunterManager hunterManager = GameManager.Instance != null ? GameManager.Instance.GetHunterManager() : null;
            hunterManager?.MarkMorningDialogueAsked(ownerHunter);
            return;
        }

        if (question.questionId == "goodbye")
        {
            investigationManager?.CompleteInvestigation();
        }
    }

    private void HandleResponseFinished(InvestigationQuestion question, string responseText)
    {
        if (question == null) return;
        if (!IsCardGameQuestion(question.questionId)) return;

        OpenCardGameAfterDialogueResponse();
    }

    private bool CanOfferCardGame()
    {
        if (!injectCardGameDialogueOption || ownerHunter == null || ownerHunter.GetState() != HunterState.Idle) return false;

        TimeManager timeManager = GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null;
        return timeManager != null && timeManager.GetDayState() == TimeManager.DayState.Active;
    }

    private bool IsCardGameQuestion(string questionId)
    {
        return !string.IsNullOrWhiteSpace(questionId) &&
               string.Equals(questionId, cardGameQuestionId, System.StringComparison.OrdinalIgnoreCase);
    }

    private bool CanInjectMorningDialogue(HunterData hunterData)
    {
        return injectMorningDialogueOption
            && hunterData != null
            && !string.IsNullOrWhiteSpace(morningDialogueQuestionText);
    }

    private bool IsManualMorningDialogueDuplicate(HunterDialogueQuestion question)
    {
        if (!injectMorningDialogueOption || question == null) return false;
        if (string.IsNullOrWhiteSpace(question.questionText) || string.IsNullOrWhiteSpace(morningDialogueQuestionText)) return false;
        return string.Equals(question.questionText.Trim(), morningDialogueQuestionText.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }

    private string BuildMorningDialogueAnswer()
    {
        HunterManager hunterManager = GameManager.Instance != null ? GameManager.Instance.GetHunterManager() : null;
        if (hunterManager == null || ownerHunter == null)
        {
            return "Nothing new today.";
        }

        return hunterManager.GetMorningDialogueAnswer(ownerHunter, repeatedMorningDialoguePrefix, out _);
    }

    private bool IsMorningDialogueQuestion(string questionId)
    {
        return !string.IsNullOrWhiteSpace(questionId) &&
               string.Equals(questionId, morningDialogueQuestionId, System.StringComparison.OrdinalIgnoreCase);
    }

    private void OpenCardGameAfterDialogueResponse()
    {
        if (cardGameUI == null)
        {
            cardGameUI = SceneLookup.Find<CardGameUI>(true);
        }

        if (cardGameUI == null)
        {
            Debug.LogWarning("HunterInteractable: Cannot open card game because no CardGameUI exists in the scene.", this);
            return;
        }

        investigationManager?.HideDialoguePanel();
        cardGameUI.Show(ownerHunter, () =>
        {
            investigationManager?.CompleteInvestigation();
        });
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
        TutorialManager.ReportEvent(TutorialIds.EventHunterDialogueClosed);
    }

    private void ResolveManagers()
    {
        if (investigationManager == null && GameManager.Instance != null)
        {
            investigationManager = GameManager.Instance.GetInvestigationManager();
        }
        if (recruitmentManager == null)
        {
            recruitmentManager = SceneLookup.Find<HunterRecruitmentManager>();
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
        ownerHunter.transform.rotation = desired;
    }

    private void AimAtHunter(PlayerInteraction player)
    {
        if (player == null) return;
        var cam = player.GetPlayerCamera();
        if (cam == null) return;

        Vector3 target;
        if (ownerHunter != null && focusDialogueCameraOnFace && TryGetDialogueFacePoint(out Vector3 facePoint))
        {
            target = facePoint + faceLookOffset;
        }
        else
        {
            target = ownerHunter != null
                ? ownerHunter.transform.position + Vector3.up * cameraLookOffset.y
                : cam.transform.position + cam.transform.forward;
        }

        Vector3 dir = (target - cam.transform.position).normalized;
        cam.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }

    public void SetHealVfxActive(bool value)
    {
        if (healVfx != null)
        {
            healVfx.SetActive(value);
        }
    }

}
