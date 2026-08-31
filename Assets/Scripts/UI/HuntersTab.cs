using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HuntersTab : MonoBehaviour
{
    [SerializeField] private Transform listParent;
    [SerializeField] private HunterRosterItem hunterRosterItemPrefab;
    [SerializeField] private Button levelUpButton;
    [SerializeField] private TMP_Text levelUpButtonText;
    [SerializeField] private TMP_Text levelUpCostText;
    [SerializeField] private string levelUpButtonBaseLabel = "Level Up";
    [SerializeField] private Button fireButton;

    [Header("Details Panel")]
    [SerializeField] private HunterDetailsPanel detailsPanel;

    private Hunter selectedHunter;
    private void Awake()
    {
        if (levelUpButtonText == null && levelUpButton != null)
        {
            levelUpButtonText = levelUpButton.GetComponentInChildren<TMP_Text>();
        }

        if (fireButton != null)
        {
            fireButton.onClick.RemoveListener(FireSelectedHunter);
            fireButton.onClick.AddListener(FireSelectedHunter);
        }

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

        if (selectedHunter != null && (!hunters.Contains(selectedHunter) || selectedHunter.GetState() == HunterState.Dead))
        {
            selectedHunter = null;
        }

        foreach (var hunter in hunters)
        {
            if (hunter == null) continue;
            if (hunter.GetState() == HunterState.Dead) continue;
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

        UpdateActionButtonStates();
    }

    public void PayAndLevelUpAffordable()
    {
        HunterManager manager = GameManager.Instance != null ? GameManager.Instance.GetHunterManager() : null;
        GoldManager gold = GameManager.Instance != null ? GameManager.Instance.GetGoldManager() : null;
        if (manager == null || gold == null || selectedHunter == null) return;

        manager.TryPayLevelUp(selectedHunter, gold);

        Refresh();
    }

    public void FireSelectedHunter()
    {
        HunterManager manager = GameManager.Instance != null ? GameManager.Instance.GetHunterManager() : null;
        if (manager == null || selectedHunter == null) return;

        if (manager.FireHunter(selectedHunter))
        {
            selectedHunter = null;
            Refresh();
        }
        else
        {
            UpdateActionButtonStates();
        }
    }

    private void HandleHunterSelected(Hunter hunter)
    {
        if (hunter != null && hunter.GetState() == HunterState.Dead)
        {
            hunter = null;
        }

        selectedHunter = hunter;
        if (hunter != null)
        {
            detailsPanel?.ShowHunter(hunter);
        }
        else
        {
            detailsPanel?.Clear();
        }
        UpdateActionButtonStates();
    }

    public void ClearSelection()
    {
        selectedHunter = null;
        detailsPanel?.Clear();
        UpdateActionButtonStates();
    }

    private void UpdateActionButtonStates()
    {
        UpdateLevelUpButtonState();
        UpdateFireButtonState();
    }

    private void UpdateLevelUpButtonState()
    {
        HunterManager manager = GameManager.Instance != null ? GameManager.Instance.GetHunterManager() : null;
        GoldManager gold = GameManager.Instance != null ? GameManager.Instance.GetGoldManager() : null;
        if (manager == null || gold == null || selectedHunter == null)
        {
            SetLevelUpButtonInteractable(false, "Select a hunter first.");
            RefreshLevelUpPriceText(null, gold);
            return;
        }

        bool canLevelSelected = selectedHunter.CanLevelUp() && gold.GetGold() >= selectedHunter.GetLevelUpCost();
        SetLevelUpButtonInteractable(canLevelSelected, GetLevelUpUnavailableReason(selectedHunter, gold));
        RefreshLevelUpPriceText(selectedHunter, gold);
    }

    private void UpdateFireButtonState()
    {
        if (fireButton == null) return;

        HunterManager manager = GameManager.Instance != null ? GameManager.Instance.GetHunterManager() : null;
        bool canFire = manager != null && selectedHunter != null && manager.CanFireHunter(selectedHunter);
        SetFireButtonInteractable(canFire, GetFireUnavailableReason(manager));
    }

    private void SetLevelUpButtonInteractable(bool value, string unavailableReason = null)
    {
        if (levelUpButton == null) return;
        levelUpButton.interactable = value;
        if (value)
        {
            UnavailableReasonButton.ClearReason(levelUpButton);
        }
        else
        {
            UnavailableReasonButton.SetReason(levelUpButton, unavailableReason);
        }
        var visualFeedback = levelUpButton.GetComponent<UIButtonVisualFeedback>();
        if (visualFeedback != null)
        {
            visualFeedback.RefreshVisualState(true);
        }
    }

    private void RefreshLevelUpPriceText(Hunter hunter, GoldManager gold)
    {
        string buttonLabel = levelUpButtonBaseLabel;
        string status = string.Empty;

        if (hunter != null)
        {
            int xpForNext = hunter.GetXPToNextLevel();
            if (hunter.IsAtMaxLevel() || xpForNext == int.MaxValue)
            {
                status = "Max level";
            }
            else
            {
                int cost = hunter.GetLevelUpCost();
                int currentGold = gold != null ? gold.GetGold() : 0;
                int xpNeeded = Mathf.Max(0, xpForNext - hunter.GetXP());
                int requiredReputation = hunter.GetRequiredReputationForNextLevel();
                int currentReputation = GetCurrentReputation();
                bool reputationLocked = requiredReputation > currentReputation;
                buttonLabel = reputationLocked
                    ? $"Requires Rep {requiredReputation}"
                    : $"{levelUpButtonBaseLabel} ({cost}g)";

                if (hunter.CanLevelUp())
                {
                    status = currentGold >= cost
                        ? $"Level up cost: {cost} gold"
                        : $"Level up cost: {cost} gold ({currentGold}/{cost})";
                }
                else if (reputationLocked)
                {
                    status = $"Requires Reputation {requiredReputation}. Current: {currentReputation}.";
                }
                else
                {
                    status = $"Level up cost: {cost} gold. Needs {xpNeeded} XP.";
                }
            }
        }

        if (levelUpButtonText != null)
        {
            levelUpButtonText.text = buttonLabel;
        }

        if (levelUpCostText != null)
        {
            levelUpCostText.text = status;
        }
    }

    private void SetFireButtonInteractable(bool value, string unavailableReason = null)
    {
        if (fireButton == null) return;
        fireButton.interactable = value;
        if (value)
        {
            UnavailableReasonButton.ClearReason(fireButton);
        }
        else
        {
            UnavailableReasonButton.SetReason(fireButton, unavailableReason);
        }
        var visualFeedback = fireButton.GetComponent<UIButtonVisualFeedback>();
        if (visualFeedback != null)
        {
            visualFeedback.RefreshVisualState(true);
        }
    }

    private string GetLevelUpUnavailableReason(Hunter hunter, GoldManager gold)
    {
        if (hunter == null) return "Select a hunter first.";
        int xpForNext = hunter.GetXPToNextLevel();
        if (hunter.IsAtMaxLevel() || xpForNext == int.MaxValue) return "This hunter is already at max level.";
        int requiredReputation = hunter.GetRequiredReputationForNextLevel();
        int currentReputation = GetCurrentReputation();
        if (requiredReputation > currentReputation) return $"Requires Reputation {requiredReputation}. Current: {currentReputation}.";
        int xpNeeded = Mathf.Max(0, xpForNext - hunter.GetXP());
        if (!hunter.HasEnoughXPForNextLevel()) return $"Needs {xpNeeded} more XP.";

        int cost = hunter.GetLevelUpCost();
        int currentGold = gold != null ? gold.GetGold() : 0;
        if (currentGold < cost) return $"Needs {cost} gold. You have {currentGold}.";

        return "Cannot level up this hunter right now.";
    }

    private int GetCurrentReputation()
    {
        ReputationManager reputation = GameManager.Instance != null ? GameManager.Instance.GetReputationManager() : null;
        return reputation != null ? reputation.GetReputation() : 0;
    }

    private string GetFireUnavailableReason(HunterManager manager)
    {
        if (selectedHunter == null) return "Select a hunter first.";
        if (manager == null) return "Hunter manager is not ready.";
        if (selectedHunter.GetState() == HunterState.OnMission) return "Hunters on orders cannot be fired.";
        if (selectedHunter.GetState() == HunterState.Dead) return "Dead hunters cannot be fired.";
        if (selectedHunter.GetState() == HunterState.Candidate) return "Candidates cannot be fired.";
        return "This hunter cannot be fired right now.";
    }
}
