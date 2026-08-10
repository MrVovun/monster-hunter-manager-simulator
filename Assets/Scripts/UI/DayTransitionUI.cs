using System.Collections;
using UnityEngine;

public class DayTransitionUI : MonoBehaviour
{
    public static DayTransitionUI Instance { get; private set; }

    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField] private float fadeOutSeconds = 0.45f;
    [SerializeField] private float blackHoldSeconds = 0.35f;
    [SerializeField] private float fadeInSeconds = 0.45f;

    private Coroutine routine;
    private FirstPersonController lockedController;
    private bool lockedControllerWasAlreadyLocked;

    public bool IsPlaying => routine != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (fadeGroup == null)
        {
            fadeGroup = GetComponent<CanvasGroup>();
        }

        if (fadeGroup != null)
        {
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
            fadeGroup.interactable = false;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public bool Play(System.Action midpointAction, FirstPersonController controller = null)
    {
        if (!isActiveAndEnabled || fadeGroup == null || routine != null)
        {
            return false;
        }

        routine = StartCoroutine(PlayRoutine(midpointAction, controller));
        return true;
    }

    private IEnumerator PlayRoutine(System.Action midpointAction, FirstPersonController controller)
    {
        LockPlayer(controller);
        fadeGroup.blocksRaycasts = true;

        yield return FadeTo(1f, fadeOutSeconds);
        midpointAction?.Invoke();

        if (blackHoldSeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(blackHoldSeconds);
        }

        yield return FadeTo(0f, fadeInSeconds);

        fadeGroup.blocksRaycasts = false;
        UnlockPlayer();
        routine = null;
    }

    private IEnumerator FadeTo(float targetAlpha, float seconds)
    {
        float startAlpha = fadeGroup.alpha;
        float duration = Mathf.Max(0f, seconds);
        if (duration <= 0f)
        {
            fadeGroup.alpha = targetAlpha;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            fadeGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        fadeGroup.alpha = targetAlpha;
    }

    private void LockPlayer(FirstPersonController controller)
    {
        lockedController = controller != null ? controller : FindFirstObjectByType<FirstPersonController>();
        if (lockedController == null) return;

        lockedControllerWasAlreadyLocked = lockedController.IsMovementLocked();
        if (!lockedControllerWasAlreadyLocked)
        {
            lockedController.LockMovement();
        }
    }

    private void UnlockPlayer()
    {
        if (lockedController != null && !lockedControllerWasAlreadyLocked)
        {
            lockedController.UnlockMovement();
        }

        lockedController = null;
        lockedControllerWasAlreadyLocked = false;
    }
}
