using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OrdersTab : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform ordersListParent;
    [SerializeField] private GameObject orderItemPrefab;
    [SerializeField] private OrderDetailPanel orderDetailPanel;
    
    private List<Order> activeOrders = new List<Order>();
    private Order selectedOrder;
    
    private void Awake()
    {
        if (orderDetailPanel == null)
        {
            orderDetailPanel = GetComponentInChildren<OrderDetailPanel>();
        }
    }
    
    public void Refresh()
    {
        UpdateOrdersList();
    }
    
    private void UpdateOrdersList()
    {
        if (ordersListParent == null) return;
        
        // Clear existing items
        foreach (Transform child in ordersListParent)
        {
            Destroy(child.gameObject);
        }
        
        // Get active orders
        OrderManager orderManager = GameManager.Instance != null ? 
            GameManager.Instance.GetOrderManager() : null;
        
        if (orderManager == null) return;
        
        activeOrders = orderManager.GetActiveOrders();
        
        // Create UI items for each order
        foreach (var order in activeOrders)
        {
            CreateOrderItem(order);
        }
    }
    
    private void CreateOrderItem(Order order)
    {
        if (orderItemPrefab == null || ordersListParent == null) return;
        
        GameObject itemObj = Instantiate(orderItemPrefab, ordersListParent);
        OrderListItem item = itemObj.GetComponent<OrderListItem>();
        if (item == null)
        {
            item = itemObj.AddComponent<OrderListItem>();
        }
        
        item.Initialize(order, this);
    }
    
    public void SelectOrder(Order order)
    {
        selectedOrder = order;
        if (orderDetailPanel != null)
        {
            orderDetailPanel.ShowOrder(order);
        }
    }
    
    public Order GetSelectedOrder()
    {
        return selectedOrder;
    }
}

// Helper class for order list items
public class OrderListItem : MonoBehaviour
{
    private Order order;
    private OrdersTab parentTab;
    private Text orderText;
    private Button button;
    
    public void Initialize(Order order, OrdersTab tab)
    {
        this.order = order;
        this.parentTab = tab;
        
        // Set up UI
        button = GetComponent<Button>();
        if (button == null)
        {
            button = gameObject.AddComponent<Button>();
        }
        
        button.onClick.AddListener(OnClicked);
        
        // Find or create text
        orderText = GetComponentInChildren<Text>();
        if (orderText == null)
        {
            GameObject textObj = new GameObject("OrderText");
            textObj.transform.SetParent(transform, false);
            orderText = textObj.AddComponent<Text>();
            orderText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            orderText.fontSize = 14;
            orderText.color = Color.white;
        }
        
        UpdateDisplay();
    }
    
    private void UpdateDisplay()
    {
        if (orderText == null || order == null) return;
        
        string status = order.state == OrderState.Accepted ? "Awaiting Party" : "In Progress";
        orderText.text = $"{order.orderTitle} - {status} ({order.GetAssignedPartySize()}/{order.maxPartySize})";
    }
    
    private void OnClicked()
    {
        parentTab?.SelectOrder(order);
    }
}

