using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIFeedbackAutoBinder : MonoBehaviour
{
    [SerializeField] private bool includeInactiveButtons = true;
    [SerializeField] private bool bindOnEnable = true;
    [SerializeField] private bool bindWhenChildrenChange = true;
    [SerializeField] private bool includeCustomPointerClickTargets = true;
    [SerializeField] private bool playClickFeedback = true;
    [SerializeField] private bool playHoverFeedback = false;

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

    [ContextMenu("Bind Buttons")]
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
                if (behaviour is UIButtonFeedback) continue;
                if (behaviour is IPointerClickHandler)
                {
                    targets.Add(behaviour.gameObject);
                }
            }
        }

        foreach (GameObject target in targets)
        {
            if (target == null) continue;

            UIButtonFeedback feedback = target.GetComponent<UIButtonFeedback>();
            if (feedback == null)
            {
                feedback = target.AddComponent<UIButtonFeedback>();
            }
            feedback.Configure(playClickFeedback, playHoverFeedback);
        }
    }
}
