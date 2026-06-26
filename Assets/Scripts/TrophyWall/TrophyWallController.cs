using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TrophyWallController : MonoBehaviour
{
    private const string FamilyTagName = "family";

    public enum LayoutMode
    {
        BoundsStretch,
        FixedSpacing
    }

    [Header("Config")]
    [SerializeField] private TrophyWallConfig config;
    [SerializeField] private MonsterLibrary monsterLibrary;
    [SerializeField] private MonsterSlainTracker slainTracker;
    [SerializeField] private bool logFamiliesOnRebuild = false;
    [Header("Layout")]
    [SerializeField] private LayoutMode layoutMode = LayoutMode.BoundsStretch;
    [SerializeField] private Transform topLeft;
    [SerializeField] private Transform bottomRight;
    [Tooltip("Origin used by Fixed Spacing mode. Falls back to Top Left when unset.")]
    [SerializeField] private Transform slotOrigin;
    [Tooltip("Horizontal/vertical distance between trophy slots in Fixed Spacing mode.")]
    [SerializeField] private Vector2 slotSpacing = new Vector2(1.25f, 1.1f);
    [Tooltip("In Fixed Spacing mode, splits long family columns after this many rows. 0 means unlimited.")]
    [SerializeField] private int maxRowsPerColumn = 0;
    [Tooltip("In Fixed Spacing mode, derives slot spacing from Top Left and Bottom Right bounds instead of Slot Spacing.")]
    [SerializeField] private bool fitFixedSpacingInsideBounds = false;
    [SerializeField] private Transform contentRoot;
    [Tooltip("Optional reference to define forward/up for spawned trophies. If unset, falls back to derived wall axes.")]
    [SerializeField] private Transform facingReference;
    [Tooltip("Optional rotation offset applied to every spawned head/frame/plaque (use if source prefabs face a different forward).")]
    [SerializeField] private Vector3 rotationOffsetEuler = Vector3.zero;
    [Header("Head Placement")]
    [Tooltip("Offset applied only to trophy heads (not frames/plaques).")]
    [SerializeField] private Vector3 headOffset = Vector3.zero;
    [Tooltip("Moves each spawned head so its renderer bounds center sits on the slot. This compensates for bad prefab pivots.")]
    [SerializeField] private bool centerHeadBoundsOnSlot = true;
    [Tooltip("Additional target offset used when centering head renderer bounds.")]
    [SerializeField] private Vector3 headBoundsCenterOffset = Vector3.zero;
    [Tooltip("Optional border prefab override. If unset, the config frame prefab/tier is used.")]
    [SerializeField] private GameObject borderPrefabOverride;

    [Header("Plaque Labels")]
    [SerializeField] private bool writePlaqueLabels = true;
    [SerializeField] private Vector3 plaqueOffset = Vector3.zero;
    [Tooltip("TMP child name used for the monster name. Falls back to the first TMP text under the plaque.")]
    [SerializeField] private string monsterNameTextChildName = "MonsterNameText";
    [Tooltip("TMP child name used for the family name. Falls back to the second TMP text under the plaque.")]
    [SerializeField] private string familyNameTextChildName = "FamilyNameText";
    [SerializeField] private string unknownFamilyLabel = "Unknown";

    [Header("Border Scaling")]
    [SerializeField] private bool scaleBorderToHeadBounds = true;
    [SerializeField] private bool centerBorderOnHeadBounds = true;
    [SerializeField] private Vector3 borderOffset = Vector3.zero;
    [Tooltip("Extra uniform scale applied after matching the head bounds.")]
    [SerializeField] private float borderBoundsPadding = 1.2f;
    [SerializeField] private float minimumBorderScale = 0.01f;
    [SerializeField] private float maximumBorderScale = 3f;

    [Header("Preview")]
    [Tooltip("Shows every trophy head even when kill count is 0. Useful for wall layout/design passes.")]
    [SerializeField] private bool previewAllTrophies = false;

    private readonly List<PlacedTrophy> spawned = new List<PlacedTrophy>();
    private Vector3 homeUp;
    private Vector3 homeRight;
    private int activeLayoutColumnCount = 1;
    private int activeLayoutMaxRows = 1;

    private class PlacedTrophy
    {
        public GameObject plaque;
        public GameObject head;
        public GameObject frame;
    }

    private class TrophyColumn
    {
        public readonly List<MonsterData> monsters = new List<MonsterData>();
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

    [ContextMenu("Rebuild Trophy Wall")]
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
        List<TrophyColumn> layoutColumns = BuildLayoutColumns(familyMap, families);
        activeLayoutColumnCount = Mathf.Max(1, layoutColumns.Count);
        activeLayoutMaxRows = 1;
        foreach (var column in layoutColumns)
        {
            if (column != null)
            {
                activeLayoutMaxRows = Mathf.Max(activeLayoutMaxRows, column.monsters.Count);
            }
        }

        for (int col = 0; col < layoutColumns.Count; col++)
        {
            TrophyColumn column = layoutColumns[col];
            if (column == null) continue;

            int rowsForThisColumn = column.monsters.Count;
            for (int row = 0; row < rowsForThisColumn; row++)
            {
                MonsterData monster = column.monsters[row];
                Vector3 position = GetSlotPosition(row, rowsForThisColumn, col, layoutColumns.Count);
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
            plaque = Instantiate(config.emptyPlaquePrefab, position + finalRot * plaqueOffset, finalRot, GetParent());
            ApplyPlaqueLabels(plaque, monster);
        }

        GameObject head = null;
        GameObject frame = null;
        int kills = GetDisplayedKillCount(monster);
        if (monster != null && monster.trophyHeadPrefab != null && kills > 0)
        {
            Quaternion headRot = finalRot * Quaternion.Euler(monster.trophyRotationOffsetEuler);
            Vector3 headPos = position + finalRot * (headOffset + monster.trophyPositionOffset);
            head = Instantiate(monster.trophyHeadPrefab, headPos, headRot, GetParent());
            ApplyScale(head, monster.trophyScale);
            CenterHeadOnSlot(head, position, finalRot, monster.trophyPositionOffset);
        }

        GameObject framePrefab = GetBorderPrefab(kills);
        if (framePrefab != null && monster != null && kills > 0)
        {
            frame = Instantiate(framePrefab, position + finalRot * borderOffset, finalRot, GetParent());
            ScaleBorderToHead(frame, head);
            CenterBorderOnHead(frame, head);
        }

        spawned.Add(new PlacedTrophy
        {
            plaque = plaque,
            head = head,
            frame = frame
        });
    }

    private GameObject GetBorderPrefab(int kills)
    {
        if (borderPrefabOverride != null)
        {
            return kills > 0 ? borderPrefabOverride : null;
        }

        return GetFramePrefab(kills);
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
        if (layoutMode == LayoutMode.FixedSpacing)
        {
            Transform origin = slotOrigin != null ? slotOrigin : topLeft;
            Vector3 originPosition = origin != null ? origin.position : transform.position;
            Vector3 right = GetLayoutRight();
            Vector3 up = GetLayoutUp();
            Vector2 spacing = GetEffectiveSlotSpacing(totalColumns);
            float horizontalOffset = spacing.x * columnIndex;
            float verticalOffset = spacing.y * rowIndex;
            return originPosition + right * horizontalOffset - up * verticalOffset;
        }

        if (topLeft == null || bottomRight == null)
        {
            return transform.position;
        }

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

    private Vector2 GetEffectiveSlotSpacing(int totalColumns)
    {
        Vector2 spacing = new Vector2(Mathf.Max(0.01f, slotSpacing.x), Mathf.Max(0.01f, slotSpacing.y));
        if (!fitFixedSpacingInsideBounds || topLeft == null || bottomRight == null)
        {
            return spacing;
        }

        Vector3 delta = bottomRight.position - topLeft.position;
        Vector3 right = GetLayoutRight();
        Vector3 up = GetLayoutUp();
        float width = Mathf.Abs(Vector3.Dot(delta, right));
        float height = Mathf.Abs(Vector3.Dot(delta, up));
        if (totalColumns > 1 && width > 0.001f)
        {
            spacing.x = width / (totalColumns - 1);
        }

        if (activeLayoutMaxRows > 1 && height > 0.001f)
        {
            spacing.y = height / (activeLayoutMaxRows - 1);
        }

        spacing.x = Mathf.Max(0.01f, spacing.x);
        spacing.y = Mathf.Max(0.01f, spacing.y);
        return spacing;
    }

    private Vector3 GetLayoutRight()
    {
        if (facingReference != null)
        {
            return facingReference.right.normalized;
        }

        if (topLeft != null && bottomRight != null)
        {
            Vector3 diagonal = bottomRight.position - topLeft.position;
            diagonal.y = 0f;
            if (diagonal.sqrMagnitude > 0.0001f)
            {
                return diagonal.normalized;
            }
        }

        return transform.right.normalized;
    }

    private Vector3 GetLayoutUp()
    {
        if (facingReference != null)
        {
            return facingReference.up.normalized;
        }

        return Vector3.up;
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

    private List<TrophyColumn> BuildLayoutColumns(Dictionary<string, List<MonsterData>> familyMap, List<string> families)
    {
        List<TrophyColumn> columns = new List<TrophyColumn>();
        if (families == null || families.Count == 0) return columns;

        int maxRows = Mathf.Max(0, maxRowsPerColumn);
        foreach (string family in families)
        {
            if (string.IsNullOrWhiteSpace(family)) continue;
            if (!familyMap.TryGetValue(family, out var monsters) || monsters == null || monsters.Count == 0) continue;

            if (layoutMode != LayoutMode.FixedSpacing || maxRows <= 0)
            {
                TrophyColumn column = new TrophyColumn();
                column.monsters.AddRange(monsters);
                columns.Add(column);
                continue;
            }

            for (int i = 0; i < monsters.Count; i += maxRows)
            {
                TrophyColumn column = new TrophyColumn();
                int count = Mathf.Min(maxRows, monsters.Count - i);
                for (int j = 0; j < count; j++)
                {
                    column.monsters.Add(monsters[i + j]);
                }
                columns.Add(column);
            }
        }

        return columns;
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

    private int GetDisplayedKillCount(MonsterData monster)
    {
        int kills = slainTracker != null ? slainTracker.GetKillCount(monster) : 0;
        return previewAllTrophies && monster != null && monster.trophyHeadPrefab != null
            ? Mathf.Max(1, kills)
            : kills;
    }

    private string GetFamilyName(MonsterData monster)
    {
        string family = monster != null ? NormalizeFamily(monster.GetTagValue(FamilyTagName)) : string.Empty;
        return string.IsNullOrWhiteSpace(family) ? unknownFamilyLabel : family;
    }

    private string GetMonsterName(MonsterData monster)
    {
        if (monster == null) return string.Empty;
        return string.IsNullOrWhiteSpace(monster.displayName) ? monster.name : monster.displayName.Trim();
    }

    private void ApplyPlaqueLabels(GameObject plaque, MonsterData monster)
    {
        if (!writePlaqueLabels || plaque == null || monster == null) return;

        TMP_Text[] texts = plaque.GetComponentsInChildren<TMP_Text>(true);
        if (texts == null || texts.Length == 0) return;

        string monsterName = GetMonsterName(monster);
        string familyName = GetFamilyName(monster);
        TMP_Text monsterText = FindTextByChildName(texts, monsterNameTextChildName);
        TMP_Text familyText = FindTextByChildName(texts, familyNameTextChildName);

        if (monsterText == null && texts.Length > 0)
        {
            monsterText = texts[0];
        }

        if (familyText == null && texts.Length > 1)
        {
            familyText = texts[1];
        }

        if (monsterText != null)
        {
            monsterText.text = monsterName;
        }

        if (familyText != null && familyText != monsterText)
        {
            familyText.text = familyName;
        }
        else if (monsterText != null && texts.Length == 1)
        {
            monsterText.text = $"{monsterName}\n{familyName}";
        }
    }

    private TMP_Text FindTextByChildName(TMP_Text[] texts, string childName)
    {
        if (texts == null || string.IsNullOrWhiteSpace(childName)) return null;
        foreach (var text in texts)
        {
            if (text == null) continue;
            if (string.Equals(text.name, childName, StringComparison.OrdinalIgnoreCase))
            {
                return text;
            }
        }

        return null;
    }

    private void ScaleBorderToHead(GameObject border, GameObject head)
    {
        if (!scaleBorderToHeadBounds || border == null || head == null) return;
        if (!TryGetRendererBounds(head, out Bounds headBounds)) return;
        if (!TryGetRendererBounds(border, out Bounds borderBounds)) return;

        float xScale = borderBounds.size.x > 0.001f ? headBounds.size.x / borderBounds.size.x : 1f;
        float yScale = borderBounds.size.y > 0.001f ? headBounds.size.y / borderBounds.size.y : 1f;
        float targetScale = Mathf.Max(xScale, yScale, minimumBorderScale) * Mathf.Max(0.01f, borderBoundsPadding);
        targetScale = Mathf.Clamp(targetScale, Mathf.Max(0.01f, minimumBorderScale), Mathf.Max(minimumBorderScale, maximumBorderScale));
        border.transform.localScale *= targetScale;
    }

    private void CenterHeadOnSlot(GameObject head, Vector3 slotPosition, Quaternion slotRotation, Vector3 monsterOffset)
    {
        if (!centerHeadBoundsOnSlot || head == null) return;
        if (!TryGetRendererBounds(head, out Bounds headBounds)) return;

        Vector3 targetCenter = slotPosition + slotRotation * (headBoundsCenterOffset + monsterOffset);
        Vector3 delta = targetCenter - headBounds.center;
        head.transform.position += delta;
    }

    private void CenterBorderOnHead(GameObject border, GameObject head)
    {
        if (!centerBorderOnHeadBounds || border == null || head == null) return;
        if (!TryGetRendererBounds(head, out Bounds headBounds)) return;
        if (!TryGetRendererBounds(border, out Bounds borderBounds)) return;

        Vector3 delta = headBounds.center - borderBounds.center;
        border.transform.position += delta;
    }

    private bool TryGetRendererBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        if (root == null) return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool found = false;
        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;
            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return found;
    }

    [ContextMenu("Clear Spawned Trophies")]
    public void ClearSpawned()
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
        homeRight = GetLayoutRight();
        homeUp = GetLayoutUp();
    }

    private void ApplyScale(GameObject instance, float scale)
    {
        if (instance == null) return;
        float s = Mathf.Approximately(scale, 0f) ? 1f : scale;
        instance.transform.localScale *= s;
    }
}
