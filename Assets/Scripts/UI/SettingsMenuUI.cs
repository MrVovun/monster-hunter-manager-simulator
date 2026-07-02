using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsMenuUI : MonoBehaviour
{
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private TMP_Text masterVolumeValueText;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private Toggle disableTutorialToggle;
    [SerializeField] private Button closeButton;
    [SerializeField] private GameObject root;

    private bool refreshing;

    private void Awake()
    {
        EnsureSettingsManager();
        HookControls();
        Refresh();
    }

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        refreshing = true;

        GameSettingsManager settings = EnsureSettingsManager();
        if (settings != null)
        {
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.SetValueWithoutNotify(settings.MasterVolume);
            }

            if (masterVolumeValueText != null)
            {
                masterVolumeValueText.text = $"{Mathf.RoundToInt(settings.MasterVolume * 100f)}%";
            }

            if (fullscreenToggle != null)
            {
                fullscreenToggle.SetIsOnWithoutNotify(settings.Fullscreen);
            }

            if (qualityDropdown != null)
            {
                qualityDropdown.ClearOptions();
                qualityDropdown.AddOptions(new System.Collections.Generic.List<string>(QualitySettings.names));
                qualityDropdown.SetValueWithoutNotify(Mathf.Clamp(settings.QualityIndex, 0, Mathf.Max(0, QualitySettings.names.Length - 1)));
            }
        }

        if (disableTutorialToggle != null)
        {
            bool disabled = TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialDisabled();
            disableTutorialToggle.SetIsOnWithoutNotify(disabled);
        }

        refreshing = false;
    }

    public void Close()
    {
        if (root != null)
        {
            root.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void HookControls()
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.RemoveAllListeners();
            masterVolumeSlider.onValueChanged.AddListener(HandleVolumeChanged);
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.onValueChanged.RemoveAllListeners();
            fullscreenToggle.onValueChanged.AddListener(HandleFullscreenChanged);
        }

        if (qualityDropdown != null)
        {
            qualityDropdown.onValueChanged.RemoveAllListeners();
            qualityDropdown.onValueChanged.AddListener(HandleQualityChanged);
        }

        if (disableTutorialToggle != null)
        {
            disableTutorialToggle.onValueChanged.RemoveAllListeners();
            disableTutorialToggle.onValueChanged.AddListener(HandleTutorialToggleChanged);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }
    }

    private void HandleVolumeChanged(float value)
    {
        if (refreshing) return;
        EnsureSettingsManager()?.SetMasterVolume(value);
        if (masterVolumeValueText != null)
        {
            masterVolumeValueText.text = $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
        }
    }

    private void HandleFullscreenChanged(bool value)
    {
        if (refreshing) return;
        EnsureSettingsManager()?.SetFullscreen(value);
    }

    private void HandleQualityChanged(int value)
    {
        if (refreshing) return;
        EnsureSettingsManager()?.SetQualityIndex(value);
    }

    private void HandleTutorialToggleChanged(bool disabled)
    {
        if (refreshing) return;
        TutorialManager.Instance?.SetTutorialDisabled(disabled);
    }

    private GameSettingsManager EnsureSettingsManager()
    {
        if (GameSettingsManager.Instance != null)
        {
            return GameSettingsManager.Instance;
        }

        var existing = FindObjectOfType<GameSettingsManager>();
        if (existing != null)
        {
            return existing;
        }

        var go = new GameObject("GameSettingsManager");
        return go.AddComponent<GameSettingsManager>();
    }
}
