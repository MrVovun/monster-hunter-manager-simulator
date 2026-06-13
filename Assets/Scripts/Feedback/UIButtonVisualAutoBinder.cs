using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIButtonVisualAutoBinder : MonoBehaviour
{
    [SerializeField] private bool includeInactiveButtons = true;
    [SerializeField] private bool bindOnEnable = true;
    [SerializeField] private bool bindWhenChildrenChange = true;
    [SerializeField] private bool includeCustomPointerClickTargets = true;

    [Header("Color")]
    [SerializeField] private bool useColor = true;
    [SerializeField] private Color hoverColor = new Color(1f, 1f, 1f, 0.88f);
    [SerializeField] private Color pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    [SerializeField] private Color disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.65f);

    [Header("Scale")]
    [SerializeField] private bool useScale = true;
    [SerializeField] private float hoverScale = 1.03f;
    [SerializeField] private float pressedScale = 0.98f;

    [Header("Timing")]
    [SerializeField] [Range(0.01f, 0.5f)] private float transitionDuration = 0.08f;

    private void OnEnable()
    {
        if (bindOnEnable)
        {
            BindButtons();
        }
    }

    private void OnTransformChildrenChanged()
    {
        if (bindWhenChildrenChange && isActiveAndEnabled)
        {
            BindButtons();
        }
    }

    [ContextMenu("Bind Button Visuals")]
    public void BindButtons()
    {
        HashSet<GameObject> targets = new HashSet<GameObject>();

        Button[] buttons = GetComponentsInChildren<Button>(includeInactiveButtons);
        foreach (Button button in buttons)
        {
            if (button != null)
            {
                targets.Add(button.gameObject);
            }
        }

        if (includeCustomPointerClickTargets)
        {
            MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(includeInactiveButtons);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null) continue;
                if (behaviour is UIButtonVisualFeedback) continue;
                if (behaviour is IPointerClickHandler)
                {
                    targets.Add(behaviour.gameObject);
                }
            }
        }

        foreach (GameObject target in targets)
        {
            if (target == null) continue;

            UIButtonVisualFeedback feedback = target.GetComponent<UIButtonVisualFeedback>();
            if (feedback == null)
            {
                feedback = target.AddComponent<UIButtonVisualFeedback>();
            }

            feedback.Configure(
                useColor,
                useScale,
                hoverColor,
                pressedColor,
                disabledColor,
                hoverScale,
                pressedScale,
                transitionDuration);
        }
    }
}
