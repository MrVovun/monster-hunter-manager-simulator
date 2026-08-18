using UnityEngine;
using UnityEngine.UI;

public class BriefingChalkHolder : MonoBehaviour
{
    [SerializeField] private GameObject heldChalkVisual;
    [SerializeField] private bool followCursor = true;
    [SerializeField] private Vector2 cursorOffset;
    [SerializeField] private Camera worldCursorCamera;
    [SerializeField] private float worldCursorDistance = 0.5f;

    [Header("Drawing Mode")]
    [SerializeField] private Vector3 drawingScaleMultiplier = Vector3.one;
    [SerializeField] private Vector3 drawingLocalRotationOffset;

    private RectTransform rectTransform;
    private Graphic[] graphics;
    private CanvasGroup canvasGroup;
    private Transform visualTransform;
    private Vector3 defaultLocalPosition;
    private Quaternion defaultLocalRotation;
    private Vector3 defaultLocalScale;
    private bool defaultTransformCaptured;
    private static bool drawingModeActive;
    private static Camera activeDrawingCamera;
    private static float activeWorldCursorDistance = -1f;
    private static Collider activeDrawingSurfaceCollider;
    private static float activeDrawingSurfaceOffset = 0.02f;

    public static void RefreshAll()
    {
        var holders = SceneLookup.FindAll<BriefingChalkHolder>(true);
        foreach (var holder in holders)
        {
            if (holder != null)
            {
                holder.ApplyVisualState();
            }
        }
    }

    public static void SetDrawingModeActive(bool value, Camera drawingCamera = null, float worldCursorDistance = -1f, Collider drawingSurfaceCollider = null, float drawingSurfaceOffset = 0.02f)
    {
        drawingModeActive = value;
        activeDrawingCamera = value ? drawingCamera : null;
        activeWorldCursorDistance = value ? worldCursorDistance : -1f;
        activeDrawingSurfaceCollider = value ? drawingSurfaceCollider : null;
        activeDrawingSurfaceOffset = Mathf.Max(0f, drawingSurfaceOffset);

        var holders = SceneLookup.FindAll<BriefingChalkHolder>(true);
        foreach (var holder in holders)
        {
            if (holder != null)
            {
                holder.ApplyDrawingModeState(value);
                holder.ApplyVisualState();
            }
        }
    }

    private void Reset()
    {
        if (heldChalkVisual == null)
        {
            heldChalkVisual = gameObject;
        }
    }

    private void Awake()
    {
        if (heldChalkVisual == null)
        {
            heldChalkVisual = gameObject;
        }

        rectTransform = heldChalkVisual != null ? heldChalkVisual.GetComponent<RectTransform>() : GetComponent<RectTransform>();
        GameObject visual = heldChalkVisual != null ? heldChalkVisual : gameObject;
        visualTransform = visual != null ? visual.transform : transform;
        graphics = visual != null ? visual.GetComponentsInChildren<Graphic>(true) : null;
        canvasGroup = visual != null ? visual.GetComponent<CanvasGroup>() : null;
        ApplyVisualState();
    }

    private void Update()
    {
        if (!followCursor || !BriefingChalkPickup.HasChalk || !drawingModeActive) return;
        FollowCursor();
    }

    private void OnEnable()
    {
        BriefingChalkPickup.OnChalkChanged += HandleChalkChanged;
        ApplyVisualState();
    }

    private void OnDisable()
    {
        BriefingChalkPickup.OnChalkChanged -= HandleChalkChanged;
    }

    private void HandleChalkChanged(bool _)
    {
        ApplyVisualState();
    }

    public void ApplyVisualState()
    {
        GameObject visual = heldChalkVisual != null ? heldChalkVisual : gameObject;
        if (visual != null)
        {
            visual.SetActive(BriefingChalkPickup.HasChalk);
        }

        bool blocksRaycasts = false;
        if (graphics != null)
        {
            foreach (var graphic in graphics)
            {
                if (graphic != null)
                {
                    graphic.raycastTarget = blocksRaycasts;
                }
            }
        }

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = blocksRaycasts;
            canvasGroup.interactable = false;
        }
    }

    private void ApplyDrawingModeState(bool active)
    {
        if (visualTransform == null)
        {
            GameObject visual = heldChalkVisual != null ? heldChalkVisual : gameObject;
            visualTransform = visual != null ? visual.transform : transform;
        }

        if (visualTransform == null) return;

        if (active)
        {
            CaptureDefaultTransform();
            visualTransform.localScale = Vector3.Scale(defaultLocalScale, drawingScaleMultiplier);
            visualTransform.localRotation = defaultLocalRotation * Quaternion.Euler(drawingLocalRotationOffset);
            return;
        }

        RestoreDefaultTransform();
    }

    private void CaptureDefaultTransform()
    {
        if (visualTransform == null || defaultTransformCaptured) return;

        defaultLocalPosition = visualTransform.localPosition;
        defaultLocalRotation = visualTransform.localRotation;
        defaultLocalScale = visualTransform.localScale;
        defaultTransformCaptured = true;
    }

    private void RestoreDefaultTransform()
    {
        if (visualTransform == null || !defaultTransformCaptured) return;

        visualTransform.localPosition = defaultLocalPosition;
        visualTransform.localRotation = defaultLocalRotation;
        visualTransform.localScale = defaultLocalScale;
        defaultTransformCaptured = false;
    }

    private void FollowCursor()
    {
        Vector2 pointerPosition = InputKeyUtility.GetPointerPosition();
        pointerPosition += cursorOffset;

        if (rectTransform != null)
        {
            rectTransform.position = pointerPosition;
            return;
        }

        Transform target = visualTransform != null ? visualTransform : heldChalkVisual != null ? heldChalkVisual.transform : transform;
        Camera cameraToUse = worldCursorCamera != null ? worldCursorCamera : activeDrawingCamera != null ? activeDrawingCamera : Camera.main;
        if (target == null || cameraToUse == null) return;

        Ray ray = cameraToUse.ScreenPointToRay(pointerPosition);
        if (activeDrawingSurfaceCollider != null && activeDrawingSurfaceCollider.Raycast(ray, out RaycastHit hit, 100f))
        {
            target.position = hit.point - ray.direction.normalized * activeDrawingSurfaceOffset;
            return;
        }

        float distance = activeWorldCursorDistance > 0f ? activeWorldCursorDistance : worldCursorDistance;
        Vector3 screenPosition = new Vector3(pointerPosition.x, pointerPosition.y, Mathf.Max(0.01f, distance));
        target.position = cameraToUse.ScreenToWorldPoint(screenPosition);
    }
}
