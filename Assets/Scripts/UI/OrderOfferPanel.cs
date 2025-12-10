using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OrderOfferPanel : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text statsText;
    [Header("Separated Stats")]
    [SerializeField] private TMP_Text monsterText;
    [SerializeField] private TMP_Text difficultyText;
    [SerializeField] private TMP_Text rewardGoldText;
    [SerializeField] private TMP_Text rewardXPText;
    [SerializeField] private TMP_Text partySizeText;
    [SerializeField] private TMP_Text prepTimeText;
    [SerializeField] private TraitTooltipPanel traitTooltipPanel;
    [SerializeField] private Button backButton;
    [Header("Monster Declaration")]
    [SerializeField] private TMP_Dropdown monsterDropdown;
    [SerializeField] private TMP_Text declarationHintText;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button referButton;

    private Order currentOrder;
    private InvestigationManager activeInvestigation;
    private CursorLockMode previousLockState;
    private bool previousCursorVisible;
    private Action onHiddenAction;
    private Action onBackAction;
    private bool backRequested;
    private readonly List<InvestigationManager.MonsterCandidate> cachedCandidates = new List<InvestigationManager.MonsterCandidate>();

    public event Action<OrderOfferPanel> OnPanelHidden;

    public void Show(Order order, Action onHidden = null, Action onBack = null, InvestigationManager investigation = null)
    {
        currentOrder = order;
        activeInvestigation = investigation;
        onHiddenAction = onHidden;
        onBackAction = onBack;
        backRequested = false;
        ConfigureBackButton();
        RememberCursor();
        UnlockCursor();
        SetRootActive(true);
        UpdateUI();
        PopulateMonsterDropdown();
        UpdateActionButtons();
    }

    private void ConfigureBackButton()
    {
        if (backButton == null) return;
        bool enableBack = onBackAction != null;
        backButton.gameObject.SetActive(enableBack);
        backButton.onClick.RemoveAllListeners();
        if (enableBack)
        {
            backButton.onClick.AddListener(HandleBackPressed);
        }
    }

    private void HandleBackPressed()
    {
        backRequested = true;
        Hide();
    }

    public void Hide()
    {
        var savedOrder = currentOrder;
        SetRootActive(false);
        RestoreCursor();
        OnPanelHidden?.Invoke(this);
        if (backRequested)
        {
            currentOrder = savedOrder;
            onBackAction?.Invoke();
        }
        else
        {
            currentOrder = null;
            onHiddenAction?.Invoke();
        }
        onHiddenAction = null;
        onBackAction = null;
        backRequested = false;
    }

    private void SetRootActive(bool active)
    {
        GameObject root = panelRoot != null ? panelRoot : gameObject;
        if (root != null)
        {
            root.SetActive(active);
        }
    }

    private void UpdateUI()
    {
        if (currentOrder == null) return;

        if (titleText != null) titleText.text = currentOrder.orderTitle;
        if (descriptionText != null) descriptionText.text = currentOrder.description;

        string monsterName = currentOrder.GetMonsterName();

        if (statsText != null)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Monster: {monsterName}");
            sb.AppendLine($"Difficulty: {currentOrder.difficulty}");
            sb.AppendLine($"Reward: {currentOrder.goldReward}g / {currentOrder.xpReward}xp");
            sb.AppendLine($"Party Size: {currentOrder.minPartySize}-{currentOrder.maxPartySize}");
            sb.AppendLine($"Prep Time: {currentOrder.prepTimeLimit:0}s");
            statsText.text = sb.ToString();
        }

        if (monsterText != null)
        {
            monsterText.text = string.IsNullOrEmpty(monsterName) ? "???" : monsterName;
        }

        if (difficultyText != null)
        {
            difficultyText.text = currentOrder.difficulty.ToString();
        }

        if (rewardGoldText != null)
        {
            rewardGoldText.text = $"{currentOrder.goldReward} gold";
        }
        if (rewardXPText != null)
        {
            rewardXPText.text = $"{currentOrder.xpReward} XP";
        }

        if (partySizeText != null)
        {
            partySizeText.text = $"{currentOrder.minPartySize}-{currentOrder.maxPartySize}";
        }

        if (prepTimeText != null)
        {
            prepTimeText.text = $"{currentOrder.prepTimeLimit:0}s";
        }
    }

    private void PopulateMonsterDropdown()
    {
        if (monsterDropdown == null) return;

        monsterDropdown.onValueChanged.RemoveAllListeners();
        monsterDropdown.ClearOptions();
        cachedCandidates.Clear();

        if (activeInvestigation != null)
        {
            cachedCandidates.AddRange(activeInvestigation.GetMonsterCandidates());
        }
        else
        {
            var library = GameManager.Instance?.GetGameConfig()?.monsterLibrary;
            if (library != null)
            {
                foreach (var monster in library.GetMonsters())
                {
                    cachedCandidates.Add(new InvestigationManager.MonsterCandidate
                    {
                        monster = monster,
                        confidence = 0f
                    });
                }
            }
        }

        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();
        options.Add(new TMP_Dropdown.OptionData("Select monster..."));

        foreach (var candidate in cachedCandidates)
        {
            string label = candidate.monster != null ? candidate.monster.displayName : "Unknown";
            if (candidate.confidence > 0f)
            {
                label += $" - {(candidate.confidence * 100f):0}%";
            }
            options.Add(new TMP_Dropdown.OptionData(label));
        }

        monsterDropdown.AddOptions(options);

        int selectedIndex = 0;
        if (currentOrder != null && currentOrder.declaredMonster != null)
        {
            for (int i = 0; i < cachedCandidates.Count; i++)
            {
                if (cachedCandidates[i].monster == currentOrder.declaredMonster)
                {
                    selectedIndex = i + 1;
                    break;
                }
            }
        }
        monsterDropdown.value = selectedIndex;
        monsterDropdown.onValueChanged.AddListener(OnMonsterDropdownChanged);
        UpdateDeclarationHint();
    }

    private void OnMonsterDropdownChanged(int selection)
    {
        if (currentOrder == null) return;

        if (selection <= 0 || selection - 1 >= cachedCandidates.Count)
        {
            currentOrder.declaredMonster = null;
        }
        else
        {
            currentOrder.declaredMonster = cachedCandidates[selection - 1].monster;
        }

        UpdateDeclarationHint();
        UpdateActionButtons();
        // refresh monster text to show declared name
        if (monsterText != null)
        {
            monsterText.text = currentOrder.GetMonsterName();
        }
    }

    private void UpdateDeclarationHint()
    {
        if (declarationHintText == null) return;
        if (currentOrder != null && currentOrder.declaredMonster != null)
        {
            declarationHintText.text = $"Declared: {currentOrder.declaredMonster.displayName}";
        }
        else
        {
            declarationHintText.text = "Declare the monster before accepting or referring.";
        }
    }

    private void UpdateActionButtons()
    {
        bool declarationValid = currentOrder != null && currentOrder.declaredMonster != null;
        if (acceptButton != null)
        {
            acceptButton.interactable = declarationValid;
        }
        if (referButton != null)
        {
            referButton.interactable = declarationValid;
        }
    }

    private OrderManager GetOrderManager()
    {
        return GameManager.Instance != null ? GameManager.Instance.GetOrderManager() : null;
    }

    public void AcceptOrder()
    {
        if (currentOrder == null || currentOrder.declaredMonster == null)
        {
            Debug.LogWarning("OrderOfferPanel: Cannot accept without declaring a monster.");
            return;
        }
        OrderManager manager = GetOrderManager();
        if (manager != null && currentOrder != null)
        {
            manager.AcceptOrder(currentOrder);
        }
        Hide();
    }

    public void DeclineOrder()
    {
        OrderManager manager = GetOrderManager();
        if (manager != null && currentOrder != null)
        {
            manager.DeclineOrder(currentOrder);
        }
        Hide();
    }

    public void ReferOrder()
    {
        if (currentOrder == null || currentOrder.declaredMonster == null)
        {
            Debug.LogWarning("OrderOfferPanel: Cannot refer without declaring a monster.");
            return;
        }
        OrderManager manager = GetOrderManager();
        if (manager != null && currentOrder != null)
        {
            manager.ReferOrder(currentOrder);
        }
        Hide();
    }

    private void RememberCursor()
    {
        previousLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestoreCursor()
    {
        Cursor.lockState = previousLockState;
        Cursor.visible = previousCursorVisible;
    }
}
