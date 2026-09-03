using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine;

public class ArmoryManager : MonoBehaviour
{
    [Header("Unlock")]
    [SerializeField] private GuildConstructionManager constructionManager;
    [SerializeField] private GuildConstructionDefinition armoryConstruction;

    [Header("Scene")]
    [SerializeField] private Transform displayPoint;
    [SerializeField] private Camera armoryCamera;
    [SerializeField] private bool rotateHunterTowardCamera = true;
    [SerializeField] private float cameraTransitionDuration = 0.45f;
    [SerializeField] private List<GuildDoorController> routeDoorsToOpen = new List<GuildDoorController>();

    [Header("Model Rotation")]
    [SerializeField] private bool allowMouseModelRotation = true;
    [Tooltip("0 = left mouse, 1 = right mouse, 2 = middle mouse. Right mouse is recommended so normal UI clicks stay clean.")]
    [Range(0, 2)]
    [SerializeField] private int rotateMouseButton = 1;
    [SerializeField] private float modelRotationSpeed = 0.35f;
    [SerializeField] private bool invertModelRotation = false;
    [SerializeField] private bool ignoreRotationWhenPointerOverUI = true;

    [Header("Animation")]
    [SerializeField] private SharedCharacterAnimator.ClipEntry battleStanceClip;

    private HunterManager hunterManager;
    private TimeManager timeManager;
    private ArmoryUI activeUI;
    private PlayerInteraction activePlayer;
    private Action onClosed;
    private Hunter selectedHunter;
    private Camera disabledPlayerCamera;
    private Coroutine cameraRoutine;
    private Action pendingClosedCallback;
    private Vector3 cameraHomePosition;
    private Quaternion cameraHomeRotation;
    private bool cameraHomeCached;
    private bool closing;

    public event Action OnStateChanged;

    private void Awake()
    {
        ResolveReferences();
        CacheCameraHome();
        if (armoryCamera != null)
        {
            armoryCamera.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        HandleModelRotationInput();
    }

    public bool CanOpenArmory()
    {
        ResolveReferences();
        if (!IsUnlocked()) return false;
        if (timeManager == null) return true;

        TimeManager.DayState state = timeManager.GetDayState();
        return state == TimeManager.DayState.PreBell || state == TimeManager.DayState.Active;
    }

    public IReadOnlyList<Hunter> GetHunters()
    {
        ResolveReferences();
        if (hunterManager == null) return Array.Empty<Hunter>();

        return hunterManager.GetAllHunters()
            .Where(h => h != null && h.IsAvailableForOrders())
            .OrderBy(h => h.Data != null ? h.Data.hunterName : h.name)
            .ToList();
    }

    public IReadOnlyList<P09HumanoidLibrary.PartOption> GetWeaponOptions()
    {
        if (selectedHunter == null) return Array.Empty<P09HumanoidLibrary.PartOption>();

        P09HumanoidPreset preset = selectedHunter.GetRuntimeP09Preset();
        P09HumanoidLibrary library = selectedHunter.GetP09Library();
        if (preset == null || library == null) return Array.Empty<P09HumanoidLibrary.PartOption>();

        return library.GetWeaponOptions(preset.sexId);
    }

    public Hunter GetSelectedHunter()
    {
        return selectedHunter;
    }

    public int GetSelectedWeaponId()
    {
        return selectedHunter != null ? selectedHunter.GetEquippedWeaponId() : 0;
    }

    public bool Open(ArmoryUI ui, PlayerInteraction player, Action closedCallback)
    {
        if (ui == null || !CanOpenArmory()) return false;

        activeUI = ui;
        activePlayer = player;
        onClosed = closedCallback;
        closing = false;

        StartCameraTransition(true, activePlayer != null ? activePlayer.GetPlayerCamera() : Camera.main);
        OnStateChanged?.Invoke();
        return true;
    }

    public bool TrySelectHunter(Hunter hunter)
    {
        if (hunter == null || !hunter.CanUseArmory() || displayPoint == null) return false;
        if (selectedHunter == hunter) return true;

        ClearSelectedHunter();
        OpenRouteDoors();

        selectedHunter = hunter;
        if (!selectedHunter.BeginArmoryDisplay(displayPoint, battleStanceClip))
        {
            selectedHunter = null;
            return false;
        }

        RotateSelectedHunterTowardCamera();

        OnStateChanged?.Invoke();
        return true;
    }

    public bool TryEquipWeapon(int weaponId)
    {
        if (selectedHunter == null) return false;

        selectedHunter.SetEquippedWeaponId(weaponId);
        ResolveReferences();
        hunterManager?.NotifyHunterEquipmentChanged(selectedHunter);
        OnStateChanged?.Invoke();
        return true;
    }

    public void Close()
    {
        if (closing) return;
        closing = true;

        ClearSelectedHunter();
        activeUI = null;

        Camera playerCamera = activePlayer != null ? activePlayer.GetPlayerCamera() : Camera.main;
        StartCameraTransition(false, playerCamera);

        pendingClosedCallback = onClosed;
        onClosed = null;
        activePlayer = null;
        OnStateChanged?.Invoke();
    }

    private void ClearSelectedHunter()
    {
        if (selectedHunter == null) return;
        selectedHunter.EndArmoryDisplay();
        selectedHunter = null;
    }

    private void RotateSelectedHunterTowardCamera()
    {
        if (!rotateHunterTowardCamera || selectedHunter == null || armoryCamera == null) return;

        Vector3 toCamera = armoryCamera.transform.position - selectedHunter.transform.position;
        toCamera.y = 0f;
        if (toCamera.sqrMagnitude <= 0.0001f) return;

        selectedHunter.transform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
    }

    private void HandleModelRotationInput()
    {
        if (!allowMouseModelRotation || selectedHunter == null || closing) return;
        if (ignoreRotationWhenPointerOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (!IsRotateButtonPressed()) return;

        Vector2 delta = GetMouseDelta();
        if (Mathf.Abs(delta.x) <= 0.01f) return;

        float sign = invertModelRotation ? 1f : -1f;
        selectedHunter.transform.Rotate(Vector3.up, delta.x * modelRotationSpeed * sign, Space.World);
    }

    private bool IsRotateButtonPressed()
    {
        if (Mouse.current == null)
        {
            return false;
        }

        return rotateMouseButton switch
        {
            0 => Mouse.current.leftButton.isPressed,
            2 => Mouse.current.middleButton.isPressed,
            _ => Mouse.current.rightButton.isPressed
        };
    }

    private static Vector2 GetMouseDelta()
    {
        if (Mouse.current == null)
        {
            return Vector2.zero;
        }

        return Mouse.current.delta.ReadValue();
    }

    private void ResolveReferences()
    {
        if (GameManager.Instance != null)
        {
            if (constructionManager == null) constructionManager = GameManager.Instance.GetConstructionManager();
            if (hunterManager == null) hunterManager = GameManager.Instance.GetHunterManager();
            if (timeManager == null) timeManager = GameManager.Instance.GetTimeManager();
        }
    }

    private bool IsUnlocked()
    {
        if (armoryConstruction == null) return true;
        return constructionManager != null && constructionManager.IsBuilt(armoryConstruction);
    }

    private void OpenRouteDoors()
    {
        if (routeDoorsToOpen == null) return;
        foreach (var door in routeDoorsToOpen)
        {
            door?.OpenForRoute();
        }
    }

    private void CacheCameraHome()
    {
        if (cameraHomeCached || armoryCamera == null) return;
        cameraHomePosition = armoryCamera.transform.position;
        cameraHomeRotation = armoryCamera.transform.rotation;
        cameraHomeCached = true;
    }

    private void StartCameraTransition(bool entering, Camera playerCamera)
    {
        if (armoryCamera == null)
        {
            if (!entering)
            {
                Action callback = pendingClosedCallback;
                pendingClosedCallback = null;
                callback?.Invoke();
            }
            return;
        }

        CacheCameraHome();
        if (cameraRoutine != null)
        {
            StopCoroutine(cameraRoutine);
        }

        cameraRoutine = StartCoroutine(HandleCameraTransition(entering, playerCamera));
    }

    private IEnumerator HandleCameraTransition(bool entering, Camera playerCamera)
    {
        float duration = Mathf.Max(0.01f, cameraTransitionDuration);

        if (entering)
        {
            Vector3 startPos = playerCamera != null ? playerCamera.transform.position : cameraHomePosition;
            Quaternion startRot = playerCamera != null ? playerCamera.transform.rotation : cameraHomeRotation;

            disabledPlayerCamera = playerCamera != null && playerCamera != armoryCamera && playerCamera.enabled ? playerCamera : null;
            if (disabledPlayerCamera != null)
            {
                disabledPlayerCamera.enabled = false;
            }

            armoryCamera.transform.SetPositionAndRotation(startPos, startRot);
            armoryCamera.gameObject.SetActive(true);
            armoryCamera.enabled = true;

            yield return LerpCamera(startPos, startRot, cameraHomePosition, cameraHomeRotation, duration);
            armoryCamera.transform.SetPositionAndRotation(cameraHomePosition, cameraHomeRotation);
        }
        else
        {
            Vector3 startPos = armoryCamera.transform.position;
            Quaternion startRot = armoryCamera.transform.rotation;
            Vector3 endPos = playerCamera != null ? playerCamera.transform.position : cameraHomePosition;
            Quaternion endRot = playerCamera != null ? playerCamera.transform.rotation : cameraHomeRotation;

            yield return LerpCamera(startPos, startRot, endPos, endRot, duration);

            if (disabledPlayerCamera != null)
            {
                disabledPlayerCamera.enabled = true;
                disabledPlayerCamera = null;
            }

            armoryCamera.enabled = false;
            armoryCamera.transform.SetPositionAndRotation(cameraHomePosition, cameraHomeRotation);
            armoryCamera.gameObject.SetActive(false);

            Action callback = pendingClosedCallback;
            pendingClosedCallback = null;
            callback?.Invoke();
        }

        cameraRoutine = null;
    }

    private IEnumerator LerpCamera(Vector3 startPos, Quaternion startRot, Vector3 endPos, Quaternion endRot, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            armoryCamera.transform.SetPositionAndRotation(Vector3.Lerp(startPos, endPos, t), Quaternion.Slerp(startRot, endRot, t));
            yield return null;
        }
    }
}
