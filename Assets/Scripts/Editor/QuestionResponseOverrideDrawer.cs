#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(MonsterData.QuestionResponseOverride))]
public class QuestionResponseOverrideDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float line = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;

        var questionProp = property.FindPropertyRelative("question");
        var tagIndexProp = property.FindPropertyRelative("tagIndex");
        var responseProp = property.FindPropertyRelative("responseText");

        Rect questionRect = new Rect(position.x, position.y, position.width, line);
        EditorGUI.PropertyField(questionRect, questionProp);

        Rect categoryRect = new Rect(position.x, questionRect.yMax + spacing, position.width, line);
        DrawCategoryPopup(categoryRect, property, tagIndexProp);

        float responseHeight = EditorGUI.GetPropertyHeight(responseProp);
        Rect responseRect = new Rect(position.x, categoryRect.yMax + spacing, position.width, responseHeight);
        EditorGUI.PropertyField(responseRect, responseProp);

        EditorGUI.EndProperty();
    }

    private void DrawCategoryPopup(Rect rect, SerializedProperty property, SerializedProperty tagIndexProp)
    {
        var monster = property.serializedObject.targetObject as MonsterData;
        var categoryList = monster != null ? monster.evidenceTags : null;

        if (monster == null || categoryList == null || categoryList.Count == 0)
        {
            EditorGUI.LabelField(rect, "Category", "Add evidence tags first");
            return;
        }

        string[] options = BuildCategoryOptions(categoryList);
        int currentIndex = Mathf.Clamp(tagIndexProp.intValue + 1, 0, options.Length - 1);
        int newIndex = EditorGUI.Popup(rect, "Category", currentIndex, options);
        if (newIndex <= 0)
        {
            tagIndexProp.intValue = -1;
        }
        else
        {
            tagIndexProp.intValue = newIndex - 1;
        }
    }

    private static string[] BuildCategoryOptions(System.Collections.Generic.List<MonsterData.MonsterTagAssignment> tags)
    {
        string[] options = new string[tags.Count + 1];
        options[0] = "<Any>";
        for (int i = 0; i < tags.Count; i++)
        {
            string category = string.IsNullOrEmpty(tags[i].categoryName) ? $"Category {i + 1}" : tags[i].categoryName;
            options[i + 1] = category;
        }
        return options;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        var responseProp = property.FindPropertyRelative("responseText");
        float responseHeight = EditorGUI.GetPropertyHeight(responseProp);
        return line * 2f + spacing * 2f + responseHeight;
    }
}
#endif
