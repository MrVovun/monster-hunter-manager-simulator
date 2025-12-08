#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(InvestigationQuestion))]
public class InvestigationQuestionDrawer : Editor
{
    private SerializedProperty questionIdProp;
    private SerializedProperty promptProp;
    private SerializedProperty askDurationProp;
    private SerializedProperty requiredEvidenceProp;
    private SerializedProperty revealedCategoriesProp;
    private SerializedProperty explicitRevealsProp;
    private SerializedProperty followUpsProp;

    private EvidenceTagLibrary tagLibrary;

    private void OnEnable()
    {
        questionIdProp = serializedObject.FindProperty("questionId");
        promptProp = serializedObject.FindProperty("promptText");
        askDurationProp = serializedObject.FindProperty("askDurationSeconds");
        requiredEvidenceProp = serializedObject.FindProperty("requiredEvidence");
        revealedCategoriesProp = serializedObject.FindProperty("revealedCategories");
        explicitRevealsProp = serializedObject.FindProperty("explicitReveals");
        followUpsProp = serializedObject.FindProperty("followUps");

        var config = GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null;
        tagLibrary = config != null ? config.evidenceTagLibrary : null;
        if (tagLibrary == null)
        {
            tagLibrary = FindAsset<EvidenceTagLibrary>();
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(questionIdProp);
        EditorGUILayout.PropertyField(promptProp);
        EditorGUILayout.PropertyField(askDurationProp);

        if (tagLibrary == null)
        {
            EditorGUILayout.HelpBox("Assign an EvidenceTagLibrary via GameConfig to edit questions.", MessageType.Warning);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        DrawEvidenceList("Required Evidence", requiredEvidenceProp, true);
        DrawCategoryList("Revealed Categories", revealedCategoriesProp);
        DrawEvidenceList("Explicit Reveals", explicitRevealsProp, true);
        EditorGUILayout.PropertyField(followUpsProp);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawEvidenceList(string label, SerializedProperty listProp, bool includeValue)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

        int newCount = Mathf.Max(0, EditorGUILayout.IntField("Count", listProp.arraySize));
        if (newCount != listProp.arraySize)
        {
            listProp.arraySize = newCount;
        }

        EditorGUI.indentLevel++;
        for (int i = 0; i < listProp.arraySize; i++)
        {
            SerializedProperty element = listProp.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawCategoryPopup(element.FindPropertyRelative("categoryIndex"));
            if (includeValue)
            {
                DrawValuePopup(element.FindPropertyRelative("categoryIndex"), element.FindPropertyRelative("valueIndex"));
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUI.indentLevel--;
    }

    private void DrawCategoryList(string label, SerializedProperty listProp)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

        int newCount = Mathf.Max(0, EditorGUILayout.IntField("Count", listProp.arraySize));
        if (newCount != listProp.arraySize)
        {
            listProp.arraySize = newCount;
        }

        EditorGUI.indentLevel++;
        for (int i = 0; i < listProp.arraySize; i++)
        {
            SerializedProperty element = listProp.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawCategoryPopup(element.FindPropertyRelative("categoryIndex"));
            EditorGUILayout.EndVertical();
        }
        EditorGUI.indentLevel--;
    }

    private void DrawCategoryPopup(SerializedProperty categoryIndexProp)
    {
        string[] options = BuildCategoryOptions();
        int currentIndex = Mathf.Clamp(categoryIndexProp.intValue + 1, 0, options.Length - 1);
        int newIndex = EditorGUILayout.Popup("Category", currentIndex, options);
        categoryIndexProp.intValue = newIndex - 1;
    }

    private void DrawValuePopup(SerializedProperty categoryIndexProp, SerializedProperty valueIndexProp)
    {
        int categoryIndex = categoryIndexProp.intValue;
        if (categoryIndex < 0 || categoryIndex >= tagLibrary.Categories.Count)
        {
            EditorGUILayout.HelpBox("Select a category first.", MessageType.Info);
            return;
        }

        var category = tagLibrary.Categories[categoryIndex];
        if (category.values == null || category.values.Count == 0)
        {
            EditorGUILayout.HelpBox("No values defined for this category.", MessageType.Info);
            return;
        }

        string[] options = BuildValueOptions(category);
        int currentIndex = Mathf.Clamp(valueIndexProp.intValue + 1, 0, options.Length - 1);
        int newIndex = EditorGUILayout.Popup("Value", currentIndex, options);
        valueIndexProp.intValue = newIndex - 1;
    }

    private string[] BuildCategoryOptions()
    {
        if (tagLibrary == null || tagLibrary.Categories == null) return new[] { "No categories" };
        string[] options = new string[tagLibrary.Categories.Count + 1];
        options[0] = "<Any>";
        for (int i = 0; i < tagLibrary.Categories.Count; i++)
        {
            var category = tagLibrary.Categories[i];
            options[i + 1] = string.IsNullOrEmpty(category.displayName) ? category.categoryName : category.displayName;
        }
        return options;
    }

    private string[] BuildValueOptions(EvidenceTagLibrary.TagCategory category)
    {
        string[] options = new string[category.values.Count + 1];
        options[0] = "<Any>";
        for (int i = 0; i < category.values.Count; i++)
        {
            var value = category.values[i];
            options[i + 1] = string.IsNullOrEmpty(value.displayName) ? value.valueName : value.displayName;
        }
        return options;
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
