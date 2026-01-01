using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class TrophyWallInteractable : Interactable
{
    [Header("Trophy Wall")]
    [SerializeField] private TrophyWallController wallController;
    [SerializeField] private Camera trophyCamera;
    [SerializeField] private float cameraTransitionDuration = 0.5f;
    [SerializeField] private CanvasGroup exitHint;
    [SerializeField] private bool debugCameraFlow = false;

    private PlayerInteraction activePlayer;
    private Camera playerCamera;
    private Coroutine cameraRoutine;
    private Vector3 trophyHomePos;
    private Quaternion trophyHomeRot;
    private bool homeCached;
    private bool viewing;
    private bool isClosing;
    private bool suppressExitInputThisFrame;

    private void Awake()
    {
        // We manage cameras manually
        useCustomCamera = false;
    }

    private void Reset()
    {
        locksPlayer = true;
        interactionPrompt = "[E] View Trophy Wall";
        interactionType = InteractionType.Trigger;
        useCustomCamera = false;
    }

    private void Update()
    {
        if (!viewing) return;
        if (suppressExitInputThisFrame)
        {
            suppressExitInputThisFrame = false;
            return;
        }
        if (Keyboard.current == null) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (!isClosing)
            {
                isClosing = true;
                EndView();
            }
        }
    }

    public override void Interact(PlayerInteraction player)
    {
        if (isClosing) return;

        if (viewing)
        {
            isClosing = true;
            EndView();
            return;
        }

        if (wallController != null)
        {
            wallController.Rebuild();
        }

        activePlayer = player;
        playerCamera = player != null ? player.GetPlayerCamera() : Camera.main;
        OnInteractionStart(player); // lock movement
        ShowExitHint(true);
        viewing = true;
        suppressExitInputThisFrame = true; // avoid immediate close from same key press
        RegisterLockRelease(ReleaseFromLock);
        StartCameraTransition(true);
    }

    private void EndView()
    {
        if (!viewing) return;
        viewing = false;
        ShowExitHint(false);
        StartCameraTransition(false);
        ReleaseFromLock();
    }

    private void StartCameraTransition(bool entering)
    {
        CacheHome();
        if (cameraRoutine != null)
        {
            StopCoroutine(cameraRoutine);
        }
        cameraRoutine = StartCoroutine(HandleCameraTransition(entering));
    }

    private IEnumerator HandleCameraTransition(bool entering)
    {
        Camera source = playerCamera != null ? playerCamera : Camera.main;
        if (trophyCamera == null)
        {
            if (debugCameraFlow) Debug.LogWarning("TrophyWall: trophyCamera missing.");
            yield break;
        }

        float duration = Mathf.Max(0.05f, cameraTransitionDuration);

        if (entering)
        {
            Vector3 startPos = source != null ? source.transform.position : trophyHomePos;
            Quaternion startRot = source != null ? source.transform.rotation : trophyHomeRot;
            Vector3 endPos = trophyHomePos;
            Quaternion endRot = trophyHomeRot;

            if (source != null)
            {
                source.enabled = false;
            }
            if (debugCameraFlow)
            {
                Debug.Log($"TrophyWall Enter: source={(source != null ? source.name : "null")}, trophy={(trophyCamera != null ? trophyCamera.name : "null")}");
            }

            trophyCamera.gameObject.SetActive(true);
            trophyCamera.enabled = true;
            trophyCamera.transform.SetPositionAndRotation(startPos, startRot);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                trophyCamera.transform.position = Vector3.Lerp(startPos, endPos, t);
                trophyCamera.transform.rotation = Quaternion.Slerp(startRot, endRot, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            trophyCamera.transform.SetPositionAndRotation(endPos, endRot);
        }
        else
        {
            Vector3 startPos = trophyCamera.transform.position;
            Quaternion startRot = trophyCamera.transform.rotation;
            Vector3 endPos = source != null ? source.transform.position : trophyHomePos;
            Quaternion endRot = source != null ? source.transform.rotation : trophyHomeRot;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                trophyCamera.transform.position = Vector3.Lerp(startPos, endPos, t);
                trophyCamera.transform.rotation = Quaternion.Slerp(startRot, endRot, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            trophyCamera.transform.SetPositionAndRotation(trophyHomePos, trophyHomeRot);
            trophyCamera.gameObject.SetActive(false);
            if (debugCameraFlow)
            {
                Debug.Log($"TrophyWall Exit: restored source={(source != null ? source.name : "null")}");
            }
            RestoreCameras(source);
        }

        cameraRoutine = null;
        isClosing = false;
    }

    private void RestoreCameras(Camera source)
    {
        if (source != null)
        {
            source.enabled = true;
        }
    }

    private void ReleaseFromLock()
    {
        if (activePlayer != null)
        {
            OnInteractionEnd(activePlayer);
        }
        activePlayer = null;
        ClearLockRelease(ReleaseFromLock);
    }

    private void CacheHome()
    {
        if (trophyCamera == null || homeCached) return;
        trophyHomePos = trophyCamera.transform.position;
        trophyHomeRot = trophyCamera.transform.rotation;
        homeCached = true;
    }

    private void ShowExitHint(bool visible)
    {
        if (exitHint == null) return;
        exitHint.alpha = visible ? 1f : 0f;
        exitHint.blocksRaycasts = visible;
        exitHint.interactable = visible;
    }
}
