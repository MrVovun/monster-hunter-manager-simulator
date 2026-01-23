using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class OrderManager : MonoBehaviour
{
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

    public System.Action<MissionReport> OnMissionResolved;
    public event System.Action OnOrdersChanged;

    public void Initialize(OrderGenerator generator, MissionResolver resolver, TimeManager timeMgr)
    {
        orderGenerator = generator != null ? generator : FindObjectOfType<OrderGenerator>();
        missionResolver = resolver != null ? resolver : FindObjectOfType<MissionResolver>();
        timeManager = timeMgr != null ? timeMgr : FindObjectOfType<TimeManager>();
    }

    public Order GenerateAndOfferOrder()
    {
        Order newOrder = orderGenerator != null ? orderGenerator.GenerateRandomOrder() : new Order();
        newOrder.state = OrderState.Offered;
        offeredOrders.Add(newOrder);
        NotifyOrdersChanged();
        return newOrder;
    }

    public bool AcceptOrder(Order order)
    {
        if (order == null) return false;

        if (IsOrderLimitReached())
        {
            Debug.LogWarning("OrderManager: Cannot accept order because the active order limit has been reached.");
            return false;
        }

        offeredOrders.Remove(order);
        if (!activeOrders.Contains(order))
        {
            activeOrders.Add(order);
        }

        order.state = OrderState.Accepted;
        NotifyOrdersChanged();
        return true;
    }

    public void DeclineOrder(Order order)
    {
        if (order == null) return;
        offeredOrders.Remove(order);
        CleanupTimers(order);
        order.state = OrderState.Failed;
        NotifyOrdersChanged();
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
    }

    public bool StartMission(Order order, List<Hunter> party)
    {
        if (order == null || party == null || party.Count == 0) return false;
        if (order.state != OrderState.Accepted) return false;
        if (party.Count < order.minPartySize || party.Count > order.maxPartySize) return false;

        order.assignedHunters.Clear();
        order.assignedHunters.AddRange(party);

        foreach (var hunter in party)
        {
            if (hunter != null)
            {
                hunter.SetState(HunterState.OnMission);
            }
        }

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

    private bool IsOrderLimitReached()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null) return false;

        GameConfig config = manager.GetGameConfig();
        if (config == null) return false;

        int reputation = manager.GetReputation();
        int limit = config.GetOrderLimit(reputation);
        if (limit <= 0) return false;

        int activeCount = 0;
        foreach (var order in activeOrders)
        {
            if (order != null && order.IsActive())
            {
                activeCount++;
            }
        }

        return activeCount >= limit;
    }

    public bool CanAcceptMoreOrders()
    {
        return !IsOrderLimitReached();
    }
    
    public List<Order> GetActiveOrders()
    {
        return activeOrders.Where(o => o != null && o.IsActive()).ToList();
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
}
