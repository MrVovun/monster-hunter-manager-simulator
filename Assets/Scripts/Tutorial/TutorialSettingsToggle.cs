using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public class TutorialSettingsToggle : MonoBehaviour
{
    private Toggle toggle;

    private void Awake()
    {
        toggle = GetComponent<Toggle>();
        toggle.onValueChanged.AddListener(HandleValueChanged);
    }

    private void OnEnable()
    {
        if (toggle != null && TutorialManager.Instance != null)
        {
            toggle.SetIsOnWithoutNotify(TutorialManager.Instance.IsTutorialDisabled());
        }
    }

    private void HandleValueChanged(bool disabled)
    {
        TutorialManager.Instance?.SetTutorialDisabled(disabled);
    }
}
