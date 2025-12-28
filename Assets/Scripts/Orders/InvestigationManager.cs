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

    private readonly Dictionary<string, InvestigationQuestion> questionLookup = new Dictionary<string, InvestigationQuestion>();
    private Vector3 dialogueCameraHomePosition;
    private Quaternion dialogueCameraHomeRotation;
    private bool dialogueCameraCached;
    private Camera lastPlayerCamera;
    private bool freeBrowseLockActive;

    public InvestigationCase CurrentCase { get; private set; }
    public Order CurrentOrder { get; private set; }
    public event Action OnCaseUpdated;

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
        if (CurrentCase == null || question == null) return string.Empty;

        StringBuilder responseBuilder = new StringBuilder();

        foreach (var categorySelection in question.revealedCategories)
        {
            string categoryName = categorySelection.GetCategoryName(tagLibrary);
            if (string.IsNullOrEmpty(categoryName)) continue;

            string truthValue = CurrentCase.truthMonster?.GetTagValue(categoryName);
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
        if (clientSpawner != null)
        {
            clientSpawner.DismissCurrentClient();
        }
        CurrentCase = null;
        CurrentOrder = null;
    }

    public void BeginInvestigationUI(InvestigationCase investigationCase, System.Action onClose)
    {
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

    public void ShowBestiaryForDeclaration(System.Action<MonsterData> onSelected, System.Action onClosed)
    {
        ShowBestiary(true, CurrentCase, onSelected, onClosed);
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

    public Camera GetDialogueCamera()
    {
        return dialogueCamera;
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
}
