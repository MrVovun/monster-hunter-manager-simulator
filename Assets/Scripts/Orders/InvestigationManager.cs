using System;
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

    private readonly Dictionary<string, InvestigationQuestion> questionLookup = new Dictionary<string, InvestigationQuestion>();

    public InvestigationCase CurrentCase { get; private set; }
    public Order CurrentOrder { get; private set; }
    public event Action OnCaseUpdated;

    private void Awake()
    {
        ApplyConfigDefaults();
        BuildQuestionLookup();
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
        if (order == null) return;

        CurrentCase = new InvestigationCase();
        CurrentCase.truthMonster = order.monsterData;
        CurrentCase.truthTraits = GenerateTruthTraits(order.monsterData);
        CurrentCase.clientProfile = PickClientProfile();
        order.investigationCase = CurrentCase;
        CurrentOrder = order;

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

        NotifyCaseUpdated();
        return responseBuilder.ToString().Trim();
    }

    private void AppendResponseLine(StringBuilder builder, string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        if (builder.Length > 0) builder.AppendLine();
        builder.Append(line);
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
        int count = UnityEngine.Random.Range(min, max + 1);
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
    }

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
        var library = monsterLibrary != null ? monsterLibrary.GetMonsters() : null;
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
        clientSpawner?.DespawnCurrentClient();
        CurrentCase = null;
        CurrentOrder = null;
    }

    [SerializeField] private InvestigationDialogueUI dialogueUI;

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
}
