using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class MonsterSlainTracker : MonoBehaviour
{
    [Serializable]
    private class SaveData
    {
        public List<string> monsterIds = new List<string>();
        public List<int> counts = new List<int>();
    }

    public event Action OnCountsChanged;

    private readonly Dictionary<string, int> killCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private string savePath;
    private OrderManager orderManager;

    private void Awake()
    {
        savePath = Path.Combine(Application.persistentDataPath, "monster_kill_counts.json");
    }

    private void Start()
    {
        orderManager = GameManager.Instance != null ? GameManager.Instance.GetOrderManager() : null;
        if (orderManager != null)
        {
            orderManager.OnMissionResolved += HandleMissionResolved;
        }

        Load();
    }

    private void OnDestroy()
    {
        if (orderManager != null)
        {
            orderManager.OnMissionResolved -= HandleMissionResolved;
        }
    }

    private void HandleMissionResolved(MissionReport report)
    {
        if (report == null || !report.success) return;
        MonsterData monster = report.order != null ? report.order.monsterData : null;
        if (monster == null || string.IsNullOrEmpty(monster.monsterId)) return;

        string id = monster.monsterId;
        if (!killCounts.ContainsKey(id))
        {
            killCounts[id] = 0;
        }
        killCounts[id]++;
        Save();
        OnCountsChanged?.Invoke();
    }

    public int GetKillCount(MonsterData monster)
    {
        if (monster == null) return 0;
        return GetKillCount(monster.monsterId);
    }

    public int GetKillCount(string monsterId)
    {
        if (string.IsNullOrEmpty(monsterId)) return 0;
        return killCounts.TryGetValue(monsterId, out int value) ? value : 0;
    }

    public void AddKills(MonsterData monster, int amount)
    {
        if (monster == null || amount == 0) return;
        AddKills(monster.monsterId, amount);
    }

    public void AddKills(string monsterId, int amount)
    {
        if (string.IsNullOrEmpty(monsterId) || amount == 0) return;
        if (!killCounts.ContainsKey(monsterId))
        {
            killCounts[monsterId] = 0;
        }
        killCounts[monsterId] = Mathf.Max(0, killCounts[monsterId] + amount);
        Save();
        OnCountsChanged?.Invoke();
    }

    public void ResetAll()
    {
        killCounts.Clear();
        Save();
        OnCountsChanged?.Invoke();
    }

    private void Load()
    {
        killCounts.Clear();
        if (!File.Exists(savePath)) return;
        try
        {
            string json = File.ReadAllText(savePath);
            var data = JsonUtility.FromJson<SaveData>(json);
            if (data == null || data.monsterIds == null || data.counts == null) return;
            int count = Mathf.Min(data.monsterIds.Count, data.counts.Count);
            for (int i = 0; i < count; i++)
            {
                string id = data.monsterIds[i];
                int value = data.counts[i];
                if (string.IsNullOrEmpty(id)) continue;
                killCounts[id] = Mathf.Max(0, value);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"MonsterSlainTracker: Failed to load kill counts. {ex.Message}");
        }
    }

    private void Save()
    {
        try
        {
            var data = new SaveData();
            foreach (var kvp in killCounts)
            {
                data.monsterIds.Add(kvp.Key);
                data.counts.Add(kvp.Value);
            }

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(savePath, json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"MonsterSlainTracker: Failed to save kill counts. {ex.Message}");
        }
    }
}
