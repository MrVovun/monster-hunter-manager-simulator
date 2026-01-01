using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class MonsterFamilyImporter
{
    [MenuItem("Tools/Import/Monster Families CSV...", priority = 200)]
    public static void ImportFamilies()
    {
        string path = EditorUtility.OpenFilePanel("Select monster families CSV", Application.dataPath, "csv,txt");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            evidenceTagLibrary = ResolveEvidenceTagLibrary();
            var rows = ParseCsvRobust(path);
            if (rows.Count == 0)
            {
                Debug.LogWarning("[MonsterFamilyImporter] No data rows found.");
                return;
            }

            LogTagStats(rows);

            var rowLookup = BuildRowLookup(rows);
            if (rowLookup.Count == 0)
            {
                Debug.LogWarning("[MonsterFamilyImporter] No ID/name rows found.");
                return;
            }

            var monsters = LoadMonsters();
            int updated = 0;
            int matchedIds = 0;
            HashSet<string> matchedLookupKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var monster in monsters)
            {
                if (monster == null || string.IsNullOrWhiteSpace(monster.monsterId)) continue;
                if (!TryFindRow(monster, rowLookup, out var row, matchedLookupKeys, out var matchedKey)) continue;
                matchedIds++;
                if (ApplyRow(monster, row, matchedKey))
                {
                    updated++;
                }
            }

            LogUnmatchedRows(rowLookup, matchedLookupKeys);

            AssetDatabase.SaveAssets();
            Debug.Log($"[MonsterFamilyImporter] Updated {updated} MonsterData assets (matched {matchedIds} IDs) from {path}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MonsterFamilyImporter] Failed to import. {ex.Message}");
        }
    }

    private static List<Dictionary<string, string>> ParseCsvRobust(string path)
    {
        var lines = File.ReadAllLines(path);
        List<Dictionary<string, string>> rows = new List<Dictionary<string, string>>();
        if (lines.Length == 0) return rows;

        char delimiter = DetectDelimiter(lines[0]);
        var headers = SplitCsvLine(lines[0], delimiter).Select(h => h.Trim()).ToArray();

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var parts = SplitCsvLine(lines[i], delimiter);
            Dictionary<string, string> row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < headers.Length && c < parts.Count; c++)
            {
                row[headers[c]] = parts[c].Trim();
            }
            rows.Add(row);
        }
        return rows;
    }

    private static char DetectDelimiter(string headerLine)
    {
        char[] candidates = new[] { ',', ';', '\t' };
        char best = ',';
        int bestCount = -1;
        foreach (var c in candidates)
        {
            int count = headerLine.Count(ch => ch == c);
            if (count > bestCount)
            {
                bestCount = count;
                best = c;
            }
        }
        return best;
    }

    private static List<string> SplitCsvLine(string line, char delimiter)
    {
        List<string> result = new List<string>();
        bool inQuotes = false;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '\"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')
                {
                    sb.Append('\"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == delimiter && !inQuotes)
            {
                result.Add(sb.ToString());
                sb.Length = 0;
            }
            else
            {
                sb.Append(c);
            }
        }
        result.Add(sb.ToString());
        return result;
    }

    private static Dictionary<string, Dictionary<string, string>> BuildRowLookup(List<Dictionary<string, string>> rows)
    {
        var map = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (!row.TryGetValue("ID", out var id))
            {
                row.TryGetValue("monsterId", out id);
            }
            if (string.IsNullOrWhiteSpace(id)) continue;

            map[id.Trim()] = row;
        }
        return map;
    }

    private static void LogTagStats(List<Dictionary<string, string>> rows)
    {
        string[] targets = { "tag_tail", "tag_winged" };
        foreach (var target in targets)
        {
            HashSet<string> values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int nonEmpty = 0;
            foreach (var row in rows)
            {
                if (row.TryGetValue(target, out var v))
                {
                    string trimmed = v != null ? v.Trim() : string.Empty;
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        nonEmpty++;
                        values.Add(trimmed);
                    }
                }
            }
            Debug.Log($"[MonsterFamilyImporter] {target}: non-empty rows={nonEmpty}, unique values=[{string.Join(", ", values)}]");
        }
    }

    private static bool TryFindRow(MonsterData monster, Dictionary<string, Dictionary<string, string>> lookup, out Dictionary<string, string> row, HashSet<string> matchedKeys, out string matchedKey)
    {
        matchedKey = null;
        // First try by ID
        if (!string.IsNullOrWhiteSpace(monster.monsterId) && lookup.TryGetValue(monster.monsterId, out row))
        {
            matchedKeys?.Add(monster.monsterId);
            matchedKey = monster.monsterId;
            return true;
        }

        // Fallback by Name/displayName if unique
        row = null;
        foreach (var kvp in lookup)
        {
            if (kvp.Value.TryGetValue("Name", out var name))
            {
                if (string.Equals(name?.Trim(), monster.displayName, StringComparison.OrdinalIgnoreCase))
                {
                    row = kvp.Value;
                    matchedKeys?.Add(kvp.Key);
                    matchedKey = kvp.Key;
                    return true;
                }
            }
        }

        // Fallback by asset name (without extension)
        string assetName = monster.name;
        foreach (var kvp in lookup)
        {
            if (kvp.Value.TryGetValue("Name", out var name))
            {
                if (string.Equals(name?.Trim(), assetName, StringComparison.OrdinalIgnoreCase))
                {
                    row = kvp.Value;
                    matchedKeys?.Add(kvp.Key);
                    matchedKey = kvp.Key;
                    return true;
                }
            }
        }

        return false;
    }

    private static List<MonsterData> LoadMonsters()
    {
        List<MonsterData> monsters = new List<MonsterData>();
        string[] guids = AssetDatabase.FindAssets("t:MonsterData");
        foreach (var guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var monster = AssetDatabase.LoadAssetAtPath<MonsterData>(assetPath);
            if (monster != null)
            {
                monsters.Add(monster);
            }
        }
        return monsters;
    }

    private static bool ApplyRow(MonsterData monster, Dictionary<string, string> row, string matchedKey)
    {
        bool changed = false;
        List<string> tagSummary = new List<string>();
        if (monster.evidenceTags == null)
        {
            monster.evidenceTags = new List<MonsterData.MonsterTagAssignment>();
        }

        // Optional core fields
        if (row.TryGetValue("Name", out var name) && !string.IsNullOrWhiteSpace(name) && !string.Equals(monster.displayName, name))
        {
            monster.displayName = name;
            changed = true;
        }
        if (row.TryGetValue("Description", out var desc) && !string.IsNullOrWhiteSpace(desc) && !string.Equals(monster.description, desc))
        {
            monster.description = desc;
            changed = true;
        }
        if (row.TryGetValue("Min diff", out var minDiffStr) && int.TryParse(minDiffStr, out var minDiff) && monster.minimumDifficulty != minDiff)
        {
            monster.minimumDifficulty = Mathf.Max(0, minDiff);
            changed = true;
        }
        if (row.TryGetValue("Rep req", out var repStr) && int.TryParse(repStr, out var rep) && monster.requiredReputation != rep)
        {
            monster.requiredReputation = Mathf.Max(0, rep);
            changed = true;
        }

        // Tags from tag_* columns (CSV is single source of truth: remove existing and set even if empty)
        foreach (var kvp in row)
        {
            if (!kvp.Key.StartsWith("tag_", StringComparison.OrdinalIgnoreCase)) continue;
            string category = kvp.Key.Substring("tag_".Length).Trim();
            string value = kvp.Value != null ? kvp.Value.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(category)) continue;

            // Remove any existing entries for this category to avoid duplicates/stale values
            monster.evidenceTags.RemoveAll(t => t != null && string.Equals(t.categoryName, category, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(value))
            {
                string normalizedValue = NormalizeTagValue(category, value);
                var tag = new MonsterData.MonsterTagAssignment { categoryName = category, valueName = normalizedValue };
                monster.evidenceTags.Add(tag);
                tagSummary.Add($"{category}='{normalizedValue}'");
            }
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(monster);

            // Per-monster debug summary
            Debug.Log($"[MonsterFamilyImporter] Applied row '{matchedKey ?? "?"}' -> Monster '{monster.displayName}' (id={monster.monsterId}) Tags: {string.Join(", ", tagSummary)}");
        }
        return changed;
    }

    private static EvidenceTagLibrary evidenceTagLibrary;

    private static EvidenceTagLibrary ResolveEvidenceTagLibrary()
    {
        // Try GameConfig first
        var configGuid = AssetDatabase.FindAssets("t:GameConfig").FirstOrDefault();
        if (!string.IsNullOrEmpty(configGuid))
        {
            var configPath = AssetDatabase.GUIDToAssetPath(configGuid);
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(configPath);
            if (config != null && config.evidenceTagLibrary != null)
            {
                return config.evidenceTagLibrary;
            }
        }

        // Fallback: direct EvidenceTagLibrary asset
        var libGuid = AssetDatabase.FindAssets("t:EvidenceTagLibrary").FirstOrDefault();
        if (!string.IsNullOrEmpty(libGuid))
        {
            var libPath = AssetDatabase.GUIDToAssetPath(libGuid);
            var lib = AssetDatabase.LoadAssetAtPath<EvidenceTagLibrary>(libPath);
            if (lib != null) return lib;
        }

        Debug.LogWarning("[MonsterFamilyImporter] EvidenceTagLibrary not found. Tag value normalization will be skipped.");
        return null;
    }

    private static string NormalizeTagValue(string category, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        if (evidenceTagLibrary == null) return value;

        var cat = evidenceTagLibrary.GetCategory(category);
        if (cat == null) return value;

        var found = cat.values.FirstOrDefault(v => string.Equals(v?.valueName, value, StringComparison.OrdinalIgnoreCase));
        if (found != null)
        {
            return found.valueName; // canonical casing
        }

        // Try loose match trimming spaces
        found = cat.values.FirstOrDefault(v => string.Equals(v?.valueName?.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase));
        if (found != null)
        {
            return found.valueName;
        }

        Debug.LogWarning($"[MonsterFamilyImporter] Value '{value}' not found in EvidenceTagLibrary for category '{category}'. Using raw value.");
        return value;
    }

    private static void LogUnmatchedRows(Dictionary<string, Dictionary<string, string>> lookup, HashSet<string> matchedKeys)
    {
        if (matchedKeys == null) return;
        List<string> unmatched = new List<string>();
        foreach (var key in lookup.Keys)
        {
            if (!matchedKeys.Contains(key))
            {
                unmatched.Add(key);
            }
        }
        if (unmatched.Count > 0)
        {
            Debug.LogWarning($"[MonsterFamilyImporter] Unmatched CSV rows (IDs): {string.Join(", ", unmatched)}");
        }
    }
}
