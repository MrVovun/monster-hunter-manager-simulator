using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OrdersTab : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform ordersListParent;
    [SerializeField] private GameObject orderItemPrefab;
    [SerializeField] private OrderDetailPanel orderDetailPanel;
    [SerializeField] private Button cancelOrderButton;
    
    [Header("Hunter Roster")]
    [SerializeField] private Transform hunterRosterParent;
    [SerializeField] private HunterRosterItem hunterRosterItemPrefab;
    
    private System.Collections.Generic.List<Order> activeOrders = new System.Collections.Generic.List<Order>();
    private Order selectedOrder;
    private readonly List<OrderListItem> orderItemInstances = new List<OrderListItem>();
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

        if (cancelOrderButton != null)
        {
            cancelOrderButton.onClick.RemoveListener(CancelSelectedOrder);
            cancelOrderButton.onClick.AddListener(CancelSelectedOrder);
        }

        hunterManager = GameManager.Instance != null ? GameManager.Instance.GetHunterManager() : null;
        if (hunterManager != null)
        {
            hunterManager.OnHuntersChanged += HandleHuntersChanged;
        }

        UpdateCancelOrderButtonState();
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
        orderItemInstances.Clear();
        
        // Get active orders
        OrderManager orderManager = GameManager.Instance != null ? 
            GameManager.Instance.GetOrderManager() : null;
        
        if (orderManager == null) return;
        
        activeOrders = orderManager.GetActiveOrders();
        if (selectedOrder != null && !activeOrders.Contains(selectedOrder))
        {
            ClearSelection();
        }
        
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
        orderItemInstances.Add(item);
    }
    
    public void SelectOrder(Order order)
    {
        selectedOrder = order;
        if (orderDetailPanel != null)
        {
            orderDetailPanel.ShowOrder(order);
        }
        RefreshHunterRosterStates();
        UpdateCancelOrderButtonState();
        RefreshOrderListSelection();
    }

    public void ClearSelection()
    {
        selectedOrder = null;
        orderDetailPanel?.ClearSelection();
        UpdateCancelOrderButtonState();
        RefreshOrderListSelection();
    }

    public void CancelSelectedOrder()
    {
        OrderManager orderManager = GameManager.Instance != null ? GameManager.Instance.GetOrderManager() : null;
        if (orderManager == null || selectedOrder == null) return;

        if (orderManager.CancelOrder(selectedOrder))
        {
            selectedOrder = null;
            Refresh();
            orderDetailPanel?.ClearSelection();
        }
        else
        {
            UpdateCancelOrderButtonState();
        }
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

    private void Update()
    {
        for (int i = 0; i < orderItemInstances.Count; i++)
        {
            if (orderItemInstances[i] != null)
            {
                orderItemInstances[i].RefreshLiveState();
            }
        }
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
        UpdateCancelOrderButtonState();
        RefreshOrderListSelection();
    }

    internal void ForceRosterStateRefresh()
    {
        RefreshHunterRosterStates();
    }

    public void OnTabDeselected()
    {
        orderDetailPanel?.ClearParty();
    }

    private void UpdateCancelOrderButtonState()
    {
        if (cancelOrderButton == null) return;

        OrderManager orderManager = GameManager.Instance != null ? GameManager.Instance.GetOrderManager() : null;
        bool canCancel = orderManager != null && orderManager.CanCancelOrder(selectedOrder);
        cancelOrderButton.interactable = canCancel;
        var visualFeedback = cancelOrderButton.GetComponent<UIButtonVisualFeedback>();
        if (visualFeedback != null)
        {
            visualFeedback.RefreshVisualState(true);
        }
    }

    private void RefreshOrderListSelection()
    {
        for (int i = 0; i < orderItemInstances.Count; i++)
        {
            if (orderItemInstances[i] != null)
            {
                orderItemInstances[i].SetSelected(orderItemInstances[i].Order == selectedOrder);
            }
        }
    }
}
