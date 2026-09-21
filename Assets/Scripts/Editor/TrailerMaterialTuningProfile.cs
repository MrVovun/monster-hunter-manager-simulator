using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "TrailerMaterialTuningProfile", menuName = "Tools/Trailer Material Tuning Profile")]
public class TrailerMaterialTuningProfile : ScriptableObject
{
    private static readonly string[] BaseColorProperties =
    {
        "_BaseColor",
        "_Color",
        "_MainColor"
    };

    private static readonly string[] SmoothnessProperties =
    {
        "_Smoothness",
        "_Glossiness"
    };

    private static readonly string[] MetallicProperties =
    {
        "_Metallic"
    };

    private static readonly string[] EmissionColorProperties =
    {
        "_EmissionColor",
        "_Emission_Color"
    };

    private static readonly string[] MainTextureProperties =
    {
        "_BaseMap",
        "_MainTex",
        "_BaseColorMap",
        "_Albedo_Map"
    };

    [Tooltip("When enabled, Apply captures original material values the first time it touches a material.")]
    public bool captureOriginalsOnApply = true;

    public List<MaterialTuningGroup> groups = new();
    public List<MaterialSnapshot> originalSnapshots = new();

    public int Apply()
    {
        if (captureOriginalsOnApply)
        {
            CaptureOriginals(false);
        }

        int changed = 0;
        foreach (MaterialTuningGroup group in groups)
        {
            if (group == null || !group.enabled)
            {
                continue;
            }

            foreach (Material material in group.materials)
            {
                if (material == null)
                {
                    continue;
                }

                MaterialSnapshot snapshot = FindSnapshot(material);
                if (snapshot != null)
                {
                    RestoreMaterial(snapshot);
                }

                ApplyToMaterial(material, group);
                changed++;
            }
        }

        return changed;
    }

    public int CaptureOriginals(bool overwriteExisting)
    {
        int captured = 0;
        foreach (Material material in EnumerateMaterials())
        {
            if (material == null)
            {
                continue;
            }

            MaterialSnapshot existing = FindSnapshot(material);
            if (existing != null && !overwriteExisting)
            {
                continue;
            }

            MaterialSnapshot snapshot = CaptureMaterial(material);
            if (existing != null)
            {
                int index = originalSnapshots.IndexOf(existing);
                originalSnapshots[index] = snapshot;
            }
            else
            {
                originalSnapshots.Add(snapshot);
            }

            captured++;
        }

        return captured;
    }

    public int RestoreOriginals()
    {
        int restored = 0;
        foreach (MaterialSnapshot snapshot in originalSnapshots)
        {
            if (snapshot == null || snapshot.material == null)
            {
                continue;
            }

            RestoreMaterial(snapshot);
            restored++;
        }

        return restored;
    }

    public void RemoveDuplicateMaterials()
    {
        foreach (MaterialTuningGroup group in groups)
        {
            if (group == null)
            {
                continue;
            }

            HashSet<Material> seen = new();
            for (int i = group.materials.Count - 1; i >= 0; i--)
            {
                Material material = group.materials[i];
                if (material == null)
                {
                    group.materials.RemoveAt(i);
                    continue;
                }

                if (!seen.Add(material))
                {
                    group.materials.RemoveAt(i);
                }
            }
        }
    }

    private IEnumerable<Material> EnumerateMaterials()
    {
        foreach (MaterialTuningGroup group in groups)
        {
            if (group == null)
            {
                continue;
            }

            foreach (Material material in group.materials)
            {
                if (material != null)
                {
                    yield return material;
                }
            }
        }
    }

    private MaterialSnapshot FindSnapshot(Material material)
    {
        return originalSnapshots.Find(snapshot => snapshot != null && snapshot.material == material);
    }

    private static MaterialSnapshot CaptureMaterial(Material material)
    {
        MaterialSnapshot snapshot = new()
        {
            material = material
        };

        CaptureColors(material, snapshot.colors, BaseColorProperties);
        CaptureColors(material, snapshot.colors, EmissionColorProperties);
        CaptureFloats(material, snapshot.floats, SmoothnessProperties);
        CaptureFloats(material, snapshot.floats, MetallicProperties);
        CaptureTextures(material, snapshot.textures, MainTextureProperties);

        return snapshot;
    }

    private static void CaptureColors(Material material, List<ColorPropertySnapshot> snapshots, string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (!material.HasProperty(propertyName))
            {
                continue;
            }

            snapshots.Add(new ColorPropertySnapshot
            {
                propertyName = propertyName,
                value = material.GetColor(propertyName)
            });
        }
    }

    private static void CaptureFloats(Material material, List<FloatPropertySnapshot> snapshots, string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (!material.HasProperty(propertyName))
            {
                continue;
            }

            snapshots.Add(new FloatPropertySnapshot
            {
                propertyName = propertyName,
                value = material.GetFloat(propertyName)
            });
        }
    }

    private static void CaptureTextures(Material material, List<TexturePropertySnapshot> snapshots, string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (!material.HasProperty(propertyName))
            {
                continue;
            }

            snapshots.Add(new TexturePropertySnapshot
            {
                propertyName = propertyName,
                value = material.GetTexture(propertyName),
                scale = material.GetTextureScale(propertyName),
                offset = material.GetTextureOffset(propertyName)
            });
        }
    }

    private static void ApplyToMaterial(Material material, MaterialTuningGroup group)
    {
        if (group.affectBaseColor)
        {
            foreach (string propertyName in BaseColorProperties)
            {
                if (!material.HasProperty(propertyName))
                {
                    continue;
                }

                Color color = material.GetColor(propertyName);
                color = TuneColor(color, group.brightness, group.saturation, group.tint, group.tintWeight);
                material.SetColor(propertyName, color);
            }
        }

        if (group.affectSmoothness)
        {
            MultiplyFloats(material, SmoothnessProperties, group.smoothnessMultiplier);
        }

        if (group.affectMetallic)
        {
            MultiplyFloats(material, MetallicProperties, group.metallicMultiplier);
        }

        if (group.affectEmission)
        {
            foreach (string propertyName in EmissionColorProperties)
            {
                if (!material.HasProperty(propertyName))
                {
                    continue;
                }

                Color emission = material.GetColor(propertyName);
                emission.r *= group.emissionMultiplier;
                emission.g *= group.emissionMultiplier;
                emission.b *= group.emissionMultiplier;
                material.SetColor(propertyName, emission);
            }
        }

        if (group.affectMainTexture)
        {
            ApplyTextureTuning(material, group);
        }
    }

    private static Color TuneColor(Color color, float brightness, float saturation, Color tint, float tintWeight)
    {
        float alpha = color.a;
        Color.RGBToHSV(color, out float h, out float s, out float v);
        s = Mathf.Clamp01(s * Mathf.Max(0f, saturation));
        v = Mathf.Clamp(v * Mathf.Max(0f, brightness), 0f, 8f);
        Color tuned = Color.HSVToRGB(h, s, v, true);
        tuned = Color.Lerp(tuned, new Color(tint.r, tint.g, tint.b, tuned.a), Mathf.Clamp01(tintWeight));
        tuned.a = alpha;
        return tuned;
    }

    private static void MultiplyFloats(Material material, string[] propertyNames, float multiplier)
    {
        foreach (string propertyName in propertyNames)
        {
            if (!material.HasProperty(propertyName))
            {
                continue;
            }

            material.SetFloat(propertyName, Mathf.Max(0f, material.GetFloat(propertyName) * multiplier));
        }
    }

    private static void RestoreMaterial(MaterialSnapshot snapshot)
    {
        foreach (ColorPropertySnapshot color in snapshot.colors)
        {
            if (snapshot.material.HasProperty(color.propertyName))
            {
                snapshot.material.SetColor(color.propertyName, color.value);
            }
        }

        foreach (FloatPropertySnapshot value in snapshot.floats)
        {
            if (snapshot.material.HasProperty(value.propertyName))
            {
                snapshot.material.SetFloat(value.propertyName, value.value);
            }
        }

        foreach (TexturePropertySnapshot texture in snapshot.textures)
        {
            if (!snapshot.material.HasProperty(texture.propertyName))
            {
                continue;
            }

            snapshot.material.SetTexture(texture.propertyName, texture.value);
            snapshot.material.SetTextureScale(texture.propertyName, texture.scale);
            snapshot.material.SetTextureOffset(texture.propertyName, texture.offset);
        }
    }

    private static void ApplyTextureTuning(Material material, MaterialTuningGroup group)
    {
        Texture sourceTexture = GetFirstTexture(material, MainTextureProperties, out string sourceProperty);
        if (sourceTexture == null || string.IsNullOrEmpty(sourceProperty))
        {
            return;
        }

        Texture2D source = GetReadableTexture(sourceTexture);
        if (source == null)
        {
            return;
        }

        Texture2D tuned = CreateTunedTexture(source, group.brightness, group.saturation, group.tint, group.tintWeight);
        string outputPath = GetTextureOutputPath(group, material, sourceTexture);
        byte[] png = tuned.EncodeToPNG();
        File.WriteAllBytes(outputPath, png);
        UnityEngine.Object.DestroyImmediate(tuned);

        AssetDatabase.ImportAsset(outputPath);
        Texture2D imported = AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);
        if (imported == null)
        {
            return;
        }

        Vector2 scale = material.GetTextureScale(sourceProperty);
        Vector2 offset = material.GetTextureOffset(sourceProperty);
        foreach (string propertyName in MainTextureProperties)
        {
            if (!material.HasProperty(propertyName) || material.GetTexture(propertyName) == null)
            {
                continue;
            }

            material.SetTexture(propertyName, imported);
            material.SetTextureScale(propertyName, scale);
            material.SetTextureOffset(propertyName, offset);
        }
    }

    private static Texture GetFirstTexture(Material material, string[] propertyNames, out string propertyName)
    {
        foreach (string candidate in propertyNames)
        {
            if (!material.HasProperty(candidate))
            {
                continue;
            }

            Texture texture = material.GetTexture(candidate);
            if (texture != null)
            {
                propertyName = candidate;
                return texture;
            }
        }

        propertyName = string.Empty;
        return null;
    }

    private static Texture2D GetReadableTexture(Texture texture)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture renderTexture = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);

        Graphics.Blit(texture, renderTexture);
        RenderTexture.active = renderTexture;
        Texture2D copy = new(texture.width, texture.height, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
        copy.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTexture);
        return copy;
    }

    private static Texture2D CreateTunedTexture(Texture2D source, float brightness, float saturation, Color tint, float tintWeight)
    {
        Texture2D tuned = new(source.width, source.height, TextureFormat.RGBA32, false);
        Color[] pixels = source.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = TuneColor(pixels[i], brightness, saturation, tint, tintWeight);
        }

        tuned.SetPixels(pixels);
        tuned.Apply();
        return tuned;
    }

    private static string GetTextureOutputPath(MaterialTuningGroup group, Material material, Texture sourceTexture)
    {
        string folder = string.IsNullOrWhiteSpace(group.textureOutputFolder)
            ? "Assets/Settings/TrailerMaterialTuningTextures"
            : group.textureOutputFolder;

        EnsureAssetFolder(folder);

        string fileName = $"{SanitizeFileName(group.groupName)}_{SanitizeFileName(material.name)}_{SanitizeFileName(sourceTexture.name)}.png";
        return $"{folder.TrimEnd('/', '\\')}/{fileName}";
    }

    private static void EnsureAssetFolder(string folder)
    {
        folder = folder.Replace("\\", "/").TrimEnd('/');
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unnamed";
        }

        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return value.Replace(' ', '_');
    }
}

[Serializable]
public class MaterialTuningGroup
{
    public string groupName = "Material Group";
    public bool enabled = true;
    public List<Material> materials = new();

    [Header("Color")]
    public bool affectBaseColor = true;
    [Min(0f)] public float brightness = 1f;
    [Min(0f)] public float saturation = 1f;
    public Color tint = Color.white;
    [Range(0f, 1f)] public float tintWeight = 0f;

    [Header("Texture Color Grading")]
    [Tooltip("Use this for textured materials where Base Color is white/gray and saturation changes do not visibly affect the material. Creates tuned texture copies in the project.")]
    public bool affectMainTexture = false;
    public string textureOutputFolder = "Assets/Settings/TrailerMaterialTuningTextures";

    [Header("Surface")]
    public bool affectSmoothness = true;
    [Min(0f)] public float smoothnessMultiplier = 1f;
    public bool affectMetallic = true;
    [Min(0f)] public float metallicMultiplier = 1f;

    [Header("Glow")]
    public bool affectEmission = false;
    [Min(0f)] public float emissionMultiplier = 1f;
}

[Serializable]
public class MaterialSnapshot
{
    public Material material;
    public List<ColorPropertySnapshot> colors = new();
    public List<FloatPropertySnapshot> floats = new();
    public List<TexturePropertySnapshot> textures = new();
}

[Serializable]
public class ColorPropertySnapshot
{
    public string propertyName;
    public Color value;
}

[Serializable]
public class FloatPropertySnapshot
{
    public string propertyName;
    public float value;
}

[Serializable]
public class TexturePropertySnapshot
{
    public string propertyName;
    public Texture value;
    public Vector2 scale = Vector2.one;
    public Vector2 offset = Vector2.zero;
}
