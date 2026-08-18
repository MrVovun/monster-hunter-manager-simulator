using System.Collections;
using UnityEngine;

public class MusicManager : MonoBehaviour
{
    private enum MusicMode
    {
        MainMenu,
        TimeOfDay
    }

    [Header("Mode")]
    [SerializeField] private MusicMode mode = MusicMode.TimeOfDay;
    [SerializeField] private TimeManager timeManager;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.7f;
    [SerializeField] private float fadeSeconds = 1f;

    [Header("Main Menu")]
    [SerializeField] private AudioClip mainMenuClip;

    [Header("Game Day States")]
    [SerializeField] private AudioClip preWorkdayClip;
    [SerializeField] private AudioClip workdayClip;
    [SerializeField] private AudioClip eveningClip;

    [Header("Game Over")]
    [SerializeField] private AudioClip gameOverClip;

    private Coroutine fadeRoutine;
    private AudioClip currentClip;
    private bool gameOverOverrideActive;

    private void Awake()
    {
        EnsureSettingsManager();

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.volume = GetTargetVolume();
    }

    private void OnEnable()
    {
        GameSettingsManager.OnAudioSettingsChanged -= HandleAudioSettingsChanged;
        GameSettingsManager.OnAudioSettingsChanged += HandleAudioSettingsChanged;

        if (mode == MusicMode.TimeOfDay)
        {
            BindTimeManager();
        }

        RefreshMusic(immediate: true);
    }

    private void OnDisable()
    {
        if (timeManager != null)
        {
            timeManager.OnDayStateChanged -= HandleDayStateChanged;
        }

        GameSettingsManager.OnAudioSettingsChanged -= HandleAudioSettingsChanged;
    }

    private void OnValidate()
    {
        musicVolume = Mathf.Clamp01(musicVolume);
        fadeSeconds = Mathf.Max(0f, fadeSeconds);

        if (audioSource != null)
        {
            audioSource.volume = musicVolume;
            audioSource.loop = true;
        }
    }

    public void RefreshMusic(bool immediate = false)
    {
        AudioClip nextClip = GetDesiredClip();
        PlayClip(nextClip, immediate);
    }

    public void PlayGameOverMusic(bool immediate = false)
    {
        gameOverOverrideActive = true;
        PlayClip(gameOverClip, immediate);
    }

    public void ClearGameOverOverride(bool immediate = false)
    {
        gameOverOverrideActive = false;
        RefreshMusic(immediate);
    }

    private void HandleDayStateChanged(TimeManager.DayState _)
    {
        RefreshMusic();
    }

    private void HandleAudioSettingsChanged()
    {
        if (audioSource != null)
        {
            audioSource.volume = GetTargetVolume();
        }
    }

    private void BindTimeManager()
    {
        if (timeManager == null)
        {
            timeManager = SceneLookup.Find<TimeManager>();
        }

        if (timeManager == null) return;

        timeManager.OnDayStateChanged -= HandleDayStateChanged;
        timeManager.OnDayStateChanged += HandleDayStateChanged;
    }

    private AudioClip GetDesiredClip()
    {
        if (gameOverOverrideActive)
        {
            return gameOverClip;
        }

        if (mode == MusicMode.MainMenu)
        {
            return mainMenuClip;
        }

        if (timeManager == null)
        {
            BindTimeManager();
        }

        if (timeManager == null)
        {
            return preWorkdayClip;
        }

        switch (timeManager.GetDayState())
        {
            case TimeManager.DayState.PreBell:
                return preWorkdayClip;
            case TimeManager.DayState.Active:
                return workdayClip;
            case TimeManager.DayState.Evening:
                return eveningClip;
            default:
                return preWorkdayClip;
        }
    }

    private void PlayClip(AudioClip clip, bool immediate)
    {
        if (audioSource == null) return;
        if (currentClip == clip && audioSource.isPlaying) return;

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        if (immediate || fadeSeconds <= 0f || !audioSource.isPlaying)
        {
            currentClip = clip;
            audioSource.clip = clip;
            audioSource.volume = GetTargetVolume();

            if (clip != null)
            {
                audioSource.Play();
            }
            else
            {
                audioSource.Stop();
            }

            return;
        }

        fadeRoutine = StartCoroutine(FadeToClip(clip));
    }

    private IEnumerator FadeToClip(AudioClip clip)
    {
        float startVolume = audioSource.volume;
        float duration = Mathf.Max(0.01f, fadeSeconds);

        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            audioSource.volume = Mathf.Lerp(startVolume, 0f, t / duration);
            yield return null;
        }

        currentClip = clip;
        audioSource.clip = clip;

        if (clip != null)
        {
            audioSource.Play();
        }
        else
        {
            audioSource.Stop();
        }

        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            audioSource.volume = Mathf.Lerp(0f, GetTargetVolume(), t / duration);
            yield return null;
        }

        audioSource.volume = GetTargetVolume();
        fadeRoutine = null;
    }

    private float GetTargetVolume()
    {
        GameSettingsManager settings = EnsureSettingsManager();
        if (settings != null && settings.MusicMuted)
        {
            return 0f;
        }

        float settingsVolume = settings != null ? settings.MusicVolume : 1f;
        return Mathf.Clamp01(musicVolume * settingsVolume);
    }

    private GameSettingsManager EnsureSettingsManager()
    {
        if (GameSettingsManager.Instance != null)
        {
            return GameSettingsManager.Instance;
        }

        GameSettingsManager existing = SceneLookup.Find<GameSettingsManager>();
        if (existing != null)
        {
            return existing;
        }

        GameObject go = new GameObject("GameSettingsManager");
        return go.AddComponent<GameSettingsManager>();
    }
}
