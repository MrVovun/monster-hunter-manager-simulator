using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OrdersTab : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform ordersListParent;
    [SerializeField] private GameObject orderItemPrefab;
    [SerializeField] private OrderDetailPanel orderDetailPanel;
    
    [Header("Hunter Roster")]
    [SerializeField] private Transform hunterRosterParent;
    [SerializeField] private HunterRosterItem hunterRosterItemPrefab;
    
    private System.Collections.Generic.List<Order> activeOrders = new System.Collections.Generic.List<Order>();
    private Order selectedOrder;
    private readonly System.Collections.Generic.List<HunterRosterItem> rosterItems =
        new System.Collections.Generic.List<HunterRosterItem>();
    private HunterManager hunterManager;
    private bool rosterDirty = true;
    
    private void Awake()
    {
        if (orderDetailPanel == null)
        {
            orderDetailPanel = GetComponentInChildren<OrderDetailPanel>();
        }
        
        if (orderDetailPanel != null)
        {
            orderDetailPanel.OnPartyChanged += HandlePartyChanged;
        }

        hunterManager = GameManager.Instance != null ? GameManager.Instance.GetHunterManager() : null;
        if (hunterManager != null)
        {
            hunterManager.OnHuntersChanged += HandleHuntersChanged;
        }
    }

    private void OnDestroy()
    {
        if (orderDetailPanel != null)
        {
            orderDetailPanel.OnPartyChanged -= HandlePartyChanged;
        }

        if (hunterManager != null)
        {
            hunterManager.OnHuntersChanged -= HandleHuntersChanged;
        }
    }
    
    public void Refresh()
    {
        UpdateOrdersList();
        RefreshHunterRoster();
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
        RefreshHunterRosterStates();
    }
    
    public Order GetSelectedOrder()
    {
        return selectedOrder;
    }

    private void RefreshHunterRoster()
    {
        if (hunterRosterParent == null || hunterRosterItemPrefab == null) return;

        if (rosterDirty)
        {
            foreach (Transform child in hunterRosterParent)
            {
                Destroy(child.gameObject);
            }
            rosterItems.Clear();

            if (hunterManager == null)
            {
                hunterManager = GameManager.Instance != null ? GameManager.Instance.GetHunterManager() : null;
            }

            if (hunterManager != null)
            {
                foreach (var hunter in hunterManager.GetAllHunters())
                {
                    if (hunter == null) continue;
                    if (hunter.GetState() == HunterState.Dead) continue;

                    HunterRosterItem entry = Instantiate(hunterRosterItemPrefab, hunterRosterParent);
                    entry.Initialize(hunter, this);
                    rosterItems.Add(entry);
                }
            }

            rosterDirty = false;
        }
        
        RefreshHunterRosterStates();
    }

    private void RefreshHunterRosterStates()
    {
        bool anyItem = false;
        foreach (var item in rosterItems)
        {
            if (item == null) continue;
            item.Refresh();
            anyItem = true;
        }

        if (anyItem)
        {
            ReorderRosterItems();
        }
    }

    internal bool IsHunterSelectable(Hunter hunter)
    {
        if (orderDetailPanel == null) return false;
        return orderDetailPanel.IsHunterSelectable(hunter);
    }

    internal bool IsHunterAssignedToParty(Hunter hunter)
    {
        if (orderDetailPanel == null) return false;
        return orderDetailPanel.IsHunterAssigned(hunter);
    }

    private void ReorderRosterItems()
    {
        if (hunterRosterParent == null) return;

        int insertIndex = 0;
        foreach (var item in rosterItems)
        {
            if (item == null) continue;
            if (!item.ShouldSortLast())
            {
                item.transform.SetSiblingIndex(insertIndex++);
            }
        }

        foreach (var item in rosterItems)
        {
            if (item == null) continue;
            if (item.ShouldSortLast())
            {
                item.transform.SetSiblingIndex(insertIndex++);
            }
        }
    }

    private void HandleHuntersChanged()
    {
        rosterDirty = true;
    }

    private void HandlePartyChanged()
    {
        RefreshHunterRosterStates();
    }

    internal void ForceRosterStateRefresh()
    {
        RefreshHunterRosterStates();
    }

    public void OnTabDeselected()
    {
        orderDetailPanel?.ClearParty();
    }
}

// Helper class for order list items
public class OrderListItem : MonoBehaviour
{
    private Order order;
    private OrdersTab parentTab;
    private TMP_Text orderText;
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
        orderText = GetComponentInChildren<TMP_Text>();
        if (orderText == null)
        {
            GameObject textObj = new GameObject("OrderText");
            textObj.transform.SetParent(transform, false);
            orderText = textObj.AddComponent<TextMeshProUGUI>();
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
