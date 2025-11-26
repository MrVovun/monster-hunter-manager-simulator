using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Managers")]
    [SerializeField] private OrderManager orderManager;
    [SerializeField] private GoldManager goldManager;
    [SerializeField] private ReputationManager reputationManager;
    [SerializeField] private HunterManager hunterManager;
    [SerializeField] private OrderGenerator orderGenerator;
    [SerializeField] private MissionResolver missionResolver;
    [SerializeField] private TimeManager timeManager;

    [Header("Starting Values")]
    [SerializeField] private int startingGold = 500;
    [SerializeField] private int startingReputation = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureManagerRefs();
        InitializeManagers();
        HookTimeEvents();
    }

    private void EnsureManagerRefs()
    {
        if (orderManager == null) orderManager = GetComponentInChildren<OrderManager>();
        if (goldManager == null) goldManager = GetComponentInChildren<GoldManager>();
        if (reputationManager == null) reputationManager = GetComponentInChildren<ReputationManager>();
        if (hunterManager == null) hunterManager = FindObjectOfType<HunterManager>();
        if (orderGenerator == null) orderGenerator = GetComponentInChildren<OrderGenerator>();
        if (missionResolver == null) missionResolver = GetComponentInChildren<MissionResolver>();
        if (timeManager == null) timeManager = FindObjectOfType<TimeManager>();

        // Create basics if missing so the scene can run
        if (goldManager == null) goldManager = gameObject.AddComponent<GoldManager>();
        if (reputationManager == null) reputationManager = gameObject.AddComponent<ReputationManager>();
        if (orderManager == null) orderManager = gameObject.AddComponent<OrderManager>();
        if (orderGenerator == null) orderGenerator = gameObject.AddComponent<OrderGenerator>();
        if (missionResolver == null) missionResolver = gameObject.AddComponent<MissionResolver>();
    }

    private void InitializeManagers()
    {
        goldManager.Initialize(startingGold);
        reputationManager.Initialize(startingReputation);
        orderManager.Initialize(orderGenerator, missionResolver, timeManager);
    }

    private void HookTimeEvents()
    {
        if (timeManager != null)
        {
            timeManager.OnDayStarted += HandleDayStarted;
            // Pay once at startup to cover day 0
            HandleDayStarted(timeManager.GetCurrentDayIndex());
        }
    }

    private void HandleDayStarted(int dayIndex)
    {
        if (hunterManager == null || goldManager == null) return;
        bool paid = hunterManager.PayUpkeep(goldManager);
        if (!paid)
        {
            Debug.LogWarning($"Day {dayIndex}: Unable to pay upkeep (gold too low)." );
        }
    }

    public OrderManager GetOrderManager() => orderManager;
    public GoldManager GetGoldManager() => goldManager;
    public ReputationManager GetReputationManager() => reputationManager;
    public HunterManager GetHunterManager() => hunterManager;
    public TimeManager GetTimeManager() => timeManager;

    public int GetReputation()
    {
        return reputationManager != null ? reputationManager.GetReputation() : 0;
    }
}
