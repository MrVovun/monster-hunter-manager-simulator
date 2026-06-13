using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class InvestigationManager : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private EvidenceTagLibrary tagLibrary;
    [SerializeField] private MonsterLibrary monsterLibrary;
    [SerializeField] private List<InvestigationQuestion> questions = new List<InvestigationQuestion>();
    [SerializeField] private List<ClientProfile> clientProfiles = new List<ClientProfile>();
    [SerializeField] private ClientSpawner clientSpawner;
    [SerializeField] private OrderOfferPanel orderOfferPanel;
    [SerializeField] private InvestigationDialogueUI dialogueUI;
    [SerializeField] private BestiaryUI bestiaryUI;
    [SerializeField] private PlayerInteraction playerInteraction;
    [SerializeField] private FirstPersonController playerController;
    [SerializeField] private Camera dialogueCamera;
    [SerializeField] private float cameraTransitionDuration = 0.5f;
    [Header("VFX")]
    [SerializeField] private GameObject thinkingVfxPrefab;
    [SerializeField] private Vector3 thinkingVfxOffset = new Vector3(0f, 1.7f, 0f);

    private readonly Dictionary<string, InvestigationQuestion> questionLookup = new Dictionary<string, InvestigationQuestion>();
    private Vector3 dialogueCameraHomePosition;
    private Quaternion dialogueCameraHomeRotation;
    private bool dialogueCameraCached;
    private Camera lastPlayerCamera;
    private bool freeBrowseLockActive;
    private bool hunterDialogueActive;
    private List<InvestigationQuestion> hunterQuestions = new List<InvestigationQuestion>();
    private Dictionary<string, string> hunterAnswers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private System.Action<InvestigationQuestion> hunterQuestionCallback;
    private System.Action hunterCloseCallback;
    private Hunter currentHunter;
    private string hunterGreetingOverride = "...";
    private Vector3 dialogueCameraBasePosition;
    private Quaternion dialogueCameraBaseRotation;
    private bool dialogueCameraBaseCached;
    public bool IsHunterDialogueActive => hunterDialogueActive;
    private System.Action<InvestigationQuestion, string> hunterResponseFinishedCallback;
    private const int TalkingIdleValue = 0;
    // Animator expects speaking variants in the range 1..8
    private const int TalkingSpeakBase = 1;
    private const int ActionIdleValue = 0;
    private const int ActionThinkingValue = 4;
    private GameObject activeThinkingVfx;
    public InvestigationCase CurrentCase { get; private set; }
    public Order CurrentOrder { get; private set; }
    public event Action OnCaseUpdated;

    public bool HasActiveClientInvestigation()
    {
        if (hunterDialogueActive) return false;
        if (CurrentCase != null || CurrentOrder != null) return true;
        if (clientSpawner == null)
        {
            clientSpawner = FindObjectOfType<ClientSpawner>();
        }
        return clientSpawner != null && clientSpawner.HasActiveClient;
    }

    private void Awake()
    {
        ApplyConfigDefaults();
        BuildQuestionLookup();
        CacheDialogueCameraHome();
    }

    private void ApplyConfigDefaults()
    {
        var config = GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null;
        if (config == null) return;

        if (tagLibrary == null)
        {
            tagLibrary = config.evidenceTagLibrary;
        }
        if (monsterLibrary == null)
        {
            monsterLibrary = config.monsterLibrary;
        }

        if ((questions == null || questions.Count == 0) && config.defaultInvestigationQuestions != null)
        {
            questions = new List<InvestigationQuestion>(config.defaultInvestigationQuestions);
        }

        if ((clientProfiles == null || clientProfiles.Count == 0) && config.defaultClientProfiles != null)
        {
            clientProfiles = new List<ClientProfile>(config.defaultClientProfiles);
        }
    }

    private void BuildQuestionLookup()
    {
        questionLookup.Clear();
        foreach (var question in questions)
        {
            if (question == null || string.IsNullOrEmpty(question.questionId)) continue;
            if (!questionLookup.ContainsKey(question.questionId))
            {
                questionLookup.Add(question.questionId, question);
            }
        }
    }

    public void StartInvestigation(Order order)
    {
        InitializeInvestigation(order, null);
        if (CurrentCase == null) return;
        SpawnClient();
        NotifyCaseUpdated();
    }

    public bool CanAskQuestion(InvestigationQuestion question)
    {
        if (CurrentCase == null || question == null) return false;
        foreach (var requirement in question.requiredEvidence)
        {
            if (!requirement.IsSatisfiedBy(CurrentCase.GetKnownTagValue, tagLibrary))
            {
                return false;
            }
        }

        return true;
    }

    public float GetQuestionDuration(InvestigationQuestion question)
    {
        if (hunterDialogueActive)
        {
            return Mathf.Max(0f, question != null ? question.askDurationSeconds : 0f);
        }
        if (question == null) return 0f;
        float duration = Mathf.Max(0f, question.askDurationSeconds);
        if (CurrentCase?.clientProfile != null)
        {
            duration += Mathf.Max(0f, CurrentCase.clientProfile.responseDelaySeconds);
        }

        return duration;
    }

    public string ResolveQuestion(InvestigationQuestion question)
    {
        if (hunterDialogueActive)
        {
            hunterQuestionCallback?.Invoke(question);
            if (question != null && hunterAnswers.TryGetValue(question.questionId, out var ans))
            {
                return ans;
            }
            return "...";
        }
        if (CurrentCase == null || question == null) return string.Empty;

        StringBuilder responseBuilder = new StringBuilder();

        foreach (var categorySelection in question.revealedCategories)
        {
            string categoryName = categorySelection.GetCategoryName(tagLibrary);
            if (string.IsNullOrEmpty(categoryName)) continue;

            string truthValue = CurrentCase.truthMonster?.GetTagValue(categoryName);
            if (string.IsNullOrWhiteSpace(truthValue)) continue;
            string responseText = BuildResponseText(question, categoryName, truthValue);
            CurrentCase.RevealTag(categoryName, truthValue, responseText);
            AppendResponseLine(responseBuilder, responseText);
        }

        foreach (var explicitReveal in question.explicitReveals)
        {
            string categoryName = explicitReveal.GetCategoryName(tagLibrary);
            if (string.IsNullOrEmpty(categoryName)) continue;

            string value = explicitReveal.GetValueName(tagLibrary);
            if (string.IsNullOrEmpty(value))
            {
                value = CurrentCase.truthMonster?.GetTagValue(categoryName);
            }
            if (string.IsNullOrWhiteSpace(value)) continue;

            string responseText = BuildResponseText(question, categoryName, value);
            CurrentCase.RevealTag(categoryName, value, responseText);
            AppendResponseLine(responseBuilder, responseText);
        }

        RevealTraitsFromQuestion(question, responseBuilder);

        NotifyCaseUpdated();
        return responseBuilder.ToString().Trim();
    }

    private void AppendResponseLine(StringBuilder builder, string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        if (builder.Length > 0) builder.AppendLine();
        builder.Append(line);
    }

    private void RevealTraitsFromQuestion(InvestigationQuestion question, StringBuilder responseBuilder)
    {
        if (question == null || CurrentCase == null || question.revealedTraits == null) return;

        foreach (var traitReference in question.revealedTraits)
        {
            TryConfirmTrait(traitReference, responseBuilder);
        }
    }

    private void TryConfirmTrait(MonsterTrait traitReference, StringBuilder responseBuilder)
    {
        if (traitReference == null || CurrentCase?.truthTraits == null) return;

        string targetId = !string.IsNullOrEmpty(traitReference.traitId) ? traitReference.traitId : null;
        foreach (var trait in CurrentCase.truthTraits)
        {
            if (trait == null) continue;
            bool match = false;
            if (!string.IsNullOrEmpty(targetId))
            {
                match = string.Equals(trait.traitId, targetId, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                match = trait == traitReference;
            }

            if (match)
            {
                CurrentCase.ConfirmTrait(trait);
                string message = string.IsNullOrWhiteSpace(trait.dialogueRevealText)
                    ? $"Trait confirmed: {trait.displayName}"
                    : trait.dialogueRevealText.Trim();
                AppendResponseLine(responseBuilder, message);
                return;
            }
        }
    }

    public List<InvestigationQuestion> GetAvailableQuestions()
    {
        if (hunterDialogueActive)
        {
            return new List<InvestigationQuestion>(hunterQuestions);
        }
        var result = new List<InvestigationQuestion>();
        foreach (var question in questions)
        {
            if (question == null) continue;
            if (CanAskQuestion(question))
            {
                result.Add(question);
            }
        }
        return result;
    }

    private List<MonsterTrait> GenerateTruthTraits(MonsterData monster)
    {
        var selected = new List<MonsterTrait>();
        if (monster == null || monster.possibleTraits == null || monster.possibleTraits.Count == 0)
        {
            return selected;
        }

        int min = Mathf.Max(0, monster.traitCountRange.x);
        int max = Mathf.Max(min, monster.traitCountRange.y);
        GameConfig config = GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null;
        int count = config != null
            ? config.RollTraitCount(min, max)
            : UnityEngine.Random.Range(min, max + 1);
        List<MonsterTrait> pool = new List<MonsterTrait>(monster.possibleTraits);
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int index = UnityEngine.Random.Range(0, pool.Count);
            selected.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return selected;
    }

    private ClientProfile PickClientProfile()
    {
        if (clientProfiles == null || clientProfiles.Count == 0) return null;
        int index = UnityEngine.Random.Range(0, clientProfiles.Count);
        return clientProfiles[index];
    }

    private Coroutine dialogueCameraRoutine;

    private void SpawnClient()
    {
        if (clientSpawner == null)
        {
            clientSpawner = FindObjectOfType<ClientSpawner>();
        }

        if (clientSpawner == null)
        {
            Debug.LogWarning("InvestigationManager: No ClientSpawner found in scene.");
            return;
        }

        clientSpawner.SpawnClientForCase(CurrentCase);
        if (dialogueCamera != null)
        {
            dialogueCamera.transform.position = dialogueCameraHomePosition;
            dialogueCamera.transform.rotation = dialogueCameraHomeRotation;
            dialogueCamera.gameObject.SetActive(false);
        }
    }

    private SharedCharacterAnimator GetActiveClientAnimator()
    {
        if (hunterDialogueActive) return null;
        if (clientSpawner == null)
        {
            clientSpawner = FindObjectOfType<ClientSpawner>();
        }
        return clientSpawner != null ? clientSpawner.GetActiveAnimator() : null;
    }

    private UnityEngine.AI.NavMeshAgent GetActiveClientAgent()
    {
        if (hunterDialogueActive) return null;
        if (clientSpawner == null)
        {
            clientSpawner = FindObjectOfType<ClientSpawner>();
        }
        return clientSpawner != null && clientSpawner.HasActiveClient
            ? clientSpawner.GetActiveClientAgent()
            : null;
    }

    public void PlayClientThinkingAnimation()
    {
        if (hunterDialogueActive) return;
        var anim = GetActiveClientAnimator();
        var agent = GetActiveClientAgent();
        if (anim != null)
        {
            anim.SetActionValue(ActionThinkingValue);
            anim.SetTalkingValue(TalkingIdleValue);
            anim.AutoUpdateVelocity = false;
            anim.SetMoving(false);
            anim.PlayThinkingClip();
        }

        if (agent != null)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        SetThinkingVfxActive(true);
    }

    public void PlayClientSpeakingAnimation()
    {
        if (hunterDialogueActive) return;
        var anim = GetActiveClientAnimator();
        var agent = GetActiveClientAgent();
        if (anim != null)
        {
            anim.SetActionValue(ActionIdleValue);
            int variant = UnityEngine.Random.Range(0, 8);
            anim.SetTalkingValue(TalkingSpeakBase + variant);
            anim.AutoUpdateVelocity = false;
            anim.SetMoving(false);
            anim.PlayRandomSpeakingClip();
        }

        if (agent != null)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        SetThinkingVfxActive(false);
    }

    public void StopClientTalkingAnimation()
    {
        var anim = GetActiveClientAnimator();
        var agent = GetActiveClientAgent();
        if (anim != null)
        {
            anim.SetActionValue(ActionIdleValue);
            anim.SetTalkingValue(TalkingIdleValue);
            anim.AutoUpdateVelocity = true;
        }

        if (agent != null)
        {
            agent.isStopped = false;
        }

        SetThinkingVfxActive(false);
    }

    private void InitializeInvestigation(Order order, ClientProfile overrideProfile)
    {
        if (order == null)
        {
            CurrentCase = null;
            CurrentOrder = null;
            return;
        }

        CurrentCase = new InvestigationCase();
        CurrentCase.truthMonster = order.monsterData;
        CurrentCase.truthTraits = GenerateTruthTraits(order.monsterData);
        CurrentCase.clientProfile = overrideProfile != null ? overrideProfile : PickClientProfile();
        order.investigationCase = CurrentCase;
        CurrentOrder = order;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void DebugStartInvestigation(Order order, ClientProfile overrideProfile)
    {
        InitializeInvestigation(order, overrideProfile);
        if (CurrentCase == null) return;
        SpawnClient();
        NotifyCaseUpdated();
    }
#endif

    private string BuildResponseText(InvestigationQuestion question, string categoryName, string valueName)
    {
        string text = CurrentCase?.truthMonster?.GetInvestigationResponse(tagLibrary, question, categoryName, valueName);
        if (string.IsNullOrWhiteSpace(text))
        {
            string warning = $"[Investigation] Missing response text for question '{question?.name}' (category='{categoryName}', value='{valueName}') on monster '{CurrentCase?.truthMonster?.displayName ?? "Unknown"}'.";
            Debug.LogWarning(warning, CurrentCase?.truthMonster);
            text = warning;
        }
        return text;
    }

    public struct MonsterCandidate
    {
        public MonsterData monster;
        public float confidence;
    }

    public List<MonsterCandidate> GetMonsterCandidates()
    {
        List<MonsterCandidate> result = new List<MonsterCandidate>();
        var library = GetAccessibleMonsters();
        if (library == null || library.Count == 0)
        {
            return result;
        }

        int knownCount = CurrentCase != null ? CurrentCase.knownTags.Count : 0;

        if (CurrentCase != null && CurrentCase.knownTags != null && CurrentCase.knownTags.Count > 0)
        {
            // Build list of all monsters that match every revealed tag
            List<MonsterData> matching = new List<MonsterData>();
            foreach (var monster in library)
            {
                if (monster == null) continue;
                if (MatchesKnownTags(monster))
                {
                    matching.Add(monster);
                }
            }

            if (matching.Count > 0)
            {
                float slice = 1f / matching.Count;
                foreach (var monster in library)
                {
                    if (monster == null) continue;
                    float confidence = matching.Contains(monster) ? slice : 0f;
                    result.Add(new MonsterCandidate { monster = monster, confidence = confidence });
                }

                SortCandidates(result);
                return result;
            }
        }

        // No known tags or no matches: confidence unknown (0) but still list monsters
        foreach (var monster in library)
        {
            if (monster == null) continue;
            result.Add(new MonsterCandidate { monster = monster, confidence = 0f });
        }

        SortCandidates(result);
        return result;
    }

    private bool MatchesKnownTags(MonsterData monster)
    {
        if (monster == null || CurrentCase == null || CurrentCase.knownTags == null) return false;
        foreach (var knowledge in CurrentCase.knownTags)
        {
            if (string.IsNullOrEmpty(knowledge.categoryName)) continue;
            string monsterValue = monster.GetTagValue(knowledge.categoryName);
            if (string.IsNullOrEmpty(monsterValue)) continue;

            if (string.Equals(monsterValue, knowledge.valueName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    private void SortCandidates(List<MonsterCandidate> candidates)
    {
        candidates.Sort((a, b) =>
        {
            int cmp = b.confidence.CompareTo(a.confidence);
            if (cmp != 0) return cmp;
            return string.Compare(a.monster?.displayName, b.monster?.displayName, StringComparison.OrdinalIgnoreCase);
        });
    }

    private void NotifyCaseUpdated()
    {
        OnCaseUpdated?.Invoke();
    }

    public void CompleteInvestigation()
    {
        if (hunterDialogueActive)
        {
            HandleHunterDialogueClosed();
            return;
        }
        if (clientSpawner != null)
        {
            clientSpawner.DismissCurrentClient();
        }
        CurrentCase = null;
        CurrentOrder = null;
    }

    public void BeginInvestigationUI(InvestigationCase investigationCase, System.Action onClose)
    {
        if (!hunterDialogueActive && investigationCase != CurrentCase)
        {
            Debug.LogWarning(
                "InvestigationManager: Refusing to open a client dialogue for a stale investigation case.",
                this);
            onClose?.Invoke();
            return;
        }

        if (dialogueUI == null)
        {
            dialogueUI = FindObjectOfType<InvestigationDialogueUI>(true);
        }

        if (dialogueUI == null)
        {
            Debug.LogWarning("InvestigationManager: Cannot open dialogue UI because it's missing.");
            onClose?.Invoke();
            return;
        }

        dialogueUI.Show(investigationCase, this, onClose);
    }

    public void ShowOrderDetails(System.Action onBack)
    {
        if (orderOfferPanel == null)
        {
            orderOfferPanel = FindObjectOfType<OrderOfferPanel>(true);
        }

        if (orderOfferPanel == null || CurrentOrder == null)
        {
            onBack?.Invoke();
            return;
        }

        orderOfferPanel.Show(CurrentOrder, HandleOrderPanelDecision, onBack, this);
    }

    private void HandleOrderPanelDecision()
    {
        dialogueUI?.Close();
        CompleteInvestigation();
    }

    public void BeginHunterDialogue(List<InvestigationQuestion> questions, Dictionary<string, string> answers, Hunter hunter, Camera overrideCamera, float transitionDuration, System.Action<InvestigationQuestion> onQuestionSelected, System.Action onClosed, bool useDialogueCamera = true, System.Action<InvestigationQuestion, string> onResponseFinished = null)
    {
        hunterDialogueActive = true;
        hunterQuestions = questions != null ? new List<InvestigationQuestion>(questions) : new List<InvestigationQuestion>();
        hunterAnswers.Clear();
        if (answers != null)
        {
            foreach (var kvp in answers)
            {
                hunterAnswers[kvp.Key] = kvp.Value;
            }
        }
        hunterQuestionCallback = onQuestionSelected;
        hunterCloseCallback = onClosed;
        hunterResponseFinishedCallback = onResponseFinished;
        currentHunter = hunter;
        hunterGreetingOverride = hunter != null && hunter.Data != null && !string.IsNullOrWhiteSpace(hunter.Data.greeting)
            ? hunter.Data.greeting
            : "...";

        // Hunter dialogue uses its own temporary UI context. Keep any active client
        // investigation intact so returning to that client still has its order.
        InvestigationCase hunterDialogueCase = new InvestigationCase();
        if (overrideCamera != null)
        {
            dialogueCamera = overrideCamera;
        }

        if (useDialogueCamera)
        {
            if (currentHunter != null)
            {
                Vector3 forward = currentHunter.transform.forward;
                Vector3 targetPos = currentHunter.transform.position - forward * 1.8f + Vector3.up * 1.6f;
                Vector3 lookTarget = currentHunter.transform.position + Vector3.up * 1.5f;
                Quaternion targetRot = Quaternion.LookRotation((lookTarget - targetPos).normalized, Vector3.up);
                SetDialogueCameraHome(targetPos, targetRot);
            }

            ToggleDialogueCamera(true, lastPlayerCamera != null ? lastPlayerCamera : (playerInteraction != null ? playerInteraction.GetPlayerCamera() : Camera.main));
        }
        else
        {
            // Do not toggle cameras when using player view; just cache the last player camera.
            var playerCam = lastPlayerCamera != null ? lastPlayerCamera : (playerInteraction != null ? playerInteraction.GetPlayerCamera() : Camera.main);
            if (playerCam != null)
            {
                lastPlayerCamera = playerCam;
            }
        }
        if (dialogueUI == null)
        {
            dialogueUI = FindObjectOfType<InvestigationDialogueUI>(true);
        }
        if (dialogueUI != null)
        {
            dialogueUI.Show(hunterDialogueCase, this, HandleHunterDialogueClosed);
        }
        else
        {
            HandleHunterDialogueClosed();
        }
    }

    public void BeginHunterHeal(Hunter hunter, float duration, System.Action onComplete)
    {
        StartCoroutine(HunterHealRoutine(duration, onComplete));
    }

    private IEnumerator HunterHealRoutine(float duration, System.Action onComplete)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, duration));
        onComplete?.Invoke();
    }

    public void ShowBestiaryForDeclaration(System.Action<MonsterData> onSelected, System.Action onClosed)
    {
        ShowBestiary(true, CurrentCase, onSelected, onClosed);
    }

    private void HandleHunterDialogueClosed()
    {
        hunterDialogueActive = false;
        hunterQuestions.Clear();
        hunterAnswers.Clear();
        hunterQuestionCallback = null;
        var closeCb = hunterCloseCallback;
        hunterCloseCallback = null;
        closeCb?.Invoke();
        if (dialogueUI != null)
        {
            dialogueUI.Close(invokeCallback: false);
        }
        ToggleDialogueCamera(false, lastPlayerCamera != null ? lastPlayerCamera : (playerInteraction != null ? playerInteraction.GetPlayerCamera() : Camera.main));
        RestoreDialogueCameraHome();
    }

    public void HandleResponsePlaybackFinished(InvestigationQuestion question, string responseText)
    {
        if (!hunterDialogueActive || question == null) return;
        hunterResponseFinishedCallback?.Invoke(question, responseText);
    }

    public string GetHunterGreeting()
    {
        return hunterGreetingOverride;
    }
    public void ShowBestiaryFree(System.Action onClosed = null)
    {
        ShowBestiary(false, null, null, onClosed);
    }

    private void ShowBestiary(bool allowSelection, InvestigationCase context, System.Action<MonsterData> onSelected, System.Action onClosed)
    {
        var ui = GetBestiaryUI();
        if (ui == null)
        {
            onClosed?.Invoke();
            return;
        }

        bool freeBrowseMode = !allowSelection;

        if (ui.IsVisible)
        {
            if (freeBrowseMode)
            {
                ui.Hide();
            }
            return;
        }

        System.Action wrappedClosed = () =>
        {
            if (freeBrowseMode)
            {
                ReleaseFreeBrowseLock();
            }
            onClosed?.Invoke();
        };

        if (freeBrowseMode)
        {
            ApplyFreeBrowseLock();
        }

        var monsters = GetAccessibleMonsters();
        ui.Show(monsters, allowSelection, context, monster =>
        {
            onSelected?.Invoke(monster);
        }, wrappedClosed);

        if (freeBrowseMode && !ui.IsVisible)
        {
            ReleaseFreeBrowseLock();
        }
    }

    private void DeactivateDialogueCamera()
    {
        if (dialogueCamera == null) return;
        Camera target = lastPlayerCamera != null ? lastPlayerCamera : Camera.main;
        if (target != null)
        {
            target.enabled = true;
        }
        dialogueCamera.transform.position = dialogueCameraHomePosition;
        dialogueCamera.transform.rotation = dialogueCameraHomeRotation;
        dialogueCamera.gameObject.SetActive(false);
    }

    public void ToggleDialogueCamera(bool activate, Camera playerCamera)
    {
        if (dialogueCamera == null) return;
        if (!activate && !dialogueCamera.gameObject.activeSelf)
        {
            return;
        }
        if (playerCamera != null)
        {
            lastPlayerCamera = playerCamera;
        }
        if (dialogueCameraRoutine != null)
        {
            StopCoroutine(dialogueCameraRoutine);
        }
        dialogueCameraRoutine = StartCoroutine(HandleDialogueCameraTransition(activate, playerCamera));
    }

    private IEnumerator HandleDialogueCameraTransition(bool entering, Camera playerCamera)
    {
        CacheDialogueCameraHome();
        Camera sourceCamera = playerCamera != null
            ? playerCamera
            : (lastPlayerCamera != null ? lastPlayerCamera : Camera.main);
        float duration = Mathf.Max(0.05f, cameraTransitionDuration);

        if (entering)
        {
            Vector3 startPos = sourceCamera != null ? sourceCamera.transform.position : dialogueCameraHomePosition;
            Quaternion startRot = sourceCamera != null ? sourceCamera.transform.rotation : dialogueCameraHomeRotation;
            Vector3 endPos = dialogueCameraHomePosition;
            Quaternion endRot = dialogueCameraHomeRotation;

            if (sourceCamera != null)
            {
                sourceCamera.enabled = false;
            }

            dialogueCamera.transform.SetPositionAndRotation(startPos, startRot);
            dialogueCamera.gameObject.SetActive(true);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                dialogueCamera.transform.position = Vector3.Lerp(startPos, endPos, t);
                dialogueCamera.transform.rotation = Quaternion.Slerp(startRot, endRot, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            dialogueCamera.transform.position = endPos;
            dialogueCamera.transform.rotation = endRot;
        }
        else
        {
            Vector3 startPos = dialogueCamera.transform.position;
            Quaternion startRot = dialogueCamera.transform.rotation;
            Vector3 endPos = sourceCamera != null ? sourceCamera.transform.position : dialogueCameraHomePosition;
            Quaternion endRot = sourceCamera != null ? sourceCamera.transform.rotation : dialogueCameraHomeRotation;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                dialogueCamera.transform.position = Vector3.Lerp(startPos, endPos, t);
                dialogueCamera.transform.rotation = Quaternion.Slerp(startRot, endRot, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            dialogueCamera.transform.position = dialogueCameraHomePosition;
            dialogueCamera.transform.rotation = dialogueCameraHomeRotation;
            if (sourceCamera != null)
            {
                sourceCamera.enabled = true;
            }
            dialogueCamera.gameObject.SetActive(false);
        }
    }

    public void SetDialogueCameraHome(Vector3 position, Quaternion rotation, bool setAsBase = false)
    {
        dialogueCameraHomePosition = position;
        dialogueCameraHomeRotation = rotation;
        dialogueCameraCached = true;
        if (setAsBase)
        {
            dialogueCameraBasePosition = position;
            dialogueCameraBaseRotation = rotation;
            dialogueCameraBaseCached = true;
        }
        if (dialogueCamera != null)
        {
            dialogueCamera.transform.SetPositionAndRotation(position, rotation);
        }
    }

    public void RestoreDialogueCameraHome()
    {
        if (!dialogueCameraBaseCached) return;
        dialogueCameraHomePosition = dialogueCameraBasePosition;
        dialogueCameraHomeRotation = dialogueCameraBaseRotation;
        dialogueCameraCached = true;
    }

    public Camera GetDialogueCamera()
    {
        return dialogueCamera;
    }

    public void HideDialogueUI()
    {
        if (dialogueUI != null)
        {
            dialogueUI.Close();
        }
    }

    public void HideDialoguePanel()
    {
        if (dialogueUI == null)
        {
            dialogueUI = FindObjectOfType<InvestigationDialogueUI>(true);
        }
        if (dialogueUI != null)
        {
            dialogueUI.HidePanel();
        }
    }

    public void ShowDialogueResponse(string text, bool refreshQuestions = true)
    {
        if (dialogueUI == null)
        {
            dialogueUI = FindObjectOfType<InvestigationDialogueUI>(true);
        }
        if (dialogueUI != null)
        {
            dialogueUI.ShowPanelWithResponse(text, refreshQuestions);
        }
    }

    public void RemoveHunterQuestion(string questionId)
    {
        if (!hunterDialogueActive || string.IsNullOrEmpty(questionId)) return;
        hunterQuestions.RemoveAll(q => q != null && string.Equals(q.questionId, questionId, StringComparison.OrdinalIgnoreCase));
    }

    public BestiaryUI GetBestiaryUI()
    {
        if (bestiaryUI == null)
        {
            bestiaryUI = FindObjectOfType<BestiaryUI>(true);
        }
        return bestiaryUI;
    }

    private void CacheDialogueCameraHome()
    {
        if (dialogueCamera == null || dialogueCameraCached) return;
        if (!dialogueCameraBaseCached && dialogueCamera != null)
        {
            dialogueCameraBasePosition = dialogueCamera.transform.position;
            dialogueCameraBaseRotation = dialogueCamera.transform.rotation;
            dialogueCameraBaseCached = true;
        }
        dialogueCameraHomePosition = dialogueCamera.transform.position;
        dialogueCameraHomeRotation = dialogueCamera.transform.rotation;
        dialogueCameraCached = true;
    }

    public List<MonsterData> GetAccessibleMonsters()
    {
        List<MonsterData> result = new List<MonsterData>();
        var library = monsterLibrary != null ? monsterLibrary.GetMonsters() : null;
        int reputation = GameManager.Instance != null ? GameManager.Instance.GetReputation() : 0;

        if (library != null)
        {
            foreach (var monster in library)
            {
                if (monster == null) continue;
                if (reputation >= monster.requiredReputation)
                {
                    result.Add(monster);
                }
            }
        }
        return result;
    }

    private void ApplyFreeBrowseLock()
    {
        var controller = ResolvePlayerController();
        if (controller != null && !controller.IsMovementLocked())
        {
            controller.LockMovement();
            freeBrowseLockActive = true;
        }
        else
        {
            freeBrowseLockActive = false;
        }

        if (InteractionPromptUI.Instance != null)
        {
            InteractionPromptUI.Instance.HidePrompt();
        }
    }

    private void ReleaseFreeBrowseLock()
    {
        if (!freeBrowseLockActive) return;

        var controller = ResolvePlayerController();
        if (controller != null)
        {
            controller.UnlockMovement();
        }
        freeBrowseLockActive = false;
    }

    private FirstPersonController ResolvePlayerController()
    {
        if (playerController != null) return playerController;

        var interaction = ResolvePlayerInteraction();
        if (interaction != null)
        {
            playerController = interaction.GetFirstPersonController();
            if (playerController != null) return playerController;
        }

        playerController = FindObjectOfType<FirstPersonController>();
        return playerController;
    }

    private PlayerInteraction ResolvePlayerInteraction()
    {
        if (playerInteraction != null) return playerInteraction;
        playerInteraction = FindObjectOfType<PlayerInteraction>();
        return playerInteraction;
    }

    private void SetThinkingVfxActive(bool value)
    {
        if (thinkingVfxPrefab == null) return;

        if (value)
        {
            if (activeThinkingVfx == null)
            {
                Transform anchor = null;
                var anim = GetActiveClientAnimator();
                if (anim != null) anchor = anim.transform;
                if (anchor == null)
                {
                    var agent = GetActiveClientAgent();
                    if (agent != null) anchor = agent.transform;
                }

                if (anchor == null) return;

                activeThinkingVfx = Instantiate(thinkingVfxPrefab, anchor);
                activeThinkingVfx.transform.localPosition = thinkingVfxOffset;
                activeThinkingVfx.transform.localRotation = Quaternion.identity;
            }
            activeThinkingVfx.SetActive(true);
        }
        else
        {
            if (activeThinkingVfx != null)
            {
                activeThinkingVfx.SetActive(false);
            }
        }
    }
}
