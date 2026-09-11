using System.Collections.Generic;
using UnityEngine;

public class MissionBonusChestManager : MonoBehaviour
{
    [Header("Reward Condition")]
    [SerializeField] private bool useGameConfigThreshold = true;
    [SerializeField] private float requiredSuccessChancePercent = MissionOutcomeCalculator.MaxSuccessChance;
    [SerializeField, Range(0f, 1f)] private float mimicChance = 0.05f;

    [Header("Prefabs")]
    [SerializeField] private GameObject chestPrefab;
    [SerializeField] private GameObject mimicPrefab;

    [Header("Placement")]
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();
    [SerializeField] private bool parentToSpawnPoint = false;

    private readonly List<GameObject> spawnedRewards = new List<GameObject>();
    private OrderManager orderManager;

    private void OnEnable()
    {
        TrackOrderManager();
    }

    private void Start()
    {
        TrackOrderManager();
    }

    private void OnDisable()
    {
        if (orderManager != null)
        {
            orderManager.OnMissionResolved -= HandleMissionResolved;
        }
    }

    private void TrackOrderManager()
    {
        OrderManager manager = GameManager.Instance != null
            ? GameManager.Instance.GetOrderManager()
            : SceneLookup.Find<OrderManager>();

        if (manager == orderManager) return;

        if (orderManager != null)
        {
            orderManager.OnMissionResolved -= HandleMissionResolved;
        }

        orderManager = manager;
        if (orderManager != null)
        {
            orderManager.OnMissionResolved -= HandleMissionResolved;
            orderManager.OnMissionResolved += HandleMissionResolved;
        }
    }

    private void HandleMissionResolved(MissionReport report)
    {
        if (!ShouldSpawnReward(report)) return;
        SpawnReward(Random.value < mimicChance);
    }

    private bool ShouldSpawnReward(MissionReport report)
    {
        if (report == null || !report.success) return false;
        return report.successChancePercent >= GetRequiredSuccessChancePercent();
    }

    private float GetRequiredSuccessChancePercent()
    {
        if (useGameConfigThreshold)
        {
            var config = GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null;
            if (config != null)
            {
                return Mathf.Max(1f, config.bonusRewardThresholdPercent);
            }
        }

        return Mathf.Max(1f, requiredSuccessChancePercent);
    }

    public void DebugSpawnChest()
    {
        SpawnReward(false);
    }

    public void DebugSpawnMimic()
    {
        SpawnReward(true);
    }

    private void SpawnReward(bool isMimic)
    {
        CleanupSpawnedRewards();

        Transform spawnPoint = GetFreeSpawnPoint();
        if (spawnPoint == null)
        {
            Debug.LogWarning("MissionBonusChestManager: No free chest spawn point is available.");
            return;
        }

        GameObject prefab = isMimic ? mimicPrefab : chestPrefab;
        if (prefab == null)
        {
            Debug.LogWarning($"MissionBonusChestManager: Missing {(isMimic ? "mimic" : "chest")} prefab.");
            return;
        }

        Transform parent = parentToSpawnPoint ? spawnPoint : null;
        GameObject instance = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation, parent);
        spawnedRewards.Add(instance);

        BonusChestInteractable interactable = instance.GetComponent<BonusChestInteractable>();
        if (interactable == null)
        {
            Debug.LogWarning($"MissionBonusChestManager: Spawned {(isMimic ? "mimic" : "chest")} prefab '{prefab.name}' has no BonusChestInteractable. Add and configure it on the prefab.", instance);
        }
    }

    private Transform GetFreeSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            return transform;
        }

        foreach (var point in spawnPoints)
        {
            if (point == null) continue;
            bool occupied = false;
            foreach (var reward in spawnedRewards)
            {
                if (reward == null) continue;
                if (Vector3.Distance(reward.transform.position, point.position) < 0.25f)
                {
                    occupied = true;
                    break;
                }
            }

            if (!occupied) return point;
        }

        return null;
    }

    private void CleanupSpawnedRewards()
    {
        for (int i = spawnedRewards.Count - 1; i >= 0; i--)
        {
            if (spawnedRewards[i] == null)
            {
                spawnedRewards.RemoveAt(i);
            }
        }
    }
}
