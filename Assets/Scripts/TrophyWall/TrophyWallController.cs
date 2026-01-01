using System;
using System.Collections.Generic;
using UnityEngine;

public class TrophyWallController : MonoBehaviour
{
    private const string FamilyTagName = "family";

    [Header("Config")]
    [SerializeField] private TrophyWallConfig config;
    [SerializeField] private MonsterLibrary monsterLibrary;
    [SerializeField] private MonsterSlainTracker slainTracker;
    [SerializeField] private bool logFamiliesOnRebuild = false;
    [Header("Layout")]
    [SerializeField] private Transform topLeft;
    [SerializeField] private Transform bottomRight;
    [SerializeField] private Transform contentRoot;
    [Tooltip("Optional reference to define forward/up for spawned trophies. If unset, falls back to derived wall axes.")]
    [SerializeField] private Transform facingReference;
    [Tooltip("Optional rotation offset applied to every spawned head/frame/plaque (use if source prefabs face a different forward).")]
    [SerializeField] private Vector3 rotationOffsetEuler = Vector3.zero;
    [Tooltip("Offset applied only to trophy heads (not frames/plaques).")]
    [SerializeField] private Vector3 headOffset = Vector3.zero;

    private readonly List<PlacedTrophy> spawned = new List<PlacedTrophy>();
    private Vector3 homeUp;
    private Vector3 homeRight;

    private class PlacedTrophy
    {
        public GameObject plaque;
        public GameObject head;
        public GameObject frame;
    }

    private void Awake()
    {
        if (monsterLibrary == null && GameManager.Instance != null)
        {
            monsterLibrary = GameManager.Instance.GetGameConfig()?.monsterLibrary;
        }

        if (slainTracker == null)
        {
            slainTracker = FindObjectOfType<MonsterSlainTracker>();
        }

        CacheBasis();
    }

    private void OnEnable()
    {
        if (slainTracker != null)
        {
            slainTracker.OnCountsChanged += HandleCountsChanged;
        }
        Rebuild();
    }

    private void Start()
    {
        // Ensure initial layout even if no counts changed event fired yet
        Rebuild();
    }

    private void OnDisable()
    {
        if (slainTracker != null)
        {
            slainTracker.OnCountsChanged -= HandleCountsChanged;
        }
    }

    private void HandleCountsChanged()
    {
        Rebuild();
    }

    public void Rebuild()
    {
        ClearSpawned();
        var monsters = monsterLibrary != null ? monsterLibrary.GetMonsters() : null;
        if (monsters == null || monsters.Count == 0 || config == null)
        {
            Debug.LogWarning("TrophyWallController: No monsters found in library or config missing.");
            return;
        }

        var familyMap = BuildFamilyListings(monsters);
        List<string> families = BuildFamilyOrder(familyMap);
        if (logFamiliesOnRebuild)
        {
            foreach (var kvp in familyMap)
            {
                Debug.Log($"[TrophyWall] Family '{kvp.Key}' count={kvp.Value.Count}");
            }
        }
        if (families.Count == 0) return;
        int columns = families.Count;
        if (columns == 0) return;

        for (int col = 0; col < columns; col++)
        {
            string family = families[col];
            if (!familyMap.TryGetValue(family, out var list))
            {
                list = null;
            }

            int rowsForThisFamily = list != null ? list.Count : 0;
            for (int row = 0; row < rowsForThisFamily; row++)
            {
                MonsterData monster = list[row];
                Vector3 position = GetSlotPosition(row, rowsForThisFamily, col, columns);
                Quaternion rotation = GetSlotRotation();
                SpawnSlot(monster, position, rotation);
            }
        }
    }

    private void SpawnSlot(MonsterData monster, Vector3 position, Quaternion rotation)
    {
        GameObject plaque = null;
        Quaternion finalRot = rotation * Quaternion.Euler(rotationOffsetEuler);

        if (monster != null && config.emptyPlaquePrefab != null)
        {
            plaque = Instantiate(config.emptyPlaquePrefab, position, finalRot, GetParent());
        }

        GameObject head = null;
        GameObject frame = null;
        int kills = slainTracker != null ? slainTracker.GetKillCount(monster) : 0;
        if (monster != null && monster.trophyHeadPrefab != null && kills > 0)
        {
            Vector3 headPos = position + finalRot * headOffset;
            head = Instantiate(monster.trophyHeadPrefab, headPos, finalRot, GetParent());
            ApplyScale(head, monster.trophyScale);
        }

        GameObject framePrefab = GetFramePrefab(kills);
        if (framePrefab != null && monster != null && kills > 0)
        {
            frame = Instantiate(framePrefab, position, finalRot, GetParent());
        }

        spawned.Add(new PlacedTrophy
        {
            plaque = plaque,
            head = head,
            frame = frame
        });
    }

    private Transform GetParent()
    {
        return contentRoot != null ? contentRoot : transform;
    }

    private GameObject GetFramePrefab(int kills)
    {
        GameObject best = config.baseFramePrefab;
        if (kills <= 0)
        {
            return null;
        }

        if (config.frameTiers != null)
        {
            for (int i = 0; i < config.frameTiers.Count; i++)
            {
                var tier = config.frameTiers[i];
                if (tier == null || tier.framePrefab == null) continue;
                if (kills >= tier.killThreshold)
                {
                    best = tier.framePrefab;
                }
                else
                {
                    break;
                }
            }
        }

        return best;
    }

    private Vector3 GetSlotPosition(int rowIndex, int totalRows, int columnIndex, int totalColumns)
    {
        float colT = totalColumns <= 1 ? 0.5f : (float)columnIndex / (totalColumns - 1);
        float rowT = totalRows <= 1 ? 0.5f : (float)rowIndex / (totalRows - 1);

        Vector3 leftToRight = Vector3.Lerp(topLeft.position, bottomRight.position, colT);
        Vector3 topToBottom = Vector3.Lerp(topLeft.position, bottomRight.position, rowT);

        // Reconstruct point inside the rectangle defined by the two corners
        float x = leftToRight.x;
        float y = topToBottom.y;
        float z = Mathf.Lerp(topLeft.position.z, bottomRight.position.z, colT);
        return new Vector3(x, y, z);
    }

    private Quaternion GetSlotRotation()
    {
        if (facingReference != null)
        {
            return Quaternion.LookRotation(facingReference.forward, facingReference.up);
        }
        return Quaternion.LookRotation(-homeRight, homeUp);
    }

    private List<string> BuildFamilyOrder(Dictionary<string, List<MonsterData>> familyMap)
    {
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<string> result = new List<string>();

        if (config != null && config.familyOrder != null)
        {
            foreach (var family in config.familyOrder)
            {
                if (string.IsNullOrWhiteSpace(family)) continue;
                if (seen.Contains(family)) continue;
                if (familyMap.ContainsKey(family))
                {
                    seen.Add(family);
                    result.Add(family);
                }
            }
        }

        List<string> extras = new List<string>(familyMap.Keys);
        extras.RemoveAll(f => seen.Contains(f));
        extras.Sort(StringComparer.OrdinalIgnoreCase);
        result.AddRange(extras);
        return result;
    }

    private Dictionary<string, List<MonsterData>> BuildFamilyListings(List<MonsterData> monsters)
    {
        var familyMap = new Dictionary<string, List<MonsterData>>(StringComparer.OrdinalIgnoreCase);
        foreach (var monster in monsters)
        {
            if (monster == null) continue;
            string family = NormalizeFamily(monster.GetTagValue(FamilyTagName));
            if (string.IsNullOrWhiteSpace(family))
            {
                family = "Unknown";
            }

            if (!familyMap.TryGetValue(family, out var list))
            {
                list = new List<MonsterData>();
                familyMap[family] = list;
            }
            list.Add(monster);
        }

        foreach (var kvp in familyMap)
        {
            kvp.Value.Sort((a, b) =>
            {
                int diff = a.minimumDifficulty.CompareTo(b.minimumDifficulty);
                if (diff != 0) return diff;
                return string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase);
            });
        }

        return familyMap;
    }

    private string NormalizeFamily(string family)
    {
        return string.IsNullOrWhiteSpace(family) ? string.Empty : family.Trim();
    }

    private void ClearSpawned()
    {
        foreach (var placed in spawned)
        {
            if (placed == null) continue;
            if (placed.plaque != null) DestroyImmediate(placed.plaque);
            if (placed.head != null) DestroyImmediate(placed.head);
            if (placed.frame != null) DestroyImmediate(placed.frame);
        }
        spawned.Clear();
    }

    private void CacheBasis()
    {
        if (topLeft != null && bottomRight != null)
        {
            homeRight = (bottomRight.position - topLeft.position);
            homeUp = Vector3.up;
        }
        else
        {
            homeRight = Vector3.right;
            homeUp = Vector3.up;
        }
    }

    private void ApplyScale(GameObject instance, float scale)
    {
        if (instance == null) return;
        float s = Mathf.Approximately(scale, 0f) ? 1f : scale;
        instance.transform.localScale *= s;
    }
}
