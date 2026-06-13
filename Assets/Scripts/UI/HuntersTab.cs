using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HuntersTab : MonoBehaviour
{
    [SerializeField] private Transform listParent;
    [SerializeField] private HunterRosterItem hunterRosterItemPrefab;
    [SerializeField] private Button levelUpButton;

    [Header("Details Panel")]
    [SerializeField] private HunterDetailsPanel detailsPanel;

    private Hunter selectedHunter;
    private void Awake()
    {
        ClearSelection();
    }

    public void Refresh()
    {
        HunterManager manager = GameManager.Instance != null ? GameManager.Instance.GetHunterManager() : null;
        if (manager == null || listParent == null || hunterRosterItemPrefab == null) return;

        var hunters = manager.GetAllHunters();

        foreach (Transform child in listParent)
        {
            Destroy(child.gameObject);
        }

        if (selectedHunter != null && !hunters.Contains(selectedHunter))
        {
            selectedHunter = null;
        }

        foreach (var hunter in hunters)
        {
            if (hunter == null) continue;
            HunterRosterItem item = Instantiate(hunterRosterItemPrefab, listParent);
            item.InitializeForHuntersTab(hunter, this, HandleHunterSelected);
        }

        if (selectedHunter != null)
        {
            detailsPanel?.ShowHunter(selectedHunter);
        }
        else
        {
            detailsPanel?.Clear();
        }

        UpdateLevelUpButtonState(hunters);
    }

    public void PayAndLevelUpAffordable()
    {
        HunterManager manager = GameManager.Instance != null ? GameManager.Instance.GetHunterManager() : null;
        GoldManager gold = GameManager.Instance != null ? GameManager.Instance.GetGoldManager() : null;
        if (manager == null || gold == null) return;

        foreach (var hunter in manager.GetAllHunters())
        {
            if (hunter != null && hunter.CanLevelUp())
            {
                manager.TryPayLevelUp(hunter, gold);
            }
        }

        Refresh();
    }

    private void HandleHunterSelected(Hunter hunter)
    {
        selectedHunter = hunter;
        if (hunter != null)
        {
            detailsPanel?.ShowHunter(hunter);
        }
        else
        {
            detailsPanel?.Clear();
        }
        UpdateLevelUpButtonState();
    }

    public void ClearSelection()
    {
        selectedHunter = null;
        detailsPanel?.Clear();
        UpdateLevelUpButtonState();
    }

    private void UpdateLevelUpButtonState(List<Hunter> hunters = null)
    {
        if (levelUpButton == null) return;

        HunterManager manager = GameManager.Instance != null ? GameManager.Instance.GetHunterManager() : null;
        GoldManager gold = GameManager.Instance != null ? GameManager.Instance.GetGoldManager() : null;
        if (manager == null || gold == null)
        {
            SetLevelUpButtonInteractable(false);
            return;
        }

        if (hunters == null)
        {
            hunters = manager.GetAllHunters();
        }

        bool canLevelAny = false;
        foreach (var hunter in hunters)
        {
            if (hunter == null || !hunter.CanLevelUp()) continue;
            if (gold.GetGold() < hunter.GetLevelUpCost()) continue;
            canLevelAny = true;
            break;
        }

        SetLevelUpButtonInteractable(canLevelAny);
    }

    private void SetLevelUpButtonInteractable(bool value)
    {
        levelUpButton.interactable = value;
        var visualFeedback = levelUpButton.GetComponent<UIButtonVisualFeedback>();
        if (visualFeedback != null)
        {
            visualFeedback.RefreshVisualState(true);
        }
    }
}
