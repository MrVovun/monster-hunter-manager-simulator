using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Visuals/Blob Shadow Projector")]
public class BlobShadowProjector : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private bool createVisualIfMissing = true;
    [SerializeField] private Transform shadowVisual;
    [SerializeField] private MeshRenderer shadowRenderer;
    [SerializeField] private Material shadowMaterial;
    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.45f);
    [SerializeField] private Vector2 baseSize = new Vector2(0.85f, 0.5f);

    [Header("Grounding")]
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private float raycastStartHeight = 1f;
    [SerializeField] private float raycastDistance = 4f;
    [SerializeField] private float groundOffset = 0.015f;
    [SerializeField] private bool alignToGroundNormal = true;
    [SerializeField] private bool hideWhenNoGround = true;

    [Header("Height Response")]
    [SerializeField] private float fadeStartHeight = 0.05f;
    [SerializeField] private float fadeEndHeight = 1.5f;
    [SerializeField] private float minScale = 0.75f;
    [SerializeField] private float maxScale = 1.25f;

    private static Mesh sharedOvalMesh;
    private static Texture2D sharedShadowTexture;
    private Material runtimeMaterial;
    private Color currentColor;

    private void OnEnable()
    {
        EnsureVisual();
        EnsureMaterialInstance();
        currentColor = shadowColor;
        ApplyColor(currentColor);
        ApplyPreviewScale();
    }

    private void Awake()
    {
        EnsureVisual();
        EnsureMaterialInstance();
        currentColor = shadowColor;
        ApplyColor(currentColor);
        ApplyPreviewScale();
    }

    private void LateUpdate()
    {
        if (shadowVisual == null)
        {
            return;
        }

        Vector3 origin = transform.position + Vector3.up * raycastStartHeight;
        float maxDistance = raycastStartHeight + raycastDistance;

        if (!TryFindGround(origin, maxDistance, out RaycastHit hit))
        {
            SetVisible(!hideWhenNoGround);
            ApplyPreviewScale();
            return;
        }

        SetVisible(true);

        float height = Mathf.Max(0f, transform.position.y - hit.point.y);
        float heightT = Mathf.InverseLerp(fadeStartHeight, fadeEndHeight, height);
        float scale = Mathf.Lerp(minScale, maxScale, heightT);
        float alpha = shadowColor.a * (1f - heightT);

        shadowVisual.position = hit.point + hit.normal * groundOffset;
        shadowVisual.rotation = GetShadowRotation(hit.normal);
        shadowVisual.localScale = new Vector3(baseSize.x * scale, 1f, baseSize.y * scale);

        Color targetColor = new Color(shadowColor.r, shadowColor.g, shadowColor.b, alpha);
        if (currentColor != targetColor)
        {
            currentColor = targetColor;
            ApplyColor(currentColor);
        }
    }

    private void OnValidate()
    {
        baseSize.x = Mathf.Max(0f, baseSize.x);
        baseSize.y = Mathf.Max(0f, baseSize.y);
        raycastStartHeight = Mathf.Max(0f, raycastStartHeight);
        raycastDistance = Mathf.Max(0.01f, raycastDistance);
        groundOffset = Mathf.Max(0f, groundOffset);
        fadeEndHeight = Mathf.Max(fadeStartHeight + 0.01f, fadeEndHeight);
        minScale = Mathf.Max(0f, minScale);
        maxScale = Mathf.Max(0f, maxScale);

        if (isActiveAndEnabled)
        {
            EnsureVisual();
            EnsureMaterialInstance();
            ApplyColor(shadowColor);
            ApplyPreviewScale();
        }
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            if (Application.isPlaying)
            {
                Destroy(runtimeMaterial);
            }
            else
            {
                DestroyImmediate(runtimeMaterial);
            }
        }
    }

    private Quaternion GetShadowRotation(Vector3 groundNormal)
    {
        Quaternion groundRotation = alignToGroundNormal
            ? Quaternion.FromToRotation(Vector3.up, groundNormal)
            : Quaternion.identity;

        Quaternion yawRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        return groundRotation * yawRotation;
    }

    private bool TryFindGround(Vector3 origin, float maxDistance, out RaycastHit groundHit)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, maxDistance, groundLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.transform != null && hit.transform.IsChildOf(transform))
            {
                continue;
            }

            groundHit = hit;
            return true;
        }

        groundHit = default;
        return false;
    }

    private void EnsureVisual()
    {
        if (shadowVisual != null)
        {
            MeshFilter meshFilter = shadowVisual.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = shadowVisual.gameObject.AddComponent<MeshFilter>();
            }

            if (meshFilter.sharedMesh == null)
            {
                meshFilter.sharedMesh = GetOvalMesh();
            }

            if (shadowRenderer == null)
            {
                shadowRenderer = shadowVisual.GetComponentInChildren<MeshRenderer>(true);
            }

            if (shadowRenderer == null)
            {
                shadowRenderer = shadowVisual.gameObject.AddComponent<MeshRenderer>();
            }

            ConfigureRenderer(shadowRenderer);
            ApplyPreviewScale();
            return;
        }

        if (!createVisualIfMissing)
        {
            return;
        }

        GameObject visual = new GameObject("BlobShadowVisual");
        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;

        MeshFilter createdMeshFilter = visual.AddComponent<MeshFilter>();
        createdMeshFilter.sharedMesh = GetOvalMesh();
        shadowRenderer = visual.AddComponent<MeshRenderer>();
        ConfigureRenderer(shadowRenderer);

        shadowVisual = visual.transform;
        ApplyPreviewScale();
    }

    private void ApplyPreviewScale()
    {
        if (shadowVisual == null)
        {
            return;
        }

        shadowVisual.localScale = new Vector3(baseSize.x, 1f, baseSize.y);
    }

    private static void ConfigureRenderer(MeshRenderer renderer)
    {
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    private void EnsureMaterialInstance()
    {
        if (shadowRenderer == null)
        {
            return;
        }

        if (runtimeMaterial != null)
        {
            return;
        }

        runtimeMaterial = shadowMaterial != null ? new Material(shadowMaterial) : CreateDefaultMaterial();
        runtimeMaterial.name = $"{runtimeMaterial.name} (Runtime)";
        shadowRenderer.sharedMaterial = runtimeMaterial;
    }

    private static Mesh GetOvalMesh()
    {
        if (sharedOvalMesh != null)
        {
            return sharedOvalMesh;
        }

        sharedOvalMesh = new Mesh
        {
            name = "Generated Blob Shadow Oval"
        };

        sharedOvalMesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3(-0.5f, 0f, 0.5f),
            new Vector3(0.5f, 0f, 0.5f),
            new Vector3(0.5f, 0f, -0.5f)
        };
        sharedOvalMesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0f)
        };
        sharedOvalMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        sharedOvalMesh.RecalculateBounds();
        sharedOvalMesh.RecalculateNormals();

        return sharedOvalMesh;
    }

    private static Material CreateDefaultMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = new Material(shader)
        {
            name = "Generated Blob Shadow Material",
            renderQueue = 3000
        };

        Texture2D shadowTexture = GetShadowTexture();
        material.mainTexture = shadowTexture;
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", shadowTexture);
        }

        ConfigureTransparentMaterial(material);
        return material;
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
    }

    private static Texture2D GetShadowTexture()
    {
        if (sharedShadowTexture != null)
        {
            return sharedShadowTexture;
        }

        const int size = 128;
        sharedShadowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Generated Blob Shadow Texture",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.48f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = Mathf.SmoothStep(1f, 0f, distance);
                alpha *= alpha;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        sharedShadowTexture.SetPixels(pixels);
        sharedShadowTexture.Apply(false, true);
        return sharedShadowTexture;
    }

    private void ApplyColor(Color color)
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        if (runtimeMaterial.HasProperty("_BaseColor"))
        {
            runtimeMaterial.SetColor("_BaseColor", color);
        }

        if (runtimeMaterial.HasProperty("_Color"))
        {
            runtimeMaterial.SetColor("_Color", color);
        }
    }

    private void SetVisible(bool visible)
    {
        if (shadowRenderer != null && shadowRenderer.enabled != visible)
        {
            shadowRenderer.enabled = visible;
        }
    }
}
