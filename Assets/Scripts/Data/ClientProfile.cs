using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ClientProfile", menuName = "Guild Manager/Client Profile")]
public class ClientProfile : ScriptableObject
{
    public string profileId;
    public string categoryName;
    [Tooltip("Additional delay applied to each question asked to this client.")]
    public float responseDelaySeconds = 0f;
    [Tooltip("Optional visual prefabs to spawn while this client is present.")]
    public List<GameObject> visualPrefabs = new List<GameObject>();

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
}
