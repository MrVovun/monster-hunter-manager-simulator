#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using P09.Modular.Humanoid.Data;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

[CustomEditor(typeof(P09HumanoidPreset))]
public class P09HumanoidPresetEditor : Editor
{
    private const float IconSize = 48f;
    private readonly AdvancedDropdownState dropdownState = new AdvancedDropdownState();

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var preset = (P09HumanoidPreset)target;

        DrawSourceFields();
        EditorGUILayout.Space();

        if (preset.library == null)
        {
            EditorGUILayout.HelpBox("Assign a P09 Humanoid Library before editing modular parts.", MessageType.Warning);
            ApplyModifiedProperties();
            return;
        }

        DrawBodyFields(preset);
        EditorGUILayout.Space();
        DrawEquipmentFields(preset);
        EditorGUILayout.Space();
        DrawValidation(preset);

        ApplyModifiedProperties();
    }

    private void ApplyModifiedProperties()
    {
        if (serializedObject.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(target);
        }
    }

    private void DrawSourceFields()
    {
        EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("baseVisualPrefab"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("library"));
    }

    private void DrawBodyFields(P09HumanoidPreset preset)
    {
        EditorGUILayout.LabelField("Body", EditorStyles.boldLabel);
        DrawPartPopup(preset, EditPartType.Sex, "sexId", "Sex");
        DrawPartPopup(preset, EditPartType.FaceType, "faceTypeId", "Face Type");
        DrawPartPopup(preset, EditPartType.HairStyle, "hairStyleId", "Hair Style");
        DrawPartPopup(preset, EditPartType.HairColor, "hairColorId", "Hair Color");
        DrawPartPopup(preset, EditPartType.Skin, "skinId", "Skin Color");
        DrawPartPopup(preset, EditPartType.EyeColor, "eyeColorId", "Eye Color");

        if (preset.sexId == 1)
        {
            DrawPartPopup(preset, EditPartType.FacialHair, "facialHairId", "Facial Hair");
        }
        else if (preset.sexId == 2)
        {
            DrawPartPopup(preset, EditPartType.BustSize, "bustSizeId", "Bust Size");
        }
        else
        {
            EditorGUILayout.HelpBox("Sex ID should be 1 for Male or 2 for Female.", MessageType.Warning);
        }
    }

    private void DrawEquipmentFields(P09HumanoidPreset preset)
    {
        EditorGUILayout.LabelField("Equipment", EditorStyles.boldLabel);
        DrawPartPopup(preset, EditPartType.Weapon, "weaponId", "Weapon");
        DrawPartPopup(preset, EditPartType.Shield, "shieldId", "Shield");
        DrawPartPopup(preset, EditPartType.Head, "headId", "Head");
        DrawPartPopup(preset, EditPartType.Chest, "chestId", "Chest");
        DrawPartPopup(preset, EditPartType.Arm, "armId", "Arm");
        DrawPartPopup(preset, EditPartType.Waist, "waistId", "Waist");
        DrawPartPopup(preset, EditPartType.Leg, "legId", "Leg");
    }

    private void DrawPartPopup(P09HumanoidPreset preset, EditPartType type, string propertyName, string label)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null) return;

        List<IEditPartData> parts = preset.library.GetPartDataList(type, preset.sexId);
        if (parts == null || parts.Count == 0)
        {
            EditorGUILayout.PropertyField(property, new GUIContent(label));
            EditorGUILayout.HelpBox($"No P09 part data found for {type} and sex id {preset.sexId}.", MessageType.Warning);
            return;
        }

        List<IEditPartData> sortedParts = parts
            .Where(p => p != null)
            .OrderBy(p => p.ContentId)
            .ToList();

        if (sortedParts.Count == 0)
        {
            EditorGUILayout.PropertyField(property, new GUIContent(label));
            EditorGUILayout.HelpBox($"The P09 part data list for {type} contains no valid entries.", MessageType.Warning);
            return;
        }

        int currentId = property.intValue;
        int selectedIndex = sortedParts.FindIndex(p => p.ContentId == currentId);

        var options = new List<string>();
        var optionIds = new List<int>();
        var optionParts = new List<IEditPartData>();

        if (AllowsEmptySelection(type) && sortedParts.All(p => p.ContentId != 0))
        {
            options.Add(GetEmptySelectionLabel(type));
            optionIds.Add(0);
            optionParts.Add(null);
        }

        foreach (IEditPartData part in sortedParts)
        {
            options.Add($"{part.ContentId} - {part.DisplayName}");
            optionIds.Add(part.ContentId);
            optionParts.Add(part);
        }

        selectedIndex = optionIds.FindIndex(id => id == currentId);
        if (selectedIndex < 0)
        {
            selectedIndex = 0;
            options.Insert(0, $"Missing ID {currentId}");
            optionIds.Insert(0, currentId);
            optionParts.Insert(0, null);
        }

        EditorGUILayout.BeginHorizontal();
        Rect fieldRect = EditorGUILayout.GetControlRect();
        Rect dropdownRect = EditorGUI.PrefixLabel(fieldRect, new GUIContent(label));
        string selectedLabel = selectedIndex >= 0 && selectedIndex < options.Count ? options[selectedIndex] : $"Missing ID {currentId}";
        if (EditorGUI.DropdownButton(dropdownRect, new GUIContent(selectedLabel), FocusType.Keyboard))
        {
            var picker = new PartPickerDropdown(dropdownState, options, optionIds, selectedIndex, id =>
            {
                serializedObject.Update();
                SerializedProperty targetProperty = serializedObject.FindProperty(propertyName);
                if (targetProperty != null)
                {
                    targetProperty.intValue = id;
                }

                ApplyModifiedProperties();
                Repaint();
            });
            picker.Show(dropdownRect);
        }

        IEditPartData currentPart = null;
        int currentIndex = optionIds.FindIndex(id => id == property.intValue);
        if (currentIndex >= 0 && currentIndex < optionParts.Count)
        {
            currentPart = optionParts[currentIndex];
        }

        DrawPartIcon(currentPart, preset.sexId);
        EditorGUILayout.EndHorizontal();
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

    private static string GetEmptySelectionLabel(EditPartType type)
    {
        return type switch
        {
            EditPartType.Weapon => "0 - None (No Weapon)",
            EditPartType.Shield => "0 - None (No Shield)",
            EditPartType.Head => "0 - None (No Helmet)",
            EditPartType.Chest => "0 - None (No Chest Armor)",
            EditPartType.Arm => "0 - None (No Arm Armor)",
            EditPartType.Waist => "0 - None (No Waist Armor)",
            EditPartType.Leg => "0 - None (No Leg Armor)",
            _ => "0 - None"
        };
    }

    private static void DrawPartIcon(IEditPartData part, int sexId)
    {
        Sprite icon = GetIcon(part, sexId);
        Rect rect = GUILayoutUtility.GetRect(IconSize, IconSize, GUILayout.Width(IconSize), GUILayout.Height(IconSize));
        if (icon == null || icon.texture == null)
        {
            GUI.Box(rect, GUIContent.none);
            return;
        }

        Rect textureRect = icon.textureRect;
        var texCoords = new Rect(
            textureRect.x / icon.texture.width,
            textureRect.y / icon.texture.height,
            textureRect.width / icon.texture.width,
            textureRect.height / icon.texture.height);

        GUI.DrawTextureWithTexCoords(rect, icon.texture, texCoords, true);
    }

    private static Sprite GetIcon(IEditPartData part, int sexId)
    {
        return part switch
        {
            ArmorEditPartData armor => sexId == 1 ? armor.MaleIcon : armor.FemaleIcon,
            WeaponEditPartData weapon => weapon.Icon,
            RendererEditPartData renderer => renderer.Icon,
            ColorEditPartData color => color.Icon,
            _ => null
        };
    }

    private sealed class PartPickerDropdown : AdvancedDropdown
    {
        private readonly IReadOnlyList<string> labels;
        private readonly IReadOnlyList<int> ids;
        private readonly int selectedIndex;
        private readonly System.Action<int> onSelected;

        public PartPickerDropdown(
            AdvancedDropdownState state,
            IReadOnlyList<string> labels,
            IReadOnlyList<int> ids,
            int selectedIndex,
            System.Action<int> onSelected) : base(state)
        {
            this.labels = labels;
            this.ids = ids;
            this.selectedIndex = selectedIndex;
            this.onSelected = onSelected;
            minimumSize = new Vector2(320f, 360f);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("Select Part");
            for (int i = 0; i < labels.Count; i++)
            {
                string itemLabel = i == selectedIndex ? $"{labels[i]} [selected]" : labels[i];
                root.AddChild(new PartPickerItem(itemLabel, ids[i]));
            }

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item is PartPickerItem pickerItem)
            {
                onSelected?.Invoke(pickerItem.Id);
            }
        }
    }

    private sealed class PartPickerItem : AdvancedDropdownItem
    {
        public int Id { get; }

        public PartPickerItem(string name, int id) : base(name)
        {
            Id = id;
        }
    }

    private void DrawValidation(P09HumanoidPreset preset)
    {
        EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

        List<string> warnings = preset.library.ValidatePreset(preset);
        if (preset.baseVisualPrefab == null)
        {
            warnings.Add("Base visual prefab is missing.");
        }

        if (warnings.Count == 0)
        {
            EditorGUILayout.HelpBox("Preset IDs are valid for the assigned library.", MessageType.Info);
            return;
        }

        foreach (string warning in warnings)
        {
            EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }
    }
}
#endif
