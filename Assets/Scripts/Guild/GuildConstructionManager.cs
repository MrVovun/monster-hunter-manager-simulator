using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class GuildConstructionManager : MonoBehaviour
{
    [Serializable]
    private class ConstructionSaveData
    {
        public List<string> builtIds = new List<string>();
    }

    public enum ConstructionStatus
    {
        Available,
        Unavailable,
        Built
    }

    [Header("Scene References")]
    [SerializeField] private List<GuildConstructionInstance> constructionInstances = new List<GuildConstructionInstance>();
    [SerializeField] private AudioClip buildSfx;
    [SerializeField] private AudioSource audioSource;

    public event Action OnStateChanged;
    public event Action<GuildConstructionDefinition> OnConstructionBuilt;

    private readonly Dictionary<string, GuildConstructionDefinition> definitionLookup = new Dictionary<string, GuildConstructionDefinition>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<GuildConstructionInstance>> instanceLookup = new Dictionary<string, List<GuildConstructionInstance>>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> builtIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly List<GuildConstructionDefinition> orderedDefinitions = new List<GuildConstructionDefinition>();

    private GoldManager goldManager;
    private ReputationManager reputationManager;
    private GameConfig gameConfig;
    private string savePath;

    private void Awake()
    {
        goldManager = GameManager.Instance != null ? GameManager.Instance.GetGoldManager() : null;
        reputationManager = GameManager.Instance != null ? GameManager.Instance.GetReputationManager() : null;
        gameConfig = GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null;
        RegisterDefinitions();
        RegisterInstances();
        savePath = Path.Combine(Application.persistentDataPath, "guild_construction_state.json");
        LoadState();
        ApplyStateToInstances();
        OnStateChanged?.Invoke();
    }

    private void RegisterDefinitions()
    {
        definitionLookup.Clear();
        orderedDefinitions.Clear();
        if (gameConfig?.guildConstructions == null) return;

        foreach (var def in gameConfig.guildConstructions)
        {
            RegisterDefinition(def);
        }
    }

    private void RegisterInstances()
    {
        instanceLookup.Clear();
        if (constructionInstances == null)
        {
            constructionInstances = new List<GuildConstructionInstance>();
        }

        var discovered = SceneLookup.FindAll<GuildConstructionInstance>(true);
        foreach (var instance in discovered)
        {
            if (instance != null && !constructionInstances.Contains(instance))
            {
                constructionInstances.Add(instance);
            }
        }

        constructionInstances.RemoveAll(i => i == null || i.Definition == null || string.IsNullOrEmpty(i.Definition.ConstructionId));
        foreach (var instance in constructionInstances)
        {
            RegisterDefinition(instance.Definition);

            var id = instance.Definition.ConstructionId;
            if (!instanceLookup.TryGetValue(id, out var list))
            {
                list = new List<GuildConstructionInstance>();
                instanceLookup[id] = list;
            }
            list.Add(instance);
        }
    }

    private void RegisterDefinition(GuildConstructionDefinition definition)
    {
        if (definition == null || string.IsNullOrEmpty(definition.ConstructionId)) return;
        if (definitionLookup.ContainsKey(definition.ConstructionId)) return;

        definitionLookup.Add(definition.ConstructionId, definition);
        orderedDefinitions.Add(definition);
    }

    private void LoadState()
    {
        builtIds.Clear();
        foreach (var def in orderedDefinitions)
        {
            if (def != null && def.startsBuilt && !string.IsNullOrEmpty(def.ConstructionId))
            {
                builtIds.Add(def.ConstructionId);
            }
        }

        if (!File.Exists(savePath))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(savePath);
            if (string.IsNullOrWhiteSpace(json)) return;
            var data = JsonUtility.FromJson<ConstructionSaveData>(json);
            if (data?.builtIds == null) return;
            foreach (var id in data.builtIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                if (definitionLookup.ContainsKey(id))
                {
                    builtIds.Add(id);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"GuildConstructionManager: Failed to load state. {ex.Message}");
        }
    }

    private void SaveState()
    {
        try
        {
            var data = new ConstructionSaveData
            {
                builtIds = new List<string>(builtIds)
            };
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(savePath, json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"GuildConstructionManager: Failed to save state. {ex.Message}");
        }
    }

    private void ApplyStateToInstances()
    {
        foreach (var def in orderedDefinitions)
        {
            ApplyDefinitionState(def, IsBuilt(def));
        }
    }

    private void ApplyDefinitionState(GuildConstructionDefinition definition, bool built)
    {
        if (definition == null || string.IsNullOrEmpty(definition.ConstructionId)) return;
        if (instanceLookup.TryGetValue(definition.ConstructionId, out var instances))
        {
            foreach (var instance in instances)
            {
                if (instance == null) continue;
                instance.ApplyState(built);
            }
        }
    }

    public IReadOnlyList<GuildConstructionDefinition> GetAllDefinitions() => orderedDefinitions;

    public List<GuildConstructionDefinition> GetDefinitionsForDisplay()
    {
        List<GuildConstructionDefinition> list = new List<GuildConstructionDefinition>();
        foreach (var def in orderedDefinitions)
        {
            if (def == null) continue;
            if (ShouldDisplay(def))
            {
                list.Add(def);
            }
        }

        list.Sort((a, b) =>
        {
            int statusOrder = GetStatusOrder(GetStatus(a)).CompareTo(GetStatusOrder(GetStatus(b)));
            if (statusOrder != 0) return statusOrder;
            return string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase);
        });
        return list;
    }

    private int GetStatusOrder(ConstructionStatus status)
    {
        switch (status)
        {
            case ConstructionStatus.Available:
                return 0;
            case ConstructionStatus.Unavailable:
                return 1;
            default:
                return 2;
        }
    }

    private bool ShouldDisplay(GuildConstructionDefinition definition)
    {
        if (definition == null) return false;
        if (IsBuilt(definition)) return true;
        return MeetsVisibilityPrerequisite(definition);
    }

    private bool MeetsVisibilityPrerequisite(GuildConstructionDefinition definition)
    {
        if (definition == null || definition.prerequisite == null) return true;
        return IsBuilt(definition.prerequisite);
    }

    public bool IsBuilt(GuildConstructionDefinition definition)
    {
        if (definition == null || string.IsNullOrEmpty(definition.ConstructionId)) return false;
        return builtIds.Contains(definition.ConstructionId);
    }

    public int GetBuiltHunterCapacityIncrease()
    {
        int total = 0;
        foreach (var def in orderedDefinitions)
        {
            if (def == null || !IsBuilt(def)) continue;
            total += Mathf.Max(0, def.hunterCapacityIncrease);
        }
        return total;
    }

    public ConstructionStatus GetStatus(GuildConstructionDefinition definition)
    {
        if (definition == null) return ConstructionStatus.Unavailable;
        if (IsBuilt(definition)) return ConstructionStatus.Built;
        if (!HasRequiredReputation(definition)) return ConstructionStatus.Unavailable;
        if (!HasRequiredGold(definition)) return ConstructionStatus.Unavailable;
        return ConstructionStatus.Available;
    }

    private bool HasRequiredReputation(GuildConstructionDefinition definition)
    {
        if (definition == null) return false;
        int reputation = reputationManager != null ? reputationManager.GetReputation() : 0;
        return reputation >= definition.requiredReputation;
    }

    public bool HasRequiredGold(GuildConstructionDefinition definition)
    {
        if (definition == null) return false;
        if (definition.goldCost <= 0) return true;
        return goldManager != null && goldManager.GetGold() >= definition.goldCost;
    }

    public string GetUnavailableReason(GuildConstructionDefinition definition)
    {
        if (definition == null) return string.Empty;
        if (!MeetsVisibilityPrerequisite(definition))
        {
            return definition.prerequisite != null
                ? $"Requires {definition.prerequisite.displayName}"
                : "Requires prerequisite construction";
        }
        if (!HasRequiredReputation(definition))
        {
            int reputation = reputationManager != null ? reputationManager.GetReputation() : 0;
            return $"Requires reputation {definition.requiredReputation} (current {reputation})";
        }
        if (!HasRequiredGold(definition))
        {
            int gold = goldManager != null ? goldManager.GetGold() : 0;
            return $"Requires {definition.goldCost} gold (current {gold})";
        }
        return string.Empty;
    }

    public bool TryBuild(GuildConstructionDefinition definition)
    {
        if (definition == null) return false;
        if (IsBuilt(definition)) return false;
        if (!MeetsVisibilityPrerequisite(definition)) return false;
        if (!HasRequiredReputation(definition)) return false;

        if (definition.goldCost > 0 && goldManager != null)
        {
            if (!goldManager.SpendGold(definition.goldCost))
            {
                return false;
            }
        }

        builtIds.Add(definition.ConstructionId);
        ApplyDefinitionState(definition, true);
        SaveState();
        PlayBuildSfx();
        OnStateChanged?.Invoke();
        OnConstructionBuilt?.Invoke(definition);

        var config = GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null;
        var tm = GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null;
        float cost = config != null ? config.actionTimeSettings.buildSeconds : 0f;
        tm?.AdvanceTime(cost);
        return true;
    }

    public void ResetAllConstructions()
    {
        builtIds.Clear();
        foreach (var def in orderedDefinitions)
        {
            if (def != null && def.startsBuilt && !string.IsNullOrEmpty(def.ConstructionId))
            {
                builtIds.Add(def.ConstructionId);
            }
        }

        ApplyStateToInstances();
        SaveState();
        OnStateChanged?.Invoke();
    }

    private void PlayBuildSfx()
    {
        if (buildSfx == null) return;
        if (audioSource != null)
        {
            audioSource.PlayOneShot(buildSfx);
        }
        else
        {
            AudioSource.PlayClipAtPoint(buildSfx, transform.position);
        }
    }
}
