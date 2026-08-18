using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class NotificationEntry
{
    public string title;
    public string body;
    public NotificationSeverity severity;
    public string timestampUtc;
    public int dayIndex;
}

public enum NotificationSeverity
{
    Info = 0,
    Success = 1,
    Warning = 2
}

public class NotificationManager : MonoBehaviour
{
    [Serializable]
    private class NotificationSaveData
    {
        public List<NotificationEntry> entries = new List<NotificationEntry>();
    }

    [Header("Settings")]
    [SerializeField] private int maxHistoryEntries = 50;
    [SerializeField] private bool persistHistory = true;
    [SerializeField] private bool dedupeConsecutive = true;
    [SerializeField] private float dedupeWindowSeconds = 1.5f;

    [Header("Auto Hook")]
    [SerializeField] private bool autoHookSystems = true;
    [SerializeField] private TimeManager timeManager;
    [SerializeField] private OrderManager orderManager;
    [SerializeField] private GoldManager goldManager;
    [SerializeField] private HunterManager hunterManager;
    [SerializeField] private HunterRecruitmentManager recruitmentManager;
    [SerializeField] private GuildConstructionManager constructionManager;
    [SerializeField] private ClientSpawner clientSpawner;

    [Header("Message Templates")]
    [SerializeField] private NotificationMessageLibrary messageLibrary;

    public event Action<NotificationEntry> OnNotificationAdded;
    public event Action OnHistoryCleared;

    private readonly List<NotificationEntry> history = new List<NotificationEntry>();
    private string savePath;
    private bool subscribed;
    private int trackedDayIndex = -1;
    private readonly HashSet<TimeManager.DayState> notifiedStatesForDay = new HashSet<TimeManager.DayState>();

    private string lastPublishedTitle;
    private string lastPublishedBody;
    private NotificationSeverity lastPublishedSeverity;
    private float lastPublishedRealtime;
    private NotificationMessageLibrary fallbackMessageLibrary;

    private NotificationMessageLibrary Messages
    {
        get
        {
            if (messageLibrary != null)
            {
                return messageLibrary;
            }

            messageLibrary = Resources.Load<NotificationMessageLibrary>("NotificationMessageLibrary");
            if (messageLibrary != null)
            {
                return messageLibrary;
            }

            if (fallbackMessageLibrary == null)
            {
                fallbackMessageLibrary = ScriptableObject.CreateInstance<NotificationMessageLibrary>();
                fallbackMessageLibrary.hideFlags = HideFlags.HideAndDontSave;
            }

            return fallbackMessageLibrary;
        }
    }

    private void Awake()
    {
        savePath = Path.Combine(Application.persistentDataPath, "notifications_history.json");
        if (persistHistory)
        {
            LoadHistory();
        }
    }

    private void OnEnable()
    {
        if (!autoHookSystems) return;
        ResolveReferences();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        SaveHistory();
    }

    public IReadOnlyList<NotificationEntry> GetHistory()
    {
        return history;
    }

    public void ClearHistory()
    {
        history.Clear();
        lastPublishedTitle = null;
        lastPublishedBody = null;
        lastPublishedRealtime = 0f;
        SaveHistory();
        OnHistoryCleared?.Invoke();
    }

    public void Publish(string title, string body, NotificationSeverity severity = NotificationSeverity.Info)
    {
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        string safeTitle = string.IsNullOrWhiteSpace(title) ? "Notification" : title.Trim();
        string safeBody = string.IsNullOrWhiteSpace(body) ? string.Empty : body.Trim();

        if (dedupeConsecutive &&
            string.Equals(lastPublishedTitle, safeTitle, StringComparison.Ordinal) &&
            string.Equals(lastPublishedBody, safeBody, StringComparison.Ordinal) &&
            lastPublishedSeverity == severity &&
            (Time.realtimeSinceStartup - lastPublishedRealtime) <= Mathf.Max(0f, dedupeWindowSeconds))
        {
            return;
        }

        int dayIndex = timeManager != null ? timeManager.GetCurrentDayIndex() : 0;
        NotificationEntry entry = new NotificationEntry
        {
            title = safeTitle,
            body = safeBody,
            severity = severity,
            timestampUtc = DateTime.UtcNow.ToString("o"),
            dayIndex = dayIndex
        };

        history.Add(entry);
        TrimHistory();
        OnNotificationAdded?.Invoke(entry);
        SaveHistory();

        lastPublishedTitle = safeTitle;
        lastPublishedBody = safeBody;
        lastPublishedSeverity = severity;
        lastPublishedRealtime = Time.realtimeSinceStartup;
    }

    public void NotifyHunterTreated(Hunter hunter)
    {
        if (hunter == null) return;
        PublishTemplate(Messages.hunterTreatedMessage, Tokens("hunter", GetHunterName(hunter)));
    }

    public void NotifyUnpaidUpkeep(bool crisis, int unpaidAmount, int reputationRankLoss, int reputationRank, float successPenaltyPercent)
    {
        PublishTemplate(
            crisis ? Messages.upkeepCrisisMessage : Messages.unpaidUpkeepMessage,
            Tokens(
                "unpaid_amount", unpaidAmount.ToString(),
                "reputation_rank_loss", reputationRankLoss.ToString(),
                "reputation_rank", reputationRank.ToString(),
                "success_penalty", successPenaltyPercent.ToString("0.#")));
    }

    public void NotifyGameOver(string reason)
    {
        PublishTemplate(Messages.gameOverMessage, Tokens("reason", reason ?? string.Empty));
    }

    public void NotifyHiringUnavailable()
    {
        PublishTemplate(Messages.hiringUnavailableMessage);
    }

    public void NotifyHiringBlocked()
    {
        PublishTemplate(Messages.hiringBlockedMessage);
    }

    public void NotifyHunterLeft(string hunterName)
    {
        PublishTemplate(Messages.hunterLeftMessage, Tokens("hunter", string.IsNullOrWhiteSpace(hunterName) ? "Hunter" : hunterName));
    }

    private void ResolveReferences()
    {
        if (GameManager.Instance != null)
        {
            if (timeManager == null) timeManager = GameManager.Instance.GetTimeManager();
            if (orderManager == null) orderManager = GameManager.Instance.GetOrderManager();
            if (goldManager == null) goldManager = GameManager.Instance.GetGoldManager();
            if (hunterManager == null) hunterManager = GameManager.Instance.GetHunterManager();
            if (recruitmentManager == null) recruitmentManager = SceneLookup.Find<HunterRecruitmentManager>();
            if (constructionManager == null) constructionManager = GameManager.Instance.GetConstructionManager();
            if (clientSpawner == null) clientSpawner = SceneLookup.Find<ClientSpawner>();
        }
        else
        {
            if (timeManager == null) timeManager = SceneLookup.Find<TimeManager>();
            if (orderManager == null) orderManager = SceneLookup.Find<OrderManager>();
            if (goldManager == null) goldManager = SceneLookup.Find<GoldManager>();
            if (hunterManager == null) hunterManager = SceneLookup.Find<HunterManager>();
            if (recruitmentManager == null) recruitmentManager = SceneLookup.Find<HunterRecruitmentManager>();
            if (constructionManager == null) constructionManager = SceneLookup.Find<GuildConstructionManager>();
            if (clientSpawner == null) clientSpawner = SceneLookup.Find<ClientSpawner>();
        }
    }

    private void Subscribe()
    {
        if (subscribed) return;

        if (timeManager != null)
        {
            timeManager.OnDayStateChanged += HandleDayStateChanged;
            trackedDayIndex = timeManager.GetCurrentDayIndex();
            notifiedStatesForDay.Clear();
        }

        if (orderManager != null)
        {
            orderManager.OnMissionResolved += HandleMissionResolved;
            orderManager.OnOrderAccepted += HandleOrderAccepted;
            orderManager.OnOrderReferred += HandleOrderReferred;
            orderManager.OnMissionStarted += HandleMissionStarted;
        }

        if (goldManager != null)
        {
            goldManager.OnSpendFailed += HandleGoldSpendFailed;
        }

        if (hunterManager != null)
        {
            hunterManager.OnHunterLeveledUp += HandleHunterLeveledUp;
        }

        if (recruitmentManager != null)
        {
            recruitmentManager.OnCandidateArrived += HandleCandidateArrived;
            recruitmentManager.OnCampaignEnded += HandleCampaignEnded;
        }

        if (constructionManager != null)
        {
            constructionManager.OnConstructionBuilt += HandleConstructionBuilt;
        }

        if (clientSpawner != null)
        {
            clientSpawner.OnClientArrived += HandleClientArrived;
        }

        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;

        if (timeManager != null)
        {
            timeManager.OnDayStateChanged -= HandleDayStateChanged;
        }

        if (orderManager != null)
        {
            orderManager.OnMissionResolved -= HandleMissionResolved;
            orderManager.OnOrderAccepted -= HandleOrderAccepted;
            orderManager.OnOrderReferred -= HandleOrderReferred;
            orderManager.OnMissionStarted -= HandleMissionStarted;
        }

        if (goldManager != null)
        {
            goldManager.OnSpendFailed -= HandleGoldSpendFailed;
        }

        if (hunterManager != null)
        {
            hunterManager.OnHunterLeveledUp -= HandleHunterLeveledUp;
        }

        if (recruitmentManager != null)
        {
            recruitmentManager.OnCandidateArrived -= HandleCandidateArrived;
            recruitmentManager.OnCampaignEnded -= HandleCampaignEnded;
        }

        if (constructionManager != null)
        {
            constructionManager.OnConstructionBuilt -= HandleConstructionBuilt;
        }

        if (clientSpawner != null)
        {
            clientSpawner.OnClientArrived -= HandleClientArrived;
        }

        subscribed = false;
    }

    private void HandleDayStateChanged(TimeManager.DayState state)
    {
        int dayIndex = timeManager != null ? timeManager.GetCurrentDayIndex() : trackedDayIndex;
        if (dayIndex != trackedDayIndex)
        {
            trackedDayIndex = dayIndex;
            notifiedStatesForDay.Clear();
        }

        if (notifiedStatesForDay.Contains(state))
        {
            return;
        }
        notifiedStatesForDay.Add(state);

        switch (state)
        {
            case TimeManager.DayState.PreBell:
                PublishTemplate(Messages.dayPlanningMessage, Tokens("day", (dayIndex + 1).ToString()));
                break;
            case TimeManager.DayState.Active:
                PublishTemplate(Messages.workdayStartedMessage);
                break;
            case TimeManager.DayState.Evening:
                PublishTemplate(Messages.eveningMessage);
                break;
        }
    }

    private void HandleMissionResolved(MissionReport report)
    {
        if (report == null) return;

        string title = report.success ? "Mission Success" : "Mission Failed";
        string orderTitle = report.order != null && !string.IsNullOrWhiteSpace(report.order.orderTitle)
            ? report.order.orderTitle
            : "Unnamed mission";

        int dead = report.GetDeathsCount();
        int wounded = report.GetInjuriesCount();
        int xp = report.GetTotalXP();
        string casualtySummary = dead > 0 || wounded > 0
            ? $"Casualties: {dead} dead, {wounded} wounded."
            : "No casualties.";

        var missionTokens = Tokens(
            "order", orderTitle,
            "gold", report.goldEarned.ToString(),
            "xp", xp.ToString(),
            "dead", dead.ToString(),
            "wounded", wounded.ToString(),
            "casualties", casualtySummary);
        PublishTemplate(report.success ? Messages.missionSuccessMessage : Messages.missionFailedMessage, missionTokens);

        foreach (var result in report.hunterResults)
        {
            if (result == null || result.hunter == null) continue;
            string hunterName = GetHunterName(result.hunter);
            if (result.died)
            {
                PublishTemplate(Messages.hunterDiedMessage, Tokens("hunter", hunterName, "order", orderTitle));
                continue;
            }

            if (result.injured)
            {
                PublishTemplate(Messages.hunterWoundedMessage, Tokens("hunter", hunterName, "order", orderTitle));
            }

            if (result.leveledUp)
            {
                PublishTemplate(Messages.hunterLeveledUpMessage, Tokens("hunter", hunterName, "level", result.hunter.GetLevel().ToString()));
            }
        }
    }

    private void HandleOrderAccepted(Order order)
    {
        if (order == null) return;
        PublishTemplate(Messages.orderAcceptedMessage, Tokens("order", GetOrderTitle(order)));
    }

    private void HandleOrderReferred(Order order)
    {
        if (order == null) return;
        PublishTemplate(Messages.orderReferredMessage, Tokens("order", GetOrderTitle(order)));
    }

    private void HandleMissionStarted(Order order, List<Hunter> party)
    {
        if (order == null) return;
        int partyCount = party != null ? party.Count : 0;
        PublishTemplate(Messages.partySentMessage, Tokens(
            "hunter_count", partyCount.ToString(),
            "hunter_plural", partyCount == 1 ? string.Empty : "s",
            "order", GetOrderTitle(order)));
    }

    private void HandleGoldSpendFailed(int requestedAmount, int currentGold)
    {
        PublishTemplate(Messages.notEnoughGoldMessage, Tokens(
            "requested_gold", requestedAmount.ToString(),
            "current_gold", currentGold.ToString()));
    }

    private void HandleHunterLeveledUp(Hunter hunter)
    {
        if (hunter == null) return;
        PublishTemplate(Messages.hunterLeveledUpMessage, Tokens(
            "hunter", GetHunterName(hunter),
            "level", hunter.GetLevel().ToString()));
    }

    private void HandleClientArrived(InvestigationCase investigationCase)
    {
        string category = investigationCase?.clientProfile != null
            ? investigationCase.clientProfile.categoryName
            : null;
        string label = string.IsNullOrWhiteSpace(category) ? "A new client" : $"A {category} client";
        PublishTemplate(Messages.newClientArrivedMessage, Tokens("client_label", label, "client_category", category ?? string.Empty));
    }

    private void HandleCandidateArrived(HunterRecruitmentManager.RecruitmentCandidate candidate)
    {
        if (candidate == null || candidate.hunter == null) return;
        string name = string.IsNullOrWhiteSpace(candidate.hunter.hunterName) ? "A candidate" : candidate.hunter.hunterName;
        PublishTemplate(Messages.candidateArrivedMessage, Tokens("candidate", name));
    }

    private void HandleCampaignEnded(string reason)
    {
        string reasonSuffix = string.IsNullOrWhiteSpace(reason) ? string.Empty : $": {reason}";
        PublishTemplate(Messages.hiringCampaignEndedMessage, Tokens("reason", reason ?? string.Empty, "reason_suffix", reasonSuffix));
    }

    private void HandleConstructionBuilt(GuildConstructionDefinition definition)
    {
        if (definition == null) return;
        string name = string.IsNullOrWhiteSpace(definition.displayName) ? "Construction" : definition.displayName;
        PublishTemplate(Messages.constructionCompletedMessage, Tokens("construction", name));
    }

    private string GetOrderTitle(Order order)
    {
        return order != null && !string.IsNullOrWhiteSpace(order.orderTitle)
            ? order.orderTitle
            : "Unnamed order";
    }

    private string GetHunterName(Hunter hunter)
    {
        if (hunter == null) return "Hunter";
        if (hunter.Data != null && !string.IsNullOrWhiteSpace(hunter.Data.hunterName))
        {
            return hunter.Data.hunterName;
        }
        return string.IsNullOrWhiteSpace(hunter.name) ? "Hunter" : hunter.name;
    }

    private void PublishTemplate(NotificationMessageTemplate template, Dictionary<string, string> tokens = null)
    {
        if (template == null)
        {
            return;
        }

        string title = ApplyTokens(template.title, tokens);
        string body = ApplyTokens(template.body, tokens);
        Publish(title, body, template.severity);
    }

    private string ApplyTokens(string value, Dictionary<string, string> tokens)
    {
        if (string.IsNullOrEmpty(value) || tokens == null || tokens.Count == 0)
        {
            return value;
        }

        string result = value;
        foreach (var token in tokens)
        {
            result = result.Replace("{" + token.Key + "}", token.Value ?? string.Empty);
        }
        return result;
    }

    private Dictionary<string, string> Tokens(params string[] pairs)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (pairs == null) return result;

        for (int i = 0; i + 1 < pairs.Length; i += 2)
        {
            result[pairs[i]] = pairs[i + 1];
        }
        return result;
    }

    private void TrimHistory()
    {
        int max = Mathf.Max(1, maxHistoryEntries);
        while (history.Count > max)
        {
            history.RemoveAt(0);
        }
    }

    private void SaveHistory()
    {
        if (!persistHistory) return;
        if (string.IsNullOrEmpty(savePath)) return;

        try
        {
            NotificationSaveData data = new NotificationSaveData
            {
                entries = new List<NotificationEntry>(history)
            };
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(savePath, json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"NotificationManager: Failed to save notification history. {ex.Message}");
        }
    }

    private void LoadHistory()
    {
        history.Clear();
        if (string.IsNullOrEmpty(savePath) || !File.Exists(savePath))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(savePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            NotificationSaveData data = JsonUtility.FromJson<NotificationSaveData>(json);
            if (data?.entries == null)
            {
                return;
            }

            history.AddRange(data.entries);
            TrimHistory();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"NotificationManager: Failed to load notification history. {ex.Message}");
        }
    }
}
