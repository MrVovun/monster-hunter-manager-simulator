using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TrophyWallConfig))]
public class TrophyWallConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("emptyPlaquePrefab"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("baseFramePrefab"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("frameTiers"), includeChildren: true);

        DrawFamilyOrder();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawFamilyOrder()
    {
        var familyProp = serializedObject.FindProperty("familyOrder");
        var options = GetFamilyOptions();
        EditorGUILayout.LabelField("Family Order", EditorStyles.boldLabel);

        if (options.Count == 0)
        {
            EditorGUILayout.HelpBox("No families found. Family tags are read from MonsterData assets (tag category 'family').", MessageType.Info);
            EditorGUILayout.PropertyField(familyProp, true);
            return;
        }

        for (int i = 0; i < familyProp.arraySize; i++)
        {
            var element = familyProp.GetArrayElementAtIndex(i);
            string current = element.stringValue;
            int currentIndex = Mathf.Max(0, options.IndexOf(current));
            EditorGUILayout.BeginHorizontal();
            int newIndex = EditorGUILayout.Popup($"Entry {i + 1}", currentIndex, options.ToArray());
            element.stringValue = options[Mathf.Clamp(newIndex, 0, options.Count - 1)];
            if (GUILayout.Button("X", GUILayout.Width(24f)))
            {
                familyProp.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                break;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Family Entry"))
        {
            familyProp.arraySize++;
            familyProp.GetArrayElementAtIndex(familyProp.arraySize - 1).stringValue = options[0];
        }
        EditorGUILayout.EndHorizontal();
    }

    private List<string> GetFamilyOptions()
    {
        HashSet<string> set = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        string[] guids = AssetDatabase.FindAssets("t:MonsterData");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var monster = AssetDatabase.LoadAssetAtPath<MonsterData>(path);
            if (monster == null) continue;
            string family = NormalizeFamily(monster.GetTagValue("family"));
            if (string.IsNullOrWhiteSpace(family)) continue;
            set.Add(family);
        }
        List<string> options = new List<string>(set);
        options.Sort(System.StringComparer.OrdinalIgnoreCase);
        if (options.Count == 0)
        {
            options.Add("<none>");
        }
        return options;
    }

    private string NormalizeFamily(string family)
    {
        return string.IsNullOrWhiteSpace(family) ? string.Empty : family.Trim();
    }
}
