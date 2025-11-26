using System.Collections.Generic;
using UnityEngine;

public class TimeManager : MonoBehaviour
{
    [Header("Time Settings")]
    [SerializeField] private float timeScale = 60f; // 1 real second = 60 game seconds (1 game minute)
    [SerializeField] private bool pauseTime = false;
    
    private float gameTimeInSeconds = 0f; // Total game time elapsed
    private List<MissionTimer> activeTimers = new List<MissionTimer>();
    private int currentDayIndex = 0; // Day 0 at game start
    
    public delegate void TimeUpdateDelegate(float gameTimeDelta);
    public event TimeUpdateDelegate OnTimeUpdate;
    public event System.Action<int> OnDayStarted; // Fires with day index (0-based)

    private void Start()
    {
        // Fire day 0 start so systems can run once at beginning
        OnDayStarted?.Invoke(currentDayIndex);
    }
    
    private void Update()
    {
        if (pauseTime) return;
        
        float realDeltaTime = Time.deltaTime;
        float gameDeltaTime = realDeltaTime * timeScale;
        
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
        
        OnTimeUpdate?.Invoke(gameDeltaTime);

        // Check day rollover (1 day = 86400 game seconds)
        int newDayIndex = Mathf.FloorToInt(gameTimeInSeconds / 86400f);
        if (newDayIndex > currentDayIndex)
        {
            currentDayIndex = newDayIndex;
            OnDayStarted?.Invoke(currentDayIndex);
        }
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
