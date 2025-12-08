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
    public List<string> confirmedTraitIds = new List<string>();
    public List<EvidenceRecord> history = new List<EvidenceRecord>();

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

    public void ConfirmTrait(MonsterTrait trait)
    {
        if (trait == null) return;
        if (!confirmedTraitIds.Contains(trait.traitId))
        {
            confirmedTraitIds.Add(trait.traitId);
        }
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
}
