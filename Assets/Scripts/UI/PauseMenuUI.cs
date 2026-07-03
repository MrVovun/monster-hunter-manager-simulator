using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PauseMenuUI : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference pauseAction;
    [SerializeField] private Key fallbackKey = Key.Escape;
    [SerializeField] private Key secondaryFallbackKey = Key.P;

    [Header("Scene")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button quitButton;

    private bool paused;
    private float previousTimeScale = 1f;
    private CursorLockMode previousLockState;
    private bool previousCursorVisible;
    private bool cursorCaptured;
    private FirstPersonController playerController;
    private bool playerMovementWasLocked;
    private bool playerMovementCaptured;
    private TutorialPopupUI tutorialPopup;

    private void Awake()
    {
        HookButtons();
        SetRootActive(false);
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (pauseAction != null && pauseAction.action != null)
        {
            pauseAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (pauseAction != null && pauseAction.action != null)
        {
            pauseAction.action.Disable();
        }

        if (paused)
        {
            Resume();
        }
    }

    private void Update()
    {
        if (!WasPausePressed()) return;
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver()) return;

        if (settingsPanel != null && settingsPanel.activeSelf)
        {
            settingsPanel.SetActive(false);
            return;
        }

        if (paused)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    public void Pause()
    {
        if (paused) return;
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver()) return;

        paused = true;
        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        CapturePlayerMovement();
        PauseTutorialVoice();
        CaptureCursor();
        SetRootActive(true);
    }

    public void Resume()
    {
        if (!paused) return;

        paused = false;
        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        SetRootActive(false);
        RestoreCursor();
        RestorePlayerMovement();
        ResumeTutorialVoice();
    }

    public void OpenSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        ResumeTutorialVoice();
        RestorePlayerMovement();
        RestoreCursor();
        GameSaveUtility.LoadSceneFresh(mainMenuSceneName);
    }

    public void Quit()
    {
        Time.timeScale = 1f;
        GameSaveUtility.QuitGame();
    }

    private bool WasPausePressed()
    {
        if (pauseAction != null && pauseAction.action != null)
        {
            return pauseAction.action.WasPressedThisFrame();
        }

        if (Keyboard.current != null)
        {
            if (Keyboard.current[fallbackKey].wasPressedThisFrame)
            {
                return true;
            }

            if (secondaryFallbackKey != Key.None && Keyboard.current[secondaryFallbackKey].wasPressedThisFrame)
            {
                return true;
            }
        }

        if (TryGetLegacyKeyCode(fallbackKey, out KeyCode legacyFallback) && Input.GetKeyDown(legacyFallback))
        {
            return true;
        }

        return secondaryFallbackKey != Key.None
            && TryGetLegacyKeyCode(secondaryFallbackKey, out KeyCode legacySecondary)
            && Input.GetKeyDown(legacySecondary);
    }

    private bool TryGetLegacyKeyCode(Key key, out KeyCode keyCode)
    {
        switch (key)
        {
            case Key.Escape:
                keyCode = KeyCode.Escape;
                return true;
            case Key.P:
                keyCode = KeyCode.P;
                return true;
            case Key.Tab:
                keyCode = KeyCode.Tab;
                return true;
            case Key.Space:
                keyCode = KeyCode.Space;
                return true;
            case Key.Enter:
            case Key.NumpadEnter:
                keyCode = KeyCode.Return;
                return true;
            default:
                keyCode = KeyCode.None;
                return false;
        }
    }

    private void HookButtons()
    {
        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveAllListeners();
            resumeButton.onClick.AddListener(Resume);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveAllListeners();
            settingsButton.onClick.AddListener(OpenSettings);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveAllListeners();
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(Quit);
        }
    }

    private void CaptureCursor()
    {
        if (cursorCaptured) return;

        previousLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        cursorCaptured = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestoreCursor()
    {
        if (!cursorCaptured) return;

        Cursor.lockState = previousLockState;
        Cursor.visible = previousCursorVisible;
        cursorCaptured = false;
    }

    private void CapturePlayerMovement()
    {
        if (playerMovementCaptured) return;

        if (playerController == null)
        {
            playerController = FindFirstObjectByType<FirstPersonController>();
        }

        if (playerController == null) return;

        playerMovementWasLocked = playerController.IsMovementLocked();
        playerMovementCaptured = true;

        if (!playerMovementWasLocked)
        {
            playerController.LockMovement();
        }
    }

    private void RestorePlayerMovement()
    {
        if (!playerMovementCaptured) return;

        if (playerController != null && !playerMovementWasLocked)
        {
            playerController.UnlockMovement();
        }

        playerMovementCaptured = false;
    }

    private void PauseTutorialVoice()
    {
        if (tutorialPopup == null)
        {
            tutorialPopup = FindFirstObjectByType<TutorialPopupUI>(FindObjectsInactive.Include);
        }

        tutorialPopup?.PauseVoice();
    }

    private void ResumeTutorialVoice()
    {
        tutorialPopup?.ResumeVoice();
    }

    private void SetRootActive(bool active)
    {
        if (root != null)
        {
            root.SetActive(active);
        }
        else
        {
            gameObject.SetActive(active);
        }
    }
}
