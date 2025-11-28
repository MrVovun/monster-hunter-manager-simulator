using UnityEngine;
using UnityEngine.UI;

public class WarTableUI : MonoBehaviour
{
    [Header("Tab Buttons")]
    [SerializeField] private Button ordersTabButton;
    [SerializeField] private Button huntersTabButton;
    [SerializeField] private Button economyTabButton;
    [SerializeField] private Button statisticsTabButton;
    
    [Header("Tab Panels")]
    [SerializeField] private GameObject ordersTabPanel;
    [SerializeField] private GameObject huntersTabPanel;
    [SerializeField] private GameObject economyTabPanel;
    [SerializeField] private GameObject statisticsTabPanel;
    
    [Header("Tab Components")]
    [SerializeField] private OrdersTab ordersTab;
    [SerializeField] private HuntersTab huntersTab;
    [SerializeField] private EconomyTab economyTab;
    [SerializeField] private StatisticsTab statisticsTab;

    [Header("Auto Refresh")]
    [SerializeField] private float refreshIntervalSeconds = 0.5f;
    private float refreshTimer = 0f;
    
    private int currentTabIndex = 0;
    private CursorLockMode previousLockState;
    private bool previousCursorVisible;
    private bool ordersDirty;
    private bool huntersDirty;
    private bool economyDirty;
    private bool statisticsDirty;

    private OrderManager orderManager;
    private HunterManager hunterManager;
    private GoldManager goldManager;
    private ReputationManager reputationManager;
    
    private void Awake()
    {
        // Find components if not assigned
        if (ordersTab == null) ordersTab = GetComponentInChildren<OrdersTab>();
        if (huntersTab == null) huntersTab = GetComponentInChildren<HuntersTab>();
        if (economyTab == null) economyTab = GetComponentInChildren<EconomyTab>();
        if (statisticsTab == null) statisticsTab = GetComponentInChildren<StatisticsTab>();
        
        // Set up tab buttons
        if (ordersTabButton != null)
            ordersTabButton.onClick.AddListener(() => SwitchTab(0));
        if (huntersTabButton != null)
            huntersTabButton.onClick.AddListener(() => SwitchTab(1));
        if (economyTabButton != null)
            economyTabButton.onClick.AddListener(() => SwitchTab(2));
        if (statisticsTabButton != null)
            statisticsTabButton.onClick.AddListener(() => SwitchTab(3));
    }
    
    private void Start()
    {
        SwitchTab(0); // Start with Orders tab
    }
    
    private void OnEnable()
    {
        RememberCursor();
        UnlockCursor();
        SubscribeToDataSources();
        MarkAllTabsDirty();
        RefreshDirtyTabs();
        refreshTimer = refreshIntervalSeconds;
    }

    private void Update()
    {
        if (!isActiveAndEnabled) return;

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f)
        {
            RefreshDirtyTabs();
            refreshTimer = Mathf.Max(0.05f, refreshIntervalSeconds);
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromDataSources();
        RestoreCursor();
    }

    private void OnDestroy()
    {
        UnsubscribeFromDataSources();
    }
    
    public void SwitchTab(int tabIndex)
    {
        int previousTab = currentTabIndex;
        currentTabIndex = tabIndex;

        if (previousTab == 0 && tabIndex != 0)
        {
            ordersTab?.OnTabDeselected();
        }
        
        // Hide all panels
        if (ordersTabPanel != null) ordersTabPanel.SetActive(tabIndex == 0);
        if (huntersTabPanel != null) huntersTabPanel.SetActive(tabIndex == 1);
        if (economyTabPanel != null) economyTabPanel.SetActive(tabIndex == 2);
        if (statisticsTabPanel != null) statisticsTabPanel.SetActive(tabIndex == 3);
        
        // Refresh active tab
        switch (tabIndex)
        {
            case 0:
                ordersTab?.Refresh();
                break;
            case 1:
                huntersTab?.Refresh();
                break;
            case 2:
                economyTab?.Refresh();
                break;
            case 3:
                statisticsTab?.Refresh();
                break;
        }
    }
    
    public void RefreshAllTabs()
    {
        MarkAllTabsDirty();
        RefreshDirtyTabs();
    }
    
    public void CloseUI()
    {
        RestoreCursor();
        Interactable.ReleaseActiveLock();
        gameObject.SetActive(false);
    }

    private void SubscribeToDataSources()
    {
        UnsubscribeFromDataSources();

        if (GameManager.Instance == null) return;

        orderManager = GameManager.Instance.GetOrderManager();
        hunterManager = GameManager.Instance.GetHunterManager();
        goldManager = GameManager.Instance.GetGoldManager();
        reputationManager = GameManager.Instance.GetReputationManager();

        if (orderManager != null)
        {
            orderManager.OnOrdersChanged += HandleOrdersChanged;
        }

        if (hunterManager != null)
        {
            hunterManager.OnHuntersChanged += HandleHuntersChanged;
        }

        if (goldManager != null)
        {
            goldManager.OnGoldChanged += HandleGoldChanged;
        }

        if (reputationManager != null)
        {
            reputationManager.OnReputationChanged += HandleReputationChanged;
        }
    }

    private void UnsubscribeFromDataSources()
    {
        if (orderManager != null)
        {
            orderManager.OnOrdersChanged -= HandleOrdersChanged;
            orderManager = null;
        }

        if (hunterManager != null)
        {
            hunterManager.OnHuntersChanged -= HandleHuntersChanged;
            hunterManager = null;
        }

        if (goldManager != null)
        {
            goldManager.OnGoldChanged -= HandleGoldChanged;
            goldManager = null;
        }

        if (reputationManager != null)
        {
            reputationManager.OnReputationChanged -= HandleReputationChanged;
            reputationManager = null;
        }
    }

    private void HandleOrdersChanged()
    {
        ordersDirty = true;
        huntersDirty = true;
        economyDirty = true;
        statisticsDirty = true;
        TryRefreshDirtyTabsImmediately();
    }

    private void HandleHuntersChanged()
    {
        huntersDirty = true;
        economyDirty = true;
        TryRefreshDirtyTabsImmediately();
    }

    private void HandleGoldChanged(int _)
    {
        economyDirty = true;
        TryRefreshDirtyTabsImmediately();
    }

    private void HandleReputationChanged(int _)
    {
        economyDirty = true;
        huntersDirty = true;
        TryRefreshDirtyTabsImmediately();
    }

    private void MarkAllTabsDirty()
    {
        ordersDirty = true;
        huntersDirty = true;
        economyDirty = true;
        statisticsDirty = true;
    }

    private void RefreshDirtyTabs()
    {
        if (ordersDirty)
        {
            ordersTab?.Refresh();
            ordersDirty = false;
        }

        if (huntersDirty)
        {
            huntersTab?.Refresh();
            huntersDirty = false;
        }

        if (economyDirty)
        {
            economyTab?.Refresh();
            economyDirty = false;
        }

        if (statisticsDirty)
        {
            statisticsTab?.Refresh();
            statisticsDirty = false;
        }
    }

    private void TryRefreshDirtyTabsImmediately()
    {
        if (!isActiveAndEnabled) return;

        RefreshDirtyTabs();
        refreshTimer = Mathf.Max(0.05f, refreshIntervalSeconds);
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
