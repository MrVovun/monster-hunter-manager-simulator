using System;
using System.Collections.Generic;
using System.Linq;
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
    [Header("Revealed Traits")]
    [SerializeField] private Transform revealedTraitsParent;
    [SerializeField] private GameObject traitItemPrefab;
    [SerializeField] private Image traitIconPrototype;
    [SerializeField] private Button backButton;
    [Header("Monster Declaration")]
    [SerializeField] private Button selectMonsterButton;
    [SerializeField] private TMP_Text declarationHintText;
    [SerializeField] private TMP_Text declaredMonsterText;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button referButton;
    [Header("Monster Visuals")]
    [SerializeField] private Image declaredMonsterPortrait;

    private Order currentOrder;
    private InvestigationManager activeInvestigation;
    private CursorLockMode previousLockState;
    private bool previousCursorVisible;
    private Action onHiddenAction;
    private Action onBackAction;
    private bool backRequested;
    private readonly List<GameObject> spawnedTraitItems = new List<GameObject>();

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
        UpdateDeclaredMonsterPortrait();
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
        ClearRevealedTraitItems();
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
        if (descriptionText != null)
        {
            descriptionText.text = currentOrder.GetDescriptionFor(Order.DescriptionAudience.DeclaredMonster);
        }

        string monsterName = currentOrder.GetDeclaredOrGenericMonsterName();

        if (statsText != null)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Monster: {monsterName}");
            sb.AppendLine($"Difficulty: {currentOrder.difficulty}");
            sb.AppendLine($"Reward: {currentOrder.goldReward}g / {currentOrder.xpReward}xp");
            sb.AppendLine($"Party Size: {currentOrder.minPartySize} - {currentOrder.maxPartySize}");
            sb.AppendLine($"Prep Time: {currentOrder.prepTimeLimit:0}s");
            statsText.text = sb.ToString();
        }

        if (monsterText != null)
        {
            monsterText.text = string.IsNullOrEmpty(monsterName) ? "monster" : monsterName;
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

        UpdateRevealedTraitsUI();
    }

    private void UpdateDeclaredMonsterUI()
    {
        string declaredName = currentOrder != null ? currentOrder.GetDeclaredOrGenericMonsterName() : "monster";

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

        UpdateDeclaredMonsterPortrait();
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

    private void UpdateRevealedTraitsUI()
    {
        ClearRevealedTraitItems();

        if (revealedTraitsParent == null)
        {
            return;
        }

        var caseData = currentOrder?.investigationCase;
        if (caseData == null || caseData.confirmedTraitIds == null || caseData.confirmedTraitIds.Count == 0)
        {
            revealedTraitsParent.gameObject.SetActive(false);
            return;
        }

        List<MonsterTrait> traits = new List<MonsterTrait>();
        foreach (var traitId in caseData.confirmedTraitIds)
        {
            if (string.IsNullOrEmpty(traitId)) continue;
            var trait = caseData.truthTraits?.FirstOrDefault(t => t != null && string.Equals(t.traitId, traitId, StringComparison.OrdinalIgnoreCase));
            if (trait != null)
            {
                traits.Add(trait);
            }
        }

        if (traits.Count == 0)
        {
            revealedTraitsParent.gameObject.SetActive(false);
            return;
        }

        revealedTraitsParent.gameObject.SetActive(true);
        foreach (var trait in traits)
        {
            var item = CreateTraitItem(trait);
            if (item == null) continue;
            item.transform.SetParent(revealedTraitsParent, false);
            spawnedTraitItems.Add(item);
        }
    }

    private void ClearRevealedTraitItems()
    {
        foreach (var item in spawnedTraitItems)
        {
            if (item != null)
            {
                Destroy(item);
            }
        }
        spawnedTraitItems.Clear();

        if (revealedTraitsParent != null)
        {
            foreach (Transform child in revealedTraitsParent)
            {
                Destroy(child.gameObject);
            }
            revealedTraitsParent.gameObject.SetActive(false);
        }
    }

    private GameObject CreateTraitItem(MonsterTrait trait)
    {
        GameObject item = traitItemPrefab != null ? Instantiate(traitItemPrefab) : new GameObject("RevealedTrait");
        RectTransform rect = item.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = item.AddComponent<RectTransform>();
        }

        TMP_Text text = item.GetComponentInChildren<TMP_Text>();
        if (text != null)
        {
            text.text = string.Empty;
            text.gameObject.SetActive(false);
        }

        Image icon = FindOrCreateTraitIcon(item);
        if (icon != null)
        {
            Sprite sprite = trait != null ? trait.icon : null;
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        if (traitTooltipPanel != null)
        {
            var tooltip = item.GetComponent<TraitTooltipTrigger>();
            if (tooltip == null)
            {
                tooltip = item.AddComponent<TraitTooltipTrigger>();
            }
            tooltip.Initialize(traitTooltipPanel, rect, trait != null ? trait.displayName : "Trait", trait != null ? trait.description : string.Empty);
        }

        return item;
    }

    private void UpdateDeclaredMonsterPortrait()
    {
        if (declaredMonsterPortrait == null) return;

        Sprite sprite = currentOrder?.declaredMonster != null ? currentOrder.declaredMonster.portrait : null;
        declaredMonsterPortrait.sprite = sprite;
        declaredMonsterPortrait.enabled = sprite != null;
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

    private Image FindOrCreateTraitIcon(GameObject item)
    {
        if (item == null) return null;

        Image icon = null;
        var images = item.GetComponentsInChildren<Image>(true);
        foreach (var candidate in images)
        {
            if (candidate == null) continue;
            if (candidate.transform == item.transform && item.GetComponent<Button>() != null)
            {
                continue;
            }
            icon = candidate;
            break;
        }

        if (icon == null && traitIconPrototype != null)
        {
            icon = Instantiate(traitIconPrototype, item.transform);
        }

        if (icon == null)
        {
            icon = item.GetComponent<Image>();
            if (icon == null)
            {
                icon = item.AddComponent<Image>();
            }
        }

        return icon;
    }
}
