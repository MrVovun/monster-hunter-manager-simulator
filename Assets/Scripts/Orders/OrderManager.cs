using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class OrderManager : MonoBehaviour
{
    [Header("Runtime Orders")]
    [SerializeField] private List<Order> offeredOrders = new List<Order>();
    [SerializeField] private List<Order> activeOrders = new List<Order>();
    [SerializeField] private List<MissionReport> missionHistory = new List<MissionReport>();

    [Header("Referral Settings")]
    [SerializeField] private int referralPayout = 25;

    private OrderGenerator orderGenerator;
    private MissionResolver missionResolver;
    private TimeManager timeManager;

    public System.Action<MissionReport> OnMissionResolved;

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
        return newOrder;
    }

    public void AcceptOrder(Order order)
    {
        if (order == null) return;

        offeredOrders.Remove(order);
        if (!activeOrders.Contains(order))
        {
            activeOrders.Add(order);
        }

        order.state = OrderState.Accepted;
        StartPrepTimer(order);
    }

    public void DeclineOrder(Order order)
    {
        if (order == null) return;
        offeredOrders.Remove(order);
        CleanupTimers(order);
        order.state = OrderState.Failed;
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
    }

    public bool StartMission(Order order, List<Hunter> party)
    {
        if (order == null || party == null || party.Count == 0) return false;
        if (order.state != OrderState.Accepted) return false;
        if (party.Count < order.minPartySize || party.Count > order.maxPartySize) return false;

        CleanupPrepTimer(order);
        order.assignedHunters.Clear();
        order.assignedHunters.AddRange(party);

        foreach (var hunter in party)
        {
            if (hunter != null)
            {
                hunter.SetState(HunterState.OnMission);
            }
        }

        order.state = OrderState.InProgress;
        StartMissionTimer(order);
        return true;
    }

    private void StartPrepTimer(Order order)
    {
        if (timeManager == null || order == null || order.prepTimeLimit <= 0f) return;

        order.prepTimer = new MissionTimer(order.prepTimeLimit);
        order.prepTimer.OnExpired = () => ExpireOrder(order);
        timeManager.RegisterTimer(order.prepTimer);
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
    }

    private void CleanupPrepTimer(Order order)
    {
        if (order?.prepTimer != null && timeManager != null)
        {
            timeManager.UnregisterTimer(order.prepTimer);
            order.prepTimer = null;
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
        CleanupPrepTimer(order);
        CleanupMissionTimer(order);
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
}
