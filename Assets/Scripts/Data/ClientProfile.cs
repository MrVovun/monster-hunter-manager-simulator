using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ClientProfile", menuName = "Guild Manager/Client Profile")]
public class ClientProfile : ScriptableObject
{
    public string profileId;
    public string categoryName;
    [Tooltip("Additional action-time cost applied to each question asked to this client. Does not affect real-time answer delay.")]
    public float responseDelaySeconds = 0f;
    [Tooltip("Optional visual prefabs to spawn while this client is present.")]
    public List<GameObject> visualPrefabs = new List<GameObject>();
    [Tooltip("Optional P09 modular presets. If any are assigned, the client spawner picks from these before falling back to visual prefabs.")]
    public List<P09HumanoidPreset> p09VisualPresets = new List<P09HumanoidPreset>();

    private void OnEnable()
    {
        EnsureId();
    }

    private void OnValidate()
    {
        EnsureId();
    }

    private void EnsureId()
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            profileId = Guid.NewGuid().ToString("N");
        }
    }

    public GameObject GetNextVisualPrefab()
    {
        if (visualPrefabs == null || visualPrefabs.Count == 0) return null;
        int index = UnityEngine.Random.Range(0, visualPrefabs.Count);
        return visualPrefabs[index];
    }

    public P09HumanoidPreset GetNextP09VisualPreset()
    {
        if (p09VisualPresets == null || p09VisualPresets.Count == 0) return null;
        int index = UnityEngine.Random.Range(0, p09VisualPresets.Count);
        return p09VisualPresets[index];
    }
}
