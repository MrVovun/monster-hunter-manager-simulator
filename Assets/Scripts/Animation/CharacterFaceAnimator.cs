using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CharacterFaceAnimator : MonoBehaviour
{
    [Header("Renderers")]
    [SerializeField] private Renderer leftEyeRenderer;
    [SerializeField] private Renderer rightEyeRenderer;
    [SerializeField] private Renderer mouthRenderer;
    [SerializeField] private bool findRenderersByName = true;
    [SerializeField] private string leftEyeObjectName = "_EyeL";
    [SerializeField] private string rightEyeObjectName = "_EyeR";
    [SerializeField] private string mouthObjectName = "_Mouth";

    [Header("Atlas")]
    [Min(1)] [SerializeField] private int atlasColumns = 2;
    [Min(1)] [SerializeField] private int atlasRows = 2;
    [SerializeField] private Vector2 textureScale = Vector2.one;
    [SerializeField] private Vector2 baseTextureOffset = Vector2.zero;

    [Header("Eyes")]
    [SerializeField] private Vector2Int restingEyeFrame = new Vector2Int(0, 0);
    [SerializeField] private Vector2Int blinkEyeFrame = new Vector2Int(1, 1);
    [SerializeField] private Vector2Int[] gazeEyeFrames =
    {
        new Vector2Int(0, 0),
        new Vector2Int(1, 0),
        new Vector2Int(0, 1)
    };
    [SerializeField] private Vector2 gazeDelayRange = new Vector2(1.5f, 3.5f);
    [SerializeField] private Vector2 blinkDelayRange = new Vector2(2.5f, 5f);
    [Min(0.01f)] [SerializeField] private float blinkDuration = 0.12f;

    [Header("Mouth")]
    [SerializeField] private Vector2Int restingMouthFrame = new Vector2Int(1, 0);
    [SerializeField] private Vector2Int[] speakingMouthFrames =
    {
        new Vector2Int(0, 0),
        new Vector2Int(0, 1),
        new Vector2Int(1, 1)
    };
    [Min(0.01f)] [SerializeField] private float minimumMouthFrameDuration = 0.07f;

    private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
    private static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");

    private MaterialPropertyBlock propertyBlock;
    private Vector2Int currentGazeFrame;
    private float gazeTimer;
    private float blinkTimer;
    private float blinkRemaining;
    private float nextMouthChangeTime;
    private bool mouthIsOpen;
    private bool speaking;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        ResolveRenderers();
        currentGazeFrame = ClampFrame(restingEyeFrame);
        ApplyEyeFrame(currentGazeFrame);
        ApplyMouthFrame(restingMouthFrame);
        ScheduleNextGaze();
        ScheduleNextBlink();
    }

    private void OnEnable()
    {
        currentGazeFrame = ClampFrame(restingEyeFrame);
        ApplyEyeFrame(currentGazeFrame);
        ApplyMouthFrame(restingMouthFrame);
        ScheduleNextGaze();
        ScheduleNextBlink();
    }

    private void OnDisable()
    {
        speaking = false;
        mouthIsOpen = false;
        ApplyEyeFrame(restingEyeFrame);
        ApplyMouthFrame(restingMouthFrame);
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;

        if (blinkRemaining > 0f)
        {
            blinkRemaining -= deltaTime;
            if (blinkRemaining <= 0f)
            {
                ApplyEyeFrame(currentGazeFrame);
                ScheduleNextBlink();
            }
        }
        else
        {
            blinkTimer -= deltaTime;
            if (blinkTimer <= 0f)
            {
                ApplyEyeFrame(blinkEyeFrame);
                blinkRemaining = Mathf.Max(0.01f, blinkDuration);
            }
        }

        gazeTimer -= deltaTime;
        if (gazeTimer <= 0f)
        {
            currentGazeFrame = ChooseGazeFrame();
            if (blinkRemaining <= 0f)
            {
                ApplyEyeFrame(currentGazeFrame);
            }
            ScheduleNextGaze();
        }
    }

    public void BeginSpeaking()
    {
        speaking = true;
        mouthIsOpen = false;
        nextMouthChangeTime = 0f;
        ApplyMouthFrame(restingMouthFrame);
    }

    public void ShowCharacter(char character)
    {
        if (!speaking)
        {
            return;
        }

        if (char.IsWhiteSpace(character) || char.IsPunctuation(character))
        {
            mouthIsOpen = false;
            ApplyMouthFrame(restingMouthFrame);
            nextMouthChangeTime = Time.unscaledTime + minimumMouthFrameDuration;
            return;
        }

        if (Time.unscaledTime < nextMouthChangeTime)
        {
            return;
        }

        mouthIsOpen = !mouthIsOpen;
        ApplyMouthFrame(mouthIsOpen ? ChooseSpeakingMouthFrame() : restingMouthFrame);
        nextMouthChangeTime = Time.unscaledTime + minimumMouthFrameDuration;
    }

    public void StopSpeaking()
    {
        speaking = false;
        mouthIsOpen = false;
        ApplyMouthFrame(restingMouthFrame);
    }

    private void ResolveRenderers()
    {
        if (!findRenderersByName)
        {
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        leftEyeRenderer = leftEyeRenderer != null ? leftEyeRenderer : FindRenderer(renderers, leftEyeObjectName);
        rightEyeRenderer = rightEyeRenderer != null ? rightEyeRenderer : FindRenderer(renderers, rightEyeObjectName);
        mouthRenderer = mouthRenderer != null ? mouthRenderer : FindRenderer(renderers, mouthObjectName);
    }

    private static Renderer FindRenderer(Renderer[] renderers, string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        return Array.Find(renderers, renderer => renderer != null && renderer.gameObject.name == objectName);
    }

    private Vector2Int ChooseGazeFrame()
    {
        if (gazeEyeFrames == null || gazeEyeFrames.Length == 0)
        {
            return ClampFrame(restingEyeFrame);
        }

        return ClampFrame(gazeEyeFrames[UnityEngine.Random.Range(0, gazeEyeFrames.Length)]);
    }

    private Vector2Int ChooseSpeakingMouthFrame()
    {
        if (speakingMouthFrames == null || speakingMouthFrames.Length == 0)
        {
            return ClampFrame(restingMouthFrame);
        }

        return ClampFrame(speakingMouthFrames[UnityEngine.Random.Range(0, speakingMouthFrames.Length)]);
    }

    private void ApplyEyeFrame(Vector2Int frame)
    {
        ApplyFrame(leftEyeRenderer, frame);
        ApplyFrame(rightEyeRenderer, frame);
    }

    private void ApplyMouthFrame(Vector2Int frame)
    {
        ApplyFrame(mouthRenderer, frame);
    }

    private void ApplyFrame(Renderer targetRenderer, Vector2Int frame)
    {
        if (targetRenderer == null || propertyBlock == null)
        {
            return;
        }

        Vector2Int clampedFrame = ClampFrame(frame);
        Vector2 offset = baseTextureOffset + new Vector2(
            clampedFrame.x / (float)Mathf.Max(1, atlasColumns),
            clampedFrame.y / (float)Mathf.Max(1, atlasRows));
        Vector4 textureTransform = new Vector4(textureScale.x, textureScale.y, offset.x, offset.y);

        propertyBlock.Clear();
        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetVector(BaseMapStId, textureTransform);
        propertyBlock.SetVector(MainTexStId, textureTransform);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private Vector2Int ClampFrame(Vector2Int frame)
    {
        return new Vector2Int(
            Mathf.Clamp(frame.x, 0, Mathf.Max(0, atlasColumns - 1)),
            Mathf.Clamp(frame.y, 0, Mathf.Max(0, atlasRows - 1)));
    }

    private void ScheduleNextGaze()
    {
        gazeTimer = RandomRange(gazeDelayRange, 0.1f);
    }

    private void ScheduleNextBlink()
    {
        blinkTimer = RandomRange(blinkDelayRange, 0.1f);
        blinkRemaining = 0f;
    }

    private static float RandomRange(Vector2 range, float minimum)
    {
        float min = Mathf.Max(minimum, Mathf.Min(range.x, range.y));
        float max = Mathf.Max(min, Mathf.Max(range.x, range.y));
        return UnityEngine.Random.Range(min, max);
    }
}
