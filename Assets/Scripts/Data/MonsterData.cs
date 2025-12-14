using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "MonsterData", menuName = "Guild Manager/Monster")]
public class MonsterData : ScriptableObject
{
    [Header("Info")]
    public string monsterId;
    public string displayName;
    public Sprite portrait;
    [Header("Lore")]
    [TextArea(3, 6)] public string description;
    [Header("Unlock Requirements")]
    public int requiredReputation = 0;

    [Header("Traits / Counters")]
    [Tooltip("Pool of traits that this monster can roll during truth generation.")]
    public List<MonsterTrait> possibleTraits = new List<MonsterTrait>();
    [Tooltip("Minimum and maximum number of traits randomly assigned when generating a mission (inclusive).")]
    public Vector2Int traitCountRange = new Vector2Int(0, 3);

    [Header("Evidence Tags")]
    [Tooltip("One entry per tag category. Used to drive investigation logic.")]
    public List<MonsterTagAssignment> evidenceTags = new List<MonsterTagAssignment>();

    [Header("Investigation Responses")]
    [Tooltip("Optional overrides for question answers specific to this monster.")]
    public List<QuestionResponseOverride> questionResponses = new List<QuestionResponseOverride>();

    [Header("Selection Weight")]
    [Tooltip("Higher weight = more likely to be chosen.")]
    public int weight = 1;

    private void OnEnable()
    {
        EnsureId();
    }

    private void OnValidate()
    {
        EnsureId();
#if UNITY_EDITOR
        ValidateTagAssignments();
#endif
    }

    private void EnsureId()
    {
        if (string.IsNullOrWhiteSpace(monsterId))
        {
            monsterId = Guid.NewGuid().ToString("N");
        }

        traitCountRange.x = Mathf.Max(0, traitCountRange.x);
        traitCountRange.y = Mathf.Max(traitCountRange.x, traitCountRange.y);
    }

#if UNITY_EDITOR
    private void ValidateTagAssignments()
    {
        HashSet<string> usedCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in evidenceTags)
        {
            if (string.IsNullOrWhiteSpace(tag.categoryName)) continue;
            if (!usedCategories.Add(tag.categoryName))
            {
                Debug.LogWarning($"MonsterData '{name}' has duplicate tag category '{tag.categoryName}'. Only one value per category is allowed.", this);
            }
        }

        // detect identical tag sets between monsters
        var thisSignature = BuildTagSignature();
        if (string.IsNullOrEmpty(thisSignature)) return;

        string path = AssetDatabase.GetAssetPath(this);
        var allMonsters = AssetDatabase.FindAssets("t:MonsterData");
        foreach (var guid in allMonsters)
        {
            string otherPath = AssetDatabase.GUIDToAssetPath(guid);
            if (otherPath == path) continue;
            var other = AssetDatabase.LoadAssetAtPath<MonsterData>(otherPath);
            if (other == null) continue;
            if (thisSignature == other.BuildTagSignature())
            {
                Debug.LogWarning($"MonsterData '{name}' shares the same evidence tag combination as '{other.displayName}'. Consider differentiating their tags.", this);
                break;
            }
        }
    }

    private string BuildTagSignature()
    {
        if (evidenceTags == null || evidenceTags.Count == 0) return string.Empty;
        var sorted = new List<MonsterTagAssignment>(evidenceTags);
        sorted.Sort((a, b) => string.Compare(a.categoryName, b.categoryName, StringComparison.OrdinalIgnoreCase));
        StringBuilder sb = new StringBuilder();
        foreach (var tag in sorted)
        {
            if (string.IsNullOrEmpty(tag.categoryName) || string.IsNullOrEmpty(tag.valueName)) continue;
            sb.Append(tag.categoryName).Append(':').Append(tag.valueName).Append('|');
        }
        return sb.ToString();
    }
#endif

    [Serializable]
    public class MonsterTagAssignment
    {
        public string categoryName;
        public string valueName;
    }

    public string GetTagValue(string categoryName)
    {
        if (string.IsNullOrEmpty(categoryName) || evidenceTags == null) return null;
        var entry = evidenceTags.Find(t => string.Equals(t.categoryName, categoryName, StringComparison.OrdinalIgnoreCase));
        return entry?.valueName;
    }

    public string GetInvestigationResponse(EvidenceTagLibrary tagLibrary, InvestigationQuestion question, string categoryName, string valueName)
    {
        // Direct overrides first
        foreach (var response in questionResponses)
        {
            if (response == null) continue;
            if (response.question != null && response.question != question) continue;
            if (!response.MatchesCategory(this, categoryName)) continue;
            if (!string.IsNullOrWhiteSpace(response.responseText))
            {
                return response.responseText;
            }
        }
        return null;
    }

    [Serializable]
    public class QuestionResponseOverride
    {
        public InvestigationQuestion question;
        [Tooltip("Pick a tag category from this monster's evidence list. Leave unset to apply to any category.")]
        public int tagIndex = -1;
        [TextArea(2, 4)] public string responseText;

        public bool MatchesCategory(MonsterData owner, string categoryName)
        {
            if (owner == null || owner.evidenceTags == null || owner.evidenceTags.Count == 0)
            {
                return true;
            }

            if (tagIndex < 0 || tagIndex >= owner.evidenceTags.Count)
            {
                return true;
            }

            var entry = owner.evidenceTags[tagIndex];
            if (entry == null || string.IsNullOrEmpty(entry.categoryName))
            {
                return true;
            }

            return string.Equals(entry.categoryName, categoryName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
