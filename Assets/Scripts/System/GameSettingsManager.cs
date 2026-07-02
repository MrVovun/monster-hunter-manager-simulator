using UnityEngine;

public class GameSettingsManager : MonoBehaviour
{
    public const string MasterVolumeKey = "settings.masterVolume";
    public const string FullscreenKey = "settings.fullscreen";
    public const string QualityIndexKey = "settings.qualityIndex";

    public static GameSettingsManager Instance { get; private set; }

    [Range(0f, 1f)]
    [SerializeField] private float defaultMasterVolume = 1f;
    [SerializeField] private bool defaultFullscreen = true;

    public float MasterVolume { get; private set; }
    public bool Fullscreen { get; private set; }
    public int QualityIndex { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadAndApply();
    }

    public void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp01(value);
        AudioListener.volume = MasterVolume;
        PlayerPrefs.SetFloat(MasterVolumeKey, MasterVolume);
        PlayerPrefs.Save();
    }

    public void SetFullscreen(bool value)
    {
        Fullscreen = value;
        Screen.fullScreen = Fullscreen;
        PlayerPrefs.SetInt(FullscreenKey, Fullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetQualityIndex(int index)
    {
        int max = Mathf.Max(0, QualitySettings.names.Length - 1);
        QualityIndex = Mathf.Clamp(index, 0, max);
        QualitySettings.SetQualityLevel(QualityIndex, true);
        PlayerPrefs.SetInt(QualityIndexKey, QualityIndex);
        PlayerPrefs.Save();
    }

    public void LoadAndApply()
    {
        MasterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, defaultMasterVolume);
        Fullscreen = PlayerPrefs.GetInt(FullscreenKey, defaultFullscreen ? 1 : 0) == 1;
        QualityIndex = PlayerPrefs.GetInt(QualityIndexKey, QualitySettings.GetQualityLevel());

        AudioListener.volume = Mathf.Clamp01(MasterVolume);
        Screen.fullScreen = Fullscreen;
        SetQualityIndex(QualityIndex);
    }
}
