using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InvestigationCase
{
    [Header("Truth")]
    public MonsterData truthMonster;
    public List<MonsterTrait> truthTraits = new List<MonsterTrait>();

    [Header("Client")]
    public ClientProfile clientProfile;

    [Header("Case File")]
    public List<TagKnowledge> knownTags = new List<TagKnowledge>();
    [Tooltip("Revealed clues that the player has actively placed on the case board.")]
    public List<TagKnowledge> assignedTags = new List<TagKnowledge>();
    public List<string> confirmedTraitIds = new List<string>();
    public List<EvidenceRecord> history = new List<EvidenceRecord>();
    public List<QuestionResponseRoll> rolledQuestionResponses = new List<QuestionResponseRoll>();

    public MonsterData declaredMonster;

    public bool IsTagKnown(string categoryId)
    {
        if (string.IsNullOrEmpty(categoryId)) return false;
        return knownTags.Exists(t => string.Equals(t.categoryName, categoryId, StringComparison.OrdinalIgnoreCase));
    }

    public string GetKnownTagValue(string categoryId)
    {
        if (string.IsNullOrEmpty(categoryId)) return null;
        var entry = knownTags.Find(t => string.Equals(t.categoryName, categoryId, StringComparison.OrdinalIgnoreCase));
        return entry?.valueName;
    }

    public void RevealTag(string categoryId, string valueId, string summary)
    {
        if (string.IsNullOrEmpty(categoryId)) return;

        var entry = knownTags.Find(t => string.Equals(t.categoryName, categoryId, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
        {
            entry = new TagKnowledge { categoryName = categoryId };
            knownTags.Add(entry);
        }
        entry.valueName = valueId;

        var assignedEntry = assignedTags?.Find(t => string.Equals(t.categoryName, categoryId, StringComparison.OrdinalIgnoreCase));
        if (assignedEntry != null)
        {
            assignedEntry.valueName = valueId;
        }

        if (!string.IsNullOrEmpty(summary))
        {
            history.Add(new EvidenceRecord
            {
                timestamp = DateTime.Now,
                summary = summary,
                categoryName = categoryId,
                valueName = valueId
            });
        }
    }

    public bool IsTagAssigned(string categoryId)
    {
        if (string.IsNullOrEmpty(categoryId) || assignedTags == null) return false;
        return assignedTags.Exists(t => string.Equals(t.categoryName, categoryId, StringComparison.OrdinalIgnoreCase));
    }

    public bool AssignKnownTag(string categoryId)
    {
        if (string.IsNullOrEmpty(categoryId) || knownTags == null) return false;
        TagKnowledge known = knownTags.Find(t => string.Equals(t.categoryName, categoryId, StringComparison.OrdinalIgnoreCase));
        if (known == null || string.IsNullOrWhiteSpace(known.valueName)) return false;

        if (assignedTags == null)
        {
            assignedTags = new List<TagKnowledge>();
        }

        TagKnowledge assigned = assignedTags.Find(t => string.Equals(t.categoryName, categoryId, StringComparison.OrdinalIgnoreCase));
        if (assigned == null)
        {
            assignedTags.Add(new TagKnowledge
            {
                categoryName = known.categoryName,
                valueName = known.valueName
            });
        }
        else
        {
            assigned.valueName = known.valueName;
        }

        return true;
    }

    public bool UnassignTag(string categoryId)
    {
        if (string.IsNullOrEmpty(categoryId) || assignedTags == null) return false;
        return assignedTags.RemoveAll(t => string.Equals(t.categoryName, categoryId, StringComparison.OrdinalIgnoreCase)) > 0;
    }

    public void ConfirmTrait(MonsterTrait trait)
    {
        if (trait == null) return;
        if (!confirmedTraitIds.Contains(trait.traitId))
        {
            confirmedTraitIds.Add(trait.traitId);
        }
    }

    public bool TryGetRolledQuestionResponse(string questionId, string categoryName, string valueName, out string responseText)
    {
        responseText = null;
        if (rolledQuestionResponses == null || string.IsNullOrWhiteSpace(questionId) || string.IsNullOrWhiteSpace(categoryName))
        {
            return false;
        }

        foreach (var roll in rolledQuestionResponses)
        {
            if (roll == null) continue;
            if (!string.Equals(roll.questionId, questionId, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(roll.categoryName, categoryName, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(roll.valueName, valueName, StringComparison.OrdinalIgnoreCase)) continue;

            responseText = roll.responseText;
            return !string.IsNullOrWhiteSpace(responseText);
        }

        return false;
    }

    public void SetRolledQuestionResponse(string questionId, string categoryName, string valueName, string responseText)
    {
        if (string.IsNullOrWhiteSpace(questionId) || string.IsNullOrWhiteSpace(categoryName) || string.IsNullOrWhiteSpace(responseText)) return;
        if (rolledQuestionResponses == null)
        {
            rolledQuestionResponses = new List<QuestionResponseRoll>();
        }

        foreach (var roll in rolledQuestionResponses)
        {
            if (roll == null) continue;
            if (!string.Equals(roll.questionId, questionId, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(roll.categoryName, categoryName, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(roll.valueName, valueName, StringComparison.OrdinalIgnoreCase)) continue;

            roll.responseText = responseText;
            return;
        }

        rolledQuestionResponses.Add(new QuestionResponseRoll
        {
            questionId = questionId,
            categoryName = categoryName,
            valueName = valueName,
            responseText = responseText
        });
    }

    [Serializable]
    public class TagKnowledge
    {
        public string categoryName;
        public string valueName;
    }

    [Serializable]
    public class EvidenceRecord
    {
        public DateTime timestamp;
        public string summary;
        public string categoryName;
        public string valueName;
    }

    [Serializable]
    public class QuestionResponseRoll
    {
        public string questionId;
        public string categoryName;
        public string valueName;
        public string responseText;
    }
}
