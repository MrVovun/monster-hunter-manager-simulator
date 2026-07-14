using P09.Modular.Humanoid.Data;
using UnityEngine;

[CreateAssetMenu(fileName = "P09 Humanoid Preset", menuName = "Guild Manager/Visuals/P09 Humanoid Preset")]
public class P09HumanoidPreset : ScriptableObject
{
    [Header("Source")]
    [Tooltip("Reusable P09 base visual prefab. It should contain the full modular model hierarchy and an Animator.")]
    public GameObject baseVisualPrefab;
    [Tooltip("Library that points to the P09 part containers used by this preset.")]
    public P09HumanoidLibrary library;

    [Header("Body")]
    public int sexId = 1;
    public int faceTypeId = 1;
    public int hairStyleId = 1;
    public int hairColorId = 1;
    public int skinId = 1;
    public int eyeColorId = 1;
    public int facialHairId = 0;
    public int bustSizeId = 2;

    [Header("Equipment")]
    public int weaponId = 0;
    public int shieldId = 0;
    public int headId = 0;
    public int chestId = 0;
    public int armId = 0;
    public int waistId = 0;
    public int legId = 0;

    public int GetCurrentId(EditPartType editPartType)
    {
        return editPartType switch
        {
            EditPartType.Weapon => weaponId,
            EditPartType.Shield => shieldId,
            EditPartType.Head => headId,
            EditPartType.Chest => chestId,
            EditPartType.Arm => armId,
            EditPartType.Waist => waistId,
            EditPartType.Leg => legId,
            EditPartType.Sex => sexId,
            EditPartType.HairStyle => hairStyleId,
            EditPartType.HairColor => hairColorId,
            EditPartType.Skin => skinId,
            EditPartType.EyeColor => eyeColorId,
            EditPartType.FacialHair => facialHairId,
            EditPartType.BustSize => bustSizeId,
            EditPartType.FaceType => faceTypeId,
            _ => 0
        };
    }

    public void SetCurrentId(EditPartType editPartType, int id)
    {
        if (id < 0) return;

        switch (editPartType)
        {
            case EditPartType.Weapon:
                weaponId = id;
                break;
            case EditPartType.Shield:
                shieldId = id;
                break;
            case EditPartType.Head:
                headId = id;
                break;
            case EditPartType.Chest:
                chestId = id;
                break;
            case EditPartType.Arm:
                armId = id;
                break;
            case EditPartType.Waist:
                waistId = id;
                break;
            case EditPartType.Leg:
                legId = id;
                break;
            case EditPartType.Sex:
                sexId = id;
                break;
            case EditPartType.HairStyle:
                hairStyleId = id;
                break;
            case EditPartType.HairColor:
                hairColorId = id;
                break;
            case EditPartType.Skin:
                skinId = id;
                break;
            case EditPartType.EyeColor:
                eyeColorId = id;
                break;
            case EditPartType.FacialHair:
                facialHairId = id;
                break;
            case EditPartType.BustSize:
                bustSizeId = id;
                break;
            case EditPartType.FaceType:
                faceTypeId = id;
                break;
        }
    }
}
