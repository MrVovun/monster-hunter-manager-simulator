using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class GraveyardManager : MonoBehaviour
{
    [Serializable]
    private class GraveyardSaveData
    {
        public List<GraveRecord> graves = new List<GraveRecord>();
        public List<string> missionCountHunterIds = new List<string>();
        public List<int> missionCounts = new List<int>();
    }

    [Header("Placement")]
    [SerializeField] private Transform layoutAnchor;
    [SerializeField] private GameObject gravePrefab;
    [Min(1)]
    [SerializeField] private int columns = 4;
    [Min(0f)]
    [SerializeField] private float columnSpacing = 2f;
    [Min(0f)]
    [SerializeField] private float rowSpacing = 2.5f;
    [Tooltip("Maximum number of grave records and physical markers. Use 0 for unlimited.")]
    [Min(0)]
    [SerializeField] private int maxGraves = 24;

    [Header("Interaction")]
    [SerializeField] private GravePlaqueUI plaqueUI;

    private readonly List<GraveRecord> graves = new List<GraveRecord>();
    private readonly List<GameObject> spawnedMarkers = new List<GameObject>();
    private readonly Dictionary<string, int> completedMissionCounts =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<MissionReport> processedReports = new HashSet<MissionReport>();

    private string savePath;

    public event Action OnGravesChanged;
    public IReadOnlyList<GraveRecord> Graves => graves;

    private void Awake()
    {
        savePath = Path.Combine(Application.persistentDataPath, "graveyard_state.json");
        Load();
        Rebuild();
    }

    public void HandleMissionReportClosed(MissionReport report)
    {
        if (report == null || processedReports.Contains(report)) return;
        processedReports.Add(report);

        if (report.hunterResults != null)
        {
            foreach (var result in report.hunterResults)
            {
                HunterData data = result?.hunter != null ? result.hunter.Data : null;
                string hunterId = data != null ? data.hunterId : null;
                if (string.IsNullOrWhiteSpace(hunterId)) continue;

                completedMissionCounts.TryGetValue(hunterId, out int count);
                completedMissionCounts[hunterId] = count + 1;
            }

            foreach (var result in report.hunterResults)
            {
                if (result == null || !result.died) continue;
                TryAddGrave(result.hunter);
            }
        }

        Save();
        Rebuild();
        OnGravesChanged?.Invoke();
    }

    public void ShowPlaque(GraveRecord record, Action onClosed)
    {
        if (plaqueUI == null)
        {
            plaqueUI = FindObjectOfType<GravePlaqueUI>(true);
        }

        if (plaqueUI == null)
        {
            Debug.LogWarning("GraveyardManager: No GravePlaqueUI found in the scene.", this);
            onClosed?.Invoke();
            return;
        }

        plaqueUI.Show(record, onClosed);
    }

    public void Rebuild()
    {
        ClearSpawnedMarkers();
        if (gravePrefab == null || layoutAnchor == null) return;

        int count = maxGraves > 0 ? Mathf.Min(graves.Count, maxGraves) : graves.Count;
        int columnCount = Mathf.Max(1, columns);

        for (int i = 0; i < count; i++)
        {
            int column = i % columnCount;
            int row = i / columnCount;

            GameObject markerObject = Instantiate(gravePrefab, layoutAnchor);
            markerObject.name = $"Grave_{graves[i].hunterName}";
            markerObject.transform.localPosition = new Vector3(column * columnSpacing, 0f, row * rowSpacing);
            markerObject.transform.localRotation = Quaternion.identity;

            GraveMarker marker = markerObject.GetComponent<GraveMarker>();
            if (marker == null)
            {
                marker = markerObject.AddComponent<GraveMarker>();
            }
            marker.Initialize(this, graves[i]);
            spawnedMarkers.Add(markerObject);
        }
    }

    public void RemoveGravesForHunters(IEnumerable<string> hunterIds, bool resetMissionCounts = true)
    {
        if (hunterIds == null) return;

        HashSet<string> ids = new HashSet<string>(hunterIds, StringComparer.OrdinalIgnoreCase);
        if (ids.Count == 0) return;

        graves.RemoveAll(record => record != null && !string.IsNullOrWhiteSpace(record.hunterId) && ids.Contains(record.hunterId));
        if (resetMissionCounts)
        {
            foreach (string id in ids)
            {
                completedMissionCounts.Remove(id);
            }
        }

        Save();
        Rebuild();
        OnGravesChanged?.Invoke();
    }

    public void ClearGraveyard(bool resetMissionCounts = true)
    {
        graves.Clear();
        if (resetMissionCounts)
        {
            completedMissionCounts.Clear();
        }

        Save();
        Rebuild();
        OnGravesChanged?.Invoke();
    }

    private void TryAddGrave(Hunter hunter)
    {
        HunterData data = hunter != null ? hunter.Data : null;
        string hunterId = data != null ? data.hunterId : null;
        if (string.IsNullOrWhiteSpace(hunterId)) return;
        if (graves.Exists(record => record != null && string.Equals(record.hunterId, hunterId, StringComparison.OrdinalIgnoreCase))) return;
        if (maxGraves > 0 && graves.Count >= maxGraves) return;

        completedMissionCounts.TryGetValue(hunterId, out int completedMissions);
        graves.Add(new GraveRecord
        {
            hunterId = hunterId,
            hunterName = data != null && !string.IsNullOrWhiteSpace(data.hunterName) ? data.hunterName : hunter.name,
            completedMissions = completedMissions
        });
    }

    private void ClearSpawnedMarkers()
    {
        foreach (GameObject marker in spawnedMarkers)
        {
            if (marker != null)
            {
                Destroy(marker);
            }
        }
        spawnedMarkers.Clear();
    }

    private void Load()
    {
        graves.Clear();
        completedMissionCounts.Clear();
        if (!File.Exists(savePath)) return;

        try
        {
            string json = File.ReadAllText(savePath);
            if (string.IsNullOrWhiteSpace(json)) return;

            GraveyardSaveData data = JsonUtility.FromJson<GraveyardSaveData>(json);
            if (data == null) return;

            if (data.graves != null)
            {
                foreach (GraveRecord record in data.graves)
                {
                    if (record == null || string.IsNullOrWhiteSpace(record.hunterId)) continue;
                    if (maxGraves > 0 && graves.Count >= maxGraves) break;
                    graves.Add(record);
                }
            }

            if (data.missionCountHunterIds == null || data.missionCounts == null) return;
            int count = Mathf.Min(data.missionCountHunterIds.Count, data.missionCounts.Count);
            for (int i = 0; i < count; i++)
            {
                string hunterId = data.missionCountHunterIds[i];
                if (string.IsNullOrWhiteSpace(hunterId)) continue;
                completedMissionCounts[hunterId] = Mathf.Max(0, data.missionCounts[i]);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"GraveyardManager: Failed to load state. {ex.Message}", this);
        }
    }

    private void Save()
    {
        try
        {
            GraveyardSaveData data = new GraveyardSaveData
            {
                graves = new List<GraveRecord>(graves)
            };

            foreach (KeyValuePair<string, int> entry in completedMissionCounts)
            {
                data.missionCountHunterIds.Add(entry.Key);
                data.missionCounts.Add(entry.Value);
            }

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(savePath, json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"GraveyardManager: Failed to save state. {ex.Message}", this);
        }
    }
}
