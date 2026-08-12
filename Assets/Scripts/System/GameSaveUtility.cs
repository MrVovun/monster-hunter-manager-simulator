using System.IO;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameSaveUtility
{
    [Serializable]
    private class GameOverBackupData
    {
        public List<FileBackupEntry> files = new List<FileBackupEntry>();
        public List<PlayerPrefBackupEntry> playerPrefs = new List<PlayerPrefBackupEntry>();
    }

    [Serializable]
    private class FileBackupEntry
    {
        public string fileName;
        public string contents;
    }

    [Serializable]
    private class PlayerPrefBackupEntry
    {
        public string key;
        public PlayerPrefValueType type;
        public string value;
        public int intValue;
        public float floatValue;
    }

    private enum PlayerPrefValueType
    {
        String,
        Int,
        Float
    }

    private const string GameOverBackupFileName = "game_over_backup.json";

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
        "recruitment_state.json",
        "hunter_equipment_state.json"
    };

    private static readonly string[] PlayerPrefKeys =
    {
        "GuildDormitoryState",
        "GuildKitchenState",
        "tutorial.disabled",
        "tutorial.completed.first_session",
        "tutorial.progress.first_session.step",
        "tutorial.progress.first_session.eventCount",
        "settings.masterVolume",
        "settings.musicVolume",
        "settings.musicMuted",
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

    public static bool HasGameOverBackup()
    {
        return File.Exists(GetSavePath(GameOverBackupFileName));
    }

    public static void CreateGameOverBackup()
    {
        try
        {
            GameOverBackupData backup = new GameOverBackupData();
            foreach (string fileName in FileNames)
            {
                string path = GetSavePath(fileName);
                if (!File.Exists(path)) continue;

                backup.files.Add(new FileBackupEntry
                {
                    fileName = fileName,
                    contents = File.ReadAllText(path)
                });
            }

            foreach (string key in PlayerPrefKeys)
            {
                if (!PlayerPrefs.HasKey(key)) continue;
                PlayerPrefValueType type = GetPlayerPrefValueType(key);
                backup.playerPrefs.Add(new PlayerPrefBackupEntry
                {
                    key = key,
                    type = type,
                    value = type == PlayerPrefValueType.String ? PlayerPrefs.GetString(key) : string.Empty,
                    intValue = type == PlayerPrefValueType.Int ? PlayerPrefs.GetInt(key) : 0,
                    floatValue = type == PlayerPrefValueType.Float ? PlayerPrefs.GetFloat(key) : 0f
                });
            }

            File.WriteAllText(GetSavePath(GameOverBackupFileName), JsonUtility.ToJson(backup, true));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"GameSaveUtility: Failed to create game over backup. {ex.Message}");
        }
    }

    public static bool RestoreGameOverBackup()
    {
        string backupPath = GetSavePath(GameOverBackupFileName);
        if (!File.Exists(backupPath)) return false;

        try
        {
            GameOverBackupData backup = JsonUtility.FromJson<GameOverBackupData>(File.ReadAllText(backupPath));
            if (backup == null) return false;

            foreach (string fileName in FileNames)
            {
                string path = GetSavePath(fileName);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }

            foreach (var entry in backup.files)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.fileName)) continue;
                File.WriteAllText(GetSavePath(entry.fileName), entry.contents ?? string.Empty);
            }

            foreach (string key in PlayerPrefKeys)
            {
                if (!key.StartsWith("settings."))
                {
                    PlayerPrefs.DeleteKey(key);
                }
            }

            foreach (var entry in backup.playerPrefs)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key)) continue;
                switch (entry.type)
                {
                    case PlayerPrefValueType.Int:
                        PlayerPrefs.SetInt(entry.key, entry.intValue);
                        break;
                    case PlayerPrefValueType.Float:
                        PlayerPrefs.SetFloat(entry.key, entry.floatValue);
                        break;
                    default:
                        PlayerPrefs.SetString(entry.key, entry.value ?? string.Empty);
                        break;
                }
            }

            PlayerPrefs.Save();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"GameSaveUtility: Failed to restore game over backup. {ex.Message}");
            return false;
        }
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

        string backupPath = GetSavePath(GameOverBackupFileName);
        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
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

    private static PlayerPrefValueType GetPlayerPrefValueType(string key)
    {
        switch (key)
        {
            case "tutorial.disabled":
            case "tutorial.completed.first_session":
            case "tutorial.progress.first_session.step":
            case "tutorial.progress.first_session.eventCount":
            case "settings.musicMuted":
            case "settings.fullscreen":
            case "settings.qualityIndex":
                return PlayerPrefValueType.Int;
            case "settings.masterVolume":
            case "settings.musicVolume":
                return PlayerPrefValueType.Float;
            default:
                return PlayerPrefValueType.String;
        }
    }
}
