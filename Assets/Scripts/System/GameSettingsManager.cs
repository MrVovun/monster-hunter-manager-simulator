using UnityEngine;

public class GameSettingsManager : MonoBehaviour
{
    public const string MasterVolumeKey = "settings.masterVolume";
    public const string MusicVolumeKey = "settings.musicVolume";
    public const string MusicMutedKey = "settings.musicMuted";
    public const string FullscreenKey = "settings.fullscreen";
    public const string QualityIndexKey = "settings.qualityIndex";

    public static GameSettingsManager Instance { get; private set; }
    public static event System.Action OnAudioSettingsChanged;

    [Range(0f, 1f)]
    [SerializeField] private float defaultMasterVolume = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float defaultMusicVolume = 1f;
    [SerializeField] private bool defaultMusicMuted = false;
    [SerializeField] private bool defaultFullscreen = true;

    public float MasterVolume { get; private set; }
    public float MusicVolume { get; private set; }
    public bool MusicMuted { get; private set; }
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
        OnAudioSettingsChanged?.Invoke();
    }

    public void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
        PlayerPrefs.Save();
        OnAudioSettingsChanged?.Invoke();
    }

    public void SetMusicMuted(bool value)
    {
        MusicMuted = value;
        PlayerPrefs.SetInt(MusicMutedKey, MusicMuted ? 1 : 0);
        PlayerPrefs.Save();
        OnAudioSettingsChanged?.Invoke();
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
        MusicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, defaultMusicVolume);
        MusicMuted = PlayerPrefs.GetInt(MusicMutedKey, defaultMusicMuted ? 1 : 0) == 1;
        Fullscreen = PlayerPrefs.GetInt(FullscreenKey, defaultFullscreen ? 1 : 0) == 1;
        QualityIndex = PlayerPrefs.GetInt(QualityIndexKey, QualitySettings.GetQualityLevel());

        AudioListener.volume = Mathf.Clamp01(MasterVolume);
        Screen.fullScreen = Fullscreen;
        SetQualityIndex(QualityIndex);
    }
}
