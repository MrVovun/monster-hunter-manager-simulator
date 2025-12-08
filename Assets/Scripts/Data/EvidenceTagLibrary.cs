using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EvidenceTagLibrary", menuName = "Guild Manager/Evidence Tags")]
public class EvidenceTagLibrary : ScriptableObject
{
    [SerializeField] private List<TagCategory> categories = new List<TagCategory>();

    public IReadOnlyList<TagCategory> Categories => categories;

    public TagCategory GetCategory(string categoryName)
    {
        if (string.IsNullOrEmpty(categoryName)) return null;
        return categories.Find(c => string.Equals(c.categoryName, categoryName, StringComparison.OrdinalIgnoreCase));
    }

    public TagCategory.TagValue GetValue(string categoryName, string valueName)
    {
        var category = GetCategory(categoryName);
        if (category == null || string.IsNullOrEmpty(valueName)) return null;
        return category.values.Find(v => string.Equals(v.valueName, valueName, StringComparison.OrdinalIgnoreCase));
    }

    [Serializable]
    public class TagCategory
    {
        public string categoryName;
        public string displayName;
        [Tooltip("All allowed values for this category.")]
        public List<TagValue> values = new List<TagValue>();

        [Serializable]
        public class TagValue
        {
            public string valueName;
            public string displayName;
            [Tooltip("Optional tooltip/description for UI or debugging.")]
            [TextArea(1, 3)] public string description;
        }
    }
}
