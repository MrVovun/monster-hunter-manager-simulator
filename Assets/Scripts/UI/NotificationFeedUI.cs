using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NotificationFeedUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NotificationManager notificationManager;
    [SerializeField] private Transform feedParent;
    [SerializeField] private GameObject feedItemPrefab;
    [SerializeField] private ScrollRect feedScrollRect;
    [SerializeField] private GameObject toastRoot;
    [SerializeField] private TMP_Text toastText;

    [Header("Limits")]
    [SerializeField] private int maxVisibleFeedEntries = 50;
    [SerializeField] private float toastDuration = 2.0f;
    [SerializeField] private float toastFadeDuration = 0.45f;
    [SerializeField] private bool autoScrollOnNewEntry = true;
    [SerializeField] private bool autoScrollOnlyWhenNearBottom = true;
    [SerializeField] [Range(0f, 0.25f)] private float nearBottomThreshold = 0.05f;
    [SerializeField] private bool smoothAutoScroll = true;
    [SerializeField] [Range(0.01f, 1f)] private float autoScrollDuration = 0.18f;

    [Header("Severity Colors")]
    [SerializeField] private Color infoColor = new Color(0.85f, 0.9f, 1f, 1f);
    [SerializeField] private Color successColor = new Color(0.8f, 1f, 0.8f, 1f);
    [SerializeField] private Color warningColor = new Color(1f, 0.85f, 0.7f, 1f);

    [Header("Notification Audio (Optional)")]
    [SerializeField] private bool playSoundOnNotificationAdded = true;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip defaultNotificationClip;
    [SerializeField] private AudioClip infoToastClip;
    [SerializeField] private AudioClip successToastClip;
    [SerializeField] private AudioClip warningToastClip;

    private readonly List<GameObject> spawnedItems = new List<GameObject>();
    private readonly Queue<NotificationEntry> toastQueue = new Queue<NotificationEntry>();
    private Coroutine toastRoutine;
    private Coroutine scrollRoutine;
    private CanvasGroup toastCanvasGroup;

    private void OnEnable()
    {
        EnsureToastRoot();
        EnsureFeedScrollRect();
        ResolveManager();
        if (notificationManager != null)
        {
            notificationManager.OnNotificationAdded += HandleNotificationAdded;
            notificationManager.OnHistoryCleared += HandleHistoryCleared;
            RebuildFromHistory();
        }
    }

    private void OnDisable()
    {
        if (notificationManager != null)
        {
            notificationManager.OnNotificationAdded -= HandleNotificationAdded;
            notificationManager.OnHistoryCleared -= HandleHistoryCleared;
        }

        if (toastRoutine != null)
        {
            StopCoroutine(toastRoutine);
            toastRoutine = null;
        }
        if (scrollRoutine != null)
        {
            StopCoroutine(scrollRoutine);
            scrollRoutine = null;
        }
        SetToastVisible(false);
    }

    private void ResolveManager()
    {
        if (notificationManager != null) return;
        if (GameManager.Instance != null)
        {
            notificationManager = GameManager.Instance.GetNotificationManager();
        }
        if (notificationManager == null)
        {
            notificationManager = SceneLookup.Find<NotificationManager>();
        }
    }

    private void EnsureFeedScrollRect()
    {
        if (feedScrollRect != null) return;
        feedScrollRect = GetComponentInChildren<ScrollRect>(true);
        if (feedScrollRect == null)
        {
            feedScrollRect = GetComponentInParent<ScrollRect>();
        }
    }

    private void RebuildFromHistory()
    {
        ClearFeed();
        if (notificationManager == null || feedParent == null || feedItemPrefab == null) return;

        var history = notificationManager.GetHistory();
        int maxEntries = Mathf.Max(1, maxVisibleFeedEntries);
        int start = Mathf.Max(0, history.Count - maxEntries);
        for (int i = start; i < history.Count; i++)
        {
            AppendFeedItem(history[i]);
        }

        if (autoScrollOnNewEntry)
        {
            QueueScrollToBottom();
        }
    }

    private void HandleNotificationAdded(NotificationEntry entry)
    {
        if (entry == null) return;
        bool wasNearBottom = IsNearBottom();
        AppendFeedItem(entry);
        if (autoScrollOnNewEntry && (!autoScrollOnlyWhenNearBottom || wasNearBottom))
        {
            QueueScrollToBottom();
        }
        PlayNotificationAudio(entry.severity);
        EnqueueToast(entry);
    }

    private void HandleHistoryCleared()
    {
        ClearFeed();
    }

    private void AppendFeedItem(NotificationEntry entry)
    {
        if (feedParent == null || feedItemPrefab == null || entry == null) return;

        GameObject item = Instantiate(feedItemPrefab, feedParent);
        spawnedItems.Add(item);

        TMP_Text text = item.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = BuildItemText(entry);
        }

        Image background = item.GetComponent<Image>();
        if (background != null)
        {
            background.color = GetSeverityColor(entry.severity);
        }

        TrimVisibleFeed();
    }

    private void EnqueueToast(NotificationEntry entry)
    {
        if (toastText == null || entry == null) return;
        toastQueue.Enqueue(entry);
        if (toastRoutine == null)
        {
            toastRoutine = StartCoroutine(ProcessToastQueue());
        }
    }

    private IEnumerator ProcessToastQueue()
    {
        while (toastQueue.Count > 0)
        {
            NotificationEntry entry = toastQueue.Dequeue();
            if (toastText == null || entry == null) continue;

            toastText.text = BuildToastText(entry);
            toastText.color = GetSeverityColor(entry.severity);
            SetToastAlpha(1f);
            SetToastVisible(true);

            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, toastDuration));

            float fade = Mathf.Max(0f, toastFadeDuration);
            if (fade > 0f)
            {
                float elapsed = 0f;
                while (elapsed < fade)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / fade);
                    SetToastAlpha(1f - t);
                    yield return null;
                }
            }

            SetToastAlpha(1f);
            SetToastVisible(false);
        }

        toastRoutine = null;
    }

    private string BuildItemText(NotificationEntry entry)
    {
        string timeLabel = GetLocalTimeLabel(entry.timestampUtc);
        if (string.IsNullOrWhiteSpace(entry.body))
        {
            return $"[{timeLabel}] {entry.title}";
        }
        return $"[{timeLabel}] {entry.title}\n{entry.body}";
    }

    private string BuildToastText(NotificationEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.body))
        {
            return entry.title;
        }
        return $"{entry.title}: {entry.body}";
    }

    private void TrimVisibleFeed()
    {
        int maxEntries = Mathf.Max(1, maxVisibleFeedEntries);
        while (spawnedItems.Count > maxEntries)
        {
            GameObject oldest = spawnedItems[0];
            spawnedItems.RemoveAt(0);
            if (oldest != null)
            {
                Destroy(oldest);
            }
        }
    }

    private void ClearFeed()
    {
        foreach (var item in spawnedItems)
        {
            if (item != null)
            {
                Destroy(item);
            }
        }
        spawnedItems.Clear();
    }

    private Color GetSeverityColor(NotificationSeverity severity)
    {
        switch (severity)
        {
            case NotificationSeverity.Success:
                return successColor;
            case NotificationSeverity.Warning:
                return warningColor;
            default:
                return infoColor;
        }
    }

    private void PlayNotificationAudio(NotificationSeverity severity)
    {
        if (!playSoundOnNotificationAdded || audioSource == null) return;
        AudioClip clip = null;
        switch (severity)
        {
            case NotificationSeverity.Success:
                clip = successToastClip;
                break;
            case NotificationSeverity.Warning:
                clip = warningToastClip;
                break;
            default:
                clip = infoToastClip;
                break;
        }

        if (clip == null)
        {
            clip = defaultNotificationClip;
        }

        if (clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    private string GetLocalTimeLabel(string timestampUtc)
    {
        if (string.IsNullOrWhiteSpace(timestampUtc))
        {
            return "--:--";
        }

        if (DateTime.TryParse(timestampUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var utc))
        {
            DateTime local = utc.Kind == DateTimeKind.Utc ? utc.ToLocalTime() : utc;
            return local.ToString("HH:mm");
        }

        return "--:--";
    }

    private void EnsureToastRoot()
    {
        if (toastRoot == null && toastText != null)
        {
            Transform parent = toastText.transform.parent;
            toastRoot = parent != null ? parent.gameObject : toastText.gameObject;
        }

        if (toastRoot != null)
        {
            toastCanvasGroup = toastRoot.GetComponent<CanvasGroup>();
            if (toastCanvasGroup == null)
            {
                toastCanvasGroup = toastRoot.AddComponent<CanvasGroup>();
            }
            toastCanvasGroup.interactable = false;
            toastCanvasGroup.blocksRaycasts = false;

            foreach (var graphic in toastRoot.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }

            SetToastAlpha(1f);
            SetToastVisible(false);
        }
    }

    private void SetToastVisible(bool visible)
    {
        if (toastRoot != null)
        {
            toastRoot.SetActive(visible);
            return;
        }

        if (toastText != null)
        {
            toastText.gameObject.SetActive(visible);
        }
    }

    private void SetToastAlpha(float alpha)
    {
        if (toastCanvasGroup != null)
        {
            toastCanvasGroup.alpha = Mathf.Clamp01(alpha);
            return;
        }

        if (toastText != null)
        {
            Color c = toastText.color;
            c.a = Mathf.Clamp01(alpha);
            toastText.color = c;
        }
    }

    private bool IsNearBottom()
    {
        if (feedScrollRect == null) return true;
        return feedScrollRect.verticalNormalizedPosition <= Mathf.Max(0f, nearBottomThreshold);
    }

    private void QueueScrollToBottom()
    {
        if (feedScrollRect == null) return;
        if (scrollRoutine != null)
        {
            StopCoroutine(scrollRoutine);
        }
        scrollRoutine = StartCoroutine(ScrollToBottomNextFrame());
    }

    private IEnumerator ScrollToBottomNextFrame()
    {
        // Let layout groups/content size fitters settle first.
        yield return null;
        yield return new WaitForEndOfFrame();

        if (feedScrollRect == null)
        {
            scrollRoutine = null;
            yield break;
        }

        Canvas.ForceUpdateCanvases();
        RectTransform contentRect = feedScrollRect.content != null ? feedScrollRect.content : feedParent as RectTransform;
        if (contentRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        }

        // Vertical normalized: 1 = top, 0 = bottom.
        if (!smoothAutoScroll)
        {
            feedScrollRect.verticalNormalizedPosition = 0f;
            scrollRoutine = null;
            yield break;
        }

        float start = feedScrollRect.verticalNormalizedPosition;
        float duration = Mathf.Max(0.01f, autoScrollDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = 1f - Mathf.Pow(1f - t, 3f);
            feedScrollRect.verticalNormalizedPosition = Mathf.Lerp(start, 0f, t);
            yield return null;
        }

        feedScrollRect.verticalNormalizedPosition = 0f;
        scrollRoutine = null;
    }
}
