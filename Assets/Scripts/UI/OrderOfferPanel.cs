using System;
using System.Text;
using TMPro;
using UnityEngine;

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

    private Order currentOrder;
    private CursorLockMode previousLockState;
    private bool previousCursorVisible;

    public event Action<OrderOfferPanel> OnPanelHidden;

    public void Show(Order order)
    {
        currentOrder = order;
        RememberCursor();
        UnlockCursor();
        SetRootActive(true);
        UpdateUI();
    }

    public void Hide()
    {
        SetRootActive(false);
        currentOrder = null;
        RestoreCursor();
        OnPanelHidden?.Invoke(this);
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
            monsterText.text = monsterName;
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

    private OrderManager GetOrderManager()
    {
        return GameManager.Instance != null ? GameManager.Instance.GetOrderManager() : null;
    }

    public void AcceptOrder()
    {
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
