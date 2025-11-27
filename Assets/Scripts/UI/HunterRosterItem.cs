using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HunterRosterItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private CanvasGroup canvasGroup;

    private OrdersTab ownerTab;
    private Hunter hunter;
    private RectTransform rectTransform;
    private Transform originalParent;
    private Vector2 originalAnchoredPosition;
    private Vector3 originalScale;
    private Vector3 originalLocalPosition;
    private int originalSiblingIndex;
    private Canvas rootCanvas;
    private bool draggable;
    private bool isDragging;

    public void Initialize(Hunter hunter, OrdersTab owner)
    {
        this.hunter = hunter;
        ownerTab = owner;

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
            Sprite portrait = hunter.GetHunterData()?.portrait;
            portraitImage.sprite = portrait;
            portraitImage.enabled = portrait != null;
        }

        bool assigned = IsAssigned();
        bool selectable = IsSelectable();
        HunterState state = hunter.GetState();
        bool alive = state != HunterState.Dead;

        draggable = selectable && !assigned;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = assigned || !selectable ? 0.4f : 1f;
            canvasGroup.blocksRaycasts = true;
        }

        if (statusText != null)
        {
            if (!alive)
            {
                statusText.text = "Dead";
            }
            else if (assigned)
            {
                statusText.text = "In Party";
            }
            else if (state == HunterState.OnMission)
            {
                statusText.text = "On Mission";
            }
            else if (!selectable)
            {
                statusText.text = "Unavailable";
            }
            else
            {
                statusText.text = string.Empty;
            }
        }
    }

    public Hunter GetHunter()
    {
        return hunter;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!draggable || hunter == null) return;

        isDragging = true;
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();
        originalAnchoredPosition = rectTransform.anchoredPosition;
        originalLocalPosition = rectTransform.localPosition;
        originalScale = rectTransform.localScale;

        if (rootCanvas == null)
        {
            rootCanvas = GetComponentInParent<Canvas>();
        }

        if (rootCanvas != null)
        {
            transform.SetParent(rootCanvas.transform, true);
        }

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0.75f;
        }

        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || hunter == null) return;
        rectTransform.position = eventData.position;
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
        if (assigned)
        {
            ownerTab?.ForceRosterStateRefresh();
        }

        if (originalParent != null)
        {
            transform.SetParent(originalParent, false);
            transform.SetSiblingIndex(originalSiblingIndex);
            rectTransform.anchoredPosition = originalAnchoredPosition;
            rectTransform.localPosition = originalLocalPosition;
            rectTransform.localScale = originalScale;
        }

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
        }

        if (!assigned)
        {
            Refresh();
        }
    }

    public bool IsAssigned()
    {
        return ownerTab != null && ownerTab.IsHunterAssignedToParty(hunter);
    }

    public bool IsSelectable()
    {
        return ownerTab != null && ownerTab.IsHunterSelectable(hunter);
    }

    public bool ShouldSortLast()
    {
        return IsAssigned() || !IsSelectable();
    }
}
