using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GlobalHunterConfig", menuName = "Guild Manager/Global Hunter Config")]
public class GlobalHunterConfig : ScriptableObject
{
    [SerializeField] private List<RarityEntry> rarities = new List<RarityEntry>();
    [Header("Recruitment Settings")]
    [SerializeField] private int basePostingFee = 150;
    [SerializeField] private int costPerMinute = 50;
    [SerializeField] private Vector2 arrivalIntervalSeconds = new Vector2(45f, 60f);
    [SerializeField] private List<int> campaignDurationsMinutes = new List<int> { 2, 4, 6 };
    [SerializeField] private int maxCandidateQueueSize = 3;
    [SerializeField] private int defaultInitialHunterCount = 3;

    public IReadOnlyList<RarityEntry> GetRarities() => rarities;
    public IReadOnlyList<int> GetCampaignDurationsMinutes() => campaignDurationsMinutes;
    public int GetBasePostingFee() => Mathf.Max(0, basePostingFee);
    public int GetCostPerMinute() => Mathf.Max(0, costPerMinute);
    public Vector2 GetArrivalIntervalSeconds() => arrivalIntervalSeconds;
    public int GetMaxCandidateQueueSize() => Mathf.Max(1, maxCandidateQueueSize);
    public int GetDefaultInitialHunterCount() => Mathf.Max(0, defaultInitialHunterCount);
    private static GlobalHunterConfig cachedInstance;

    public static GlobalHunterConfig GetGlobalConfig()
    {
        if (cachedInstance != null) return cachedInstance;
        cachedInstance = Resources.Load<GlobalHunterConfig>("GlobalHunterConfig");
        return cachedInstance;
    }

    public RarityEntry GetRarity(RarityType type)
    {
        foreach (var entry in rarities)
        {
            if (entry != null && entry.rarity == type)
            {
                return entry;
            }
        }
        return null;
    }

    [System.Serializable]
    public class RarityEntry
    {
        public RarityType rarity;
        public string displayName;
        public Color color = Color.white;
        [Tooltip("Relative chance for this rarity to appear during recruitment.")]
        public float recruitmentWeight = 1f;
    }

    public enum RarityType
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }
}
