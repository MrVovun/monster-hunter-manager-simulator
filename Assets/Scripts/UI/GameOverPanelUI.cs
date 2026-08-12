using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameOverPanelUI : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string gameSceneName = "TestScene2";
    [SerializeField] private string mainMenuSceneName;

    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup rootGroup;
    [SerializeField] private CanvasGroup backgroundGroup;
    [SerializeField] private TMP_Text reasonText;
    [SerializeField] private Button loadButton;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button quitButton;

    [Header("Presentation")]
    [SerializeField] private float freezeShotSeconds = 0.75f;
    [SerializeField] private float backgroundFadeSeconds = 1.25f;
    [SerializeField] private float panelFadeSeconds = 0.35f;
    [SerializeField] private bool lockPlayerControl = true;
    [SerializeField] private bool hideInteractionPrompt = true;
    [SerializeField] private bool switchMusic = true;
    [SerializeField] private MusicManager musicManager;

    private Coroutine showRoutine;
    private FirstPersonController lockedController;
    private bool lockedControllerWasAlreadyLocked;
    private PlayerInteraction disabledInteraction;
    private CursorLockMode previousLockState;
    private bool previousCursorVisible;
    private bool capturedCursor;
    private bool visible;

    private void Awake()
    {
        ResolveReferences();
        HookButtons();
        Hide();
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver += Show;
            if (GameManager.Instance.IsGameOver())
            {
                Show(GameManager.Instance.GetGameOverReason());
            }
        }
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver -= Show;
        }
    }

    public void Show(string reason)
    {
        if (visible) return;
        visible = true;

        if (reasonText != null)
        {
            reasonText.text = string.IsNullOrWhiteSpace(reason)
                ? "The guild has collapsed."
                : reason;
        }

        SetRootActive(true);
        CaptureCursor();
        LockGameInput();
        RefreshLoadButton();

        if (switchMusic)
        {
            ResolveMusicManager()?.PlayGameOverMusic();
        }

        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
        }

        showRoutine = StartCoroutine(ShowRoutine());
    }

    public void Hide()
    {
        visible = false;
        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }

        SetGroup(rootGroup, 0f, false);
        SetGroup(backgroundGroup, 0f, false);
        SetRootActive(false);
    }

    public void Load()
    {
        Time.timeScale = 1f;
        GameSaveUtility.RestoreGameOverBackup();
        GameSaveUtility.LoadSceneFresh(gameSceneName);
    }

    public void NewGame()
    {
        GameSaveUtility.ClearAllSaveData(includeSettings: false);
        GameSaveUtility.LoadSceneFresh(gameSceneName);
    }

    public void MainMenu()
    {
        Time.timeScale = 1f;
        RestoreCursorForSceneChange();
        if (!string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            GameSaveUtility.LoadSceneFresh(mainMenuSceneName);
            return;
        }

        GameSaveUtility.ClearAllSaveData(includeSettings: false);
        GameSaveUtility.LoadSceneFresh(gameSceneName);
    }

    public void Quit()
    {
        Time.timeScale = 1f;
        GameSaveUtility.QuitGame();
    }

    private IEnumerator ShowRoutine()
    {
        Time.timeScale = 0f;

        SetGroup(rootGroup, 0f, true);
        SetGroup(backgroundGroup, 0f, true);

        if (freezeShotSeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(freezeShotSeconds);
        }

        if (backgroundGroup != null)
        {
            yield return FadeGroup(backgroundGroup, 1f, backgroundFadeSeconds);
        }

        if (rootGroup != null)
        {
            yield return FadeGroup(rootGroup, 1f, panelFadeSeconds);
        }

        SetGroup(rootGroup, 1f, true);
        SetGroup(backgroundGroup, 1f, true);
        showRoutine = null;
    }

    private void HookButtons()
    {
        if (loadButton != null)
        {
            loadButton.onClick.RemoveAllListeners();
            loadButton.onClick.AddListener(Load);
        }

        if (newGameButton != null)
        {
            newGameButton.onClick.RemoveAllListeners();
            newGameButton.onClick.AddListener(NewGame);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveAllListeners();
            mainMenuButton.onClick.AddListener(MainMenu);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(Quit);
        }
    }

    private void ResolveReferences()
    {
        if (root == null)
        {
            root = gameObject;
        }

        if (rootGroup == null && root != null)
        {
            rootGroup = root.GetComponent<CanvasGroup>();
            if (rootGroup == null)
            {
                rootGroup = root.AddComponent<CanvasGroup>();
            }
        }

        RefreshLoadButton();
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

    private IEnumerator FadeGroup(CanvasGroup group, float targetAlpha, float seconds)
    {
        if (group == null) yield break;

        float startAlpha = group.alpha;
        float duration = Mathf.Max(0f, seconds);
        if (duration <= 0f)
        {
            group.alpha = targetAlpha;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        group.alpha = targetAlpha;
    }

    private void SetGroup(CanvasGroup group, float alpha, bool blocksInput)
    {
        if (group == null) return;
        group.alpha = alpha;
        group.interactable = blocksInput;
        group.blocksRaycasts = blocksInput;
    }

    private void CaptureCursor()
    {
        if (!capturedCursor)
        {
            previousLockState = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            capturedCursor = true;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestoreCursorForSceneChange()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        capturedCursor = false;
    }

    private void LockGameInput()
    {
        if (hideInteractionPrompt)
        {
            InteractionPromptUI.Instance?.HidePrompt();
        }

        if (!lockPlayerControl) return;

        if (lockedController == null)
        {
            lockedController = FindFirstObjectByType<FirstPersonController>();
        }

        if (lockedController != null)
        {
            lockedControllerWasAlreadyLocked = lockedController.IsMovementLocked();
            if (!lockedControllerWasAlreadyLocked)
            {
                lockedController.LockMovement();
            }
        }

        if (disabledInteraction == null)
        {
            disabledInteraction = FindFirstObjectByType<PlayerInteraction>();
        }

        if (disabledInteraction != null)
        {
            disabledInteraction.enabled = false;
        }
    }

    private MusicManager ResolveMusicManager()
    {
        if (musicManager != null) return musicManager;
        musicManager = FindFirstObjectByType<MusicManager>();
        return musicManager;
    }

    private void RefreshLoadButton()
    {
        if (loadButton != null)
        {
            loadButton.interactable = GameSaveUtility.HasGameOverBackup();
        }
    }
}
