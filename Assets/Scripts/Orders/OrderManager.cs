using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using UnityEngine;

public class OrderManager : MonoBehaviour
{
    [Serializable]
    private class OrderManagerSaveData
    {
        public List<OrderSaveData> activeOrders = new List<OrderSaveData>();
        public List<MonsterKillCountSaveData> monsterCompletionCounts = new List<MonsterKillCountSaveData>();
    }

    [Serializable]
    private class OrderSaveData
    {
        public string orderId;
        public string orderTitle;
        public string description;
        public string monsterNamePlaceholder;
        public string monsterId;
        public string declaredMonsterId;
        public int difficulty;
        public int goldReward;
        public int xpReward;
        public float reputationPointsReward;
        public float missionDuration;
        public int maxPartySize;
        public int minPartySize;
        public OrderState state;
    }

    [Serializable]
    private class MonsterKillCountSaveData
    {
        public string monsterId;
        public int count;
    }

    [Header("Runtime Orders")]
    [SerializeField] private List<Order> offeredOrders = new List<Order>();
    [SerializeField] private List<Order> activeOrders = new List<Order>();
    [SerializeField] private List<MissionReport> missionHistory = new List<MissionReport>();
    private readonly Dictionary<string, int> monsterCompletionCounts = new Dictionary<string, int>();

    [Header("Referral Settings")]
    [SerializeField] private int referralPayout = 25;

    private OrderGenerator orderGenerator;
    private MissionResolver missionResolver;
    private TimeManager timeManager;
    private GameConfig gameConfig;
    private string savePath;

    public System.Action<MissionReport> OnMissionResolved;
    public event System.Action<Order> OnOrderAccepted;
    public event System.Action<Order> OnOrderReferred;
    public event System.Action<Order, List<Hunter>> OnMissionStarted;
    public event System.Action OnOrdersChanged;

    public void Initialize(OrderGenerator generator, MissionResolver resolver, TimeManager timeMgr)
    {
        orderGenerator = generator != null ? generator : FindObjectOfType<OrderGenerator>();
        missionResolver = resolver != null ? resolver : FindObjectOfType<MissionResolver>();
        gameConfig = GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null;
        savePath = GameSaveUtility.GetSavePath("orders_state.json");

        if (timeManager != null)
        {
            timeManager.OnDayStateChanged -= HandleDayStateChanged;
        }

        timeManager = timeMgr != null ? timeMgr : FindObjectOfType<TimeManager>();
        if (timeManager != null)
        {
            timeManager.OnDayStateChanged += HandleDayStateChanged;
        }

        LoadState();
    }

    private void OnDestroy()
    {
        if (timeManager != null)
        {
            timeManager.OnDayStateChanged -= HandleDayStateChanged;
        }
    }

    public Order GenerateAndOfferOrder()
    {
        Order newOrder = orderGenerator != null ? orderGenerator.GenerateRandomOrder() : new Order();
        newOrder.state = OrderState.Offered;
        offeredOrders.Add(newOrder);
        NotifyOrdersChanged();
        SaveState();
        return newOrder;
    }

    public bool AcceptOrder(Order order)
    {
        if (order == null) return false;

        offeredOrders.Remove(order);
        if (!activeOrders.Contains(order))
        {
            activeOrders.Add(order);
        }

        order.state = OrderState.Accepted;
        NotifyOrdersChanged();
        OnOrderAccepted?.Invoke(order);
        SaveState();
        return true;
    }

    public void DeclineOrder(Order order)
    {
        if (order == null) return;
        offeredOrders.Remove(order);
        CleanupTimers(order);
        order.state = OrderState.Failed;
        NotifyOrdersChanged();
        SaveState();
    }

    public void ReferOrder(Order order)
    {
        if (order == null) return;

        offeredOrders.Remove(order);
        CleanupTimers(order);
        order.state = OrderState.Failed;

        // Pay referral fee
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GetGoldManager()?.AddGold(referralPayout);
        }
        
        NotifyOrdersChanged();
        OnOrderReferred?.Invoke(order);
        SaveState();
    }

    public bool StartMission(Order order, List<Hunter> party)
    {
        if (order == null || party == null || party.Count == 0) return false;
        if (order.state != OrderState.Accepted) return false;
        if (party.Count < order.minPartySize || party.Count > order.maxPartySize) return false;
        var tm = GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null;
        if (tm != null && (tm.GetDayState() == TimeManager.DayState.Evening || tm.GetDayState() == TimeManager.DayState.PreBell)) return false;

        order.assignedHunters.Clear();
        order.assignedHunters.AddRange(party);

        foreach (var hunter in party)
        {
            if (hunter != null)
            {
                hunter.SetState(HunterState.OnMission);
            }
        }

        MainHallFloorDirtManager.Instance?.AddHunterDepartureDirt(party.Count);

        // Apply mission time modifiers from monster traits
        var outcome = MissionOutcomeCalculator.Evaluate(order, party);
        if (outcome != null && outcome.MissionTimeMultiplier > 0f)
        {
            order.missionDuration = Mathf.Max(1f, order.missionDuration * outcome.MissionTimeMultiplier);
        }

        order.state = OrderState.InProgress;
        StartMissionTimer(order);
        NotifyOrdersChanged();
        NotifyHunterRosterChanged();
        OnMissionStarted?.Invoke(order, new List<Hunter>(party));
        TutorialManager.ReportEvent(TutorialIds.EventMissionStarted);
        SaveState();

        var config = GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null;
        float cost = config != null ? config.actionTimeSettings.sendPartySeconds : 0f;
        tm?.AdvanceTime(cost);
        return true;
    }

    public bool CanCancelOrder(Order order)
    {
        return order != null && order.state == OrderState.Accepted;
    }

    public bool CancelOrder(Order order)
    {
        if (!CanCancelOrder(order)) return false;

        CleanupTimers(order);
        activeOrders.Remove(order);
        order.assignedHunters.Clear();
        order.state = OrderState.Canceled;
        NotifyOrdersChanged();
        NotifyHunterRosterChanged();
        SaveState();
        return true;
    }

    private void StartMissionTimer(Order order)
    {
        if (timeManager == null || order == null || order.missionDuration <= 0f) return;

        order.missionTimer = new MissionTimer(order.missionDuration);
        order.missionTimer.OnExpired = () => ResolveOrder(order);
        timeManager.RegisterTimer(order.missionTimer);
    }

    private void ExpireOrder(Order order)
    {
        if (order == null) return;
        order.state = OrderState.Expired;
        CleanupTimers(order);
        activeOrders.Remove(order);
        order.assignedHunters.Clear();
        NotifyOrdersChanged();
        SaveState();
    }

    public void ResolveOrder(Order order)
    {
        if (order == null) return;

        CleanupMissionTimer(order);
        MissionReport report = null;
        if (missionResolver != null && order.assignedHunters != null && order.assignedHunters.Count > 0)
        {
            report = missionResolver.ResolveMission(order, order.assignedHunters);
        }
        else
        {
            report = new MissionReport { order = order, success = true, goldEarned = order.goldReward };
        }

        bool success = report.success;
        order.state = success ? OrderState.Completed : OrderState.Failed;

        if (success)
        {
            IncrementMonsterCompletion(order.monsterData);
        }

        // Return surviving hunters to idle
        foreach (var hunter in order.assignedHunters)
        {
            if (hunter != null && hunter.GetState() != HunterState.Dead)
            {
                hunter.SetState(HunterState.Idle);
            }
        }

        activeOrders.Remove(order);
        order.assignedHunters.Clear();

        if (report != null)
        {
            missionHistory.Add(report);
            OnMissionResolved?.Invoke(report);
        }
        
        NotifyOrdersChanged();
        NotifyHunterRosterChanged();
        SaveState();
    }

    private void HandleDayStateChanged(TimeManager.DayState state)
    {
        if (state != TimeManager.DayState.Evening) return;

        var inProgressOrders = activeOrders
            .Where(order => order != null && order.state == OrderState.InProgress)
            .ToList();

        foreach (var order in inProgressOrders)
        {
            ResolveOrder(order);
        }
    }

    private void CleanupMissionTimer(Order order)
    {
        if (order?.missionTimer != null && timeManager != null)
        {
            timeManager.UnregisterTimer(order.missionTimer);
            order.missionTimer = null;
        }
    }

    private void CleanupTimers(Order order)
    {
        CleanupMissionTimer(order);
    }

    private void NotifyOrdersChanged()
    {
        OnOrdersChanged?.Invoke();
    }

    private void NotifyHunterRosterChanged()
    {
        var hunterManager = GameManager.Instance != null ? GameManager.Instance.GetHunterManager() : null;
        hunterManager?.NotifyRosterChanged();
    }

    public bool CanAcceptMoreOrders()
    {
        // No order limit enforced anymore.
        return true;
    }
    
    public List<Order> GetActiveOrders()
    {
        return activeOrders.Where(o => o != null && o.IsActive()).ToList();
    }

    public bool HasInProgressOrders()
    {
        return activeOrders.Any(order => order != null && order.state == OrderState.InProgress);
    }

    public void ClearNonInProgressOrders()
    {
        offeredOrders.Clear();
        for (int i = activeOrders.Count - 1; i >= 0; i--)
        {
            var o = activeOrders[i];
            if (o == null || o.state != OrderState.InProgress)
            {
                activeOrders.RemoveAt(i);
            }
        }
        NotifyOrdersChanged();
    }

    public List<Order> GetOfferedOrders()
    {
        return new List<Order>(offeredOrders);
    }

    public List<MissionReport> GetMissionHistory()
    {
        return new List<MissionReport>(missionHistory);
    }

    private void IncrementMonsterCompletion(MonsterData monster)
    {
        if (monster == null) return;
        if (!monsterCompletionCounts.ContainsKey(monster.monsterId))
        {
            monsterCompletionCounts[monster.monsterId] = 0;
        }
        monsterCompletionCounts[monster.monsterId]++;
    }

    public int GetMonsterCompletionCount(MonsterData monster)
    {
        if (monster == null) return 0;
        return monsterCompletionCounts.TryGetValue(monster.monsterId, out int value) ? value : 0;
    }

    private void LoadState()
    {
        activeOrders.Clear();
        offeredOrders.Clear();
        monsterCompletionCounts.Clear();

        if (string.IsNullOrEmpty(savePath) || !File.Exists(savePath)) return;

        try
        {
            string json = File.ReadAllText(savePath);
            if (string.IsNullOrWhiteSpace(json)) return;

            OrderManagerSaveData data = JsonUtility.FromJson<OrderManagerSaveData>(json);
            if (data == null) return;

            if (data.monsterCompletionCounts != null)
            {
                foreach (var entry in data.monsterCompletionCounts)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.monsterId)) continue;
                    monsterCompletionCounts[entry.monsterId] = Mathf.Max(0, entry.count);
                }
            }

            if (data.activeOrders == null) return;
            foreach (var saved in data.activeOrders)
            {
                Order order = RestoreOrder(saved);
                if (order == null) continue;
                activeOrders.Add(order);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"OrderManager: Failed to load order state. {ex.Message}");
        }
    }

    private void SaveState()
    {
        if (string.IsNullOrEmpty(savePath)) return;

        try
        {
            OrderManagerSaveData data = new OrderManagerSaveData();
            foreach (var order in activeOrders)
            {
                if (order == null) continue;
                if (order.state != OrderState.Accepted && order.state != OrderState.InProgress) continue;
                data.activeOrders.Add(CreateOrderSaveData(order));
            }

            foreach (var pair in monsterCompletionCounts)
            {
                data.monsterCompletionCounts.Add(new MonsterKillCountSaveData
                {
                    monsterId = pair.Key,
                    count = pair.Value
                });
            }

            File.WriteAllText(savePath, JsonUtility.ToJson(data, true));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"OrderManager: Failed to save order state. {ex.Message}");
        }
    }

    private OrderSaveData CreateOrderSaveData(Order order)
    {
        return new OrderSaveData
        {
            orderId = order.orderId.ToString("N"),
            orderTitle = order.orderTitle,
            description = order.description,
            monsterNamePlaceholder = order.monsterNamePlaceholder,
            monsterId = order.monsterData != null ? order.monsterData.monsterId : string.Empty,
            declaredMonsterId = order.declaredMonster != null ? order.declaredMonster.monsterId : string.Empty,
            difficulty = order.difficulty,
            goldReward = order.goldReward,
            xpReward = order.xpReward,
            reputationPointsReward = order.reputationPointsReward,
            missionDuration = order.missionDuration,
            maxPartySize = order.maxPartySize,
            minPartySize = order.minPartySize,
            state = order.state == OrderState.InProgress ? OrderState.Accepted : order.state
        };
    }

    private Order RestoreOrder(OrderSaveData saved)
    {
        if (saved == null) return null;

        MonsterData monster = FindMonster(saved.monsterId);
        if (monster == null) return null;

        Order order = new Order
        {
            orderTitle = saved.orderTitle,
            description = saved.description,
            monsterNamePlaceholder = string.IsNullOrWhiteSpace(saved.monsterNamePlaceholder) ? Order.DefaultMonsterPlaceholder : saved.monsterNamePlaceholder,
            monsterData = monster,
            declaredMonster = FindMonster(saved.declaredMonsterId),
            difficulty = Mathf.Max(1, saved.difficulty),
            goldReward = Mathf.Max(0, saved.goldReward),
            xpReward = Mathf.Max(0, saved.xpReward),
            reputationPointsReward = Mathf.Max(0f, saved.reputationPointsReward),
            missionDuration = Mathf.Max(1f, saved.missionDuration),
            maxPartySize = Mathf.Max(1, saved.maxPartySize),
            minPartySize = Mathf.Max(1, saved.minPartySize),
            state = saved.state == OrderState.InProgress ? OrderState.Accepted : saved.state
        };

        if (Guid.TryParse(saved.orderId, out Guid id))
        {
            order.orderId = id;
        }

        return order;
    }

    private MonsterData FindMonster(string monsterId)
    {
        if (string.IsNullOrWhiteSpace(monsterId)) return null;
        var library = gameConfig != null ? gameConfig.monsterLibrary : null;
        if (library == null) return null;

        foreach (var monster in library.GetMonsters())
        {
            if (monster == null) continue;
            if (string.Equals(monster.monsterId, monsterId, StringComparison.OrdinalIgnoreCase))
            {
                return monster;
            }
        }

        return null;
    }
}
