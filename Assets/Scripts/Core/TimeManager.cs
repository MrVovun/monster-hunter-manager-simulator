using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class TimeManager : MonoBehaviour
{
    [Serializable]
    private class TimeSaveData
    {
        public float gameTimeInSeconds;
        public int currentDayIndex;
        public float remainingDayGameSeconds;
        public DayState dayState;
    }

    public enum DayState { PreBell, Active, Evening }

    [Header("Time Settings")]
    [Tooltip("If true, game time only advances when AdvanceTime is called (action-based).")]
    [SerializeField] private bool useActionBasedTime = true;
    [SerializeField] private float timeScale = 60f; // 1 real second = 60 game seconds (1 game minute)
    [SerializeField] private bool pauseTime = false;
    [Tooltip("Length of a day in game seconds (used when action-based) or real seconds (scaled) otherwise.")]
    [SerializeField] private float dayLengthRealSeconds = 600f; // default fallback in case config is missing
    
    private float gameTimeInSeconds = 0f; // Total game time elapsed
    private List<MissionTimer> activeTimers = new List<MissionTimer>();
    private int currentDayIndex = 0; // Day 0 at game start
    private float remainingDayGameSeconds;
    private DayState dayState = DayState.PreBell;
    private string savePath;
    private bool loadedState;
    
    public delegate void TimeUpdateDelegate(float gameTimeDelta);
    public event TimeUpdateDelegate OnTimeUpdate;
    public event TimeUpdateDelegate OnTimeAdvanced; // fires only when time actually advanced
    public event System.Action<int> OnDayStarted; // Fires with day index (0-based)
    public event System.Action<DayState> OnDayStateChanged;

    private float DayLengthGameSeconds => useActionBasedTime ? Mathf.Max(1f, dayLengthRealSeconds) : Mathf.Max(1f, dayLengthRealSeconds * timeScale);

    private void Start()
    {
        // Pull day length from config if available
        var config = GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null;
        if (config != null && config.dayLengthSeconds > 0f)
        {
            dayLengthRealSeconds = config.dayLengthSeconds;
        }

        savePath = GameSaveUtility.GetSavePath("time_state.json");
        remainingDayGameSeconds = DayLengthGameSeconds;
        loadedState = LoadState();

        if (!loadedState)
        {
            // Fire day 0 start so systems can run once at beginning.
            OnDayStarted?.Invoke(currentDayIndex);
            SaveState();
        }
        else
        {
            OnDayStateChanged?.Invoke(dayState);
            OnTimeUpdate?.Invoke(0f);
        }
    }
    
    private void Update()
    {
        if (useActionBasedTime) return;
        if (pauseTime) return;
        
        float realDeltaTime = Time.deltaTime;
        float gameDeltaTime = realDeltaTime * timeScale;
        StepTime(gameDeltaTime);
    }

    /// <summary>
    /// Advances game time by the given amount of game seconds (action-based mode).
    /// Does nothing if still in PreBell or already in Evening.
    /// </summary>
    public void AdvanceTime(float gameSeconds)
    {
        if (!useActionBasedTime) return;
        StepTime(gameSeconds);
    }

    private void StepTime(float gameDeltaTime)
    {
        if (gameDeltaTime <= 0f) return;
        if (pauseTime) return;

        // In pre-bell, time shouldn't move
        if (dayState == DayState.PreBell || dayState == DayState.Evening)
        {
            return;
        }

        gameTimeInSeconds += gameDeltaTime;

        // Update all active timers
        for (int i = activeTimers.Count - 1; i >= 0; i--)
        {
            if (activeTimers[i] != null)
            {
                var timer = activeTimers[i];
                timer.Update(gameDeltaTime);
                
                if (timer.IsExpired())
                {
                    // Remove first to avoid re-entrancy double-removal
                    activeTimers.RemoveAt(i);
                    timer.OnExpired?.Invoke();
                }
            }
        }

        float appliedTime = gameDeltaTime;
        if (dayState == DayState.Active)
        {
            appliedTime = Mathf.Min(gameDeltaTime, remainingDayGameSeconds);
            remainingDayGameSeconds = Mathf.Max(0f, remainingDayGameSeconds - appliedTime);
            if (remainingDayGameSeconds <= 0f)
            {
                SetDayState(DayState.Evening);
            }
        }

        OnTimeAdvanced?.Invoke(appliedTime);
        OnTimeUpdate?.Invoke(appliedTime);
        SaveState();
    }
    
    public void RegisterTimer(MissionTimer timer)
    {
        if (timer != null && !activeTimers.Contains(timer))
        {
            activeTimers.Add(timer);
        }
    }
    
    public void UnregisterTimer(MissionTimer timer)
    {
        activeTimers.Remove(timer);
    }
    
    public float GetGameTimeInSeconds()
    {
        return gameTimeInSeconds;
    }
    
    public float GetGameTimeInMinutes()
    {
        return gameTimeInSeconds / 60f;
    }
    
    public float GetGameTimeInHours()
    {
        return gameTimeInSeconds / 3600f;
    }
    
    public float GetTimeScale()
    {
        return timeScale;
    }
    
    public void SetTimeScale(float newScale)
    {
        timeScale = Mathf.Max(0f, newScale);
    }
    
    public void PauseTime()
    {
        pauseTime = true;
    }
    
    public void ResumeTime()
    {
        pauseTime = false;
    }
    
    public bool IsTimePaused()
    {
        return pauseTime;
    }
    
    public string GetFormattedTime()
    {
        int hours = Mathf.FloorToInt(gameTimeInSeconds / 3600f);
        int minutes = Mathf.FloorToInt((gameTimeInSeconds % 3600f) / 60f);
        int seconds = Mathf.FloorToInt(gameTimeInSeconds % 60f);
        
        return string.Format("{0:00}:{1:00}:{2:00}", hours, minutes, seconds);
    }

    public int GetCurrentDayIndex()
    {
        return currentDayIndex;
    }

    public float GetSecondsIntoCurrentDay()
    {
        float secondsIntoDay = gameTimeInSeconds - (currentDayIndex * DayLengthGameSeconds);
        return Mathf.Clamp(secondsIntoDay, 0f, DayLengthGameSeconds);
    }

    public float GetSecondsRemainingInDay()
    {
        return Mathf.Max(0f, remainingDayGameSeconds);
    }

    public float GetDayLengthRealSeconds()
    {
        return dayLengthRealSeconds;
    }

    public float GetDayProgress01()
    {
        float length = DayLengthGameSeconds;
        if (length <= 0f) return 0f;
        return Mathf.Clamp01(1f - (remainingDayGameSeconds / length));
    }

    public DayState GetDayState() => dayState;
    public bool IsActionBasedTime() => useActionBasedTime;

    public void StartDayCountdown()
    {
        if (dayState != DayState.PreBell) return;
        SetDayState(DayState.Active);
        SaveState();
    }

    public void EnterEvening()
    {
        SetDayState(DayState.Evening);
        SaveState();
    }

    public void AdvanceToNextDay()
    {
        currentDayIndex++;
        remainingDayGameSeconds = DayLengthGameSeconds;
        SetDayState(DayState.PreBell);
        OnDayStarted?.Invoke(currentDayIndex);
        SaveState();
    }

    private void SetDayState(DayState newState)
    {
        if (dayState == newState) return;
        dayState = newState;
        OnDayStateChanged?.Invoke(dayState);
        SaveState();
    }

    private bool LoadState()
    {
        if (string.IsNullOrEmpty(savePath) || !File.Exists(savePath)) return false;

        try
        {
            string json = File.ReadAllText(savePath);
            if (string.IsNullOrWhiteSpace(json)) return false;

            TimeSaveData data = JsonUtility.FromJson<TimeSaveData>(json);
            if (data == null) return false;

            gameTimeInSeconds = Mathf.Max(0f, data.gameTimeInSeconds);
            currentDayIndex = Mathf.Max(0, data.currentDayIndex);
            remainingDayGameSeconds = Mathf.Clamp(data.remainingDayGameSeconds, 0f, DayLengthGameSeconds);
            dayState = data.dayState;
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"TimeManager: Failed to load time state. {ex.Message}");
            return false;
        }
    }

    private void SaveState()
    {
        if (string.IsNullOrEmpty(savePath)) return;

        try
        {
            TimeSaveData data = new TimeSaveData
            {
                gameTimeInSeconds = gameTimeInSeconds,
                currentDayIndex = currentDayIndex,
                remainingDayGameSeconds = remainingDayGameSeconds,
                dayState = dayState
            };
            File.WriteAllText(savePath, JsonUtility.ToJson(data, true));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"TimeManager: Failed to save time state. {ex.Message}");
        }
    }
}

// Timer class for mission countdowns
public class MissionTimer
{
    private float remainingTime;
    private float initialTime;
    
    public System.Action OnExpired { get; set; }
    
    public MissionTimer(float duration)
    {
        initialTime = duration;
        remainingTime = duration;
    }
    
    public void Update(float deltaTime)
    {
        remainingTime -= deltaTime;
        remainingTime = Mathf.Max(0f, remainingTime);
    }
    
    public bool IsExpired()
    {
        return remainingTime <= 0f;
    }
    
    public float GetRemainingTime()
    {
        return remainingTime;
    }
    
    public float GetProgress()
    {
        if (initialTime <= 0f) return 0f;
        return 1f - (remainingTime / initialTime);
    }
    
    public string GetFormattedRemainingTime()
    {
        int minutes = Mathf.FloorToInt(remainingTime / 60f);
        int seconds = Mathf.FloorToInt(remainingTime % 60f);
        return string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}
