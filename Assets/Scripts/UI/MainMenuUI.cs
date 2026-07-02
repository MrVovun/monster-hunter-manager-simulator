using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string gameSceneName = "TestScene2";

    [Header("Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    [Header("Panels")]
    [SerializeField] private GameObject root;
    [SerializeField] private GameObject settingsPanel;

    [Header("Optional Text")]
    [SerializeField] private TMP_Text saveStatusText;

    private void Awake()
    {
        HookButtons();
        Refresh();
    }

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        bool hasSave = GameSaveUtility.HasAnySaveData();
        if (continueButton != null)
        {
            continueButton.interactable = hasSave;
        }

        if (saveStatusText != null)
        {
            saveStatusText.text = hasSave ? "Saved guild found." : "No saved guild.";
        }
    }

    public void ContinueGame()
    {
        GameSaveUtility.LoadSceneFresh(gameSceneName);
    }

    public void NewGame()
    {
        GameSaveUtility.ClearAllSaveData(includeSettings: false);
        GameSaveUtility.LoadSceneFresh(gameSceneName);
    }

    public void OpenSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    public void Quit()
    {
        GameSaveUtility.QuitGame();
    }

    private void HookButtons()
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(ContinueGame);
        }

        if (newGameButton != null)
        {
            newGameButton.onClick.RemoveAllListeners();
            newGameButton.onClick.AddListener(NewGame);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveAllListeners();
            settingsButton.onClick.AddListener(OpenSettings);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(Quit);
        }

        if (root != null)
        {
            root.SetActive(true);
        }
    }
}
