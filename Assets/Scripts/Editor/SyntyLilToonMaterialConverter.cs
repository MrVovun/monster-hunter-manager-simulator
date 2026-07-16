using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SyntyLilToonMaterialConverter
{
    private static readonly string[] LilToonShaderNames =
    {
        "lilToon",
        "lilToon/lilToon"
    };

    [MenuItem("Tools/Materials/Convert Selected Synty Materials To lilToon")]
    private static void ConvertSelectedMaterials()
    {
        Shader lilToon = FindShader(LilToonShaderNames);
        if (lilToon == null)
        {
            EditorUtility.DisplayDialog("lilToon not found", "Could not find the lilToon shader in this project.", "OK");
            return;
        }

        List<Material> materials = CollectSelectedMaterials();
        if (materials.Count == 0)
        {
            EditorUtility.DisplayDialog("No materials selected", "Select one or more materials, renderers, prefabs, or folders first.", "OK");
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "Convert materials",
            $"Convert/repair {materials.Count} selected material(s) for lilToon?\n\nThis maps Synty texture fields like _Albedo_Map to lilToon's _MainTex.",
            "Convert",
            "Cancel");

        if (!confirmed)
        {
            return;
        }

        foreach (Material material in materials)
        {
            ConvertMaterial(material, lilToon);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Converted {materials.Count} material(s) to lilToon with texture remapping.");
    }

    private static void ConvertMaterial(Material material, Shader targetShader)
    {
        Texture mainTexture = GetFirstTexture(material, "_Albedo_Map", "_BaseMap", "_BaseColorMap", "_MainTex");
        Texture normalTexture = GetFirstTexture(material, "_Normal_Map", "_BumpMap");
        Texture emissionTexture = GetFirstTexture(material, "_Emission_Map", "_EmissionMap");

        Vector2 mainScale = GetTextureScale(material, "_Albedo_Map", "_BaseMap", "_MainTex");
        Vector2 mainOffset = GetTextureOffset(material, "_Albedo_Map", "_BaseMap", "_MainTex");
        Color baseColor = GetFirstColor(material, "_BaseColor", "_Color");
        Color emissionColor = GetFirstColor(material, "_Emission_Color", "_EmissionColor");
        float cutoff = GetFirstFloat(material, 0.5f, "_Alpha_Clip_Threshold", "_Cutoff");
        bool alphaClip = GetFirstFloat(material, 0f, "_AlphaClip", "_BUILTIN_AlphaClip") > 0.5f;

        Undo.RecordObject(material, "Convert Synty material to lilToon");
        material.shader = targetShader;

        SetTexture(material, mainTexture, mainScale, mainOffset, "_MainTex", "_BaseMap", "_BaseColorMap", "_OutlineTex");
        SetTexture(material, normalTexture, Vector2.one, Vector2.zero, "_BumpMap");
        SetTexture(material, emissionTexture, Vector2.one, Vector2.zero, "_EmissionMap");
        SetColor(material, baseColor, "_Color", "_BaseColor");
        SetColor(material, emissionColor, "_EmissionColor");
        SetFloat(material, cutoff, "_Cutoff");

        if (emissionTexture != null || emissionColor.maxColorComponent > 0f)
        {
            SetFloat(material, 1f, "_UseEmission");
        }

        if (alphaClip)
        {
            SetFloat(material, 1f, "_AlphaToMask");
        }

        EditorUtility.SetDirty(material);
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

        return materials.OrderBy(material => AssetDatabase.GetAssetPath(material)).ToList();
    }

    private static Shader FindShader(IEnumerable<string> shaderNames)
    {
        foreach (string shaderName in shaderNames)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader != null)
            {
                return shader;
            }
        }

        return null;
    }

    private static Texture GetFirstTexture(Material material, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (material.HasProperty(propertyName))
            {
                Texture texture = material.GetTexture(propertyName);
                if (texture != null)
                {
                    return texture;
                }
            }
        }

        return null;
    }

    private static Vector2 GetTextureScale(Material material, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (material.HasProperty(propertyName) && material.GetTexture(propertyName) != null)
            {
                return material.GetTextureScale(propertyName);
            }
        }

        return Vector2.one;
    }

    private static Vector2 GetTextureOffset(Material material, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (material.HasProperty(propertyName) && material.GetTexture(propertyName) != null)
            {
                return material.GetTextureOffset(propertyName);
            }
        }

        return Vector2.zero;
    }

    private static Color GetFirstColor(Material material, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (material.HasProperty(propertyName))
            {
                return material.GetColor(propertyName);
            }
        }

        return Color.white;
    }

    private static float GetFirstFloat(Material material, float fallback, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (material.HasProperty(propertyName))
            {
                return material.GetFloat(propertyName);
            }
        }

        return fallback;
    }

    private static void SetTexture(Material material, Texture texture, Vector2 scale, Vector2 offset, params string[] propertyNames)
    {
        if (texture == null)
        {
            return;
        }

        foreach (string propertyName in propertyNames)
        {
            if (!material.HasProperty(propertyName))
            {
                continue;
            }

            material.SetTexture(propertyName, texture);
            material.SetTextureScale(propertyName, scale);
            material.SetTextureOffset(propertyName, offset);
        }
    }

    private static void SetColor(Material material, Color color, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, color);
            }
        }
    }

    private static void SetFloat(Material material, float value, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }
    }
}
