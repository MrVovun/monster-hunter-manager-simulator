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
    [SerializeField] private TMP_Text reasonText;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button quitButton;

    private void Awake()
    {
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
        if (reasonText != null)
        {
            reasonText.text = string.IsNullOrWhiteSpace(reason)
                ? "The guild has collapsed."
                : reason;
        }

        SetRootActive(true);
    }

    public void Hide()
    {
        SetRootActive(false);
    }

    public void NewGame()
    {
        GameSaveUtility.ClearAllSaveData(includeSettings: false);
        GameSaveUtility.LoadSceneFresh(gameSceneName);
    }

    public void MainMenu()
    {
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
        GameSaveUtility.QuitGame();
    }

    private void HookButtons()
    {
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
