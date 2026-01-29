using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HunterTrait))]
public class HunterTraitEditor : Editor
{
    private EvidenceTagLibrary tagLibrary;
    private string[] categoryNames = new string[0];

    private void OnEnable()
    {
        LoadTagLibrary();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawDefaultInspectorExceptBonusConditions();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawDefaultInspectorExceptBonusConditions()
    {
        // Draw everything up to bonusEffects
        DrawProperty("traitId");
        DrawProperty("displayName");
        DrawProperty("description");
        DrawProperty("icon");
        DrawProperty("missionEffects");
        DrawBonusEffects();
        DrawProperty("counters");
    }

    private void DrawProperty(string propertyName)
    {
        var prop = serializedObject.FindProperty(propertyName);
        if (prop != null)
        {
            EditorGUILayout.PropertyField(prop, true);
        }
    }

    private void DrawBonusEffects()
    {
        var listProp = serializedObject.FindProperty("bonusEffects");
        if (listProp == null)
        {
            return;
        }

        EditorGUILayout.PropertyField(listProp.FindPropertyRelative("Array.size"));

        for (int i = 0; i < listProp.arraySize; i++)
        {
            var element = listProp.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.PropertyField(element.FindPropertyRelative("bonusType"));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("value"));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("stacking"));

            var conditionProp = element.FindPropertyRelative("condition");
            DrawCondition(conditionProp);

            EditorGUILayout.EndVertical();
        }
    }

    private void DrawCondition(SerializedProperty conditionProp)
    {
        if (conditionProp == null)
        {
            return;
        }

        if (tagLibrary == null || tagLibrary.Categories == null || tagLibrary.Categories.Count == 0)
        {
            EditorGUILayout.PropertyField(conditionProp, true);
            return;
        }

        var categories = tagLibrary.Categories;
        categoryNames = categories.Select(c => c.categoryName).Prepend(string.Empty).ToArray();

        var catProp = conditionProp.FindPropertyRelative("requiredMonsterTagCategory");
        var valProp = conditionProp.FindPropertyRelative("requiredMonsterTagValue");
        var soloProp = conditionProp.FindPropertyRelative("requiresSoloParty");
        var targetMonsterProp = conditionProp.FindPropertyRelative("targetMonster");
        var minPartyProp = conditionProp.FindPropertyRelative("minPartySize");
        var maxPartyProp = conditionProp.FindPropertyRelative("maxPartySize");
        var procChanceProp = conditionProp.FindPropertyRelative("procChancePercent");

        EditorGUILayout.PropertyField(targetMonsterProp);
        EditorGUILayout.PropertyField(minPartyProp);
        EditorGUILayout.PropertyField(maxPartyProp);
        EditorGUILayout.Slider(procChanceProp, 0f, 100f, new GUIContent("Proc Chance (%)"));

        int currentCatIndex = Mathf.Max(0, System.Array.IndexOf(categoryNames, catProp.stringValue));
        int newCatIndex = EditorGUILayout.Popup("Required Monster Tag Category", currentCatIndex, categoryNames);
        if (newCatIndex != currentCatIndex)
        {
            catProp.stringValue = categoryNames[newCatIndex];
            valProp.stringValue = string.Empty;
        }

        // Values dropdown
        if (newCatIndex > 0)
        {
            var selectedCategory = categories[newCatIndex - 1];
            var valueNames = selectedCategory.values.Select(v => v.valueName).Prepend(string.Empty).ToArray();
            int currentValIndex = Mathf.Max(0, System.Array.IndexOf(valueNames, valProp.stringValue));
            int newValIndex = EditorGUILayout.Popup("Required Tag Value", currentValIndex, valueNames);
            if (newValIndex != currentValIndex)
            {
                valProp.stringValue = valueNames[newValIndex];
            }
        }

        EditorGUILayout.PropertyField(soloProp);
    }

    private void LoadTagLibrary()
    {
        tagLibrary = null;
        // Try to load from GameConfig
        var config = Resources.Load<GameConfig>("GameConfig");
        if (config != null && config.evidenceTagLibrary != null)
        {
            tagLibrary = config.evidenceTagLibrary;
            return;
        }

#if UNITY_EDITOR
        // Fallback: find any EvidenceTagLibrary asset
        var guids = AssetDatabase.FindAssets("t:EvidenceTagLibrary");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var lib = AssetDatabase.LoadAssetAtPath<EvidenceTagLibrary>(path);
            if (lib != null)
            {
                tagLibrary = lib;
                break;
            }
        }
#endif
    }
}
