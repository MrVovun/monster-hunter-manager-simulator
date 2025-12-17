using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "InvestigationQuestion", menuName = "Guild Manager/Investigation Question")]
public class InvestigationQuestion : ScriptableObject
{
    [Header("Identifiers")]
    public string questionId;

    [Header("Presentation")]
    [TextArea(2, 3)] public string promptText;

    [Header("Timing")]
    [Tooltip("Base time (seconds) before the answer is revealed. Client response modifiers are added on top.")]
    public float askDurationSeconds = 2f;

    [Header("Requirements")]
    [Tooltip("List of evidence tags (category:value) that must already be known before this question is available. Leave value blank to only require the category.")]
    public List<EvidenceRequirement> requiredEvidence = new List<EvidenceRequirement>();

    [Header("Outcomes")]
    [Tooltip("Evidence categories revealed by this question. The actual value comes from the order's truth.")]
    public List<EvidenceCategorySelection> revealedCategories = new List<EvidenceCategorySelection>();
    [Tooltip("Optional extra evidence tags (category:value) that are directly confirmed.")]
    public List<EvidenceRequirement> explicitReveals = new List<EvidenceRequirement>();
    [Header("Trait Outcomes")]
    [Tooltip("Specific monster traits that get confirmed when this answer is revealed (only if the monster actually has them).")]
    public List<MonsterTrait> revealedTraits = new List<MonsterTrait>();

    [Tooltip("Optional questions that become highlighted after this answer is obtained.")]
    public List<InvestigationQuestion> followUps = new List<InvestigationQuestion>();

    private void OnEnable()
    {
        EnsureId();
    }

    private void OnValidate()
    {
        EnsureId();
    }

    private void EnsureId()
    {
        if (string.IsNullOrWhiteSpace(questionId))
        {
            questionId = Guid.NewGuid().ToString("N");
        }
    }

    [Serializable]
    public class EvidenceRequirement
    {
        public int categoryIndex = -1;
        public int valueIndex = -1;

        public string GetCategoryName(EvidenceTagLibrary library)
        {
            if (library == null || library.Categories == null) return null;
            if (categoryIndex < 0 || categoryIndex >= library.Categories.Count) return null;
            return library.Categories[categoryIndex].categoryName;
        }

        public string GetValueName(EvidenceTagLibrary library)
        {
            string category = GetCategoryName(library);
            if (string.IsNullOrEmpty(category) || library == null) return null;
            var cat = library.GetCategory(category);
            if (cat == null || cat.values == null || cat.values.Count == 0) return null;

            if (valueIndex < 0 || valueIndex >= cat.values.Count) return null;
            return cat.values[valueIndex].valueName;
        }

        public bool IsSatisfiedBy(Func<string, string> knownValueLookup, EvidenceTagLibrary library)
        {
            string categoryName = GetCategoryName(library);
            if (string.IsNullOrEmpty(categoryName)) return false;
            string knownValue = knownValueLookup?.Invoke(categoryName);

            string requiredValue = GetValueName(library);
            if (string.IsNullOrEmpty(requiredValue))
            {
                return !string.IsNullOrEmpty(knownValue);
            }

            return string.Equals(requiredValue, knownValue, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Serializable]
    public class EvidenceCategorySelection
    {
        public int categoryIndex = -1;

        public string GetCategoryName(EvidenceTagLibrary library)
        {
            if (library == null || library.Categories == null) return null;
            if (categoryIndex < 0 || categoryIndex >= library.Categories.Count) return null;
            return library.Categories[categoryIndex].categoryName;
        }
    }
}
