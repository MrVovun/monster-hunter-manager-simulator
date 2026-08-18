using System.IO;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
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

public static class SceneLookup
{
    public static T Find<T>(bool includeInactive = false) where T : UnityEngine.Object
    {
#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindFirstObjectByType<T>(
            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
#else
        return UnityEngine.Object.FindObjectOfType<T>(includeInactive);
#endif
    }

    public static T[] FindAll<T>(bool includeInactive = false) where T : UnityEngine.Object
    {
#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<T>(
            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.InstanceID);
#else
        return UnityEngine.Object.FindObjectsOfType<T>(includeInactive);
#endif
    }
}

public static class InputKeyUtility
{
    public static bool WasPressed(KeyCode keyCode)
    {
        if (Keyboard.current != null && TryConvertKeyCode(keyCode, out Key key))
        {
            return Keyboard.current[key].wasPressedThisFrame;
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(keyCode);
#else
        return false;
#endif
    }

    public static bool IsPressed(KeyCode keyCode)
    {
        if (Keyboard.current != null && TryConvertKeyCode(keyCode, out Key key))
        {
            return Keyboard.current[key].isPressed;
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(keyCode);
#else
        return false;
#endif
    }

    public static bool IsMouseButtonPressed(int button)
    {
        if (Mouse.current != null)
        {
            switch (button)
            {
                case 0:
                    return Mouse.current.leftButton.isPressed;
                case 1:
                    return Mouse.current.rightButton.isPressed;
                case 2:
                    return Mouse.current.middleButton.isPressed;
            }
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButton(button);
#else
        return false;
#endif
    }

    public static Vector2 GetPointerPosition()
    {
        if (Mouse.current != null)
        {
            return Mouse.current.position.ReadValue();
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.mousePosition;
#else
        return Vector2.zero;
#endif
    }

    private static bool TryConvertKeyCode(KeyCode keyCode, out Key key)
    {
        switch (keyCode)
        {
            case KeyCode.None:
                key = Key.None;
                return false;
            case KeyCode.Escape:
                key = Key.Escape;
                return true;
            case KeyCode.Space:
                key = Key.Space;
                return true;
            case KeyCode.Return:
                key = Key.Enter;
                return true;
            case KeyCode.KeypadEnter:
                key = Key.NumpadEnter;
                return true;
            case KeyCode.Tab:
                key = Key.Tab;
                return true;
            case KeyCode.E:
                key = Key.E;
                return true;
            case KeyCode.R:
                key = Key.R;
                return true;
            case KeyCode.P:
                key = Key.P;
                return true;
            case KeyCode.Alpha0:
                key = Key.Digit0;
                return true;
            case KeyCode.Alpha1:
                key = Key.Digit1;
                return true;
            case KeyCode.Alpha2:
                key = Key.Digit2;
                return true;
            case KeyCode.Alpha3:
                key = Key.Digit3;
                return true;
            case KeyCode.Alpha4:
                key = Key.Digit4;
                return true;
            case KeyCode.Alpha5:
                key = Key.Digit5;
                return true;
            case KeyCode.Alpha6:
                key = Key.Digit6;
                return true;
            case KeyCode.Alpha7:
                key = Key.Digit7;
                return true;
            case KeyCode.Alpha8:
                key = Key.Digit8;
                return true;
            case KeyCode.Alpha9:
                key = Key.Digit9;
                return true;
            default:
                key = Key.None;
                return false;
        }
    }
}
