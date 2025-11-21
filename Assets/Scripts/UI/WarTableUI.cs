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
    
    private int currentTabIndex = 0;
    
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
        RefreshAllTabs();
    }
    
    public void SwitchTab(int tabIndex)
    {
        currentTabIndex = tabIndex;
        
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
        ordersTab?.Refresh();
        huntersTab?.Refresh();
        economyTab?.Refresh();
        statisticsTab?.Refresh();
    }
    
    public void CloseUI()
    {
        gameObject.SetActive(false);
    }
}

