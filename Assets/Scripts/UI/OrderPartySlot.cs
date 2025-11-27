using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class OrderPartySlot : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    [SerializeField] private TMP_Text slotLabel;
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private GameObject highlightObject;
    [SerializeField] private Sprite emptyPortraitSprite;
    [SerializeField] private Color emptyTextColor = new Color(1f, 1f, 1f, 0.4f);
    [SerializeField] private Color assignedTextColor = Color.white;

    private OrderDetailPanel owner;
    private Hunter assignedHunter;
    private int slotIndex;

    public void Initialize(int index, OrderDetailPanel panel)
    {
        slotIndex = index;
        owner = panel;
        UpdateLabel();
        SetHunter(null);
    }

    private void UpdateLabel()
    {
        if (slotLabel != null)
        {
            slotLabel.text = $"Slot {slotIndex + 1}";
        }
    }

    public void SetHunter(Hunter hunter)
    {
        assignedHunter = hunter;

        if (nameText != null)
        {
            if (hunter == null)
            {
                nameText.text = "Empty";
                nameText.color = emptyTextColor;
            }
            else
            {
                nameText.text = hunter.name;
                nameText.color = assignedTextColor;
            }
        }

        if (portraitImage != null)
        {
            if (hunter == null)
            {
                portraitImage.sprite = emptyPortraitSprite;
                portraitImage.color = new Color(1f, 1f, 1f, emptyPortraitSprite != null ? 1f : 0.15f);
            }
            else
            {
                Sprite portrait = hunter.GetHunterData()?.portrait;
                portraitImage.sprite = portrait != null ? portrait : emptyPortraitSprite;
                portraitImage.color = Color.white;
            }
        }

        if (highlightObject != null)
        {
            highlightObject.SetActive(hunter != null);
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData == null) return;
        TryAssignFromPointer(eventData.pointerDrag);
    }

    private void TryAssignFromPointer(GameObject dragObject)
    {
        if (owner == null || dragObject == null) return;

        HunterRosterItem rosterItem = dragObject.GetComponent<HunterRosterItem>();
        if (rosterItem == null) return;

        TryAssignHunter(rosterItem.GetHunter());
    }

    public bool TryAssignHunter(Hunter hunter)
    {
        if (owner == null || hunter == null) return false;
        return owner.TryAssignHunterToSlot(slotIndex, hunter);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner == null) return;

        if (eventData.button == PointerEventData.InputButton.Right && assignedHunter != null)
        {
            owner.RemoveHunterFromSlot(slotIndex);
        }
    }
}
