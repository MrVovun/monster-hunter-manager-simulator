using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIButtonVisualFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    [Header("References")]
    [SerializeField] private Graphic targetGraphic;
    [SerializeField] private RectTransform scaleTarget;

    [Header("Color")]
    [SerializeField] private bool useColor = true;
    [SerializeField] private bool captureNormalColor = true;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor = new Color(1f, 1f, 1f, 0.88f);
    [SerializeField] private Color pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    [SerializeField] private Color disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.65f);

    [Header("Scale")]
    [SerializeField] private bool useScale = true;
    [SerializeField] private float hoverScale = 1.03f;
    [SerializeField] private float pressedScale = 0.98f;

    [Header("Timing")]
    [SerializeField] [Range(0.01f, 0.5f)] private float transitionDuration = 0.08f;

    private Selectable selectable;
    private Vector3 normalScale = Vector3.one;
    private bool hovered;
    private bool pressed;
    private bool lastInteractable;
    private Coroutine transitionRoutine;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
        if (targetGraphic == null)
        {
            targetGraphic = selectable != null ? selectable.targetGraphic : null;
        }
        if (targetGraphic == null)
        {
            targetGraphic = GetComponent<Graphic>();
        }
        if (targetGraphic == null)
        {
            targetGraphic = GetComponentInChildren<Graphic>(true);
        }
        if (scaleTarget == null)
        {
            scaleTarget = transform as RectTransform;
        }
        if (scaleTarget != null)
        {
            normalScale = scaleTarget.localScale;
        }
        if (captureNormalColor && targetGraphic != null)
        {
            normalColor = targetGraphic.color;
        }
        lastInteractable = IsInteractable();
    }

    private void OnEnable()
    {
        lastInteractable = IsInteractable();
        ApplyState(true);
    }

    private void Update()
    {
        bool currentInteractable = IsInteractable();
        if (currentInteractable == lastInteractable) return;

        lastInteractable = currentInteractable;
        if (!currentInteractable)
        {
            pressed = false;
        }
        ApplyState(false);
    }

    private void OnDisable()
    {
        hovered = false;
        pressed = false;
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }
    }

    public void Configure(
        bool colorEnabled,
        bool scaleEnabled,
        Color hover,
        Color pressedState,
        Color disabled,
        float hoverScaleValue,
        float pressedScaleValue,
        float duration)
    {
        useColor = colorEnabled;
        useScale = scaleEnabled;
        hoverColor = hover;
        pressedColor = pressedState;
        disabledColor = disabled;
        hoverScale = hoverScaleValue;
        pressedScale = pressedScaleValue;
        transitionDuration = Mathf.Max(0.01f, duration);
        ApplyState(false);
    }

    public void SetNormalColor(Color color, bool immediate = true)
    {
        normalColor = color;
        ApplyState(immediate);
    }

    public void RefreshVisualState(bool immediate = true)
    {
        lastInteractable = IsInteractable();
        if (!lastInteractable)
        {
            pressed = false;
        }
        ApplyState(immediate);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        ApplyState(false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
        ApplyState(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        pressed = true;
        ApplyState(false);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        pressed = false;
        ApplyState(false);
    }

    public void OnSelect(BaseEventData eventData)
    {
        hovered = true;
        ApplyState(false);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        hovered = false;
        pressed = false;
        ApplyState(false);
    }

    private void ApplyState(bool immediate)
    {
        Color nextColor = GetTargetColor();
        Vector3 nextScale = GetTargetScale();

        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
        }

        if (immediate || !isActiveAndEnabled)
        {
            SetVisuals(nextColor, nextScale);
            transitionRoutine = null;
            return;
        }

        transitionRoutine = StartCoroutine(TransitionTo(nextColor, nextScale));
    }

    private Color GetTargetColor()
    {
        if (!IsInteractable()) return disabledColor;
        if (pressed) return WithNormalAlpha(pressedColor);
        return hovered ? WithNormalAlpha(hoverColor) : normalColor;
    }

    private Color WithNormalAlpha(Color color)
    {
        color.a = normalColor.a;
        return color;
    }

    private Vector3 GetTargetScale()
    {
        if (!IsInteractable()) return normalScale;
        if (pressed) return normalScale * pressedScale;
        return hovered ? normalScale * hoverScale : normalScale;
    }

    private bool IsInteractable()
    {
        if (selectable == null)
        {
            selectable = GetComponent<Selectable>();
        }

        return selectable == null || selectable.IsInteractable();
    }

    private IEnumerator TransitionTo(Color nextColor, Vector3 nextScale)
    {
        Color startColor = targetGraphic != null ? targetGraphic.color : nextColor;
        Vector3 startScale = scaleTarget != null ? scaleTarget.localScale : nextScale;
        float duration = Mathf.Max(0.01f, transitionDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetVisuals(Color.Lerp(startColor, nextColor, t), Vector3.Lerp(startScale, nextScale, t));
            yield return null;
        }

        SetVisuals(nextColor, nextScale);
        transitionRoutine = null;
    }

    private void SetVisuals(Color color, Vector3 scale)
    {
        if (useColor && targetGraphic != null)
        {
            targetGraphic.color = color;
        }

        if (useScale && scaleTarget != null)
        {
            scaleTarget.localScale = scale;
        }
    }
}
