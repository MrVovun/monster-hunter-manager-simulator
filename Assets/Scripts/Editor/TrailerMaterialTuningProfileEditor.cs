using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TrailerMaterialTuningProfile))]
public class TrailerMaterialTuningProfileEditor : Editor
{
    private const string DefaultProfilePath = "Assets/Settings/TrailerMaterialTuningProfile.asset";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        TrailerMaterialTuningProfile profile = (TrailerMaterialTuningProfile)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Material Collection", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Select materials, prefabs, scene objects, or folders, then add them to a tuning group. " +
            "Apply writes to the actual material assets, so capture originals first if you want a reliable restore point.",
            MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Selection To First Group"))
        {
            AddSelectionToGroup(profile, 0);
        }

        if (GUILayout.Button("Add Selection To New Group"))
        {
            AddSelectionToNewGroup(profile);
        }
        EditorGUILayout.EndHorizontal();

        for (int i = 0; i < profile.groups.Count; i++)
        {
            MaterialTuningGroup group = profile.groups[i];
            string label = string.IsNullOrWhiteSpace(group?.groupName) ? $"Group {i + 1}" : group.groupName;
            if (GUILayout.Button($"Add Selection To {label}"))
            {
                AddSelectionToGroup(profile, i);
            }
        }

        if (GUILayout.Button("Remove Duplicate/Empty Material Slots"))
        {
            Undo.RecordObject(profile, "Clean trailer material tuning profile");
            profile.RemoveDuplicateMaterials();
            EditorUtility.SetDirty(profile);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Apply / Restore", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Capture Originals"))
        {
            Undo.RecordObject(profile, "Capture original material values");
            int captured = profile.CaptureOriginals(false);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log($"Trailer material tuning: captured {captured} original material snapshot(s).", profile);
        }

        if (GUILayout.Button("Recapture Originals"))
        {
            if (EditorUtility.DisplayDialog(
                    "Overwrite original snapshots?",
                    "This replaces stored restore values with the materials' current values.",
                    "Recapture",
                    "Cancel"))
            {
                Undo.RecordObject(profile, "Recapture original material values");
                int captured = profile.CaptureOriginals(true);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                Debug.Log($"Trailer material tuning: recaptured {captured} original material snapshot(s).", profile);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply Tuning"))
        {
            ApplyProfile(profile);
        }

        if (GUILayout.Button("Restore Originals"))
        {
            RestoreProfile(profile);
        }
        EditorGUILayout.EndHorizontal();
    }

    [MenuItem("Tools/Materials/Create Trailer Material Tuning Profile")]
    private static void CreateProfile()
    {
        TrailerMaterialTuningProfile existing = AssetDatabase.LoadAssetAtPath<TrailerMaterialTuningProfile>(DefaultProfilePath);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            return;
        }

        string directory = Path.GetDirectoryName(DefaultProfilePath);
        if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
        {
            Directory.CreateDirectory(directory);
            AssetDatabase.Refresh();
        }

        TrailerMaterialTuningProfile profile = CreateInstance<TrailerMaterialTuningProfile>();
        profile.groups.Add(new MaterialTuningGroup
        {
            groupName = "Characters",
            brightness = 0.9f,
            saturation = 0.85f,
            tint = new Color(0.95f, 0.86f, 0.74f, 1f),
            tintWeight = 0.08f,
            smoothnessMultiplier = 0.65f
        });
        profile.groups.Add(new MaterialTuningGroup
        {
            groupName = "Trophies",
            brightness = 0.8f,
            saturation = 0.65f,
            smoothnessMultiplier = 0.8f
        });
        profile.groups.Add(new MaterialTuningGroup
        {
            groupName = "Environment",
            brightness = 1f,
            saturation = 0.95f,
            smoothnessMultiplier = 0.9f
        });

        AssetDatabase.CreateAsset(profile, DefaultProfilePath);
        AssetDatabase.SaveAssets();
        Selection.activeObject = profile;
        EditorGUIUtility.PingObject(profile);
    }

    private static void ApplyProfile(TrailerMaterialTuningProfile profile)
    {
        List<Material> materials = CollectProfileMaterials(profile);
        Undo.RecordObjects(materials.Cast<Object>().Append(profile).ToArray(), "Apply trailer material tuning");
        int changed = profile.Apply();
        foreach (Material material in materials)
        {
            EditorUtility.SetDirty(material);
        }
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        Debug.Log($"Trailer material tuning: applied settings to {changed} material reference(s).", profile);
    }

    private static void RestoreProfile(TrailerMaterialTuningProfile profile)
    {
        List<Material> materials = profile.originalSnapshots
            .Where(snapshot => snapshot != null && snapshot.material != null)
            .Select(snapshot => snapshot.material)
            .Distinct()
            .ToList();

        Undo.RecordObjects(materials.Cast<Object>().Append(profile).ToArray(), "Restore trailer material tuning originals");
        int restored = profile.RestoreOriginals();
        foreach (Material material in materials)
        {
            EditorUtility.SetDirty(material);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"Trailer material tuning: restored {restored} material snapshot(s).", profile);
    }

    private static void AddSelectionToNewGroup(TrailerMaterialTuningProfile profile)
    {
        Undo.RecordObject(profile, "Add selected materials to new trailer tuning group");
        MaterialTuningGroup group = new()
        {
            groupName = $"Group {profile.groups.Count + 1}"
        };
        profile.groups.Add(group);
        AddMaterialsToGroup(profile, group, CollectSelectedMaterials());
    }

    private static void AddSelectionToGroup(TrailerMaterialTuningProfile profile, int groupIndex)
    {
        if (profile.groups.Count == 0)
        {
            profile.groups.Add(new MaterialTuningGroup());
        }

        Undo.RecordObject(profile, "Add selected materials to trailer tuning group");
        groupIndex = Mathf.Clamp(groupIndex, 0, profile.groups.Count - 1);
        AddMaterialsToGroup(profile, profile.groups[groupIndex], CollectSelectedMaterials());
    }

    private static void AddMaterialsToGroup(TrailerMaterialTuningProfile profile, MaterialTuningGroup group, List<Material> materials)
    {
        foreach (Material material in materials)
        {
            if (material != null && !group.materials.Contains(material))
            {
                group.materials.Add(material);
            }
        }

        EditorUtility.SetDirty(profile);
        Debug.Log($"Trailer material tuning: added {materials.Count} selected material(s) to '{group.groupName}'.", profile);
    }

    private static List<Material> CollectProfileMaterials(TrailerMaterialTuningProfile profile)
    {
        return profile.groups
            .Where(group => group != null)
            .SelectMany(group => group.materials)
            .Where(material => material != null)
            .Distinct()
            .ToList();
    }

    private static List<Material> CollectSelectedMaterials()
    {
        HashSet<Material> materials = new();

        foreach (Object selected in Selection.objects)
        {
            if (selected is Material material)
            {
                materials.Add(material);
                continue;
            }

            string path = AssetDatabase.GetAssetPath(selected);
            if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { path }))
                {
                    string materialPath = AssetDatabase.GUIDToAssetPath(guid);
                    Material folderMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (folderMaterial != null)
                    {
                        materials.Add(folderMaterial);
                    }
                }

                continue;
            }

            if (selected is GameObject gameObject)
            {
                foreach (Renderer renderer in gameObject.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material rendererMaterial in renderer.sharedMaterials)
                    {
                        if (rendererMaterial != null)
                        {
                            materials.Add(rendererMaterial);
                        }
                    }
                }
            }
        }

        return materials.OrderBy(AssetDatabase.GetAssetPath).ToList();
    }
}
