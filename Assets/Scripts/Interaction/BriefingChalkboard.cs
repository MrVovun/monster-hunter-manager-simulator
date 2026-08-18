using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BriefingChalkboard : Interactable
{
    private enum LocalAxis
    {
        X,
        Y,
        Z
    }

    private enum PaintMappingMode
    {
        Auto,
        MeshUV,
        LocalBounds
    }

    [Header("Board")]
    [SerializeField] private Renderer boardRenderer;
    [SerializeField] private Collider boardCollider;
    [Tooltip("Material slot on Board Renderer that represents the drawable board surface. If drawing appears on the frame/table edge, change this index.")]
    [SerializeField] private int drawableMaterialIndex = 0;
    [SerializeField] private string textureProperty = "_BaseMap";
    [Tooltip("Replaces the drawable material at runtime with a simple texture material. Useful for a thin paint plane in front of imported meshes with strange shaders/materials.")]
    [SerializeField] private bool useGeneratedPaintMaterial;
    [SerializeField] private int textureWidth = 1024;
    [SerializeField] private int textureHeight = 512;
    [SerializeField] private Color boardColor = new Color(0.03f, 0.08f, 0.06f, 1f);
    [SerializeField] private Color chalkColor = Color.white;

    [Header("Brush")]
    [SerializeField] private int brushRadiusPixels = 6;
    [Tooltip("Auto uses collider UVs when available. For a separate paint plane, leave this on Auto.")]
    [SerializeField] private PaintMappingMode paintMappingMode = PaintMappingMode.Auto;
    [SerializeField] private LocalAxis fallbackUAxis = LocalAxis.X;
    [SerializeField] private LocalAxis fallbackVAxis = LocalAxis.Y;

    [Header("Input")]
    [SerializeField] private KeyCode finishKey = KeyCode.E;

    [Header("Camera Transition")]
    [SerializeField] private bool disableDrawingCameraOnStart = true;
    [SerializeField] private bool animateCameraSwitch = true;
    [SerializeField] private float cameraTransitionDuration = 0.5f;

    [Header("Chalk Cursor")]
    [Tooltip("Moves the held chalk slightly toward the drawing camera from the board surface so it does not clip into the board.")]
    [SerializeField] private float chalkCursorDepthOffset = 0.05f;

    private Texture2D drawingTexture;
    private Material runtimeMaterial;
    private PlayerInteraction activePlayer;
    private bool isDrawing;
    private float drawingStartedAt;
    private CursorLockMode previousCursorLockMode;
    private bool previousCursorVisible;
    private Vector3 drawingCameraHomePosition;
    private Quaternion drawingCameraHomeRotation;
    private bool drawingCameraHomeCached;
    private Camera lastPlayerCamera;
    private Coroutine cameraTransitionRoutine;
    private Camera disabledPlayerCamera;
    private Camera disabledMainCamera;
    private bool finishInputArmed;
    private int drawingStartedFrame;
    private bool isDirty;
    private bool hasLastPaintUV;
    private Vector2 lastPaintUV;

    public bool IsDirty => isDirty;

    private void Reset()
    {
        interactionPrompt = "[E] Draw";
        interactionType = InteractionType.Trigger;
        locksPlayer = true;
        useCustomCamera = true;
        boardRenderer = GetComponent<Renderer>();
        boardCollider = GetComponent<Collider>();
    }

    private void Awake()
    {
        if (boardRenderer == null) boardRenderer = GetComponent<Renderer>();
        if (boardCollider == null) boardCollider = GetComponent<Collider>();
        CacheDrawingCameraHome();
        if (disableDrawingCameraOnStart && customCamera != null)
        {
            customCamera.gameObject.SetActive(false);
        }
        CreateBoardTexture();
    }

    private void OnEnable()
    {
        BriefingRoomManager.Instance?.RegisterChalkboard(this);
    }

    private void OnDisable()
    {
        if (cameraTransitionRoutine != null)
        {
            StopCoroutine(cameraTransitionRoutine);
            cameraTransitionRoutine = null;
        }

        if (isDrawing)
        {
            FinishDrawing();
        }
        else
        {
            RestoreSourceCameras();
            if (customCamera != null)
            {
                customCamera.enabled = false;
                customCamera.gameObject.SetActive(false);
            }
        }
        BriefingRoomManager.Instance?.UnregisterChalkboard(this);
    }

    private void Update()
    {
        if (!isDrawing) return;

        if (WasFinishPressed())
        {
            FinishDrawing();
            return;
        }

        if (IsPaintHeld())
        {
            PaintFromPointer();
        }
        else
        {
            hasLastPaintUV = false;
        }
    }

    public override bool IsInteractionAvailable()
    {
        return base.IsInteractionAvailable() && BriefingChalkPickup.HasChalk;
    }

    public override void Interact(PlayerInteraction player)
    {
        if (isDrawing)
        {
            FinishDrawing();
            return;
        }

        if (!BriefingChalkPickup.HasChalk) return;

        activePlayer = player;
        previousCursorLockMode = Cursor.lockState;
        previousCursorVisible = Cursor.visible;

        OnInteractionStart(player);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false;
        Camera drawingCamera = GetActiveDrawingCamera();
        BriefingChalkHolder.SetDrawingModeActive(true, drawingCamera, GetChalkCursorDistance(drawingCamera), boardCollider, chalkCursorDepthOffset);
        drawingStartedAt = Time.time;
        drawingStartedFrame = Time.frameCount;
        finishInputArmed = false;
        hasLastPaintUV = false;
        isDrawing = true;
    }

    public void ClearBoard()
    {
        if (drawingTexture == null)
        {
            CreateBoardTexture();
        }
        if (drawingTexture == null) return;

        Color[] pixels = new Color[drawingTexture.width * drawingTexture.height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = boardColor;
        }

        drawingTexture.SetPixels(pixels);
        drawingTexture.Apply(false);
        isDirty = false;
    }

    private void FinishDrawing()
    {
        if (!isDrawing) return;

        isDrawing = false;
        finishInputArmed = false;
        hasLastPaintUV = false;
        BriefingChalkHolder.SetDrawingModeActive(false);
        float drawingSeconds = Mathf.Max(0f, Time.time - drawingStartedAt);
        Cursor.lockState = previousCursorLockMode;
        Cursor.visible = previousCursorVisible;

        var player = activePlayer;
        activePlayer = null;
        if (player != null)
        {
            OnInteractionEnd(player);
        }

        BriefingChalkPickup.SetHasChalk(false);
        BriefingRoomManager.Instance?.CompleteDrawing(drawingSeconds);
    }

    protected override void HandleCameraSwitch(PlayerInteraction player, bool entered)
    {
        if (!useCustomCamera || customCamera == null)
        {
            base.HandleCameraSwitch(player, entered);
            return;
        }

        Camera playerCamera = player != null ? player.GetPlayerCamera() : lastPlayerCamera;
        if (playerCamera != null)
        {
            lastPlayerCamera = playerCamera;
        }

        if (cameraTransitionRoutine != null)
        {
            StopCoroutine(cameraTransitionRoutine);
            cameraTransitionRoutine = null;
        }

        if (!isActiveAndEnabled || !animateCameraSwitch || cameraTransitionDuration <= 0f)
        {
            SwitchCameraImmediate(entered, playerCamera);
            return;
        }

        cameraTransitionRoutine = StartCoroutine(HandleCameraTransition(entered, playerCamera));
    }

    private Camera GetActiveDrawingCamera()
    {
        if (customCamera != null) return customCamera;
        return Camera.main;
    }

    private float GetChalkCursorDistance(Camera drawingCamera)
    {
        if (drawingCamera == null) return -1f;

        Vector3 boardPoint = transform.position;
        if (boardCollider != null)
        {
            boardPoint = boardCollider.bounds.center;
        }
        else if (boardRenderer != null)
        {
            boardPoint = boardRenderer.bounds.center;
        }

        float depth = Vector3.Dot(boardPoint - drawingCamera.transform.position, drawingCamera.transform.forward);
        return Mathf.Max(0.01f, depth - Mathf.Max(0f, chalkCursorDepthOffset));
    }

    private void CreateBoardTexture()
    {
        if (boardRenderer == null) return;

        runtimeMaterial = useGeneratedPaintMaterial ? CreateGeneratedPaintMaterial() : GetDrawableMaterial();
        if (runtimeMaterial == null) return;

        drawingTexture = new Texture2D(Mathf.Max(64, textureWidth), Mathf.Max(64, textureHeight), TextureFormat.RGBA32, false);
        drawingTexture.wrapMode = TextureWrapMode.Clamp;
        drawingTexture.filterMode = FilterMode.Bilinear;

        ClearBoard();
        ApplyDrawingTextureToMaterial();
    }

    private Material GetDrawableMaterial()
    {
        if (boardRenderer == null) return null;

        Material[] materials = boardRenderer.materials;
        if (materials == null || materials.Length == 0) return boardRenderer.material;

        int index = Mathf.Clamp(drawableMaterialIndex, 0, materials.Length - 1);
        if (index != drawableMaterialIndex)
        {
            Debug.LogWarning($"BriefingChalkboard: Drawable Material Index {drawableMaterialIndex} is outside material range. Using {index}.", this);
        }

        Material material = materials[index];
        boardRenderer.materials = materials;
        return material;
    }

    private Material CreateGeneratedPaintMaterial()
    {
        if (boardRenderer == null) return null;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Texture");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null)
        {
            Debug.LogWarning("BriefingChalkboard: Could not find a usable generated paint shader. Falling back to the assigned drawable material.", this);
            return GetDrawableMaterial();
        }

        Material material = new Material(shader)
        {
            name = $"{name}_RuntimeChalkPaint"
        };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", Color.white);
        }
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", Color.white);
        }

        AssignDrawableMaterial(material);
        return material;
    }

    private void AssignDrawableMaterial(Material material)
    {
        if (boardRenderer == null || material == null) return;

        Material[] materials = boardRenderer.materials;
        if (materials == null || materials.Length == 0)
        {
            boardRenderer.material = material;
            return;
        }

        int index = Mathf.Clamp(drawableMaterialIndex, 0, materials.Length - 1);
        materials[index] = material;
        boardRenderer.materials = materials;
    }

    private void ApplyDrawingTextureToMaterial()
    {
        if (runtimeMaterial == null || drawingTexture == null) return;

        bool assigned = false;
        if (!string.IsNullOrWhiteSpace(textureProperty) && runtimeMaterial.HasProperty(textureProperty))
        {
            runtimeMaterial.SetTexture(textureProperty, drawingTexture);
            assigned = true;
        }

        if (runtimeMaterial.HasProperty("_BaseMap"))
        {
            runtimeMaterial.SetTexture("_BaseMap", drawingTexture);
            assigned = true;
        }

        if (runtimeMaterial.HasProperty("_MainTex"))
        {
            runtimeMaterial.SetTexture("_MainTex", drawingTexture);
            assigned = true;
        }

        runtimeMaterial.mainTexture = drawingTexture;

        if (runtimeMaterial.HasProperty("_BaseColor"))
        {
            runtimeMaterial.SetColor("_BaseColor", Color.white);
        }
        if (runtimeMaterial.HasProperty("_Color"))
        {
            runtimeMaterial.SetColor("_Color", Color.white);
        }

        if (!assigned)
        {
            Debug.LogWarning($"BriefingChalkboard: Material '{runtimeMaterial.name}' on '{boardRenderer.name}' does not expose '{textureProperty}', '_BaseMap', or '_MainTex'. Use Generated Paint Material or assign a texture-based material.", this);
        }
    }

    private void CacheDrawingCameraHome()
    {
        if (customCamera == null || drawingCameraHomeCached) return;
        drawingCameraHomePosition = customCamera.transform.position;
        drawingCameraHomeRotation = customCamera.transform.rotation;
        drawingCameraHomeCached = true;
    }

    private void SwitchCameraImmediate(bool entering, Camera playerCamera)
    {
        CacheDrawingCameraHome();

        if (entering)
        {
            DisableSourceCameras(playerCamera);

            customCamera.transform.SetPositionAndRotation(drawingCameraHomePosition, drawingCameraHomeRotation);
            customCamera.gameObject.SetActive(true);
            customCamera.enabled = true;
            return;
        }

        RestoreSourceCameras();

        customCamera.enabled = false;
        customCamera.transform.SetPositionAndRotation(drawingCameraHomePosition, drawingCameraHomeRotation);
        customCamera.gameObject.SetActive(false);
    }

    private System.Collections.IEnumerator HandleCameraTransition(bool entering, Camera playerCamera)
    {
        CacheDrawingCameraHome();

        if (entering)
        {
            Vector3 startPos = playerCamera != null ? playerCamera.transform.position : drawingCameraHomePosition;
            Quaternion startRot = playerCamera != null ? playerCamera.transform.rotation : drawingCameraHomeRotation;
            Vector3 endPos = drawingCameraHomePosition;
            Quaternion endRot = drawingCameraHomeRotation;

            DisableSourceCameras(playerCamera);

            customCamera.transform.SetPositionAndRotation(startPos, startRot);
            customCamera.gameObject.SetActive(true);
            customCamera.enabled = true;

            yield return LerpDrawingCamera(startPos, startRot, endPos, endRot);
            customCamera.transform.SetPositionAndRotation(endPos, endRot);
        }
        else
        {
            Vector3 startPos = customCamera.transform.position;
            Quaternion startRot = customCamera.transform.rotation;
            Vector3 endPos = playerCamera != null ? playerCamera.transform.position : drawingCameraHomePosition;
            Quaternion endRot = playerCamera != null ? playerCamera.transform.rotation : drawingCameraHomeRotation;

            yield return LerpDrawingCamera(startPos, startRot, endPos, endRot);

            RestoreSourceCameras();

            customCamera.enabled = false;
            customCamera.transform.SetPositionAndRotation(drawingCameraHomePosition, drawingCameraHomeRotation);
            customCamera.gameObject.SetActive(false);
        }

        cameraTransitionRoutine = null;
    }

    private System.Collections.IEnumerator LerpDrawingCamera(Vector3 startPos, Quaternion startRot, Vector3 endPos, Quaternion endRot)
    {
        float duration = Mathf.Max(0.01f, cameraTransitionDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = Mathf.SmoothStep(0f, 1f, t);
            customCamera.transform.position = Vector3.Lerp(startPos, endPos, t);
            customCamera.transform.rotation = Quaternion.Slerp(startRot, endRot, t);
            yield return null;
        }
    }

    private void DisableSourceCameras(Camera playerCamera)
    {
        disabledPlayerCamera = null;
        disabledMainCamera = null;

        Camera mainCamera = Camera.main;
        if (playerCamera != null && playerCamera != customCamera && playerCamera.enabled)
        {
            disabledPlayerCamera = playerCamera;
            disabledPlayerCamera.enabled = false;
        }

        if (mainCamera != null && mainCamera != customCamera && mainCamera != playerCamera && mainCamera.enabled)
        {
            disabledMainCamera = mainCamera;
            disabledMainCamera.enabled = false;
        }
    }

    private void RestoreSourceCameras()
    {
        if (disabledPlayerCamera != null)
        {
            disabledPlayerCamera.enabled = true;
            disabledPlayerCamera = null;
        }

        if (disabledMainCamera != null)
        {
            disabledMainCamera.enabled = true;
            disabledMainCamera = null;
        }
    }

    private bool WasFinishPressed()
    {
        if (!finishInputArmed)
        {
            if (Time.frameCount > drawingStartedFrame && !IsFinishHeld())
            {
                finishInputArmed = true;
            }
            return false;
        }

        return InputKeyUtility.WasPressed(finishKey);
    }

    private bool IsFinishHeld()
    {
        return InputKeyUtility.IsPressed(finishKey);
    }

    private bool IsPaintHeld()
    {
        return InputKeyUtility.IsMouseButtonPressed(0);
    }

    private void PaintFromPointer()
    {
        if (drawingTexture == null || boardCollider == null) return;

        Camera cameraToUse = customCamera != null && customCamera.enabled ? customCamera : Camera.main;
        if (cameraToUse == null) return;

        Vector2 pointerPosition = InputKeyUtility.GetPointerPosition();
        Ray ray = cameraToUse.ScreenPointToRay(pointerPosition);
        if (!boardCollider.Raycast(ray, out RaycastHit hit, 100f))
        {
            return;
        }

        Vector2 uv = GetPaintUV(hit);
        if (hasLastPaintUV)
        {
            PaintLine(lastPaintUV, uv);
        }
        else
        {
            PaintAtUV(uv);
        }

        lastPaintUV = uv;
        hasLastPaintUV = true;
    }

    private Vector2 GetPaintUV(RaycastHit hit)
    {
        if (paintMappingMode != PaintMappingMode.LocalBounds && boardCollider is MeshCollider)
        {
            return hit.textureCoord;
        }

        if (boardRenderer == null)
        {
            return Vector2.zero;
        }

        Bounds localBounds = GetLocalBounds();
        Vector3 localPoint = boardRenderer.transform.InverseTransformPoint(hit.point);
        float u = Mathf.InverseLerp(GetAxis(localBounds.min, fallbackUAxis), GetAxis(localBounds.max, fallbackUAxis), GetAxis(localPoint, fallbackUAxis));
        float v = Mathf.InverseLerp(GetAxis(localBounds.min, fallbackVAxis), GetAxis(localBounds.max, fallbackVAxis), GetAxis(localPoint, fallbackVAxis));
        return new Vector2(Mathf.Clamp01(u), Mathf.Clamp01(v));
    }

    private Bounds GetLocalBounds()
    {
        MeshFilter meshFilter = boardRenderer != null ? boardRenderer.GetComponent<MeshFilter>() : null;
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            return meshFilter.sharedMesh.bounds;
        }

        Vector3 size = boardRenderer != null ? boardRenderer.bounds.size : Vector3.one;
        return new Bounds(Vector3.zero, size);
    }

    private static float GetAxis(Vector3 value, LocalAxis axis)
    {
        switch (axis)
        {
            case LocalAxis.X:
                return value.x;
            case LocalAxis.Y:
                return value.y;
            case LocalAxis.Z:
                return value.z;
            default:
                return value.x;
        }
    }

    private void PaintLine(Vector2 fromUV, Vector2 toUV)
    {
        Vector2 fromPixels = new Vector2(fromUV.x * (drawingTexture.width - 1), fromUV.y * (drawingTexture.height - 1));
        Vector2 toPixels = new Vector2(toUV.x * (drawingTexture.width - 1), toUV.y * (drawingTexture.height - 1));
        float distance = Vector2.Distance(fromPixels, toPixels);
        float stepDistance = Mathf.Max(1f, brushRadiusPixels * 0.5f);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / stepDistance));

        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            PaintAtUV(Vector2.Lerp(fromUV, toUV, t), false);
        }

        drawingTexture.Apply(false);
        isDirty = true;
    }

    private void PaintAtUV(Vector2 uv, bool apply = true)
    {
        int centerX = Mathf.RoundToInt(uv.x * (drawingTexture.width - 1));
        int centerY = Mathf.RoundToInt(uv.y * (drawingTexture.height - 1));
        int radius = Mathf.Max(1, brushRadiusPixels);
        int radiusSq = radius * radius;

        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if ((x * x) + (y * y) > radiusSq) continue;

                int px = centerX + x;
                int py = centerY + y;
                if (px < 0 || px >= drawingTexture.width || py < 0 || py >= drawingTexture.height) continue;

                drawingTexture.SetPixel(px, py, chalkColor);
            }
        }

        if (apply)
        {
            drawingTexture.Apply(false);
        }
        isDirty = true;
    }
}
