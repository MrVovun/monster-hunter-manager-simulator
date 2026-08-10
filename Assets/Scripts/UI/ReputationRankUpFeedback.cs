using System.Collections;
using TMPro;
using UnityEngine;

public class ReputationRankUpFeedback : MonoBehaviour
{
    [SerializeField] private CanvasGroup rootGroup;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private string messageTemplate = "Reputation {new_rank}";
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip rankUpClip;
    [SerializeField] private float fadeInSeconds = 0.25f;
    [SerializeField] private float holdSeconds = 1.5f;
    [SerializeField] private float fadeOutSeconds = 0.5f;

    private ReputationManager reputationManager;
    private Coroutine routine;

    private void Awake()
    {
        if (rootGroup == null)
        {
            rootGroup = GetComponent<CanvasGroup>();
        }

        if (rootGroup != null)
        {
            rootGroup.alpha = 0f;
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;
        }
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        if (reputationManager != null)
        {
            reputationManager.OnReputationRankIncreased -= HandleReputationRankIncreased;
            reputationManager = null;
        }

        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    private void TrySubscribe()
    {
        if (reputationManager != null) return;
        if (GameManager.Instance == null) return;

        reputationManager = GameManager.Instance.GetReputationManager();
        if (reputationManager != null)
        {
            reputationManager.OnReputationRankIncreased += HandleReputationRankIncreased;
        }
    }

    private void HandleReputationRankIncreased(int previousRank, int newRank)
    {
        string message = messageTemplate
            .Replace("{previous_rank}", previousRank.ToString())
            .Replace("{new_rank}", newRank.ToString());

        if (messageText != null)
        {
            messageText.text = message;
        }

        if (audioSource != null && rankUpClip != null)
        {
            audioSource.PlayOneShot(rankUpClip);
        }

        if (routine != null)
        {
            StopCoroutine(routine);
        }

        routine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        yield return FadeTo(1f, fadeInSeconds);
        if (holdSeconds > 0f)
        {
            yield return new WaitForSeconds(holdSeconds);
        }
        yield return FadeTo(0f, fadeOutSeconds);
        routine = null;
    }

    private IEnumerator FadeTo(float targetAlpha, float seconds)
    {
        if (rootGroup == null)
        {
            yield break;
        }

        float startAlpha = rootGroup.alpha;
        float duration = Mathf.Max(0f, seconds);
        if (duration <= 0f)
        {
            rootGroup.alpha = targetAlpha;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            rootGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        rootGroup.alpha = targetAlpha;
    }
}
