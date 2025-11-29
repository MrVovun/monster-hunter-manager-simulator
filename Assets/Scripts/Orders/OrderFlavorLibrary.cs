using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "OrderFlavorLibrary", menuName = "Guild Manager/Order Flavor Library")]
public class OrderFlavorLibrary : ScriptableObject
{
    [SerializeField] private List<OrderFlavorEntry> entries = new List<OrderFlavorEntry>();

    public OrderFlavorEntry GetRandomFlavor()
    {
        if (entries == null || entries.Count == 0) return null;
        int index = Random.Range(0, entries.Count);
        return entries[index];
    }

    public IReadOnlyList<OrderFlavorEntry> GetEntries() => entries;
}

[System.Serializable]
public class OrderFlavorEntry
{
    public string title;
    [TextArea(2, 5)] public string description;
}

