using System.Collections.Generic;
using System.Linq;
using P09.Modular.Humanoid.Data;
using UnityEngine;

[CreateAssetMenu(fileName = "P09 Humanoid Library", menuName = "Guild Manager/Visuals/P09 Humanoid Library")]
public class P09HumanoidLibrary : ScriptableObject
{
    [Header("P09 Data")]
    [Tooltip("Assign the P09 EditPartDataContainer assets here. The runtime applier reads these to find meshes/materials by selected ids.")]
    public List<EditPartDataContainer> editPartDataContainers = new List<EditPartDataContainer>();
    [Tooltip("Optional weapon group data. Used for validation and future weapon animation support.")]
    public List<WeaponGroupData> weaponGroupData = new List<WeaponGroupData>();

    [Header("Animation")]
    public RuntimeAnimatorController maleAnimatorController;
    public RuntimeAnimatorController femaleAnimatorController;

    public List<IEditPartData> GetPartDataList(EditPartType type, int sexId)
    {
        if (editPartDataContainers == null) return null;

        EditPartDataContainer bothSexContainer = editPartDataContainers.FirstOrDefault(d => d != null && d.Type == type && d.SexId == 0);
        if (bothSexContainer != null && bothSexContainer.PartDataList is { Count: > 0 })
        {
            return bothSexContainer.PartDataList;
        }

        EditPartDataContainer sexSpecificContainer = editPartDataContainers.FirstOrDefault(d => d != null && d.Type == type && d.SexId == sexId);
        return sexSpecificContainer?.PartDataList;
    }

    public IEditPartData GetPartData(EditPartType type, int id, int sexId)
    {
        List<IEditPartData> dataList = GetPartDataList(type, sexId);
        return dataList?.FirstOrDefault(d => d != null && d.ContentId == id);
    }

    public WeaponGroupData GetWeaponGroupData(int weaponGroupId)
    {
        return weaponGroupData?.FirstOrDefault(d => d != null && d.WeaponGroupId == weaponGroupId);
    }

    public RuntimeAnimatorController GetAnimatorController(int sexId)
    {
        return sexId == 1 ? maleAnimatorController : femaleAnimatorController;
    }

    public List<string> ValidatePreset(P09HumanoidPreset preset, Transform root = null)
    {
        var warnings = new List<string>();
        if (preset == null)
        {
            warnings.Add("Preset is missing.");
            return warnings;
        }

        ValidatePart(preset, EditPartType.Sex, preset.sexId, warnings, root);
        ValidatePart(preset, EditPartType.FaceType, preset.faceTypeId, warnings, root);
        ValidatePart(preset, EditPartType.HairStyle, preset.hairStyleId, warnings, root);
        ValidatePart(preset, EditPartType.HairColor, preset.hairColorId, warnings, root);
        ValidatePart(preset, EditPartType.Skin, preset.skinId, warnings, root);
        ValidatePart(preset, EditPartType.EyeColor, preset.eyeColorId, warnings, root);

        if (preset.sexId == 1)
        {
            ValidatePart(preset, EditPartType.FacialHair, preset.facialHairId, warnings, root);
        }
        else if (preset.sexId == 2)
        {
            ValidatePart(preset, EditPartType.BustSize, preset.bustSizeId, warnings, root);
        }

        ValidatePart(preset, EditPartType.Weapon, preset.weaponId, warnings, root);
        ValidatePart(preset, EditPartType.Shield, preset.shieldId, warnings, root);
        ValidatePart(preset, EditPartType.Head, preset.headId, warnings, root);
        ValidatePart(preset, EditPartType.Chest, preset.chestId, warnings, root);
        ValidatePart(preset, EditPartType.Arm, preset.armId, warnings, root);
        ValidatePart(preset, EditPartType.Waist, preset.waistId, warnings, root);
        ValidatePart(preset, EditPartType.Leg, preset.legId, warnings, root);

        var weaponData = GetPartData(EditPartType.Weapon, preset.weaponId, preset.sexId) as WeaponEditPartData;
        if (weaponData != null && weaponData.WeaponGroupId > 0 && GetWeaponGroupData(weaponData.WeaponGroupId) == null)
        {
            warnings.Add($"Weapon '{weaponData.DisplayName}' references missing weapon group id {weaponData.WeaponGroupId}.");
        }

        return warnings;
    }

    private void ValidatePart(P09HumanoidPreset preset, EditPartType type, int id, List<string> warnings, Transform root)
    {
        if (id == 0 && AllowsEmptySelection(type))
        {
            return;
        }

        List<IEditPartData> dataList = GetPartDataList(type, preset.sexId);
        if (dataList == null || dataList.Count == 0)
        {
            warnings.Add($"No data container found for {type} and sex id {preset.sexId}.");
            return;
        }

        IEditPartData data = dataList.FirstOrDefault(d => d != null && d.ContentId == id);
        if (data == null)
        {
            warnings.Add($"No {type} entry with content id {id} for sex id {preset.sexId}.");
            return;
        }

        if (root != null && !HasMatchingTransform(root, data.MeshName, preset.sexId, preset.hairStyleId))
        {
            warnings.Add($"{type} '{data.DisplayName}' points to mesh '{data.MeshName}', but no matching transform was found under '{root.name}'.");
        }
    }

    private static bool AllowsEmptySelection(EditPartType type)
    {
        return type == EditPartType.Weapon ||
               type == EditPartType.Shield ||
               type == EditPartType.Head ||
               type == EditPartType.Chest ||
               type == EditPartType.Arm ||
               type == EditPartType.Waist ||
               type == EditPartType.Leg;
    }

    private static bool HasMatchingTransform(Transform root, string meshName, int sexId, int hairStyleId)
    {
        if (root == null || string.IsNullOrWhiteSpace(meshName)) return false;

        string sexName = sexId == 1 ? "Male" : "Female";
        string femName = sexId == 2 ? "Fem" : sexName;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            string childName = child.name;
            if (childName == meshName ||
                childName == FormatMeshName(meshName, sexName) ||
                childName == FormatMeshName(meshName, femName) ||
                childName == FormatMeshName(meshName, hairStyleId) ||
                childName == FormatMeshName(meshName, hairStyleId.ToString("D2")))
            {
                return true;
            }
        }

        return false;
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

    private void OnValidate()
    {
        if (editPartDataContainers == null) return;

        foreach (var group in editPartDataContainers.Where(c => c != null).GroupBy(c => (c.Type, c.SexId)))
        {
            var duplicateIds = group.SelectMany(c => c.PartDataList ?? new List<IEditPartData>())
                .Where(d => d != null)
                .GroupBy(d => d.ContentId)
                .Where(g => g.Key != 0 && g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateIds.Count > 0)
            {
                Debug.LogWarning($"{name}: Duplicate P09 content ids for {group.Key.Type} / sex {group.Key.SexId}: {string.Join(", ", duplicateIds)}", this);
            }
        }
    }
}
