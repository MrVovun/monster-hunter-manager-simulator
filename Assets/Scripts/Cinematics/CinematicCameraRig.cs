using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

public class CinematicCameraRig : MonoBehaviour
{
    [Serializable]
    public class Shot
    {
        public string shotName = "Shot";
        public Transform cameraPoint;
        public Transform lookAtTarget;
        public bool useLookAtTarget = true;
        public float fieldOfView = 45f;
        public float transitionSeconds = 1f;
        public AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public bool hideHud = true;
        public bool hideNotifications = true;
    }

    [Header("Camera")]
    [SerializeField] private Camera cinematicCamera;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private FirstPersonController playerController;
    [SerializeField] private bool disablePlayerCameraDuringShot = true;
    [SerializeField] private bool lockPlayerDuringShot = true;
    [SerializeField] private bool disableCameraOnAwake = true;

    [Header("Shots")]
    [SerializeField] private List<Shot> shots = new List<Shot>();
    [SerializeField] private int startShotIndex;

    [Header("HUD")]
    [Tooltip("Optional roots to hide when a shot hides HUD. If empty, common HUD components are found automatically.")]
    [SerializeField] private List<GameObject> hudRoots = new List<GameObject>();
    [Tooltip("Optional roots to hide when notifications are hidden. If empty, NotificationFeedUI is found automatically.")]
    [SerializeField] private List<GameObject> notificationRoots = new List<GameObject>();

    [Header("Hotkeys")]
    [SerializeField] private bool enableHotkeys = true;
    [SerializeField] private Key playStartShotKey = Key.F9;
    [SerializeField] private Key previousShotKey = Key.F10;
    [SerializeField] private Key nextShotKey = Key.F11;
    [SerializeField] private Key exitShotKey = Key.F8;
    [SerializeField] private Key toggleHudKey = Key.F6;
    [SerializeField] private Key toggleNotificationsKey = Key.F7;
    [SerializeField] private Key screenshotKey = Key.F12;
    [SerializeField] private Key pauseToggleKey = Key.None;
    [SerializeField] private Key slowMotionToggleKey = Key.None;

    [Header("Capture")]
    [SerializeField] private string screenshotFolderName = "TrailerScreenshots";
    [SerializeField] private int screenshotSuperSize = 1;

    [Header("Time Controls")]
    [SerializeField] private float slowMotionScale = 0.25f;

    private readonly List<RootState> hiddenHudRoots = new List<RootState>();
    private readonly List<RootState> hiddenNotificationRoots = new List<RootState>();
    private Coroutine shotRoutine;
    private int activeShotIndex = -1;
    private bool cinematicActive;
    private bool hudManuallyHidden;
    private bool notificationsManuallyHidden;
    private float cachedTimeScale = 1f;
    private bool slowMotionActive;

    public int ShotCount => shots != null ? shots.Count : 0;
    public int ActiveShotIndex => activeShotIndex;
    public bool IsCinematicActive => cinematicActive;

    private void Awake()
    {
        ResolveReferences();
        if (disableCameraOnAwake && cinematicCamera != null)
        {
            cinematicCamera.enabled = false;
            cinematicCamera.gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        StopShotRoutine();
        RestoreHud();
        RestoreNotifications();
        RestorePlayerCamera();
        RestorePlayerMovement();
        RestoreTimeScale();
    }

    private void Update()
    {
        if (!enableHotkeys || Keyboard.current == null) return;

        if (WasPressed(playStartShotKey)) PlayShot(startShotIndex);
        if (WasPressed(previousShotKey)) PlayPreviousShot();
        if (WasPressed(nextShotKey)) PlayNextShot();
        if (WasPressed(exitShotKey)) ExitCinematic();
        if (WasPressed(toggleHudKey)) ToggleHud();
        if (WasPressed(toggleNotificationsKey)) ToggleNotifications();
        if (WasPressed(screenshotKey)) CaptureScreenshot();
        if (WasPressed(pauseToggleKey)) TogglePause();
        if (WasPressed(slowMotionToggleKey)) ToggleSlowMotion();
    }

    public void PlayShot(int index)
    {
        if (shots == null || shots.Count == 0) return;

        index = Mathf.Clamp(index, 0, shots.Count - 1);
        Shot shot = shots[index];
        if (shot == null || shot.cameraPoint == null)
        {
            Debug.LogWarning($"CinematicCameraRig: Shot {index + 1} is missing a camera point.", this);
            return;
        }

        ResolveReferences();
        StopShotRoutine();
        shotRoutine = StartCoroutine(PlayShotRoutine(index, shot));
    }

    public void PlayNextShot()
    {
        if (shots == null || shots.Count == 0) return;
        int next = activeShotIndex < 0 ? startShotIndex : activeShotIndex + 1;
        if (next >= shots.Count) next = 0;
        PlayShot(next);
    }

    public void PlayPreviousShot()
    {
        if (shots == null || shots.Count == 0) return;
        int previous = activeShotIndex < 0 ? startShotIndex : activeShotIndex - 1;
        if (previous < 0) previous = shots.Count - 1;
        PlayShot(previous);
    }

    public void ExitCinematic()
    {
        StopShotRoutine();
        cinematicActive = false;
        activeShotIndex = -1;
        RestoreHud();
        RestoreNotifications();
        hudManuallyHidden = false;
        notificationsManuallyHidden = false;
        RestorePlayerCamera();
        RestorePlayerMovement();
        if (cinematicCamera != null)
        {
            cinematicCamera.enabled = false;
            cinematicCamera.gameObject.SetActive(false);
        }
    }

    public void ToggleHud()
    {
        if (hudManuallyHidden)
        {
            hudManuallyHidden = false;
            RestoreHud();
        }
        else
        {
            hudManuallyHidden = true;
            HideHud();
        }
    }

    public void ToggleNotifications()
    {
        if (notificationsManuallyHidden)
        {
            notificationsManuallyHidden = false;
            RestoreNotifications();
        }
        else
        {
            notificationsManuallyHidden = true;
            HideNotifications();
        }
    }

    public void CaptureScreenshot()
    {
        string directory = Path.Combine(Application.persistentDataPath, screenshotFolderName);
        Directory.CreateDirectory(directory);

        string shotName = activeShotIndex >= 0 && activeShotIndex < shots.Count
            ? SanitizeFileName(shots[activeShotIndex].shotName)
            : "Screenshot";
        string fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{shotName}.png";
        string path = Path.Combine(directory, fileName);
        ScreenCapture.CaptureScreenshot(path, Mathf.Max(1, screenshotSuperSize));
        Debug.Log($"CinematicCameraRig: Captured screenshot to {path}", this);
    }

    public void TogglePause()
    {
        if (Mathf.Approximately(Time.timeScale, 0f))
        {
            Time.timeScale = cachedTimeScale <= 0f ? 1f : cachedTimeScale;
        }
        else
        {
            cachedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
    }

    public void ToggleSlowMotion()
    {
        if (slowMotionActive)
        {
            RestoreTimeScale();
            return;
        }

        cachedTimeScale = Time.timeScale;
        Time.timeScale = Mathf.Clamp(slowMotionScale, 0.01f, 1f);
        slowMotionActive = true;
    }

    private IEnumerator PlayShotRoutine(int index, Shot shot)
    {
        activeShotIndex = index;
        cinematicActive = true;

        Camera sourceCamera = cinematicCamera != null && cinematicCamera.enabled ? cinematicCamera : playerCamera != null ? playerCamera : Camera.main;
        Vector3 startPosition = sourceCamera != null ? sourceCamera.transform.position : shot.cameraPoint.position;
        Quaternion startRotation = sourceCamera != null ? sourceCamera.transform.rotation : shot.cameraPoint.rotation;
        float startFov = sourceCamera != null ? sourceCamera.fieldOfView : shot.fieldOfView;

        if (cinematicCamera == null)
        {
            Debug.LogWarning("CinematicCameraRig: No cinematic camera assigned.", this);
            yield break;
        }

        cinematicCamera.transform.SetPositionAndRotation(startPosition, startRotation);
        cinematicCamera.fieldOfView = startFov;
        cinematicCamera.gameObject.SetActive(true);
        cinematicCamera.enabled = true;

        if (disablePlayerCameraDuringShot && playerCamera != null && playerCamera != cinematicCamera)
        {
            playerCamera.enabled = false;
        }

        if (lockPlayerDuringShot && playerController != null)
        {
            playerController.LockMovement();
        }

        if (shot.hideHud || hudManuallyHidden) HideHud(); else RestoreHud();
        if (shot.hideNotifications || notificationsManuallyHidden) HideNotifications(); else RestoreNotifications();

        float duration = Mathf.Max(0.01f, shot.transitionSeconds);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float t = shot.easing != null ? Mathf.Clamp01(shot.easing.Evaluate(normalized)) : normalized;
            Vector3 position = Vector3.Lerp(startPosition, shot.cameraPoint.position, t);
            Quaternion rotation = Quaternion.Slerp(startRotation, GetShotRotation(shot, position), t);
            cinematicCamera.transform.SetPositionAndRotation(position, rotation);
            cinematicCamera.fieldOfView = Mathf.Lerp(startFov, shot.fieldOfView, t);
            yield return null;
        }

        cinematicCamera.transform.SetPositionAndRotation(shot.cameraPoint.position, GetShotRotation(shot, shot.cameraPoint.position));
        cinematicCamera.fieldOfView = shot.fieldOfView;
        shotRoutine = null;
    }

    private Quaternion GetShotRotation(Shot shot, Vector3 cameraPosition)
    {
        if (shot != null && shot.useLookAtTarget && shot.lookAtTarget != null)
        {
            Vector3 direction = shot.lookAtTarget.position - cameraPosition;
            if (direction.sqrMagnitude > 0.0001f)
            {
                return Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }

        return shot != null && shot.cameraPoint != null ? shot.cameraPoint.rotation : transform.rotation;
    }

    private void HideHud()
    {
        HideRoots(CollectHudRoots(), hiddenHudRoots);
    }

    private void RestoreHud()
    {
        RestoreRoots(hiddenHudRoots);
    }

    private void HideNotifications()
    {
        HideRoots(CollectNotificationRoots(), hiddenNotificationRoots);
    }

    private void RestoreNotifications()
    {
        RestoreRoots(hiddenNotificationRoots);
    }

    private void HideRoots(List<GameObject> roots, List<RootState> hiddenRoots)
    {
        if (hiddenRoots.Count > 0) return;
        foreach (GameObject root in roots)
        {
            if (root == null) continue;
            hiddenRoots.Add(new RootState(root, root.activeSelf));
            root.SetActive(false);
        }
    }

    private void RestoreRoots(List<RootState> hiddenRoots)
    {
        foreach (RootState state in hiddenRoots)
        {
            if (state.Root != null)
            {
                state.Root.SetActive(state.WasActive);
            }
        }

        hiddenRoots.Clear();
    }

    private List<GameObject> CollectHudRoots()
    {
        List<GameObject> roots = new List<GameObject>();
        HashSet<GameObject> seen = new HashSet<GameObject>();

        AddConfiguredRoots(hudRoots, roots, seen);
        if (roots.Count == 0)
        {
            AddComponentRoot(FindFirstObjectByType<DayTimeHUD>(FindObjectsInactive.Include), roots, seen);
            AddComponentRoot(FindFirstObjectByType<TimeAdvanceFeedback>(FindObjectsInactive.Include), roots, seen);
            AddComponentRoot(FindFirstObjectByType<InteractionPromptUI>(FindObjectsInactive.Include), roots, seen);
        }

        return roots;
    }

    private List<GameObject> CollectNotificationRoots()
    {
        List<GameObject> roots = new List<GameObject>();
        HashSet<GameObject> seen = new HashSet<GameObject>();
        AddConfiguredRoots(notificationRoots, roots, seen);
        if (roots.Count == 0)
        {
            AddComponentRoot(FindFirstObjectByType<NotificationFeedUI>(FindObjectsInactive.Include), roots, seen);
        }

        return roots;
    }

    private static void AddConfiguredRoots(List<GameObject> source, List<GameObject> roots, HashSet<GameObject> seen)
    {
        if (source == null) return;
        foreach (GameObject root in source)
        {
            AddRoot(root, roots, seen);
        }
    }

    private static void AddComponentRoot(Component component, List<GameObject> roots, HashSet<GameObject> seen)
    {
        if (component == null) return;
        AddRoot(component.gameObject, roots, seen);
    }

    private static void AddRoot(GameObject root, List<GameObject> roots, HashSet<GameObject> seen)
    {
        if (root == null || seen.Contains(root)) return;
        seen.Add(root);
        roots.Add(root);
    }

    private void ResolveReferences()
    {
        if (cinematicCamera == null)
        {
            cinematicCamera = GetComponentInChildren<Camera>(true);
        }

        if (playerController == null)
        {
            playerController = FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
        }

        if (playerCamera == null && playerController != null)
        {
            playerCamera = playerController.GetPlayerCamera();
        }

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
    }

    private void RestorePlayerCamera()
    {
        if (playerCamera != null)
        {
            playerCamera.enabled = true;
        }
    }

    private void RestorePlayerMovement()
    {
        if (lockPlayerDuringShot && playerController != null)
        {
            playerController.UnlockMovement();
        }
    }

    private void RestoreTimeScale()
    {
        if (!slowMotionActive) return;
        Time.timeScale = cachedTimeScale <= 0f ? 1f : cachedTimeScale;
        slowMotionActive = false;
    }

    private void StopShotRoutine()
    {
        if (shotRoutine == null) return;
        StopCoroutine(shotRoutine);
        shotRoutine = null;
    }

    private static bool WasPressed(Key key)
    {
        return key != Key.None && Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Shot";
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return value.Trim();
    }

    private readonly struct RootState
    {
        public readonly GameObject Root;
        public readonly bool WasActive;

        public RootState(GameObject root, bool wasActive)
        {
            Root = root;
            WasActive = wasActive;
        }
    }
}
