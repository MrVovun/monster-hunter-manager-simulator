using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HunterRosterItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private CanvasGroup canvasGroup;
    [Header("Drag Visual")]
    [SerializeField] private Vector2 dragPortraitSize = new Vector2(72f, 72f);
    [SerializeField] private float sourceAlphaWhileDragging = 0.45f;
    [SerializeField] private float dragPortraitAlpha = 0.95f;

    private OrdersTab ownerTab;
    private HuntersTab huntersTab;
    private Hunter hunter;
    private RectTransform rectTransform;
    private Canvas rootCanvas;
    private bool draggable;
    private bool isDragging;
    private bool displayOnly;
    private System.Action<Hunter> onDisplaySelected;
    private RectTransform dragVisualRect;
    private GameObject dragVisualObject;

    public void Initialize(Hunter hunter, OrdersTab owner)
    {
        Setup(hunter, owner, null, false);
    }

    public void InitializeForHuntersTab(Hunter hunter, HuntersTab tab, System.Action<Hunter> onSelect)
    {
        Setup(hunter, null, tab, true);
        onDisplaySelected = onSelect;
    }

    public void InitializeDisplayOnly(Hunter hunter)
    {
        Setup(hunter, null, null, true);
    }

    public void SetDisplaySelectionHandler(System.Action<Hunter> onSelect)
    {
        onDisplaySelected = onSelect;
    }

    private void Setup(Hunter hunter, OrdersTab owner, HuntersTab huntersTabOwner, bool displayOnlyMode)
    {
        this.hunter = hunter;
        ownerTab = owner;
        huntersTab = huntersTabOwner;
        displayOnly = displayOnlyMode || owner == null;

        rectTransform = GetComponent<RectTransform>();
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        rootCanvas = GetComponentInParent<Canvas>();

        Refresh();
    }

    public void Refresh()
    {
        if (hunter == null)
        {
            gameObject.SetActive(false);
            return;
        }

        if (nameText != null)
        {
            nameText.text = hunter.name;
        }

        if (portraitImage != null)
        {
            Sprite portrait = hunter.Data?.portrait;
            portraitImage.sprite = portrait;
            portraitImage.enabled = portrait != null;
        }

        bool assigned = ownerTab != null && IsAssigned();
        bool selectable = ownerTab != null && IsSelectable();

        draggable = !displayOnly && selectable && !assigned;

        if (canvasGroup != null)
        {
            if (displayOnly)
            {
                canvasGroup.alpha = 1f;
            }
            else
            {
                canvasGroup.alpha = assigned || !selectable ? 0.4f : 1f;
            }
            canvasGroup.blocksRaycasts = true;
        }

        if (statusText != null)
        {
            bool statusSelectable = displayOnly || selectable;
            statusText.text = HunterStatusFormatter.GetStatus(hunter, assigned, statusSelectable);
        }
    }

    public Hunter GetHunter()
    {
        return hunter;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!draggable || hunter == null) return;

        InteractionFeedbackManager.PlayUIDragStart();
        isDragging = true;

        if (rootCanvas == null)
        {
            rootCanvas = GetComponentInParent<Canvas>();
        }

        CreateDragVisual();

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = sourceAlphaWhileDragging;
        }

        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || hunter == null) return;
        if (dragVisualRect != null)
        {
            dragVisualRect.position = eventData.position;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging || hunter == null) return;
        isDragging = false;

        OrderPartySlot dropSlot = null;
        if (eventData != null)
        {
            var targetObj = eventData.pointerCurrentRaycast.gameObject;
            if (targetObj == null && eventData.pointerEnter != null)
            {
                targetObj = eventData.pointerEnter;
            }

            if (targetObj != null)
            {
                dropSlot = targetObj.GetComponentInParent<OrderPartySlot>();
            }
        }

        bool assigned = dropSlot != null && dropSlot.TryAssignHunter(hunter);

        DestroyDragVisual();

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
        }

        Refresh();

        if (assigned)
        {
            ownerTab?.ForceRosterStateRefresh();
        }
    }

    private void CreateDragVisual()
    {
        DestroyDragVisual();
        if (rootCanvas == null) return;

        Sprite portrait = hunter != null ? hunter.Data?.portrait : null;
        if (portrait == null && portraitImage != null)
        {
            portrait = portraitImage.sprite;
        }
        if (portrait == null) return;

        dragVisualObject = new GameObject("HunterPortraitDragVisual", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        dragVisualRect = dragVisualObject.GetComponent<RectTransform>();
        dragVisualRect.SetParent(rootCanvas.transform, false);
        dragVisualRect.anchorMin = new Vector2(0.5f, 0.5f);
        dragVisualRect.anchorMax = new Vector2(0.5f, 0.5f);
        dragVisualRect.pivot = new Vector2(0.5f, 0.5f);
        dragVisualRect.sizeDelta = dragPortraitSize;

        Image image = dragVisualObject.GetComponent<Image>();
        image.sprite = portrait;
        image.color = new Color(1f, 1f, 1f, dragPortraitAlpha);
        image.preserveAspect = true;
        image.raycastTarget = false;

        CanvasGroup dragCanvasGroup = dragVisualObject.GetComponent<CanvasGroup>();
        dragCanvasGroup.blocksRaycasts = false;
        dragCanvasGroup.interactable = false;
        dragCanvasGroup.alpha = 1f;

        dragVisualObject.transform.SetAsLastSibling();
    }

    private void DestroyDragVisual()
    {
        if (dragVisualObject != null)
        {
            Destroy(dragVisualObject);
        }

        dragVisualObject = null;
        dragVisualRect = null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!displayOnly) return;
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            onDisplaySelected?.Invoke(hunter);
        }
    }

    public bool IsAssigned()
    {
        if (displayOnly || ownerTab == null) return false;
        return ownerTab.IsHunterAssignedToParty(hunter);
    }

    public bool IsSelectable()
    {
        if (displayOnly || ownerTab == null) return false;
        return ownerTab.IsHunterSelectable(hunter);
    }

    public bool ShouldSortLast()
    {
        return IsAssigned() || !IsSelectable();
    }
}

public static class HunterStatusFormatter
{
    public static string GetStatus(Hunter hunter, bool assignedToParty = false, bool selectableForOrder = true)
    {
        if (hunter == null) return string.Empty;

        HunterState state = hunter.GetState();
        if (state == HunterState.Dead) return "Dead";
        if (assignedToParty) return HasWound(hunter) ? "In Party - Wounded" : "In Party";

        switch (state)
        {
            case HunterState.OnMission:
                return "On Mission";
            case HunterState.Candidate:
                return "Candidate";
            case HunterState.Healing:
                return "Healing";
            case HunterState.Sleeping:
                return "Sleeping";
            case HunterState.Armory:
                return "In Armory";
            case HunterState.Idle:
                if (HasWound(hunter)) return selectableForOrder ? "Wounded" : "Wounded - Unavailable";
                return selectableForOrder ? "Ready" : "Unavailable";
            default:
                return state.ToString();
        }
    }

    private static bool HasWound(Hunter hunter)
    {
        if (hunter == null) return false;
        var interactionState = hunter.GetComponent<HunterInteractionState>();
        return interactionState != null && interactionState.IsWounded;
    }
}
