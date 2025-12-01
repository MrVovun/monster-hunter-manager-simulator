using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GlobalHunterConfig", menuName = "Guild Manager/Global Hunter Config")]
public class GlobalHunterConfig : ScriptableObject
{
    [SerializeField] private List<RarityEntry> rarities = new List<RarityEntry>();

    public IReadOnlyList<RarityEntry> GetRarities() => rarities;

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
