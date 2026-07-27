using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using P09.Modular.Humanoid.Data;
using UnityEngine;

[DisallowMultipleComponent]
public class P09HumanoidVisualApplier : MonoBehaviour
{
    [Header("Preset")]
    [SerializeField] private P09HumanoidLibrary library;
    [SerializeField] private P09HumanoidPreset preset;
    [SerializeField] private bool applyOnAwake = false;

    [Header("References")]
    [SerializeField] private Transform modelRoot;
    [SerializeField] private Animator animator;

    [Header("Weapon Attachment")]
    [SerializeField] private bool snapWeaponToHandTarget = true;
    [SerializeField] private string rightHandWeaponTargetName = "Weapon_Target_Hand_R";
    [SerializeField] private string leftHandWeaponTargetName = "Weapon_Target_Hand_L";
    [SerializeField] private Vector3 weaponLocalPositionOffset;
    [SerializeField] private Vector3 weaponLocalRotationOffset;
    [SerializeField] private Vector3 weaponLocalScale = Vector3.one;
    [SerializeField] private Vector3 shieldLocalPositionOffset;
    [SerializeField] private Vector3 shieldLocalRotationOffset;
    [SerializeField] private Vector3 shieldLocalScale = Vector3.one;

    [Header("Debug")]
    [SerializeField] private bool logAppliedParts = true;

    private const string SkinMaterialPattern = @"^P09_.*_Skin.*$";
    private const string EyeMaterialPattern = @"^P09_Eye.*$";

    private static readonly EditPartType[] EquipmentTypes =
    {
        EditPartType.Weapon,
        EditPartType.Shield,
        EditPartType.Head,
        EditPartType.Chest,
        EditPartType.Arm,
        EditPartType.Waist,
        EditPartType.Leg
    };

    public Animator Animator => animator;
    public P09HumanoidPreset Preset => preset;

    private void Awake()
    {
        CacheReferences();

        if (applyOnAwake && preset != null)
        {
            ApplyPreset(preset);
        }
    }

    public void ApplyPreset(P09HumanoidPreset newPreset)
    {
        preset = newPreset;
        CacheReferences();

        if (preset == null)
        {
            Debug.LogWarning($"{nameof(P09HumanoidVisualApplier)} on '{name}' has no preset to apply.", this);
            return;
        }

        P09HumanoidLibrary activeLibrary = preset.library != null ? preset.library : library;
        if (activeLibrary == null)
        {
            Debug.LogWarning($"{nameof(P09HumanoidVisualApplier)} on '{name}' has no library assigned.", this);
            return;
        }

        library = activeLibrary;
        int sexId = preset.sexId;

        RuntimeAnimatorController controller = library.GetAnimatorController(sexId);
        if (controller != null && animator != null)
        {
            animator.runtimeAnimatorController = controller;
        }

        Transform root = modelRoot != null ? modelRoot : transform;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Transform[] children = root.GetComponentsInChildren<Transform>(true);

        ApplySexVisibility(renderers, sexId);

        ApplyRendererSelection(renderers, root, EditPartType.FaceType, preset.faceTypeId, sexId);
        ApplyRendererSelection(renderers, root, EditPartType.HairStyle, preset.hairStyleId, sexId);

        if (sexId == 1)
        {
            ApplyRendererSelection(renderers, root, EditPartType.FacialHair, preset.facialHairId, sexId);
        }
        else if (sexId == 2)
        {
            foreach (Transform child in children)
            {
                ApplyBustSize(child, preset.bustSizeId, sexId);
            }
        }

        foreach (EditPartType equipmentType in EquipmentTypes)
        {
            ApplyRendererSelection(renderers, root, equipmentType, preset.GetCurrentId(equipmentType), sexId);
        }

        foreach (Renderer renderer in renderers)
        {
            ApplyHairColor(renderer, preset.hairColorId, sexId);
            ApplySkinColor(renderer, preset.skinId, sexId);
            ApplyEyeColor(renderer, preset.eyeColorId, sexId);
        }

        SnapEquipmentToTargets(root);
    }

    public List<string> ValidateCurrentPreset()
    {
        P09HumanoidLibrary activeLibrary = preset != null && preset.library != null ? preset.library : library;
        return activeLibrary != null
            ? activeLibrary.ValidatePreset(preset, modelRoot != null ? modelRoot : transform)
            : new List<string> { "Library is missing." };
    }

    private void ApplySexVisibility(Renderer[] renderers, int sexId)
    {
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;

            string objectName = renderer.gameObject.name;
            string meshName = GetRendererMeshName(renderer);

            bool isMaleRenderer = HasSexPrefix(objectName, meshName, "Male");
            bool isFemaleRenderer = HasSexPrefix(objectName, meshName, "Female") || HasSexPrefix(objectName, meshName, "Fem");
            if (!isMaleRenderer && !isFemaleRenderer) continue;

            bool isOppositeSexRenderer = sexId == 1 ? isFemaleRenderer : isMaleRenderer;
            if (isOppositeSexRenderer)
            {
                renderer.enabled = false;
            }
        }
    }

    private void ApplyRendererSelection(Renderer[] renderers, Transform root, EditPartType type, int currentId, int sexId)
    {
        List<IEditPartData> dataList = library.GetPartDataList(type, sexId);
        if (dataList == null) return;

        int matched = 0;
        int enabled = 0;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;

            foreach (IEditPartData data in dataList)
            {
                if (data == null || string.IsNullOrWhiteSpace(data.MeshName)) continue;

                if (!MatchesRenderer(renderer, data.MeshName, sexId, preset.hairStyleId)) continue;

                bool shouldEnable = data.ContentId == currentId;
                renderer.enabled = shouldEnable;
                matched++;
                if (shouldEnable)
                {
                    EnsureActivePath(renderer.transform, root);
                    enabled++;
                }

                break;
            }
        }

        if (logAppliedParts)
        {
            IEditPartData selectedData = dataList.FirstOrDefault(d => d != null && d.ContentId == currentId);
            string selectedName = selectedData != null ? selectedData.DisplayName : $"id {currentId}";
            Debug.Log($"P09 '{name}': {type} -> {selectedName}; matched {matched}, enabled {enabled}.", this);
        }
    }

    private void ApplyHairColor(Renderer renderer, int currentId, int sexId)
    {
        List<IEditPartData> dataList = library.GetPartDataList(EditPartType.HairColor, sexId);
        var currentData = dataList?.FirstOrDefault(d => d != null && d.ContentId == currentId) as HairColorEditPartData;
        if (currentData == null) return;

        foreach (IEditPartData data in dataList)
        {
            if (data == null || !MatchesRenderer(renderer, data.MeshName, sexId, preset.hairStyleId)) continue;

            if (renderer != null)
            {
                renderer.sharedMaterial = currentData.GetMaterial(preset.hairStyleId);
            }
        }
    }

    private void ApplySkinColor(Renderer renderer, int currentId, int sexId)
    {
        List<IEditPartData> dataList = library.GetPartDataList(EditPartType.Skin, sexId);
        var currentData = dataList?.FirstOrDefault(d => d != null && d.ContentId == currentId) as ColorEditPartData;
        if (currentData == null) return;

        if (renderer == null) return;

        Material[] materials = renderer.sharedMaterials;
        bool changed = false;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material != null && Regex.IsMatch(material.name, SkinMaterialPattern))
            {
                materials[i] = currentData.Material;
                changed = true;
            }
        }

        if (changed)
        {
            renderer.sharedMaterials = materials;
        }
    }

    private void ApplyEyeColor(Renderer renderer, int currentId, int sexId)
    {
        List<IEditPartData> dataList = library.GetPartDataList(EditPartType.EyeColor, sexId);
        var currentData = dataList?.FirstOrDefault(d => d != null && d.ContentId == currentId) as ColorEditPartData;
        if (currentData == null || renderer == null) return;
        if (!renderer.gameObject.name.Contains(currentData.MeshName) && !GetRendererMeshName(renderer).Contains(currentData.MeshName)) return;

        Material[] materials = renderer.sharedMaterials;
        bool changed = false;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material != null && Regex.IsMatch(material.name, EyeMaterialPattern))
            {
                materials[i] = currentData.Material;
                changed = true;
            }
        }

        if (changed)
        {
            renderer.sharedMaterials = materials;
        }
    }

    private void ApplyBustSize(Transform child, int currentId, int sexId)
    {
        List<IEditPartData> dataList = library.GetPartDataList(EditPartType.BustSize, sexId);
        var currentData = dataList?.FirstOrDefault(d => d != null && d.ContentId == currentId) as BustSizeEditPartData;
        if (currentData == null) return;

        if (child.name == string.Format(currentData.MeshName, "R") ||
            child.name == string.Format(currentData.MeshName, "L"))
        {
            child.localScale = currentData.Size;
        }
    }

    private void SnapEquipmentToTargets(Transform root)
    {
        if (!snapWeaponToHandTarget || root == null) return;

        Transform rightTarget = FindDeepChild(root, rightHandWeaponTargetName);
        Transform leftTarget = FindDeepChild(root, leftHandWeaponTargetName);

        if (rightTarget != null)
        {
            SnapEnabledPartToTarget(root, EditPartType.Weapon, preset.weaponId, rightTarget, weaponLocalPositionOffset, weaponLocalRotationOffset, weaponLocalScale);
        }

        if (leftTarget != null)
        {
            SnapEnabledPartToTarget(root, EditPartType.Shield, preset.shieldId, leftTarget, shieldLocalPositionOffset, shieldLocalRotationOffset, shieldLocalScale);
        }
    }

    private void SnapEnabledPartToTarget(Transform root, EditPartType type, int currentId, Transform target, Vector3 localPosition, Vector3 localRotation, Vector3 localScale)
    {
        if (currentId == 0 || target == null) return;

        List<IEditPartData> dataList = library.GetPartDataList(type, preset.sexId);
        IEditPartData selectedData = dataList?.FirstOrDefault(d => d != null && d.ContentId == currentId);
        if (selectedData == null || string.IsNullOrWhiteSpace(selectedData.MeshName)) return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled) continue;
            if (!MatchesRenderer(renderer, selectedData.MeshName, preset.sexId, preset.hairStyleId)) continue;

            Transform part = renderer.transform;
            part.SetParent(target, false);
            part.localPosition = localPosition;
            part.localRotation = Quaternion.Euler(localRotation);
            part.localScale = localScale.sqrMagnitude <= 0.0001f ? Vector3.one : localScale;
            EnsureActivePath(part, root);
            return;
        }
    }

    private void CacheReferences()
    {
        if (modelRoot == null)
        {
            modelRoot = transform;
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }
    }

    private static bool MatchesMeshName(string childName, string meshName, int sexId, int hairStyleId)
    {
        if (string.IsNullOrWhiteSpace(childName) || string.IsNullOrWhiteSpace(meshName)) return false;

        string sexName = sexId == 1 ? "Male" : "Female";
        string femName = sexId == 2 ? "Fem" : sexName;

        return childName == meshName ||
               childName == FormatMeshName(meshName, sexName) ||
               childName == FormatMeshName(meshName, femName) ||
               childName == FormatMeshName(meshName, hairStyleId) ||
               childName == FormatMeshName(meshName, hairStyleId.ToString("D2"));
    }

    private static string FormatMeshName(string meshName, object value)
    {
        try
        {
            return string.Format(meshName, value);
        }
        catch (System.FormatException)
        {
            return string.Empty;
        }
    }

    private static bool MatchesRenderer(Renderer renderer, string meshName, int sexId, int hairStyleId)
    {
        if (renderer == null) return false;

        string objectName = renderer.gameObject.name;
        string rendererMeshName = GetRendererMeshName(renderer);
        return MatchesMeshName(objectName, meshName, sexId, hairStyleId) ||
               MatchesMeshName(rendererMeshName, meshName, sexId, hairStyleId);
    }

    private static string GetRendererMeshName(Renderer renderer)
    {
        if (renderer is SkinnedMeshRenderer skinnedRenderer && skinnedRenderer.sharedMesh != null)
        {
            return skinnedRenderer.sharedMesh.name;
        }

        MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
        return meshFilter != null && meshFilter.sharedMesh != null ? meshFilter.sharedMesh.name : string.Empty;
    }

    private static bool HasSexPrefix(string objectName, string meshName, string prefix)
    {
        return StartsWithSexPrefix(objectName, prefix) || StartsWithSexPrefix(meshName, prefix);
    }

    private static bool StartsWithSexPrefix(string value, string prefix)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               (value == prefix || value.StartsWith($"{prefix}_"));
    }

    private static void EnsureActivePath(Transform leaf, Transform stopAt)
    {
        Transform current = leaf;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
            {
                current.gameObject.SetActive(true);
            }

            if (current == stopAt)
            {
                break;
            }

            current = current.parent;
        }
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName)) return null;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private void OnValidate()
    {
        CacheReferences();
    }
}
