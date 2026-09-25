using UnityEngine;

// Kept on the player, not a dirt stage: completing a wash disables the dirt visuals.
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInteraction))]
public class PlayerFloorCleaning : MonoBehaviour
{
    [Header("Cleaning")]
    [Min(0.1f)] [SerializeField] private float cleaningDurationSeconds = 3f;
    [Tooltip("A broom child of the player/camera. Hidden until cleaning starts.")]
    [SerializeField] private GameObject broomRoot;
    [Tooltip("Uses the looping animation in the broom's Animator Controller. Edit the clip in Unity's Animation window.")]
    [SerializeField] private Animator broomAnimator;

    private FirstPersonController controller;
    private MainHallFloorDirtManager dirtManager;
    private float elapsed;
    private bool ownsMovementLock;

    public bool IsCleaning { get; private set; }

    private void Awake()
    {
        controller = GetComponent<FirstPersonController>();
        if (broomRoot != null) broomRoot.SetActive(false);
    }

    public bool TryStartCleaning(MainHallFloorDirtManager manager, bool lockPlayer = false)
    {
        if (IsCleaning || !isActiveAndEnabled || Time.timeScale <= 0f || manager == null || !manager.CanClean()) return false;
        if (controller != null && controller.IsMovementLocked()) return false;
        if (broomRoot == null)
        {
            Debug.LogWarning("Assign the player's broom root before cleaning the floor.", this);
            return false;
        }

        if (broomAnimator == null) broomAnimator = broomRoot.GetComponentInChildren<Animator>(true);
        if (broomAnimator == null || broomAnimator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("Assign an Animator Controller to the player's broom before cleaning the floor.", this);
            return false;
        }

        dirtManager = manager;
        elapsed = 0f;
        IsCleaning = true;
        ownsMovementLock = lockPlayer && controller != null;
        if (ownsMovementLock) controller.LockMovement();
        broomRoot.SetActive(true);
        broomAnimator.Rebind();
        broomAnimator.Play(0, 0, 0f);
        broomAnimator.Update(0f);
        InteractionPromptUI.Instance?.HidePrompt();
        return true;
    }

    private void Update()
    {
        if (!IsCleaning) return;
        if (dirtManager == null || !dirtManager.CanClean() ||
            (GameManager.Instance != null && GameManager.Instance.IsGameOver()))
        {
            FinishCleaning(false);
            return;
        }
        // Fixed wall-clock duration while playing, with both motion and progress frozen in pause.
        if (Time.timeScale <= 0f) return;
        elapsed += Time.unscaledDeltaTime;
        if (elapsed >= Mathf.Max(0.1f, cleaningDurationSeconds)) FinishCleaning(true);
    }

    private void FinishCleaning(bool complete)
    {
        if (!IsCleaning) return;
        IsCleaning = false;
        if (broomRoot != null) broomRoot.SetActive(false);
        if (ownsMovementLock) controller?.UnlockMovement();
        ownsMovementLock = false;
        var completedManager = dirtManager;
        dirtManager = null;
        // Charge action time exactly once and only after the visual sequence finishes.
        if (complete && completedManager != null) completedManager.TryCleanFloor();
    }

    private void OnDisable() => FinishCleaning(false);
    private void OnDestroy() => FinishCleaning(false);
}
