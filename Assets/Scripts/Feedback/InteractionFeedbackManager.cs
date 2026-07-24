using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class InteractionFeedbackManager : MonoBehaviour
{
    private enum UIClickVfxMode
    {
        GeneratedUI = 0,
        Prefab = 1,
        PrefabAndGeneratedUI = 2
    }

    private static InteractionFeedbackManager instance;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip interactionClip;
    [SerializeField] private AudioClip uiClickClip;
    [SerializeField] private AudioClip uiHoverClip;
    [SerializeField] private AudioClip uiDragStartClip;

    [Header("UI Click VFX")]
    [SerializeField] private UIClickVfxMode uiClickVfxMode = UIClickVfxMode.GeneratedUI;
    [SerializeField] private GameObject uiClickVfxPrefab;
    [SerializeField] private RectTransform uiClickVfxParent;
    [SerializeField] private bool useDedicatedUiClickVfxCanvas = true;
    [SerializeField] private int dedicatedUiClickVfxSortingOrder = 32000;
    [SerializeField] private float uiClickVfxLifetime = 2f;
    [FormerlySerializedAs("sparkleGradient")]
    [SerializeField] private Gradient clickSparkleGradient = CreateDefaultSparkleGradient();
    [SerializeField] private float generatedUiClickVfxDuration = 0.32f;
    [SerializeField] private int generatedUiClickSparkleCount = 12;
    [SerializeField] private Vector2 generatedUiClickSparkleSizeRange = new Vector2(5f, 12f);
    [SerializeField] private Vector2 generatedUiClickSparkleDistanceRange = new Vector2(22f, 72f);
    [SerializeField] private float generatedUiClickSparkleGravity = 35f;
    [SerializeField] private float generatedUiClickVfxStartScale = 0.35f;
    [SerializeField] private float generatedUiClickVfxEndScale = 1f;

    [Header("UI Cursor Sparkle Trail")]
    [SerializeField] private bool enableUiCursorSparkleTrail = true;
    [SerializeField] private float cursorSparkleMinMoveDistance = 18f;
    [SerializeField] private float cursorSparkleMinInterval = 0.035f;
    [SerializeField] private int cursorSparkleCount = 2;
    [SerializeField] private Gradient cursorSparkleGradient = CreateDefaultSparkleGradient();
    [SerializeField] private float cursorSparkleDuration = 0.42f;
    [SerializeField] private Vector2 cursorSparkleSizeRange = new Vector2(3f, 8f);
    [SerializeField] private Vector2 cursorSparkleDistanceRange = new Vector2(8f, 28f);
    [SerializeField] private float cursorSparkleGravity = 55f;
    [SerializeField] private bool cursorSparklesOnlyWhenCursorActive = true;

    private RectTransform dedicatedUiClickVfxParent;
    private Sprite generatedClickSparkleSprite;
    private Sprite generatedCursorSparkleSprite;
    private Vector2 lastCursorSparklePosition;
    private float lastCursorSparkleTime;
    private bool hasLastCursorSparklePosition;

    private sealed class GeneratedClickSparkle
    {
        public RectTransform rect;
        public Image image;
        public Vector2 direction;
        public float distance;
        public float startSize;
        public float endSize;
        public float startRotation;
        public float spinDegrees;
    }

    private static Gradient CreateDefaultSparkleGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 1f, 0.92f, 1f), 0f),
                new GradientColorKey(new Color(1f, 0.78f, 0.2f, 1f), 0.45f),
                new GradientColorKey(new Color(1f, 0.55f, 0.08f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            });
        return gradient;
    }

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

    private void OnValidate()
    {
        generatedClickSparkleSprite = null;
        generatedCursorSparkleSprite = null;
    }

    private void Update()
    {
        TrySpawnCursorSparkleTrail();
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

        if (!screenPosition.HasValue && TryGetCurrentPointerPosition(out Vector2 pointerPosition))
        {
            screenPosition = pointerPosition;
        }

        if (!screenPosition.HasValue) return;

        Canvas canvas = ResolveCanvas(contextTransform);
        RectTransform parent = ResolveVfxParent(canvas);
        if (parent == null) return;

        if (uiClickVfxMode == UIClickVfxMode.GeneratedUI || uiClickVfxMode == UIClickVfxMode.PrefabAndGeneratedUI)
        {
            SpawnGeneratedUIClickVfx(parent, screenPosition.Value, canvas);
        }

        if ((uiClickVfxMode == UIClickVfxMode.Prefab || uiClickVfxMode == UIClickVfxMode.PrefabAndGeneratedUI) && uiClickVfxPrefab != null)
        {
            SpawnPrefabUIClickVfx(parent, screenPosition.Value, canvas);
        }
    }

    private void SpawnPrefabUIClickVfx(RectTransform parent, Vector2 screenPosition, Canvas canvas)
    {
        GameObject vfx = Instantiate(uiClickVfxPrefab, parent);
        RectTransform vfxRect = vfx.transform as RectTransform;
        if (vfxRect != null)
        {
            Camera uiCamera = GetCanvasCamera(canvas);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, uiCamera, out Vector2 localPoint))
            {
                vfxRect.anchoredPosition = localPoint;
            }
            vfxRect.localScale = Vector3.one;
            vfxRect.SetAsLastSibling();
        }

        float lifetime = Mathf.Max(0.1f, uiClickVfxLifetime);
        Destroy(vfx, lifetime);
    }

    private void SpawnGeneratedUIClickVfx(RectTransform parent, Vector2 screenPosition, Canvas canvas)
    {
        GameObject root = new GameObject("GeneratedUIClickVFX", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(parent, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one * Mathf.Max(0.01f, generatedUiClickVfxStartScale);
        Camera uiCamera = GetCanvasCamera(canvas);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, uiCamera, out Vector2 localPoint))
        {
            rect.anchoredPosition = localPoint;
        }

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        List<GeneratedClickSparkle> sparkles = CreateClickSparkles(root.transform);

        rect.SetAsLastSibling();
        StartCoroutine(AnimateGeneratedUIClickVfx(root, rect, group, sparkles));
    }

    private void TrySpawnCursorSparkleTrail()
    {
        if (!enableUiCursorSparkleTrail) return;
        if (!TryGetCurrentPointerPosition(out Vector2 pointerPosition)) return;
        if (cursorSparklesOnlyWhenCursorActive && !IsCursorActive())
        {
            hasLastCursorSparklePosition = false;
            return;
        }

        if (!hasLastCursorSparklePosition)
        {
            lastCursorSparklePosition = pointerPosition;
            lastCursorSparkleTime = Time.unscaledTime;
            hasLastCursorSparklePosition = true;
            return;
        }

        float minMove = Mathf.Max(0f, cursorSparkleMinMoveDistance);
        float minInterval = Mathf.Max(0f, cursorSparkleMinInterval);
        if (Vector2.Distance(pointerPosition, lastCursorSparklePosition) < minMove) return;
        if (Time.unscaledTime - lastCursorSparkleTime < minInterval) return;

        Canvas canvas = ResolveCanvas(null);
        RectTransform parent = ResolveVfxParent(canvas);
        if (parent == null) return;

        SpawnCursorSparkleTrail(parent, pointerPosition, canvas);
        lastCursorSparklePosition = pointerPosition;
        lastCursorSparkleTime = Time.unscaledTime;
    }

    private void SpawnCursorSparkleTrail(RectTransform parent, Vector2 screenPosition, Canvas canvas)
    {
        GameObject root = new GameObject("CursorSparkleTrail", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(parent, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
        Camera uiCamera = GetCanvasCamera(canvas);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, uiCamera, out Vector2 localPoint))
        {
            rect.anchoredPosition = localPoint;
        }

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        List<GeneratedClickSparkle> sparkles = CreateSparkles(
            root.transform,
            Mathf.Max(1, cursorSparkleCount),
            cursorSparkleSizeRange,
            cursorSparkleDistanceRange,
            downwardBias: true,
            GetGeneratedSparkleSprite(cursorSparkleGradient, ref generatedCursorSparkleSprite, "GeneratedUICursorSparkleSprite"));

        rect.SetAsLastSibling();
        StartCoroutine(AnimateSparkles(root, rect, group, sparkles, cursorSparkleDuration, 1f, 1f, cursorSparkleGravity));
    }

    private List<GeneratedClickSparkle> CreateClickSparkles(Transform parent)
    {
        return CreateSparkles(
            parent,
            Mathf.Max(1, generatedUiClickSparkleCount),
            generatedUiClickSparkleSizeRange,
            generatedUiClickSparkleDistanceRange,
            downwardBias: false,
            GetGeneratedSparkleSprite(clickSparkleGradient, ref generatedClickSparkleSprite, "GeneratedUIClickSparkleSprite"));
    }

    private List<GeneratedClickSparkle> CreateSparkles(Transform parent, int count, Vector2 sizeRange, Vector2 distanceRange, bool downwardBias, Sprite sparkleSprite)
    {
        var sparkles = new List<GeneratedClickSparkle>(count);
        float minSize = Mathf.Max(1f, Mathf.Min(sizeRange.x, sizeRange.y));
        float maxSize = Mathf.Max(minSize, Mathf.Max(sizeRange.x, sizeRange.y));
        float minDistance = Mathf.Max(0f, Mathf.Min(distanceRange.x, distanceRange.y));
        float maxDistance = Mathf.Max(minDistance, Mathf.Max(distanceRange.x, distanceRange.y));
        float angleStep = 360f / count;
        float angleOffset = Random.Range(0f, angleStep);

        for (int i = 0; i < count; i++)
        {
            float angle = downwardBias
                ? Random.Range(205f, 335f)
                : angleOffset + i * angleStep + Random.Range(-angleStep * 0.35f, angleStep * 0.35f);
            float radians = angle * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
            float size = Random.Range(minSize, maxSize);

            Image image = CreateClickVfxImage(parent, $"Sparkle {i + 1}", size, sparkleSprite);
            RectTransform imageRect = image.transform as RectTransform;
            imageRect.anchoredPosition = Vector2.zero;
            imageRect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            sparkles.Add(new GeneratedClickSparkle
            {
                rect = imageRect,
                image = image,
                direction = direction,
                distance = Random.Range(minDistance, maxDistance),
                startSize = size,
                endSize = size * Random.Range(0.15f, 0.45f),
                startRotation = imageRect.localEulerAngles.z,
                spinDegrees = Random.Range(-220f, 220f)
            });
        }

        return sparkles;
    }

    private Image CreateClickVfxImage(Transform parent, string name, float size, Sprite sparkleSprite)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.one * Mathf.Max(1f, size);
        rect.localScale = Vector3.one;

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sparkleSprite;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private IEnumerator AnimateGeneratedUIClickVfx(GameObject root, RectTransform rect, CanvasGroup group, List<GeneratedClickSparkle> sparkles)
    {
        yield return AnimateSparkles(
            root,
            rect,
            group,
            sparkles,
            generatedUiClickVfxDuration,
            generatedUiClickVfxStartScale,
            generatedUiClickVfxEndScale,
            generatedUiClickSparkleGravity);
    }

    private IEnumerator AnimateSparkles(GameObject root, RectTransform rect, CanvasGroup group, List<GeneratedClickSparkle> sparkles, float animationDuration, float animationStartScale, float animationEndScale, float gravity)
    {
        float duration = Mathf.Max(0.01f, animationDuration);
        float elapsed = 0f;
        float startScale = Mathf.Max(0.01f, animationStartScale);
        float endScale = Mathf.Max(startScale, animationEndScale);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            rect.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, eased);
            group.alpha = 1f - t;

            if (sparkles != null)
            {
                float gravityOffset = gravity * t * t;
                foreach (GeneratedClickSparkle sparkle in sparkles)
                {
                    if (sparkle?.rect == null || sparkle.image == null) continue;
                    sparkle.rect.anchoredPosition = sparkle.direction * (sparkle.distance * eased) + Vector2.down * gravityOffset;
                    sparkle.rect.sizeDelta = Vector2.one * Mathf.Lerp(sparkle.startSize, sparkle.endSize, t);
                    sparkle.rect.localRotation = Quaternion.Euler(0f, 0f, sparkle.startRotation + sparkle.spinDegrees * eased);
                    Color sparkleColor = Color.white;
                    sparkleColor.a = 1f - t;
                    sparkle.image.color = sparkleColor;
                }
            }

            yield return null;
        }

        Destroy(root);
    }

    private RectTransform ResolveVfxParent(Canvas contextCanvas)
    {
        if (useDedicatedUiClickVfxCanvas)
        {
            return GetOrCreateDedicatedUiClickVfxParent(contextCanvas);
        }

        if (uiClickVfxParent != null)
        {
            return uiClickVfxParent;
        }

        return contextCanvas != null ? contextCanvas.transform as RectTransform : null;
    }

    private RectTransform GetOrCreateDedicatedUiClickVfxParent(Canvas contextCanvas)
    {
        if (dedicatedUiClickVfxParent != null)
        {
            return dedicatedUiClickVfxParent;
        }

        GameObject canvasObject = new GameObject("UIClickVfxCanvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = dedicatedUiClickVfxSortingOrder;

        dedicatedUiClickVfxParent = canvasObject.transform as RectTransform;
        dedicatedUiClickVfxParent.anchorMin = Vector2.zero;
        dedicatedUiClickVfxParent.anchorMax = Vector2.one;
        dedicatedUiClickVfxParent.offsetMin = Vector2.zero;
        dedicatedUiClickVfxParent.offsetMax = Vector2.zero;
        return dedicatedUiClickVfxParent;
    }

    private static Camera GetCanvasCamera(Canvas canvas)
    {
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }

    private Sprite GetGeneratedSparkleSprite(Gradient gradient, ref Sprite cachedSprite, string spriteName)
    {
        if (cachedSprite != null)
        {
            return cachedSprite;
        }

        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = spriteName,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float max = center.x;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - center.x) / max;
                float dy = Mathf.Abs(y - center.y) / max;
                float diamond = Mathf.Clamp01(1f - (dx + dy));
                float cross = Mathf.Max(
                    Mathf.Clamp01(1f - dx * 8f) * Mathf.Clamp01(1f - dy * 1.6f),
                    Mathf.Clamp01(1f - dy * 8f) * Mathf.Clamp01(1f - dx * 1.6f));
                float core = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), center) / (max * 0.35f));
                float alpha = Mathf.Clamp01(Mathf.Max(diamond * diamond, cross * 0.55f, core));
                float gradientTime = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), center) / max);
                Color pixel = gradient != null ? gradient.Evaluate(gradientTime) : Color.white;
                pixel.a *= alpha;
                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();
        cachedSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        cachedSprite.name = spriteName;
        return cachedSprite;
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

    private static bool TryGetCurrentPointerPosition(out Vector2 position)
    {
        if (Mouse.current != null)
        {
            position = Mouse.current.position.ReadValue();
            return true;
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            position = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }

        position = default;
        return false;
    }

    private static bool IsCursorActive()
    {
        return Cursor.visible && Cursor.lockState != CursorLockMode.Locked;
    }
}
