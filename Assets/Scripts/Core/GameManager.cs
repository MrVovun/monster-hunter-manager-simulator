using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public enum UpkeepCrisisState
    {
        Stable,
        Debt,
        UnpaidDay1,
        UnpaidDay2,
        GameOver
    }

    public static GameManager Instance { get; private set; }

    [Header("Managers")]
    [SerializeField] private OrderManager orderManager;
    [SerializeField] private GoldManager goldManager;
    [SerializeField] private ReputationManager reputationManager;
    [SerializeField] private HunterManager hunterManager;
    [SerializeField] private OrderGenerator orderGenerator;
    [SerializeField] private MissionResolver missionResolver;
    [SerializeField] private TimeManager timeManager;
    [SerializeField] private InvestigationManager investigationManager;
    [SerializeField] private GuildConstructionManager constructionManager;
    [SerializeField] private NotificationManager notificationManager;
    [SerializeField] private GraveyardManager graveyardManager;
    [SerializeField] private GameConfig gameConfig;

    [Header("Starting Values")]
    [SerializeField] private int startingGold = 500;
    [SerializeField] private int startingReputation = 0;
    [SerializeField] private GameObject gameOverScreen;

    private int unpaidUpkeepStreak;
    private float activeDebtSuccessPenaltyPercent;
    private bool gameOver;

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
        HookManagerEvents();
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
        if (investigationManager == null) investigationManager = FindObjectOfType<InvestigationManager>();
        if (constructionManager == null) constructionManager = FindObjectOfType<GuildConstructionManager>();
        if (notificationManager == null) notificationManager = FindObjectOfType<NotificationManager>();
        if (graveyardManager == null) graveyardManager = FindObjectOfType<GraveyardManager>();
        if (gameConfig == null) gameConfig = Resources.Load<GameConfig>("GameConfig");

        // Create basics if missing so the scene can run
        if (goldManager == null) goldManager = gameObject.AddComponent<GoldManager>();
        if (reputationManager == null) reputationManager = gameObject.AddComponent<ReputationManager>();
        if (orderManager == null) orderManager = gameObject.AddComponent<OrderManager>();
        if (orderGenerator == null) orderGenerator = gameObject.AddComponent<OrderGenerator>();
        if (missionResolver == null) missionResolver = gameObject.AddComponent<MissionResolver>();
        if (notificationManager == null) notificationManager = gameObject.AddComponent<NotificationManager>();
    }

    private void InitializeManagers()
    {
        goldManager.Initialize(startingGold);
        reputationManager.Initialize(startingReputation);
        orderManager.Initialize(orderGenerator, missionResolver, timeManager);
    }

    private void HookManagerEvents()
    {
        if (reputationManager != null && hunterManager != null)
        {
            reputationManager.OnReputationChanged += hunterManager.OnReputationChanged;
        }
    }

    private void HookTimeEvents()
    {
        if (timeManager != null)
        {
            timeManager.OnDayStarted += HandleDayStarted;
        }
    }

    private void HandleDayStarted(int dayIndex)
    {
        if (hunterManager == null || goldManager == null) return;
        int previousDayGrossIncome = goldManager.BeginNewDayAndGetPreviousGrossIncome();
        hunterManager.OnDayStarted(dayIndex);

        int upkeepCost = hunterManager.CalculateDailyUpkeep();
        bool paid = upkeepCost <= 0 || goldManager.SpendGold(upkeepCost);
        if (paid)
        {
            unpaidUpkeepStreak = 0;
            activeDebtSuccessPenaltyPercent = 0f;
            return;
        }

        unpaidUpkeepStreak++;
        int unpaidAmount = Mathf.Max(0, upkeepCost - goldManager.GetGold());
        goldManager.AddDebt(unpaidAmount);
        if (goldManager.GetGold() > 0)
        {
            goldManager.SpendGold(goldManager.GetGold());
        }

        ApplyUnpaidUpkeepEffects(unpaidUpkeepStreak, unpaidAmount, previousDayGrossIncome);
    }

    private void ApplyUnpaidUpkeepEffects(int streak, int unpaidAmount, int previousDayGrossIncome)
    {
        var debtSettings = gameConfig != null && gameConfig.debtSettings != null
            ? gameConfig.debtSettings
            : new GameConfig.DebtSettings();

        if (streak >= 3)
        {
            TriggerGameOver("The guild failed to pay upkeep for three consecutive days.");
            return;
        }

        bool secondUnpaidDay = streak >= 2;
        activeDebtSuccessPenaltyPercent = secondUnpaidDay
            ? Mathf.Max(0f, debtSettings.unpaidDay2SuccessPenaltyPercent)
            : Mathf.Max(0f, debtSettings.unpaidDay1SuccessPenaltyPercent);

        int reputationRankLoss = secondUnpaidDay
            ? Mathf.Max(0, debtSettings.unpaidDay2ReputationRankLoss)
            : Mathf.Max(0, debtSettings.unpaidDay1ReputationRankLoss);
        int reputationRankAfterLoss = reputationManager != null
            ? reputationManager.LoseReputationRanks(reputationRankLoss)
            : 0;

        if (secondUnpaidDay && debtSettings.dismissHuntersUntilUpkeepFitsPreviousIncome)
        {
            hunterManager.DismissHuntersUntilUpkeepAtOrBelow(previousDayGrossIncome);
        }

        notificationManager?.Publish(
            secondUnpaidDay ? "Upkeep Crisis" : "Unpaid Upkeep",
            $"Unpaid upkeep became {unpaidAmount} debt. Reputation ranks lost: {reputationRankLoss}. Current rank: {reputationRankAfterLoss}. Mission success penalty: -{activeDebtSuccessPenaltyPercent:0.#}%.",
            secondUnpaidDay ? NotificationSeverity.Warning : NotificationSeverity.Warning);
    }

    private void TriggerGameOver(string reason)
    {
        if (gameOver) return;
        gameOver = true;
        activeDebtSuccessPenaltyPercent = 0f;
        notificationManager?.Publish("Game Over", reason, NotificationSeverity.Warning);
        if (gameOverScreen != null)
        {
            gameOverScreen.SetActive(true);
        }
        Time.timeScale = 0f;
    }

    public float GetDebtSuccessPenaltyPercent() => activeDebtSuccessPenaltyPercent;
    public int GetUnpaidUpkeepStreak() => unpaidUpkeepStreak;
    public bool IsGameOver() => gameOver;
    public int GetTodayUpkeepCost() => hunterManager != null ? hunterManager.CalculateDailyUpkeep() : 0;
    public int GetCurrentDebt() => goldManager != null ? goldManager.GetDebt() : 0;
    public bool IsHiringBlockedByDebt() => unpaidUpkeepStreak >= 2;

    public UpkeepCrisisState GetUpkeepCrisisState()
    {
        if (gameOver) return UpkeepCrisisState.GameOver;
        if (unpaidUpkeepStreak >= 2) return UpkeepCrisisState.UnpaidDay2;
        if (unpaidUpkeepStreak == 1) return UpkeepCrisisState.UnpaidDay1;
        return GetCurrentDebt() > 0 ? UpkeepCrisisState.Debt : UpkeepCrisisState.Stable;
    }

    public string GetUpkeepCrisisLabel()
    {
        switch (GetUpkeepCrisisState())
        {
            case UpkeepCrisisState.Debt:
                return "Debt";
            case UpkeepCrisisState.UnpaidDay1:
                return "Unpaid Day 1";
            case UpkeepCrisisState.UnpaidDay2:
                return "Upkeep Crisis";
            case UpkeepCrisisState.GameOver:
                return "Game Over";
            default:
                return "Stable";
        }
    }

    public string GetUpkeepCrisisDescription()
    {
        int debt = GetCurrentDebt();
        int upkeep = GetTodayUpkeepCost();
        switch (GetUpkeepCrisisState())
        {
            case UpkeepCrisisState.Debt:
                return $"Debt: {debt}. New income pays debt first.";
            case UpkeepCrisisState.UnpaidDay1:
                return $"Debt: {debt}. Upkeep unpaid once. Mission success -{activeDebtSuccessPenaltyPercent:0.#}%.";
            case UpkeepCrisisState.UnpaidDay2:
                return $"Debt: {debt}. Critical upkeep debt. Hiring campaigns blocked. Mission success -{activeDebtSuccessPenaltyPercent:0.#}%.";
            case UpkeepCrisisState.GameOver:
                return "The guild failed to pay upkeep for three consecutive days.";
            default:
                return $"Gold needed for next upkeep: {upkeep}.";
        }
    }

    public void HandleEndOfDaySleep()
    {
        // Accepted orders persist across days. In-progress missions are resolved
        // automatically when the workday enters evening.
        timeManager?.AdvanceToNextDay();
    }

    public OrderManager GetOrderManager() => orderManager;
    public GoldManager GetGoldManager() => goldManager;
    public ReputationManager GetReputationManager() => reputationManager;
    public HunterManager GetHunterManager() => hunterManager;
    public TimeManager GetTimeManager() => timeManager;
    public InvestigationManager GetInvestigationManager() => investigationManager;
    public GuildConstructionManager GetConstructionManager() => constructionManager;
    public NotificationManager GetNotificationManager() => notificationManager;
    public OrderGenerator GetOrderGenerator() => orderGenerator;
    public GameConfig GetGameConfig() => gameConfig;
    public GraveyardManager GetGraveyardManager()
    {
        if (graveyardManager == null)
        {
            graveyardManager = FindObjectOfType<GraveyardManager>();
        }
        return graveyardManager;
    }

    public int GetReputation()
    {
        return reputationManager != null ? reputationManager.GetReputation() : 0;
    }

    public float GetReputationPrecise()
    {
        return reputationManager != null ? reputationManager.GetReputationPrecise() : 0f;
    }

    public float GetReputationPointsPrecise()
    {
        return reputationManager != null ? reputationManager.GetReputationPointsPrecise() : 0f;
    }
}
