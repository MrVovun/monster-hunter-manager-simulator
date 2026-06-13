using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIButtonFeedback : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
{
    [SerializeField] private bool playClickFeedback = true;
    [SerializeField] private bool playHoverFeedback = false;

    private Selectable selectable;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
    }

    public void Configure(bool clickFeedback, bool hoverFeedback)
    {
        playClickFeedback = clickFeedback;
        playHoverFeedback = hoverFeedback;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!playClickFeedback || eventData.button != PointerEventData.InputButton.Left) return;
        if (!CanPlayFeedback()) return;

        InteractionFeedbackManager.PlayUIClick(eventData.position, transform);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!playHoverFeedback || !CanPlayFeedback()) return;

        InteractionFeedbackManager.PlayUIHover();
    }

    private bool CanPlayFeedback()
    {
        if (selectable == null)
        {
            selectable = GetComponent<Selectable>();
        }

        return selectable == null || selectable.IsInteractable();
    }
}
