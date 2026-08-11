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
    [Tooltip("Minimum mission difficulty required for this monster to appear in an order.")]
    public int minimumDifficulty = 0;
    [Tooltip("Mission difficulty where this monster is most likely to appear. Defaults to Minimum Difficulty when left at 0.")]
    public int preferredDifficulty = 0;
    [Tooltip("How far from Preferred Difficulty this monster remains common. Higher values keep it in rotation longer.")]
    public float difficultyFalloff = 40f;
    [Tooltip("Shape of the preferred difficulty curve. Higher values make the preference sharper.")]
    [Range(0.25f, 4f)] public float difficultyFalloffPower = 2f;

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

    [Header("Trophy Wall")]
    [Tooltip("Prefab representing this monster's trophy head for the trophy wall.")]
    public GameObject trophyHeadPrefab;
    [Tooltip("Optional scale multiplier for this monster's trophy head.")]
    public float trophyScale = 1f;
    [Tooltip("Per-monster position tweak after the wall's global head offset is applied.")]
    public Vector3 trophyPositionOffset = Vector3.zero;
    [Tooltip("Per-monster rotation tweak for this monster's trophy head.")]
    public Vector3 trophyRotationOffsetEuler = Vector3.zero;

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

        minimumDifficulty = Mathf.Max(0, minimumDifficulty);
        if (preferredDifficulty <= 0)
        {
            preferredDifficulty = minimumDifficulty;
        }
        preferredDifficulty = Mathf.Max(minimumDifficulty, preferredDifficulty);
        difficultyFalloff = Mathf.Max(1f, difficultyFalloff);
        difficultyFalloffPower = Mathf.Max(0.25f, difficultyFalloffPower);
        traitCountRange.x = Mathf.Max(0, traitCountRange.x);
        traitCountRange.y = Mathf.Max(traitCountRange.x, traitCountRange.y);
    }

    public float GetDifficultySelectionMultiplier(int difficultyValue)
    {
        if (difficultyValue < minimumDifficulty)
        {
            return 0f;
        }

        int preferred = preferredDifficulty > 0 ? preferredDifficulty : minimumDifficulty;
        float distance = Mathf.Abs(difficultyValue - preferred);
        float normalizedDistance = distance / Mathf.Max(1f, difficultyFalloff);
        float curve = Mathf.Pow(normalizedDistance, Mathf.Max(0.25f, difficultyFalloffPower));
        return 1f / (1f + curve);
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
            string pickedResponse = response.PickResponse();
            if (!string.IsNullOrWhiteSpace(pickedResponse))
            {
                return pickedResponse;
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
        [Tooltip("Legacy/fallback answer. Used when Response Pool is empty.")]
        [TextArea(2, 4)] public string responseText;
        [Tooltip("Weighted answer variants. Use this for broad/specific client answer rolls.")]
        public List<WeightedQuestionResponse> responsePool = new List<WeightedQuestionResponse>();

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

        public string PickResponse()
        {
            if (responsePool != null && responsePool.Count > 0)
            {
                int totalWeight = 0;
                foreach (var entry in responsePool)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.responseText)) continue;
                    totalWeight += Mathf.Max(1, entry.weight);
                }

                if (totalWeight > 0)
                {
                    int roll = UnityEngine.Random.Range(0, totalWeight);
                    foreach (var entry in responsePool)
                    {
                        if (entry == null || string.IsNullOrWhiteSpace(entry.responseText)) continue;
                        roll -= Mathf.Max(1, entry.weight);
                        if (roll < 0)
                        {
                            return entry.responseText;
                        }
                    }
                }
            }

            return responseText;
        }
    }

    [Serializable]
    public class WeightedQuestionResponse
    {
        [Min(1)] public int weight = 1;
        [TextArea(2, 4)] public string responseText;
    }
}
