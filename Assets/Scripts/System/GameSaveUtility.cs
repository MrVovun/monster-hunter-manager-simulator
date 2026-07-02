using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameSaveUtility
{
    private static readonly string[] FileNames =
    {
        "game_state.json",
        "gold_state.json",
        "time_state.json",
        "orders_state.json",
        "reputation_state.json",
        "guild_construction_state.json",
        "graveyard_state.json",
        "monster_kill_counts.json",
        "notifications_history.json",
        "recruitment_state.json"
    };

    private static readonly string[] PlayerPrefKeys =
    {
        "GuildDormitoryState",
        "GuildKitchenState",
        "tutorial.disabled",
        "tutorial.completed.first_session",
        "settings.masterVolume",
        "settings.fullscreen",
        "settings.qualityIndex"
    };

    public static bool HasAnySaveData()
    {
        foreach (string fileName in FileNames)
        {
            if (File.Exists(GetSavePath(fileName)))
            {
                return true;
            }
        }

        if (PlayerPrefs.HasKey("GuildDormitoryState") || PlayerPrefs.HasKey("GuildKitchenState"))
        {
            return true;
        }

        return false;
    }

    public static void ClearAllSaveData(bool includeSettings = false)
    {
        foreach (string fileName in FileNames)
        {
            string path = GetSavePath(fileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        foreach (string key in PlayerPrefKeys)
        {
            if (!includeSettings && key.StartsWith("settings."))
            {
                continue;
            }

            PlayerPrefs.DeleteKey(key);
        }

        PlayerPrefs.Save();
    }

    public static void LoadSceneFresh(string sceneName)
    {
        Time.timeScale = 1f;
        GameManager.DestroyExistingInstance();

        if (!string.IsNullOrWhiteSpace(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    public static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public static string GetSavePath(string fileName)
    {
        return Path.Combine(Application.persistentDataPath, fileName);
    }
}
