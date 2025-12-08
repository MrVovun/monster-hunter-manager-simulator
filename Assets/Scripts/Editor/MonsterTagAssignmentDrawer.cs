#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(MonsterData.MonsterTagAssignment))]
public class MonsterTagAssignmentDrawer : PropertyDrawer
{
    private static EvidenceTagLibrary cachedLibrary;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var library = GetLibrary();
        if (library == null || library.Categories == null || library.Categories.Count == 0)
        {
            EditorGUI.HelpBox(position, "Assign an EvidenceTagLibrary via GameConfig.", MessageType.Warning);
            EditorGUI.EndProperty();
            return;
        }

        var categoryProp = property.FindPropertyRelative("categoryName");
        var valueProp = property.FindPropertyRelative("valueName");

        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;

        Rect categoryRect = new Rect(position.x, position.y, position.width, lineHeight);
        Rect valueRect = new Rect(position.x, categoryRect.yMax + spacing, position.width, lineHeight);

        int categoryIndex = Mathf.Max(0, GetCategoryIndex(library, categoryProp.stringValue));
        string[] categoryLabels = BuildCategoryLabels(library);
        categoryIndex = EditorGUI.Popup(categoryRect, string.IsNullOrEmpty(label.text) ? "Category" : label.text + " Category", categoryIndex, categoryLabels);

        var selectedCategory = library.Categories[categoryIndex];
        categoryProp.stringValue = selectedCategory.categoryName;

        if (selectedCategory.values == null || selectedCategory.values.Count == 0)
        {
            EditorGUI.HelpBox(valueRect, "No values defined for this category.", MessageType.Info);
        }
        else
        {
            int valueIndex = Mathf.Max(0, GetValueIndex(selectedCategory, valueProp.stringValue));
            string[] valueLabels = BuildValueLabels(selectedCategory);
            valueIndex = EditorGUI.Popup(valueRect, "Value", valueIndex, valueLabels);
            valueProp.stringValue = selectedCategory.values[valueIndex].valueName;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var library = GetLibrary();
        if (library == null || library.Categories == null || library.Categories.Count == 0)
        {
            return EditorGUIUtility.singleLineHeight * 2f;
        }

        return (EditorGUIUtility.singleLineHeight * 2f) + EditorGUIUtility.standardVerticalSpacing;
    }

    private static EvidenceTagLibrary GetLibrary()
    {
        if (cachedLibrary != null) return cachedLibrary;

        GameConfig config = FindAsset<GameConfig>();
        if (config != null && config.evidenceTagLibrary != null)
        {
            cachedLibrary = config.evidenceTagLibrary;
            return cachedLibrary;
        }

        cachedLibrary = FindAsset<EvidenceTagLibrary>();
        return cachedLibrary;
    }

    private static int GetCategoryIndex(EvidenceTagLibrary library, string currentName)
    {
        for (int i = 0; i < library.Categories.Count; i++)
        {
            var category = library.Categories[i];
            if (!string.IsNullOrEmpty(category.categoryName) &&
                category.categoryName.Equals(currentName, System.StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return 0;
    }

    private static int GetValueIndex(EvidenceTagLibrary.TagCategory category, string currentValue)
    {
        for (int i = 0; i < category.values.Count; i++)
        {
            var value = category.values[i];
            if (!string.IsNullOrEmpty(value.valueName) &&
                value.valueName.Equals(currentValue, System.StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return 0;
    }

    private static string[] BuildCategoryLabels(EvidenceTagLibrary library)
    {
        string[] labels = new string[library.Categories.Count];
        for (int i = 0; i < labels.Length; i++)
        {
            var category = library.Categories[i];
            labels[i] = string.IsNullOrEmpty(category.displayName) ? category.categoryName : category.displayName;
        }
        return labels;
    }

    private static string[] BuildValueLabels(EvidenceTagLibrary.TagCategory category)
    {
        string[] labels = new string[category.values.Count];
        for (int i = 0; i < labels.Length; i++)
        {
            var value = category.values[i];
            labels[i] = string.IsNullOrEmpty(value.displayName) ? value.valueName : value.displayName;
        }
        return labels;
    }

    private static T FindAsset<T>() where T : UnityEngine.Object
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }
        }
        return null;
    }
}
#endif
