using System.Collections.Generic;
using UnityEngine;

public class HuntersTab : MonoBehaviour
{
    [SerializeField] private Transform listParent;
    [SerializeField] private HunterRosterItem hunterRosterItemPrefab;

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
    }

    public void ClearSelection()
    {
        selectedHunter = null;
        detailsPanel?.Clear();
    }
}
