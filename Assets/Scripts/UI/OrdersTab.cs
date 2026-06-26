using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OrdersTab : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform ordersListParent;
    [SerializeField] private GameObject orderItemPrefab;
    [Header("Order In Progress Overlay")]
    [SerializeField] private Sprite inProgressOverlaySprite;
    [SerializeField] private Vector2 inProgressOverlayAnchorMin = new Vector2(0f, 0f);
    [SerializeField] private Vector2 inProgressOverlayAnchorMax = new Vector2(1f, 0f);
    [SerializeField] private Vector2 inProgressOverlayOffsetMin = new Vector2(16f, 8f);
    [SerializeField] private Vector2 inProgressOverlayOffsetMax = new Vector2(-16f, 14f);
    [SerializeField] private Color inProgressOverlayColor = Color.white;
    [SerializeField] private bool animateInProgressOverlay = true;
    [SerializeField] [Range(0f, 1f)] private float inProgressOverlayMinAlpha = 0.65f;
    [SerializeField] [Range(0f, 1f)] private float inProgressOverlayMaxAlpha = 1f;
    [SerializeField] private float inProgressOverlayPulseSpeed = 2.5f;
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
        
        item.Initialize(
            order,
            this,
            inProgressOverlaySprite,
            inProgressOverlayAnchorMin,
            inProgressOverlayAnchorMax,
            inProgressOverlayOffsetMin,
            inProgressOverlayOffsetMax,
            inProgressOverlayColor,
            animateInProgressOverlay,
            inProgressOverlayMinAlpha,
            inProgressOverlayMaxAlpha,
            inProgressOverlayPulseSpeed);
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

// Helper class for order list items
public class OrderListItem : MonoBehaviour
{
    private static readonly Color NormalColor = new Color(0.78f, 0.66f, 0.54f, 1f);
    private static readonly Color SelectedColor = new Color(0.98f, 0.83f, 0.52f, 1f);
    private static readonly Color InProgressColor = new Color(0.64f, 0.82f, 0.95f, 1f);
    private static readonly Color WarningColor = new Color(0.93f, 0.58f, 0.38f, 1f);

    private Order order;
    private OrdersTab parentTab;
    private TMP_Text orderText;
    private TMP_Text titleText;
    private TMP_Text statusText;
    private TMP_Text monsterText;
    private TMP_Text rewardText;
    private TMP_Text difficultyText;
    private TMP_Text timeText;
    private TMP_Text styleSourceText;
    private Image backgroundImage;
    private Image inProgressOverlayImage;
    private OrderListInProgressOverlayEffect inProgressOverlayEffect;
    private Button button;
    private bool isSelected;
    private float lastProgress = -1f;
    private OrderState lastState;

    public Order Order => order;
    
    public void Initialize(
        Order order,
        OrdersTab tab,
        Sprite inProgressOverlaySprite,
        Vector2 inProgressOverlayAnchorMin,
        Vector2 inProgressOverlayAnchorMax,
        Vector2 inProgressOverlayOffsetMin,
        Vector2 inProgressOverlayOffsetMax,
        Color inProgressOverlayColor,
        bool animateInProgressOverlay,
        float inProgressOverlayMinAlpha,
        float inProgressOverlayMaxAlpha,
        float inProgressOverlayPulseSpeed)
    {
        this.order = order;
        this.parentTab = tab;
        
        // Set up UI
        button = GetComponent<Button>();
        if (button == null)
        {
            button = gameObject.AddComponent<Button>();
        }
        button.transition = Selectable.Transition.None;
        backgroundImage = GetComponent<Image>();
        
        button.onClick.AddListener(OnClicked);
        EnsureVisualFeedback();
        BindOrCreateFields(
            inProgressOverlaySprite,
            inProgressOverlayAnchorMin,
            inProgressOverlayAnchorMax,
            inProgressOverlayOffsetMin,
            inProgressOverlayOffsetMax,
            inProgressOverlayColor,
            animateInProgressOverlay,
            inProgressOverlayMinAlpha,
            inProgressOverlayMaxAlpha,
            inProgressOverlayPulseSpeed);
        
        orderText = GetFallbackText();
        if (orderText == null)
        {
            GameObject textObj = new GameObject("OrderText");
            textObj.transform.SetParent(transform, false);
            orderText = textObj.AddComponent<TextMeshProUGUI>();
            ApplyTextStyle(orderText, styleSourceText != null ? styleSourceText.fontSize : 24f, FontStyles.Normal);
        }
        
        UpdateDisplay();
        SetSelected(parentTab != null && parentTab.GetSelectedOrder() == order);
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        ApplyBackgroundColor();
    }

    public void RefreshLiveState()
    {
        if (order == null) return;

        float progress = order.missionTimer != null ? order.missionTimer.GetProgress() : 0f;
        if (lastState == order.state && Mathf.Abs(lastProgress - progress) < 0.005f)
        {
            return;
        }

        UpdateDisplay();
    }
    
    private void UpdateDisplay()
    {
        if (order == null) return;
        
        string status = GetStatusLabel(order);
        string monster = order.GetDeclaredOrGenericMonsterName();
        string party = $"{order.GetAssignedPartySize()}/{order.maxPartySize}";
        string activity = GetActivityLabel(order);

        if (titleText != null) titleText.text = order.orderTitle;
        if (statusText != null) statusText.text = $"{status}  {party}";
        if (monsterText != null) monsterText.text = monster;
        if (rewardText != null) rewardText.text = $"{order.goldReward}g / {order.xpReward}xp";
        if (difficultyText != null) difficultyText.text = $"Diff {order.difficulty}";
        if (timeText != null)
        {
            bool hasActivity = !string.IsNullOrEmpty(activity);
            timeText.gameObject.SetActive(hasActivity);
            timeText.text = activity;
        }

        if (orderText != null)
        {
            string activityLine = string.IsNullOrEmpty(activity) ? string.Empty : $"\n{activity}";
            orderText.text = $"{order.orderTitle}\n{status}  {party}\n{monster}  {order.goldReward}g / {order.xpReward}xp{activityLine}";
        }

        if (inProgressOverlayImage != null)
        {
            bool inProgress = order.state == OrderState.InProgress && order.missionTimer != null;
            inProgressOverlayImage.gameObject.SetActive(inProgress && inProgressOverlayImage.sprite != null);
        }

        lastState = order.state;
        lastProgress = order.missionTimer != null ? order.missionTimer.GetProgress() : 0f;
        ApplyBackgroundColor();
    }
    
    private void OnClicked()
    {
        parentTab?.SelectOrder(order);
    }

    private void BindOrCreateFields(
        Sprite inProgressOverlaySprite,
        Vector2 inProgressOverlayAnchorMin,
        Vector2 inProgressOverlayAnchorMax,
        Vector2 inProgressOverlayOffsetMin,
        Vector2 inProgressOverlayOffsetMax,
        Color inProgressOverlayColor,
        bool animateInProgressOverlay,
        float inProgressOverlayMinAlpha,
        float inProgressOverlayMaxAlpha,
        float inProgressOverlayPulseSpeed)
    {
        styleSourceText = GetComponentInChildren<TMP_Text>(true);
        titleText = FindText("TitleText");
        statusText = FindText("StatusText");
        monsterText = FindText("MonsterText");
        rewardText = FindText("RewardText");
        difficultyText = FindText("DifficultyText");
        timeText = FindText("TimeText");
        inProgressOverlayImage = FindImage("InProgressOverlay");
        ConfigureInProgressOverlay(
            inProgressOverlayImage,
            inProgressOverlaySprite,
            inProgressOverlayAnchorMin,
            inProgressOverlayAnchorMax,
            inProgressOverlayOffsetMin,
            inProgressOverlayOffsetMax,
            inProgressOverlayColor,
            animateInProgressOverlay,
            inProgressOverlayMinAlpha,
            inProgressOverlayMaxAlpha,
            inProgressOverlayPulseSpeed);

        bool hasStructuredFields = titleText != null
            && statusText != null
            && monsterText != null
            && rewardText != null
            && difficultyText != null
            && timeText != null;
        if (hasStructuredFields)
        {
            return;
        }

        foreach (var text in GetComponentsInChildren<TMP_Text>(true))
        {
            if (text != null)
            {
                text.gameObject.SetActive(false);
            }
        }

        float baseSize = styleSourceText != null ? styleSourceText.fontSize : 24f;
        float titleSize = Mathf.Min(baseSize * 0.72f, 18f);
        float bodySize = Mathf.Min(baseSize * 0.62f, 15f);
        float smallSize = Mathf.Min(baseSize * 0.58f, 14f);
        titleText = CreateText("TitleText", new Vector2(12f, -8f), new Vector2(-12f, -30f), titleSize, FontStyles.Bold, TextAlignmentOptions.Left);
        statusText = CreateText("StatusText", new Vector2(12f, -31f), new Vector2(-12f, -51f), bodySize, FontStyles.Normal, TextAlignmentOptions.Left);
        monsterText = CreateText("MonsterText", new Vector2(12f, -53f), new Vector2(-122f, -73f), bodySize, FontStyles.Normal, TextAlignmentOptions.Left);
        difficultyText = CreateText("DifficultyText", new Vector2(-116f, -53f), new Vector2(-66f, -73f), smallSize, FontStyles.Normal, TextAlignmentOptions.Right);
        rewardText = CreateText("RewardText", new Vector2(-62f, -53f), new Vector2(-12f, -73f), smallSize, FontStyles.Normal, TextAlignmentOptions.Right);
        timeText = CreateText("TimeText", new Vector2(12f, -75f), new Vector2(-12f, -95f), smallSize, FontStyles.Normal, TextAlignmentOptions.Left);
        inProgressOverlayImage = CreateInProgressOverlay(
            inProgressOverlaySprite,
            inProgressOverlayAnchorMin,
            inProgressOverlayAnchorMax,
            inProgressOverlayOffsetMin,
            inProgressOverlayOffsetMax,
            inProgressOverlayColor,
            animateInProgressOverlay,
            inProgressOverlayMinAlpha,
            inProgressOverlayMaxAlpha,
            inProgressOverlayPulseSpeed);
    }

    private TMP_Text GetFallbackText()
    {
        if (titleText != null) return null;
        return GetComponentInChildren<TMP_Text>(true);
    }

    private TMP_Text FindText(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private Image FindImage(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    private TMP_Text CreateText(string childName, Vector2 offsetMin, Vector2 offsetMax, float fontSize, FontStyles style, TextAlignmentOptions alignment)
    {
        var textObject = new GameObject(childName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(transform, false);

        var rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.pivot = new Vector2(0.5f, 1f);

        var text = textObject.GetComponent<TextMeshProUGUI>();
        ApplyTextStyle(text, fontSize, style);
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.margin = Vector4.zero;
        text.raycastTarget = false;
        return text;
    }

    private void ApplyTextStyle(TMP_Text target, float fontSize, FontStyles style)
    {
        if (target == null) return;

        if (styleSourceText != null)
        {
            target.font = styleSourceText.font;
            target.fontSharedMaterial = styleSourceText.fontSharedMaterial;
            target.color = styleSourceText.color;
            target.enableAutoSizing = styleSourceText.enableAutoSizing;
            target.fontSizeMin = styleSourceText.fontSizeMin;
            target.fontSizeMax = styleSourceText.fontSizeMax;
            target.characterSpacing = styleSourceText.characterSpacing;
            target.wordSpacing = styleSourceText.wordSpacing;
            target.lineSpacing = styleSourceText.lineSpacing;
            target.richText = styleSourceText.richText;
        }
        else
        {
            target.color = NormalColor;
        }

        target.fontSize = fontSize;
        target.fontStyle = style;
        target.enableAutoSizing = false;
    }

    private Image CreateInProgressOverlay(
        Sprite overlaySprite,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax,
        Color color,
        bool animate,
        float minAlpha,
        float maxAlpha,
        float pulseSpeed)
    {
        var overlayObject = new GameObject("InProgressOverlay", typeof(RectTransform), typeof(Image));
        overlayObject.transform.SetParent(transform, false);
        overlayObject.transform.SetSiblingIndex(1);

        var image = overlayObject.GetComponent<Image>();
        ConfigureInProgressOverlay(image, overlaySprite, anchorMin, anchorMax, offsetMin, offsetMax, color, animate, minAlpha, maxAlpha, pulseSpeed);
        return image;
    }

    private void ConfigureInProgressOverlay(
        Image image,
        Sprite overlaySprite,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax,
        Color color,
        bool animate,
        float minAlpha,
        float maxAlpha,
        float pulseSpeed)
    {
        if (image == null) return;

        var rect = image.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        image.sprite = overlaySprite;
        image.type = overlaySprite != null && overlaySprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
        image.gameObject.SetActive(false);

        inProgressOverlayEffect = image.GetComponent<OrderListInProgressOverlayEffect>();
        if (inProgressOverlayEffect == null)
        {
            inProgressOverlayEffect = image.gameObject.AddComponent<OrderListInProgressOverlayEffect>();
        }
        inProgressOverlayEffect.Configure(image, color, animate, minAlpha, maxAlpha, pulseSpeed);
    }

    private void EnsureVisualFeedback()
    {
        var visualFeedback = GetComponent<UIButtonVisualFeedback>();
        if (visualFeedback == null)
        {
            visualFeedback = gameObject.AddComponent<UIButtonVisualFeedback>();
        }

        visualFeedback.Configure(
            colorEnabled: true,
            scaleEnabled: false,
            hover: new Color(1f, 0.9f, 0.68f, 1f),
            pressedState: new Color(0.72f, 0.55f, 0.38f, 1f),
            disabled: new Color(0.35f, 0.35f, 0.35f, 0.65f),
            hoverScaleValue: 1f,
            pressedScaleValue: 1f,
            duration: 0.08f);
    }

    private string GetStatusLabel(Order value)
    {
        if (value == null) return "-";
        switch (value.state)
        {
            case OrderState.Accepted:
                return "Ready for party";
            case OrderState.InProgress:
                return "Hunters away";
            case OrderState.Completed:
                return "Report ready";
            case OrderState.Failed:
                return "Failed";
            case OrderState.Canceled:
                return "Canceled";
            default:
                return value.state.ToString();
        }
    }

    private string GetActivityLabel(Order value)
    {
        if (value == null) return string.Empty;
        if (value.state == OrderState.InProgress && value.missionTimer != null)
        {
            return "Expedition underway";
        }

        return string.Empty;
    }

    private void ApplyBackgroundColor()
    {
        if (backgroundImage == null) return;
        Color color;
        if (isSelected)
        {
            color = SelectedColor;
        }
        else if (order != null && order.state == OrderState.InProgress)
        {
            color = InProgressColor;
        }
        else if (order != null && order.state != OrderState.Accepted)
        {
            color = WarningColor;
        }
        else
        {
            color = Color.white;
        }

        backgroundImage.color = color;
        var visualFeedback = GetComponent<UIButtonVisualFeedback>();
        if (visualFeedback != null)
        {
            visualFeedback.SetNormalColor(color, true);
        }
    }
}

public class OrderListInProgressOverlayEffect : MonoBehaviour
{
    private Image target;
    private Color baseColor = Color.white;
    private bool animate;
    private float minAlpha;
    private float maxAlpha;
    private float pulseSpeed;
    private float elapsed;

    public void Configure(Image image, Color color, bool shouldAnimate, float minimumAlpha, float maximumAlpha, float speed)
    {
        target = image;
        baseColor = color;
        animate = shouldAnimate;
        minAlpha = Mathf.Clamp01(minimumAlpha);
        maxAlpha = Mathf.Clamp01(maximumAlpha);
        if (maxAlpha < minAlpha)
        {
            maxAlpha = minAlpha;
        }
        pulseSpeed = Mathf.Max(0f, speed);
        ApplyAlpha(maxAlpha);
        enabled = animate && pulseSpeed > 0f;
    }

    private void OnEnable()
    {
        elapsed = 0f;
        if (target != null && !animate)
        {
            ApplyAlpha(maxAlpha);
        }
    }

    private void Update()
    {
        if (target == null || !animate) return;

        elapsed += Time.unscaledDeltaTime;
        float t = (Mathf.Sin(elapsed * pulseSpeed) + 1f) * 0.5f;
        ApplyAlpha(Mathf.Lerp(minAlpha, maxAlpha, t));
    }

    private void ApplyAlpha(float alpha)
    {
        if (target == null) return;

        Color color = baseColor;
        color.a = alpha;
        target.color = color;
    }
}
