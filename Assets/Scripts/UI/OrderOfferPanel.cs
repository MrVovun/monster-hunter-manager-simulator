using System.Text;
using TMPro;
using UnityEngine;

public class OrderOfferPanel : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text statsText;

    private Order currentOrder;

    public void Show(Order order)
    {
        currentOrder = order;
        SetRootActive(true);
        UpdateUI();
    }

    public void Hide()
    {
        SetRootActive(false);
        currentOrder = null;
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

        if (statsText != null)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Monster: {currentOrder.monsterType}");
            sb.AppendLine($"Difficulty: {currentOrder.difficulty}");
            sb.AppendLine($"Reward: {currentOrder.goldReward}g / {currentOrder.xpReward}xp");
            sb.AppendLine($"Party Size: {currentOrder.minPartySize}-{currentOrder.maxPartySize}");
            sb.AppendLine($"Prep Time: {currentOrder.prepTimeLimit:0}s");
            statsText.text = sb.ToString();
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
}
