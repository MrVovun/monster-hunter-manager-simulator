using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Simple feedback for action-based time advances: shows a toast and plays a tick.
/// </summary>
public class TimeAdvanceFeedback : MonoBehaviour
{
    [SerializeField] private TimeManager timeManager;
    [SerializeField] private TMP_Text toastText;
    [SerializeField] private float toastDuration = 1.5f;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip tickClip;

    private Coroutine toastRoutine;

    private void OnEnable()
    {
        EnsureTimeManager();
        if (timeManager != null)
        {
            timeManager.OnTimeAdvanced += HandleTimeAdvanced;
        }
    }

    private void OnDisable()
    {
        if (timeManager != null)
        {
            timeManager.OnTimeAdvanced -= HandleTimeAdvanced;
        }
    }

    private void EnsureTimeManager()
    {
        if (timeManager == null && GameManager.Instance != null)
        {
            timeManager = GameManager.Instance.GetTimeManager();
        }
        if (timeManager == null)
        {
            timeManager = SceneLookup.Find<TimeManager>();
        }
    }

    private void HandleTimeAdvanced(float seconds)
    {
        if (seconds <= 0f) return;

        if (audioSource != null && tickClip != null)
        {
            audioSource.PlayOneShot(tickClip);
        }

        if (toastText != null)
        {
            if (toastRoutine != null)
            {
                StopCoroutine(toastRoutine);
            }
            toastRoutine = StartCoroutine(ShowToast(seconds));
        }
    }

    private IEnumerator ShowToast(float seconds)
    {
        if (toastText == null) yield break;

        int minutesPart = Mathf.FloorToInt(seconds / 60f);
        int secondsPart = Mathf.FloorToInt(seconds % 60f);
        if (minutesPart > 0)
        {
            toastText.text = $"+{minutesPart}m {secondsPart}s";
        }
        else
        {
            toastText.text = $"+{secondsPart}s";
        }
        toastText.gameObject.SetActive(true);

        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, toastDuration));

        toastText.gameObject.SetActive(false);
        toastRoutine = null;
    }
}
