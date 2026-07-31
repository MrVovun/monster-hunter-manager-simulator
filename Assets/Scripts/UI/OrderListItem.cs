using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OrderListItem : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text monsterNameText;
    [SerializeField] private TMP_Text difficultyText;

    [Header("Images")]
    [SerializeField] private Image monsterImage;
    [SerializeField] private Image progressBarImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private GameObject selectedHighlight;

    [Header("Progress Visual")]
    [SerializeField] private bool pulseProgressBar = true;
    [SerializeField] [Range(0f, 1f)] private float progressBarMinAlpha = 0.65f;
    [SerializeField] [Range(0f, 1f)] private float progressBarMaxAlpha = 1f;
    [SerializeField] private float progressBarPulseSpeed = 2.5f;

    [Header("Colors")]
    [SerializeField] private Color normalBackgroundColor = Color.white;
    [SerializeField] private Color selectedBackgroundColor = new Color(0.98f, 0.83f, 0.52f, 1f);
    [SerializeField] private Color inProgressBackgroundColor = new Color(0.64f, 0.82f, 0.95f, 1f);
    [SerializeField] private Color warningBackgroundColor = new Color(0.93f, 0.58f, 0.38f, 1f);

    private Order order;
    private OrdersTab parentTab;
    private Button button;
    private OrderListInProgressOverlayEffect progressEffect;
    private bool isSelected;
    private float lastProgress = -1f;
    private OrderState lastState;
    private int lastAssignedCount = -1;

    public Order Order => order;

    private void Awake()
    {
        CacheReferences();
    }

    public void Initialize(Order targetOrder, OrdersTab tab)
    {
        order = targetOrder;
        parentTab = tab;
        CacheReferences();
        HookButton();
        ConfigureProgressBar();
        UpdateDisplay(force: true);
        SetSelected(parentTab != null && parentTab.GetSelectedOrder() == order);
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        if (selectedHighlight != null)
        {
            selectedHighlight.SetActive(isSelected);
        }
        ApplyBackgroundColor();
    }

    public void RefreshLiveState()
    {
        if (order == null) return;

        float progress = order.missionTimer != null ? order.missionTimer.GetProgress() : 0f;
        int assignedCount = order.GetAssignedPartySize();
        if (lastState == order.state
            && lastAssignedCount == assignedCount
            && Mathf.Abs(lastProgress - progress) < 0.005f)
        {
            return;
        }

        UpdateDisplay(force: false);
    }

    private void UpdateDisplay(bool force)
    {
        if (order == null) return;

        if (nameText != null)
        {
            nameText.text = string.IsNullOrWhiteSpace(order.orderTitle) ? "Order" : order.orderTitle;
        }

        if (rewardText != null)
        {
            rewardText.text = $"{order.goldReward}g / {order.xpReward}xp";
        }

        if (statusText != null)
        {
            statusText.text = $"{GetStatusLabel(order)}  {order.GetAssignedPartySize()}/{order.maxPartySize}";
        }

        if (monsterNameText != null)
        {
            monsterNameText.text = order.GetDeclaredOrGenericMonsterName();
        }

        if (difficultyText != null)
        {
            difficultyText.text = $"Diff {order.difficulty}";
        }

        if (monsterImage != null)
        {
            Sprite portrait = order.declaredMonster != null ? order.declaredMonster.portrait : null;
            monsterImage.sprite = portrait;
            monsterImage.enabled = portrait != null;
            monsterImage.preserveAspect = true;
        }

        RefreshProgressBar();
        lastState = order.state;
        lastProgress = order.missionTimer != null ? order.missionTimer.GetProgress() : 0f;
        lastAssignedCount = order.GetAssignedPartySize();
        ApplyBackgroundColor();
    }

    private void RefreshProgressBar()
    {
        if (progressBarImage == null) return;

        bool inProgress = order != null && order.state == OrderState.InProgress && order.missionTimer != null;
        progressBarImage.gameObject.SetActive(inProgress && progressBarImage.sprite != null);
    }

    private void CacheReferences()
    {
        if (button == null) button = GetComponent<Button>();
        if (button == null) button = gameObject.AddComponent<Button>();
        if (backgroundImage == null) backgroundImage = GetComponent<Image>();
    }

    private void HookButton()
    {
        if (button == null) return;

        button.onClick.RemoveListener(OnClicked);
        button.onClick.AddListener(OnClicked);
        button.transition = Selectable.Transition.None;
        EnsureVisualFeedback();
    }

    private void ConfigureProgressBar()
    {
        if (progressBarImage == null) return;

        progressBarImage.raycastTarget = false;
        progressEffect = progressBarImage.GetComponent<OrderListInProgressOverlayEffect>();
        if (progressEffect == null)
        {
            progressEffect = progressBarImage.gameObject.AddComponent<OrderListInProgressOverlayEffect>();
        }

        progressEffect.Configure(
            progressBarImage,
            progressBarImage.color,
            pulseProgressBar,
            progressBarMinAlpha,
            progressBarMaxAlpha,
            progressBarPulseSpeed);
    }

    private void OnClicked()
    {
        InteractionFeedbackManager.PlayUIClick();
        parentTab?.SelectOrder(order);
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

    private void ApplyBackgroundColor()
    {
        if (backgroundImage == null) return;

        Color color;
        if (isSelected)
        {
            color = selectedBackgroundColor;
        }
        else if (order != null && order.state == OrderState.InProgress)
        {
            color = inProgressBackgroundColor;
        }
        else if (order != null && order.state != OrderState.Accepted)
        {
            color = warningBackgroundColor;
        }
        else
        {
            color = normalBackgroundColor;
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
