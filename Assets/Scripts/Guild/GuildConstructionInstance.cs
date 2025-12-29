using System.Collections.Generic;
using UnityEngine;

public class GuildConstructionInstance : MonoBehaviour
{
    [SerializeField] private GuildConstructionDefinition definition;
    [Header("State Toggle")]
    [SerializeField] private List<GameObject> enableWhenBuilt = new List<GameObject>();
    [SerializeField] private List<GameObject> disableWhenBuilt = new List<GameObject>();
    [SerializeField] private List<GuildDoorController> doorsUnlocked = new List<GuildDoorController>();

    public GuildConstructionDefinition Definition => definition;

    public void ApplyState(bool built)
    {
        SetActiveList(enableWhenBuilt, built);
        SetActiveList(disableWhenBuilt, !built);
        UpdateDoors(built);
    }

    private void SetActiveList(List<GameObject> list, bool value)
    {
        if (list == null) return;
        foreach (var go in list)
        {
            if (go == null) continue;
            if (go.activeSelf != value)
            {
                go.SetActive(value);
            }
        }
    }

    private void UpdateDoors(bool built)
    {
        if (doorsUnlocked == null) return;
        foreach (var door in doorsUnlocked)
        {
            if (door == null) continue;
            if (built)
            {
                door.RegisterUnlockSource(this);
            }
            else
            {
                door.UnregisterUnlockSource(this);
            }
        }
    }
}
