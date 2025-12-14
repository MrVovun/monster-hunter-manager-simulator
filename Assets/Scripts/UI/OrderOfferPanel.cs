using System;
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
    [SerializeField] private Button selectMonsterButton;
    [SerializeField] private TMP_Text declarationHintText;
    [SerializeField] private TMP_Text declaredMonsterText;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button referButton;

    private Order currentOrder;
    private InvestigationManager activeInvestigation;
    private CursorLockMode previousLockState;
    private bool previousCursorVisible;
    private Action onHiddenAction;
    private Action onBackAction;
    private bool backRequested;

    public event Action<OrderOfferPanel> OnPanelHidden;

    public void Show(Order order, Action onHidden = null, Action onBack = null, InvestigationManager investigation = null)
    {
        currentOrder = order;
        activeInvestigation = investigation;
        onHiddenAction = onHidden;
        onBackAction = onBack;
        backRequested = false;
        ConfigureBackButton();
        ConfigureSelectButton();
        RememberCursor();
        UnlockCursor();
        SetRootActive(true);
        UpdateUI();
        UpdateDeclaredMonsterUI();
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

    private void ConfigureSelectButton()
    {
        if (selectMonsterButton == null) return;
        bool canSelect = activeInvestigation != null;
        selectMonsterButton.interactable = canSelect;
        if (!canSelect) return;
        selectMonsterButton.onClick.RemoveAllListeners();
        selectMonsterButton.onClick.AddListener(HandleSelectMonsterPressed);
    }

    private void HandleSelectMonsterPressed()
    {
        if (activeInvestigation == null) return;
        activeInvestigation.ShowBestiaryForDeclaration(monster =>
        {
            if (currentOrder != null)
            {
                currentOrder.declaredMonster = monster;
                UpdateDeclaredMonsterUI();
                UpdateActionButtons();
                UpdateUI(); // refresh monster name
            }
        },
        null);
    }

    public void Hide()
    {
        var savedOrder = currentOrder;
        SetRootActive(false);
        activeInvestigation = null;
        RestoreCursor();
        OnPanelHidden?.Invoke(this);
        if (backRequested)
        {
            onBackAction?.Invoke();
        }
        else
        {
            onHiddenAction?.Invoke();
        }
        currentOrder = null;
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

    private void UpdateDeclaredMonsterUI()
    {
        string declaredName = currentOrder != null && currentOrder.declaredMonster != null
            ? currentOrder.declaredMonster.displayName
            : "???";

        if (declaredMonsterText != null)
        {
            declaredMonsterText.text = declaredName;
        }

        if (monsterText != null)
        {
            monsterText.text = declaredName;
        }

        if (declarationHintText != null)
        {
            declarationHintText.text = currentOrder != null && currentOrder.declaredMonster != null
                ? $"Declared: {declaredName}"
                : "Select a monster before accepting or referring.";
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
