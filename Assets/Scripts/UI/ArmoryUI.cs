using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ArmoryUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button exitButton;

    [Header("Hunters")]
    [SerializeField] private Transform hunterListParent;
    [SerializeField] private ArmoryHunterOptionUI hunterOptionPrefab;

    [Header("Weapons")]
    [SerializeField] private Transform weaponListParent;
    [SerializeField] private ArmoryWeaponOptionUI weaponOptionPrefab;

    [Header("Details")]
    [SerializeField] private TMP_Text selectedHunterText;
    [SerializeField] private TMP_Text selectedWeaponText;
    [SerializeField] private TMP_Text statusText;

    private readonly List<ArmoryHunterOptionUI> hunterOptions = new List<ArmoryHunterOptionUI>();
    private readonly List<ArmoryWeaponOptionUI> weaponOptions = new List<ArmoryWeaponOptionUI>();
    private ArmoryManager manager;
    private bool cursorCaptured;
    private CursorLockMode previousLockState;
    private bool previousCursorVisible;

    private void Awake()
    {
        if (exitButton != null)
        {
            exitButton.onClick.AddListener(Hide);
        }

        SetRootActive(false);
    }

    public void Show(ArmoryManager targetManager)
    {
        if (targetManager == null)
        {
            Debug.LogWarning("ArmoryUI: Missing armory manager.", this);
            return;
        }

        manager = targetManager;
        manager.OnStateChanged += Refresh;
        SetRootActive(true);
        CaptureCursor();
        Refresh();
    }

    public void Hide()
    {
        if (manager != null)
        {
            manager.OnStateChanged -= Refresh;
            manager.Close();
        }

        manager = null;
        ClearHunters();
        ClearWeapons();
        SetRootActive(false);
        ReleaseCursor();
    }

    private void Refresh()
    {
        RefreshHunters();
        RefreshWeapons();
        RefreshDetails();
    }

    private void RefreshHunters()
    {
        ClearHunters();
        if (manager == null || hunterListParent == null || hunterOptionPrefab == null) return;

        Hunter selectedHunter = manager.GetSelectedHunter();
        foreach (var hunter in manager.GetHunters())
        {
            if (hunter == null) continue;

            var option = Instantiate(hunterOptionPrefab, hunterListParent);
            option.Initialize(hunter, HandleHunterSelected);
            option.SetSelected(hunter == selectedHunter);
            option.SetInteractable(hunter.CanUseArmory() || hunter == selectedHunter);
            hunterOptions.Add(option);
        }
    }

    private void RefreshWeapons()
    {
        ClearWeapons();
        if (manager == null || weaponListParent == null || weaponOptionPrefab == null) return;

        int selectedWeaponId = manager.GetSelectedWeaponId();
        foreach (var weapon in manager.GetWeaponOptions())
        {
            if (weapon == null) continue;

            var option = Instantiate(weaponOptionPrefab, weaponListParent);
            option.Initialize(weapon, HandleWeaponSelected);
            option.SetSelected(weapon.id == selectedWeaponId);
            option.SetInteractable(manager.GetSelectedHunter() != null);
            weaponOptions.Add(option);
        }
    }

    private void RefreshDetails()
    {
        Hunter selectedHunter = manager != null ? manager.GetSelectedHunter() : null;

        if (selectedHunterText != null)
        {
            selectedHunterText.text = selectedHunter != null ? selectedHunter.Data != null ? selectedHunter.Data.hunterName : selectedHunter.name : "Select a hunter";
        }

        if (selectedWeaponText != null)
        {
            selectedWeaponText.text = selectedHunter != null ? $"Weapon ID: {manager.GetSelectedWeaponId()}" : string.Empty;
        }

        if (statusText != null)
        {
            statusText.text = BuildStatusText(selectedHunter);
        }
    }

    private string BuildStatusText(Hunter selectedHunter)
    {
        if (manager == null) return string.Empty;
        if (!manager.CanOpenArmory()) return "Armory is unavailable in the evening.";
        if (selectedHunter == null) return "Choose a hunter to equip.";
        return "Choose a weapon.";
    }

    private void HandleHunterSelected(Hunter hunter)
    {
        if (manager == null || hunter == null) return;
        manager.TrySelectHunter(hunter);
    }

    private void HandleWeaponSelected(P09HumanoidLibrary.PartOption weapon)
    {
        if (manager == null || weapon == null) return;
        manager.TryEquipWeapon(weapon.id);
    }

    private void ClearHunters()
    {
        foreach (var option in hunterOptions)
        {
            if (option != null)
            {
                Destroy(option.gameObject);
            }
        }
        hunterOptions.Clear();
    }

    private void ClearWeapons()
    {
        foreach (var option in weaponOptions)
        {
            if (option != null)
            {
                Destroy(option.gameObject);
            }
        }
        weaponOptions.Clear();
    }

    private void SetRootActive(bool value)
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(value);
        }
        else
        {
            gameObject.SetActive(value);
        }
    }

    private void CaptureCursor()
    {
        if (cursorCaptured) return;
        previousLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        cursorCaptured = true;
    }

    private void ReleaseCursor()
    {
        if (!cursorCaptured) return;
        Cursor.lockState = previousLockState;
        Cursor.visible = previousCursorVisible;
        cursorCaptured = false;
    }
}
