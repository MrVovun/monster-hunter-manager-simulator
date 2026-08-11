using UnityEngine;
using UnityEngine.UI;

public class WarTableUI : MonoBehaviour
{
    [Header("Tab Buttons")]
    [SerializeField] private Button ordersTabButton;
    [SerializeField] private Button huntersTabButton;
    [SerializeField] private Button economyTabButton;
    [SerializeField] private Button statisticsTabButton;
    [SerializeField] private Button hiringTabButton;
    
    [Header("Tab Panels")]
    [SerializeField] private GameObject ordersTabPanel;
    [SerializeField] private GameObject huntersTabPanel;
    [SerializeField] private GameObject economyTabPanel;
    [SerializeField] private GameObject statisticsTabPanel;
    [SerializeField] private GameObject hiringTabPanel;
    
    [Header("Tab Components")]
    [SerializeField] private OrdersTab ordersTab;
    [SerializeField] private HuntersTab huntersTab;
    [SerializeField] private EconomyTab economyTab;
    [SerializeField] private StatisticsTab statisticsTab;
    [SerializeField] private HiringTab hiringTab;

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
    private bool hiringDirty;

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
        if (hiringTab == null) hiringTab = GetComponentInChildren<HiringTab>();
        
        // Set up tab buttons
        if (ordersTabButton != null)
            ordersTabButton.onClick.AddListener(() => SwitchTab(0));
        if (huntersTabButton != null)
            huntersTabButton.onClick.AddListener(() => SwitchTab(1));
        if (economyTabButton != null)
            economyTabButton.onClick.AddListener(() => SwitchTab(2));
        if (statisticsTabButton != null)
            statisticsTabButton.onClick.AddListener(() => SwitchTab(3));
        if (hiringTabButton != null)
            hiringTabButton.onClick.AddListener(() => SwitchTab(4));
    }
    
    private void Start()
    {
        SwitchTab(0); // Start with Orders tab
    }
    
    private void OnEnable()
    {
        TutorialManager.OnTutorialGateChanged += RefreshTabButtonTutorialGates;
        RememberCursor();
        UnlockCursor();
        SubscribeToDataSources();
        MarkAllTabsDirty();
        RefreshDirtyTabs();
        RefreshTabButtonTutorialGates();
        refreshTimer = refreshIntervalSeconds;
        TutorialManager.ReportEvent(TutorialIds.EventWarTableOpened);
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
        TutorialManager.OnTutorialGateChanged -= RefreshTabButtonTutorialGates;
        ordersTab?.ClearSelection();
        huntersTab?.ClearSelection();
        UnsubscribeFromDataSources();
        RestoreCursor();
    }

    private void OnDestroy()
    {
        UnsubscribeFromDataSources();
    }
    
    public void SwitchTab(int tabIndex)
    {
        if (!IsTabAllowed(tabIndex))
        {
            return;
        }

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
        if (hiringTabPanel != null) hiringTabPanel.SetActive(tabIndex == 4);
        
        // Refresh active tab
        switch (tabIndex)
        {
            case 0:
                ordersTab?.Refresh();
                TutorialManager.ReportEvent(TutorialIds.EventOrdersTabOpened);
                break;
            case 1:
                huntersTab?.Refresh();
                TutorialManager.ReportEvent(TutorialIds.EventHuntersTabOpened);
                break;
            case 2:
                economyTab?.Refresh();
                break;
            case 3:
                statisticsTab?.Refresh();
                break;
            case 4:
                hiringTab?.Refresh();
                TutorialManager.ReportEvent(TutorialIds.EventHiringTabOpened);
                break;
        }
        RefreshTabButtonTutorialGates();
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
            goldManager.OnDebtChanged += HandleDebtChanged;
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
            goldManager.OnDebtChanged -= HandleDebtChanged;
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
        hiringDirty = true;
        TryRefreshDirtyTabsImmediately();
    }

    private void HandleHuntersChanged()
    {
        huntersDirty = true;
        economyDirty = true;
        hiringDirty = true;
        TryRefreshDirtyTabsImmediately();
    }

    private void HandleGoldChanged(int _)
    {
        huntersDirty = true;
        economyDirty = true;
        hiringDirty = true;
        TryRefreshDirtyTabsImmediately();
    }

    private void HandleDebtChanged(int _)
    {
        economyDirty = true;
        hiringDirty = true;
        TryRefreshDirtyTabsImmediately();
    }

    private void HandleReputationChanged(float _)
    {
        economyDirty = true;
        huntersDirty = true;
        hiringDirty = true;
        TryRefreshDirtyTabsImmediately();
    }

    private void MarkAllTabsDirty()
    {
        ordersDirty = true;
        huntersDirty = true;
        economyDirty = true;
        statisticsDirty = true;
        hiringDirty = true;
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

        if (hiringDirty)
        {
            hiringTab?.Refresh();
            hiringDirty = false;
        }
    }

    private bool IsTabAllowed(int tabIndex)
    {
        return TutorialManager.IsActionAllowed(GetTabActionId(tabIndex));
    }

    private string GetTabActionId(int tabIndex)
    {
        switch (tabIndex)
        {
            case 0:
                return TutorialIds.OrdersTab;
            case 1:
                return TutorialIds.HuntersTab;
            case 4:
                return TutorialIds.HiringTab;
            default:
                return string.Empty;
        }
    }

    private void RefreshTabButtonTutorialGates()
    {
        SetTabButtonAllowed(ordersTabButton, 0);
        SetTabButtonAllowed(huntersTabButton, 1);
        SetTabButtonAllowed(hiringTabButton, 4);
    }

    private void SetTabButtonAllowed(Button button, int tabIndex)
    {
        if (button == null) return;
        bool allowed = IsTabAllowed(tabIndex);
        button.interactable = allowed;
        if (allowed)
        {
            UnavailableReasonButton.ClearReason(button);
        }
        else
        {
            UnavailableReasonButton.SetReason(button, "Unavailable during the current tutorial step.");
        }
        var visualFeedback = button.GetComponent<UIButtonVisualFeedback>();
        if (visualFeedback != null)
        {
            visualFeedback.RefreshVisualState(true);
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
