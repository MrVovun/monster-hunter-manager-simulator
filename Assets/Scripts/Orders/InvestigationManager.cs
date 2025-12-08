using System;
using System.Collections.Generic;
using UnityEngine;

public class InvestigationManager : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private EvidenceTagLibrary tagLibrary;
    [SerializeField] private List<InvestigationQuestion> questions = new List<InvestigationQuestion>();
    [SerializeField] private List<ClientProfile> clientProfiles = new List<ClientProfile>();

    private readonly Dictionary<string, InvestigationQuestion> questionLookup = new Dictionary<string, InvestigationQuestion>();

    public InvestigationCase CurrentCase { get; private set; }
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

        order.state = OrderState.Offered;
        order.investigationCase = CurrentCase;
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

    public void ResolveQuestion(InvestigationQuestion question)
    {
        if (CurrentCase == null || question == null) return;

        foreach (var categorySelection in question.revealedCategories)
        {
            string categoryName = categorySelection.GetCategoryName(tagLibrary);
            if (string.IsNullOrEmpty(categoryName)) continue;

            string truthValue = CurrentCase.truthMonster?.GetTagValue(categoryName);
            string responseText = BuildResponseText(question, categoryName, truthValue);
            CurrentCase.RevealTag(categoryName, truthValue, responseText);
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
        }

        NotifyCaseUpdated();
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

    private void NotifyCaseUpdated()
    {
        OnCaseUpdated?.Invoke();
    }
}
