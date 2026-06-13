using UnityEngine;

public class InteractionFeedbackManager : MonoBehaviour
{
    private static InteractionFeedbackManager instance;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip interactionClip;
    [SerializeField] private AudioClip uiClickClip;
    [SerializeField] private AudioClip uiHoverClip;
    [SerializeField] private AudioClip uiDragStartClip;

    [Header("UI Click VFX")]
    [SerializeField] private GameObject uiClickVfxPrefab;
    [SerializeField] private RectTransform uiClickVfxParent;
    [SerializeField] private float uiClickVfxLifetime = 2f;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("Multiple InteractionFeedbackManager instances found. The first active instance will be used.", this);
            return;
        }

        instance = this;
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public static void PlayInteraction(Vector3 position)
    {
        ResolveInstance();
        if (instance == null) return;
        instance.PlayInteractionInternal(position);
    }

    public static void PlayUIClick()
    {
        ResolveInstance();
        if (instance == null) return;
        instance.PlayUIClickInternal(null, null);
    }

    public static void PlayUIClick(Vector2 screenPosition, Transform contextTransform)
    {
        ResolveInstance();
        if (instance == null) return;
        instance.PlayUIClickInternal(screenPosition, contextTransform);
    }

    public static void PlayUIHover()
    {
        ResolveInstance();
        if (instance == null) return;
        instance.PlayClip(instance.uiHoverClip);
    }

    public static void PlayUIDragStart()
    {
        ResolveInstance();
        if (instance == null) return;
        instance.PlayClip(instance.uiDragStartClip);
    }

    private static void ResolveInstance()
    {
        if (instance != null) return;
        instance = FindFirstObjectByType<InteractionFeedbackManager>();
    }

    private void PlayInteractionInternal(Vector3 position)
    {
        PlayClip(interactionClip);
    }

    private void PlayUIClickInternal(Vector2? screenPosition, Transform contextTransform)
    {
        PlayClip(uiClickClip);

        if (uiClickVfxPrefab == null || !screenPosition.HasValue) return;

        Canvas canvas = ResolveCanvas(contextTransform);
        RectTransform parent = uiClickVfxParent;
        if (parent == null && canvas != null)
        {
            parent = canvas.transform as RectTransform;
        }
        if (parent == null) return;

        GameObject vfx = Instantiate(uiClickVfxPrefab, parent);
        RectTransform vfxRect = vfx.transform as RectTransform;
        if (vfxRect != null)
        {
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition.Value, uiCamera, out Vector2 localPoint))
            {
                vfxRect.anchoredPosition = localPoint;
            }
            vfxRect.localScale = Vector3.one;
            vfxRect.SetAsLastSibling();
        }

        float lifetime = Mathf.Max(0.1f, uiClickVfxLifetime);
        Destroy(vfx, lifetime);
    }

    private Canvas ResolveCanvas(Transform contextTransform)
    {
        if (uiClickVfxParent != null)
        {
            return uiClickVfxParent.GetComponentInParent<Canvas>();
        }

        if (contextTransform != null)
        {
            Canvas contextCanvas = contextTransform.GetComponentInParent<Canvas>();
            if (contextCanvas != null) return contextCanvas;
        }

        return FindFirstObjectByType<Canvas>();
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null) return;

        if (audioSource != null)
        {
            audioSource.PlayOneShot(clip);
            return;
        }

        AudioSource.PlayClipAtPoint(clip, transform.position);
    }
}
