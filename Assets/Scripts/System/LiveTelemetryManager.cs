using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LiveTelemetryManager : MonoBehaviour
{
    public static LiveTelemetryManager Instance { get; private set; }

    private const string EventsHeader =
        "session_id,event_index,utc_timestamp,scene,day,day_state,seconds_into_day,seconds_remaining_day,realtime_seconds,gold,debt,reputation_rank,reputation_points,trust_streak,event_name,order_id,order_title,order_state,true_monster_id,true_monster_name,declared_monster_id,declared_monster_name,hunter_ids,hunter_names,party_size,success_percent,gold_delta,reputation_delta,xp_delta,wounds,deaths,result,details";

    private const string SummaryHeader =
        "session_id,utc_start,utc_end,duration_seconds,scene,final_day,final_day_state,final_gold,final_debt,final_reputation_rank,final_reputation_points,final_trust_streak,active_hunters,orders_generated,orders_accepted,orders_referred,orders_declined,orders_canceled,missions_started,missions_success,missions_failed,wounds,deaths,total_gold_from_missions,total_gold_from_referrals,total_reputation_from_missions,total_reputation_from_referrals,hunters_hired,hunters_fired,hunters_dismissed_debt,hunter_levelups,campaigns_started,candidates_arrived,candidates_reviewed,candidates_hired,candidates_declined,questions_answered,constructions_built,dirt_changes,time_advanced_seconds,day_reached_rep2,day_reached_rep3,day_reached_rep4,end_of_day_rep_ranks,end_of_day_reputation_points";

    private GameManager gameManager;
    private GameConfig config;
    private OrderManager orderManager;
    private HunterManager hunterManager;
    private HunterRecruitmentManager recruitmentManager;
    private InvestigationManager investigationManager;
    private GuildConstructionManager constructionManager;
    private GoldManager goldManager;
    private ReputationManager reputationManager;
    private TimeManager timeManager;
    private MainHallFloorDirtManager dirtManager;

    private string sessionId;
    private string sessionStartUtc;
    private float sessionStartRealtime;
    private string telemetryFolder;
    private string eventsPath;
    private string summaryPath;
    private int eventIndex;
    private bool initialized;
    private bool sessionClosed;
    private float nextOptionalBindTime;

    private int ordersGenerated;
    private int ordersAccepted;
    private int ordersReferred;
    private int ordersDeclined;
    private int ordersCanceled;
    private int missionsStarted;
    private int missionsSuccess;
    private int missionsFailed;
    private int wounds;
    private int deaths;
    private int totalGoldFromMissions;
    private int totalGoldFromReferrals;
    private float totalReputationFromMissions;
    private float totalReputationFromReferrals;
    private int huntersHired;
    private int huntersFired;
    private int huntersDismissedDebt;
    private int hunterLevelUps;
    private int campaignsStarted;
    private int candidatesArrived;
    private int candidatesReviewed;
    private int candidatesHired;
    private int candidatesDeclined;
    private int questionsAnswered;
    private int constructionsBuilt;
    private int dirtChanges;
    private float timeAdvancedSeconds;
    private int dayReachedRep2 = -1;
    private int dayReachedRep3 = -1;
    private int dayReachedRep4 = -1;
    private readonly List<int> endOfDayRepRanks = new List<int>();
    private readonly List<float> endOfDayReputationPoints = new List<float>();

    public string TelemetryFolder => telemetryFolder;
    public string EventsPath => eventsPath;
    public string SummaryPath => summaryPath;

    public void Initialize(GameManager manager)
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        gameManager = manager != null ? manager : GameManager.Instance;
        config = gameManager != null ? gameManager.GetGameConfig() : Resources.Load<GameConfig>("GameConfig");

        if (config == null || !config.enableLocalTelemetry)
        {
            return;
        }

        if (initialized)
        {
            return;
        }

        initialized = true;
        sessionClosed = false;
        sessionId = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        sessionStartUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        sessionStartRealtime = Time.realtimeSinceStartup;

        string folderName = string.IsNullOrWhiteSpace(config.localTelemetryFolderName)
            ? "Telemetry"
            : config.localTelemetryFolderName.Trim();
        telemetryFolder = Path.Combine(Application.persistentDataPath, folderName);
        Directory.CreateDirectory(telemetryFolder);
        eventsPath = Path.Combine(telemetryFolder, $"live_events_{sessionId}.csv");
        summaryPath = Path.Combine(telemetryFolder, "live_sessions.csv");

        if (config.writeLocalTelemetryEvents)
        {
            EnsureCsvHeader(eventsPath, EventsHeader);
        }

        if (config.writeLocalTelemetrySessionSummary)
        {
            EnsureCsvHeader(summaryPath, SummaryHeader);
        }

        SceneManager.sceneLoaded += HandleSceneLoaded;
        BindManagers();
        TrackEvent("session_start", Detail("version", Application.version));

        if (config.logLocalTelemetryPath)
        {
            Debug.Log($"LiveTelemetryManager: writing local telemetry to {telemetryFolder}");
        }
    }

    private void Update()
    {
        if (!initialized || Time.unscaledTime < nextOptionalBindTime)
        {
            return;
        }

        nextOptionalBindTime = Time.unscaledTime + 1f;
        BindOptionalManagers();
    }

    private void OnApplicationQuit()
    {
        CloseSession();
    }

    private void OnDestroy()
    {
        CloseSession();
        UnbindManagers();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindOptionalManagers();
        TrackEvent("scene_loaded", Detail("scene", scene.name));
    }

    private void BindManagers()
    {
        if (gameManager == null)
        {
            gameManager = GameManager.Instance;
        }

        BindOrderManager(gameManager != null ? gameManager.GetOrderManager() : SceneLookup.Find<OrderManager>());
        BindHunterManager(gameManager != null ? gameManager.GetHunterManager() : SceneLookup.Find<HunterManager>());
        BindGoldManager(gameManager != null ? gameManager.GetGoldManager() : SceneLookup.Find<GoldManager>());
        BindReputationManager(gameManager != null ? gameManager.GetReputationManager() : SceneLookup.Find<ReputationManager>());
        BindTimeManager(gameManager != null ? gameManager.GetTimeManager() : SceneLookup.Find<TimeManager>());
        BindInvestigationManager(gameManager != null ? gameManager.GetInvestigationManager() : SceneLookup.Find<InvestigationManager>());
        BindConstructionManager(gameManager != null ? gameManager.GetConstructionManager() : SceneLookup.Find<GuildConstructionManager>());
        BindOptionalManagers();

        if (gameManager != null)
        {
            gameManager.OnGameOver -= HandleGameOver;
            gameManager.OnGameOver += HandleGameOver;
        }
    }

    private void BindOptionalManagers()
    {
        BindInvestigationManager(gameManager != null && gameManager.GetInvestigationManager() != null
            ? gameManager.GetInvestigationManager()
            : SceneLookup.Find<InvestigationManager>());
        BindRecruitmentManager(SceneLookup.Find<HunterRecruitmentManager>());
        BindDirtManager(MainHallFloorDirtManager.Instance != null
            ? MainHallFloorDirtManager.Instance
            : SceneLookup.Find<MainHallFloorDirtManager>());
    }

    private void UnbindManagers()
    {
        BindOrderManager(null);
        BindHunterManager(null);
        BindRecruitmentManager(null);
        BindConstructionManager(null);
        BindGoldManager(null);
        BindReputationManager(null);
        BindTimeManager(null);
        BindInvestigationManager(null);
        BindDirtManager(null);

        if (gameManager != null)
        {
            gameManager.OnGameOver -= HandleGameOver;
        }
    }

    private void BindOrderManager(OrderManager manager)
    {
        if (orderManager == manager) return;
        if (orderManager != null)
        {
            orderManager.OnOrderGenerated -= HandleOrderGenerated;
            orderManager.OnOrderAccepted -= HandleOrderAccepted;
            orderManager.OnOrderDeclined -= HandleOrderDeclined;
            orderManager.OnOrderCanceled -= HandleOrderCanceled;
            orderManager.OnOrderReferredDetailed -= HandleOrderReferredDetailed;
            orderManager.OnMissionStarted -= HandleMissionStarted;
            orderManager.OnMissionResolved -= HandleMissionResolved;
        }

        orderManager = manager;
        if (orderManager != null)
        {
            orderManager.OnOrderGenerated += HandleOrderGenerated;
            orderManager.OnOrderAccepted += HandleOrderAccepted;
            orderManager.OnOrderDeclined += HandleOrderDeclined;
            orderManager.OnOrderCanceled += HandleOrderCanceled;
            orderManager.OnOrderReferredDetailed += HandleOrderReferredDetailed;
            orderManager.OnMissionStarted += HandleMissionStarted;
            orderManager.OnMissionResolved += HandleMissionResolved;
        }
    }

    private void BindHunterManager(HunterManager manager)
    {
        if (hunterManager == manager) return;
        if (hunterManager != null)
        {
            hunterManager.OnHunterHired -= HandleHunterHired;
            hunterManager.OnHunterFired -= HandleHunterFired;
            hunterManager.OnHunterDismissedForDebt -= HandleHunterDismissedForDebt;
            hunterManager.OnHunterLeveledUp -= HandleHunterLeveledUp;
        }

        hunterManager = manager;
        if (hunterManager != null)
        {
            hunterManager.OnHunterHired += HandleHunterHired;
            hunterManager.OnHunterFired += HandleHunterFired;
            hunterManager.OnHunterDismissedForDebt += HandleHunterDismissedForDebt;
            hunterManager.OnHunterLeveledUp += HandleHunterLeveledUp;
        }
    }

    private void BindRecruitmentManager(HunterRecruitmentManager manager)
    {
        if (recruitmentManager == manager) return;
        if (recruitmentManager != null)
        {
            recruitmentManager.OnCampaignStarted -= HandleCampaignStarted;
            recruitmentManager.OnCandidateArrived -= HandleCandidateArrived;
            recruitmentManager.OnCandidateReviewed -= HandleCandidateReviewed;
            recruitmentManager.OnCandidateHired -= HandleCandidateHired;
            recruitmentManager.OnCandidateDeclined -= HandleCandidateDeclined;
            recruitmentManager.OnCampaignEnded -= HandleCampaignEnded;
        }

        recruitmentManager = manager;
        if (recruitmentManager != null)
        {
            recruitmentManager.OnCampaignStarted += HandleCampaignStarted;
            recruitmentManager.OnCandidateArrived += HandleCandidateArrived;
            recruitmentManager.OnCandidateReviewed += HandleCandidateReviewed;
            recruitmentManager.OnCandidateHired += HandleCandidateHired;
            recruitmentManager.OnCandidateDeclined += HandleCandidateDeclined;
            recruitmentManager.OnCampaignEnded += HandleCampaignEnded;
        }
    }

    private void BindInvestigationManager(InvestigationManager manager)
    {
        if (investigationManager == manager) return;
        if (investigationManager != null)
        {
            investigationManager.OnQuestionAnswered -= HandleQuestionAnswered;
        }

        investigationManager = manager;
        if (investigationManager != null)
        {
            investigationManager.OnQuestionAnswered += HandleQuestionAnswered;
        }
    }

    private void BindConstructionManager(GuildConstructionManager manager)
    {
        if (constructionManager == manager) return;
        if (constructionManager != null)
        {
            constructionManager.OnConstructionBuilt -= HandleConstructionBuilt;
        }

        constructionManager = manager;
        if (constructionManager != null)
        {
            constructionManager.OnConstructionBuilt += HandleConstructionBuilt;
        }
    }

    private void BindGoldManager(GoldManager manager)
    {
        if (goldManager == manager) return;
        if (goldManager != null)
        {
            goldManager.OnGoldChanged -= HandleGoldChanged;
            goldManager.OnSpendFailed -= HandleSpendFailed;
            goldManager.OnDebtChanged -= HandleDebtChanged;
        }

        goldManager = manager;
        if (goldManager != null)
        {
            goldManager.OnGoldChanged += HandleGoldChanged;
            goldManager.OnSpendFailed += HandleSpendFailed;
            goldManager.OnDebtChanged += HandleDebtChanged;
        }
    }

    private void BindReputationManager(ReputationManager manager)
    {
        if (reputationManager == manager) return;
        if (reputationManager != null)
        {
            reputationManager.OnReputationRankIncreased -= HandleReputationRankIncreased;
        }

        reputationManager = manager;
        if (reputationManager != null)
        {
            reputationManager.OnReputationRankIncreased += HandleReputationRankIncreased;
        }
    }

    private void BindTimeManager(TimeManager manager)
    {
        if (timeManager == manager) return;
        if (timeManager != null)
        {
            timeManager.OnTimeAdvanced -= HandleTimeAdvanced;
            timeManager.OnDayStarted -= HandleDayStarted;
            timeManager.OnDayStateChanged -= HandleDayStateChanged;
        }

        timeManager = manager;
        if (timeManager != null)
        {
            timeManager.OnTimeAdvanced += HandleTimeAdvanced;
            timeManager.OnDayStarted += HandleDayStarted;
            timeManager.OnDayStateChanged += HandleDayStateChanged;
        }
    }

    private void BindDirtManager(MainHallFloorDirtManager manager)
    {
        if (dirtManager == manager) return;
        if (dirtManager != null)
        {
            dirtManager.OnDirtChanged -= HandleDirtChanged;
        }

        dirtManager = manager;
        if (dirtManager != null)
        {
            dirtManager.OnDirtChanged += HandleDirtChanged;
        }
    }

    private void HandleOrderGenerated(Order order)
    {
        ordersGenerated++;
        TrackOrderEvent("order_generated", order);
    }

    private void HandleOrderAccepted(Order order)
    {
        ordersAccepted++;
        TrackOrderEvent("order_accepted", order);
    }

    private void HandleOrderDeclined(Order order)
    {
        ordersDeclined++;
        TrackOrderEvent("order_declined", order);
    }

    private void HandleOrderCanceled(Order order)
    {
        ordersCanceled++;
        TrackOrderEvent("order_canceled", order);
    }

    private void HandleOrderReferredDetailed(Order order, int payout, float reputationReward, float caseQuality, float dailyMultiplier)
    {
        ordersReferred++;
        totalGoldFromReferrals += Mathf.Max(0, payout);
        totalReputationFromReferrals += Mathf.Max(0f, reputationReward);
        Dictionary<string, string> data = OrderFields(order);
        data["gold_delta"] = FormatInt(payout);
        data["reputation_delta"] = FormatFloat(reputationReward);
        data["result"] = "referred";
        data["details"] = $"caseQuality={FormatFloat(caseQuality)};dailyReferralMultiplier={FormatFloat(dailyMultiplier)}";
        TrackEvent("order_referred", data);
    }

    private void HandleMissionStarted(Order order, List<Hunter> party)
    {
        missionsStarted++;
        Dictionary<string, string> data = OrderFields(order);
        AddHunterFields(data, party);
        data["party_size"] = party != null ? party.Count.ToString(CultureInfo.InvariantCulture) : "0";
        data["details"] = $"missionDuration={FormatFloat(order != null ? order.missionDuration : 0f)};lateDispatch={Bool(order != null && order.lateDispatch)}";
        TrackEvent("mission_started", data);
    }

    private void HandleMissionResolved(MissionReport report)
    {
        if (report == null) return;

        if (report.success)
        {
            missionsSuccess++;
        }
        else
        {
            missionsFailed++;
        }

        int reportWounds = report.GetInjuriesCount();
        int reportDeaths = report.GetDeathsCount();
        wounds += reportWounds;
        deaths += reportDeaths;
        totalGoldFromMissions += Mathf.Max(0, report.goldEarned);
        totalReputationFromMissions += Mathf.Max(0f, report.reputationGained);

        Dictionary<string, string> data = OrderFields(report.order);
        AddHunterFields(data, GetHuntersFromReport(report));
        data["success_percent"] = FormatFloat(report.successChancePercent);
        data["gold_delta"] = FormatInt(report.goldEarned);
        data["reputation_delta"] = FormatFloat(report.reputationGained);
        data["xp_delta"] = FormatInt(report.GetTotalXP());
        data["wounds"] = FormatInt(reportWounds);
        data["deaths"] = FormatInt(reportDeaths);
        data["result"] = report.success ? "success" : "failure";
        TrackEvent("mission_resolved", data);
    }

    private void HandleHunterHired(Hunter hunter)
    {
        huntersHired++;
        TrackHunterEvent("hunter_hired", hunter);
    }

    private void HandleHunterFired(Hunter hunter)
    {
        huntersFired++;
        TrackHunterEvent("hunter_fired", hunter);
    }

    private void HandleHunterDismissedForDebt(string hunterName, int upkeep)
    {
        huntersDismissedDebt++;
        TrackEvent("hunter_dismissed_debt", Detail("hunter_names", hunterName, "details", $"upkeep={upkeep}"));
    }

    private void HandleHunterLeveledUp(Hunter hunter)
    {
        hunterLevelUps++;
        TrackHunterEvent("hunter_level_up", hunter);
    }

    private void HandleCampaignStarted(HunterRecruitmentManager.AdSettings settings, bool freeCampaign, float campaignCost)
    {
        campaignsStarted++;
        TrackEvent("hiring_campaign_started", Detail(
            "gold_delta", FormatFloat(-campaignCost),
            "details", $"free={Bool(freeCampaign)};duration={FormatFloat(settings.durationSeconds)};targetPower={settings.targetPower};maxUpkeep={settings.maxUpkeep};traits={Join(settings.prioritizedTraitIds)}"));
    }

    private void HandleCandidateArrived(HunterRecruitmentManager.RecruitmentCandidate candidate)
    {
        candidatesArrived++;
        TrackCandidateEvent("candidate_arrived", candidate);
    }

    private void HandleCandidateReviewed(HunterRecruitmentManager.RecruitmentCandidate candidate)
    {
        candidatesReviewed++;
        TrackCandidateEvent("candidate_reviewed", candidate);
    }

    private void HandleCandidateHired(HunterRecruitmentManager.RecruitmentCandidate candidate)
    {
        candidatesHired++;
        TrackCandidateEvent("candidate_hired", candidate);
    }

    private void HandleCandidateDeclined(HunterRecruitmentManager.RecruitmentCandidate candidate)
    {
        candidatesDeclined++;
        TrackCandidateEvent("candidate_declined", candidate);
    }

    private void HandleQuestionAnswered(InvestigationQuestion question, InvestigationCase caseData, string responseText, bool hunterDialogue)
    {
        questionsAnswered++;
        int knownTagCount = caseData != null && caseData.knownTags != null ? caseData.knownTags.Count : 0;
        int knownTraitCount = caseData != null && caseData.confirmedTraitIds != null ? caseData.confirmedTraitIds.Count : 0;
        int truthTraitCount = caseData != null && caseData.truthTraits != null ? caseData.truthTraits.Count : 0;

        TrackEvent("question_answered", Detail(
            "true_monster_id", caseData != null ? MonsterId(caseData.truthMonster) : string.Empty,
            "true_monster_name", caseData != null ? MonsterName(caseData.truthMonster) : string.Empty,
            "declared_monster_id", caseData != null ? MonsterId(caseData.declaredMonster) : string.Empty,
            "declared_monster_name", caseData != null ? MonsterName(caseData.declaredMonster) : string.Empty,
            "result", hunterDialogue ? "hunter" : "client",
            "details", $"questionId={(question != null ? question.questionId : string.Empty)};prompt={(question != null ? question.promptText : string.Empty)};knownTags={knownTagCount};knownTraits={knownTraitCount};truthTraits={truthTraitCount};responseLength={(responseText != null ? responseText.Length : 0)}"));
    }

    private void HandleCampaignEnded(string reason)
    {
        TrackEvent("hiring_campaign_ended", Detail("result", string.IsNullOrWhiteSpace(reason) ? "ended" : reason));
    }

    private void HandleConstructionBuilt(GuildConstructionDefinition definition)
    {
        constructionsBuilt++;
        TrackEvent("construction_built", Detail(
            "result", definition != null ? definition.ConstructionId : string.Empty,
            "details", definition != null ? definition.displayName : string.Empty));
    }

    private void HandleGoldChanged(int value)
    {
        TrackEvent("gold_changed", Detail("details", $"currentGold={value}"));
    }

    private void HandleSpendFailed(int amount, int currentGold)
    {
        TrackEvent("spend_failed", Detail("gold_delta", FormatInt(-amount), "details", $"currentGold={currentGold}"));
    }

    private void HandleDebtChanged(int value)
    {
        TrackEvent("debt_changed", Detail("details", $"debt={value}"));
    }

    private void HandleReputationRankIncreased(int previousRank, int newRank)
    {
        int currentDay = timeManager != null ? timeManager.GetCurrentDayIndex() : -1;
        for (int rank = Mathf.Max(2, previousRank + 1); rank <= newRank; rank++)
        {
            if (rank == 2 && dayReachedRep2 < 0) dayReachedRep2 = currentDay;
            if (rank == 3 && dayReachedRep3 < 0) dayReachedRep3 = currentDay;
            if (rank == 4 && dayReachedRep4 < 0) dayReachedRep4 = currentDay;
        }

        TrackEvent("reputation_rank_increased", Detail("result", $"{previousRank}->{newRank}"));
    }

    private void HandleTimeAdvanced(float deltaSeconds)
    {
        timeAdvancedSeconds += Mathf.Max(0f, deltaSeconds);
        TrackEvent("time_advanced", Detail("details", $"deltaSeconds={FormatFloat(deltaSeconds)}"));
    }

    private void HandleDayStarted(int dayIndex)
    {
        TrackEvent("day_started", Detail("result", dayIndex.ToString(CultureInfo.InvariantCulture)));
    }

    private void HandleDayStateChanged(TimeManager.DayState state)
    {
        if (state == TimeManager.DayState.Evening)
        {
            endOfDayRepRanks.Add(reputationManager != null ? reputationManager.GetReputation() : 0);
            endOfDayReputationPoints.Add(reputationManager != null ? reputationManager.GetReputationPointsPrecise() : 0f);
        }

        TrackEvent("day_state_changed", Detail("result", state.ToString()));
    }

    private void HandleDirtChanged()
    {
        dirtChanges++;
        TrackEvent("floor_dirt_changed", Detail(
            "result", dirtManager != null ? dirtManager.DirtPoints.ToString(CultureInfo.InvariantCulture) : "0",
            "details", dirtManager != null ? $"rewardPenalty={FormatFloat(dirtManager.CurrentRewardPenaltyPercent)}" : string.Empty));
    }

    private void HandleGameOver(string reason)
    {
        TrackEvent("game_over", Detail("result", reason));
        CloseSession();
    }

    private void TrackOrderEvent(string eventName, Order order)
    {
        TrackEvent(eventName, OrderFields(order));
    }

    private void TrackHunterEvent(string eventName, Hunter hunter)
    {
        Dictionary<string, string> data = new Dictionary<string, string>();
        AddHunterFields(data, hunter != null ? new List<Hunter> { hunter } : null);
        if (hunter != null && hunter.Data != null)
        {
            data["details"] = $"level={hunter.GetLevel()};xp={hunter.GetXP()};upkeep={hunter.GetUpkeepCost()};state={hunter.GetState()}";
        }
        TrackEvent(eventName, data);
    }

    private void TrackCandidateEvent(string eventName, HunterRecruitmentManager.RecruitmentCandidate candidate)
    {
        HunterData data = candidate != null ? candidate.hunter : null;
        TrackEvent(eventName, Detail(
            "hunter_ids", data != null ? data.hunterId : string.Empty,
            "hunter_names", data != null ? data.hunterName : string.Empty,
            "result", candidate != null ? candidate.status.ToString() : string.Empty,
            "details", data != null ? $"rarity={data.rarity};level={data.startingLevel};upkeep={data.GetUpkeepCost(data.startingLevel)};power={data.GetTotalPower(data.startingLevel)}" : string.Empty));
    }

    private void TrackEvent(string eventName, Dictionary<string, string> data = null)
    {
        if (!initialized || sessionClosed || config == null || !config.writeLocalTelemetryEvents)
        {
            return;
        }

        if (data == null)
        {
            data = new Dictionary<string, string>();
        }
        eventIndex++;

        string[] columns =
        {
            sessionId,
            FormatInt(eventIndex),
            DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            SceneManager.GetActiveScene().name,
            FormatInt(timeManager != null ? timeManager.GetCurrentDayIndex() : -1),
            timeManager != null ? timeManager.GetDayState().ToString() : string.Empty,
            FormatFloat(timeManager != null ? timeManager.GetSecondsIntoCurrentDay() : 0f),
            FormatFloat(timeManager != null ? timeManager.GetSecondsRemainingInDay() : 0f),
            FormatFloat(Time.realtimeSinceStartup - sessionStartRealtime),
            FormatInt(goldManager != null ? goldManager.GetGold() : 0),
            FormatInt(goldManager != null ? goldManager.GetDebt() : 0),
            FormatInt(reputationManager != null ? reputationManager.GetReputation() : 0),
            FormatFloat(reputationManager != null ? reputationManager.GetReputationPointsPrecise() : 0f),
            FormatInt(reputationManager != null ? reputationManager.GetTrustStreak() : 0),
            eventName,
            Get(data, "order_id"),
            Get(data, "order_title"),
            Get(data, "order_state"),
            Get(data, "true_monster_id"),
            Get(data, "true_monster_name"),
            Get(data, "declared_monster_id"),
            Get(data, "declared_monster_name"),
            Get(data, "hunter_ids"),
            Get(data, "hunter_names"),
            Get(data, "party_size"),
            Get(data, "success_percent"),
            Get(data, "gold_delta"),
            Get(data, "reputation_delta"),
            Get(data, "xp_delta"),
            Get(data, "wounds"),
            Get(data, "deaths"),
            Get(data, "result"),
            Get(data, "details")
        };

        AppendCsvLine(eventsPath, columns);
    }

    private void CloseSession()
    {
        if (!initialized || sessionClosed || config == null || !config.writeLocalTelemetrySessionSummary)
        {
            return;
        }

        sessionClosed = true;
        string[] columns =
        {
            sessionId,
            sessionStartUtc,
            DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            FormatFloat(Time.realtimeSinceStartup - sessionStartRealtime),
            SceneManager.GetActiveScene().name,
            FormatInt(timeManager != null ? timeManager.GetCurrentDayIndex() : -1),
            timeManager != null ? timeManager.GetDayState().ToString() : string.Empty,
            FormatInt(goldManager != null ? goldManager.GetGold() : 0),
            FormatInt(goldManager != null ? goldManager.GetDebt() : 0),
            FormatInt(reputationManager != null ? reputationManager.GetReputation() : 0),
            FormatFloat(reputationManager != null ? reputationManager.GetReputationPointsPrecise() : 0f),
            FormatInt(reputationManager != null ? reputationManager.GetTrustStreak() : 0),
            FormatInt(hunterManager != null ? hunterManager.GetAllHunters().Count : 0),
            FormatInt(ordersGenerated),
            FormatInt(ordersAccepted),
            FormatInt(ordersReferred),
            FormatInt(ordersDeclined),
            FormatInt(ordersCanceled),
            FormatInt(missionsStarted),
            FormatInt(missionsSuccess),
            FormatInt(missionsFailed),
            FormatInt(wounds),
            FormatInt(deaths),
            FormatInt(totalGoldFromMissions),
            FormatInt(totalGoldFromReferrals),
            FormatFloat(totalReputationFromMissions),
            FormatFloat(totalReputationFromReferrals),
            FormatInt(huntersHired),
            FormatInt(huntersFired),
            FormatInt(huntersDismissedDebt),
            FormatInt(hunterLevelUps),
            FormatInt(campaignsStarted),
            FormatInt(candidatesArrived),
            FormatInt(candidatesReviewed),
            FormatInt(candidatesHired),
            FormatInt(candidatesDeclined),
            FormatInt(questionsAnswered),
            FormatInt(constructionsBuilt),
            FormatInt(dirtChanges),
            FormatFloat(timeAdvancedSeconds),
            FormatInt(dayReachedRep2),
            FormatInt(dayReachedRep3),
            FormatInt(dayReachedRep4),
            JoinInts(endOfDayRepRanks),
            JoinFloats(endOfDayReputationPoints)
        };

        AppendCsvLine(summaryPath, columns);
    }

    private Dictionary<string, string> OrderFields(Order order)
    {
        Dictionary<string, string> data = new Dictionary<string, string>();
        if (order == null) return data;

        data["order_id"] = order.orderId.ToString();
        data["order_title"] = order.orderTitle;
        data["order_state"] = order.state.ToString();
        data["true_monster_id"] = MonsterId(order.monsterData);
        data["true_monster_name"] = MonsterName(order.monsterData);
        data["declared_monster_id"] = MonsterId(order.declaredMonster);
        data["declared_monster_name"] = MonsterName(order.declaredMonster);
        data["details"] = $"difficulty={order.difficulty};tier={order.reputationTier};rewardGold={order.goldReward};rewardXP={order.xpReward};rewardRep={FormatFloat(order.reputationPointsReward)};duration={FormatFloat(order.missionDuration)};lateDispatch={Bool(order.lateDispatch)};maxParty={order.maxPartySize}";
        return data;
    }

    private void AddHunterFields(Dictionary<string, string> data, List<Hunter> hunters)
    {
        if (data == null) return;
        if (hunters == null || hunters.Count == 0)
        {
            data["hunter_ids"] = string.Empty;
            data["hunter_names"] = string.Empty;
            data["party_size"] = "0";
            return;
        }

        List<string> ids = new List<string>();
        List<string> names = new List<string>();
        foreach (var hunter in hunters)
        {
            if (hunter == null || hunter.Data == null) continue;
            ids.Add(hunter.Data.hunterId);
            names.Add(hunter.Data.hunterName);
        }

        data["hunter_ids"] = Join(ids);
        data["hunter_names"] = Join(names);
        data["party_size"] = FormatInt(names.Count);
    }

    private List<Hunter> GetHuntersFromReport(MissionReport report)
    {
        List<Hunter> hunters = new List<Hunter>();
        if (report?.hunterResults == null) return hunters;
        foreach (var result in report.hunterResults)
        {
            if (result?.hunter != null)
            {
                hunters.Add(result.hunter);
            }
        }
        return hunters;
    }

    private static Dictionary<string, string> Detail(params string[] values)
    {
        Dictionary<string, string> data = new Dictionary<string, string>();
        if (values == null) return data;
        for (int i = 0; i + 1 < values.Length; i += 2)
        {
            data[values[i]] = values[i + 1];
        }
        return data;
    }

    private static string Get(Dictionary<string, string> data, string key)
    {
        if (data == null || string.IsNullOrEmpty(key)) return string.Empty;
        return data.TryGetValue(key, out string value) ? value ?? string.Empty : string.Empty;
    }

    private static string MonsterId(MonsterData monster)
    {
        return monster != null ? monster.monsterId : string.Empty;
    }

    private static string MonsterName(MonsterData monster)
    {
        if (monster == null) return string.Empty;
        return !string.IsNullOrWhiteSpace(monster.displayName) ? monster.displayName : monster.name;
    }

    private static void EnsureCsvHeader(string path, string header)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (File.Exists(path) && new FileInfo(path).Length > 0) return;
        File.WriteAllText(path, header + Environment.NewLine, Encoding.UTF8);
    }

    private static void AppendCsvLine(string path, IEnumerable<string> values)
    {
        if (string.IsNullOrWhiteSpace(path) || values == null) return;

        StringBuilder builder = new StringBuilder();
        bool first = true;
        foreach (string value in values)
        {
            if (!first)
            {
                builder.Append(',');
            }
            first = false;
            builder.Append(EscapeCsv(value));
        }
        builder.AppendLine();

        try
        {
            File.AppendAllText(path, builder.ToString(), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"LiveTelemetryManager: Failed to write telemetry. {ex.Message}");
        }
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        bool mustQuote = value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r");
        string normalized = value.Replace("\"", "\"\"");
        return mustQuote ? $"\"{normalized}\"" : normalized;
    }

    private static string FormatInt(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string Bool(bool value)
    {
        return value ? "true" : "false";
    }

    private static string Join(IEnumerable<string> values)
    {
        if (values == null) return string.Empty;
        return string.Join("|", values);
    }

    private static string JoinInts(IEnumerable<int> values)
    {
        if (values == null) return string.Empty;
        List<string> formatted = new List<string>();
        foreach (int value in values)
        {
            formatted.Add(FormatInt(value));
        }
        return Join(formatted);
    }

    private static string JoinFloats(IEnumerable<float> values)
    {
        if (values == null) return string.Empty;
        List<string> formatted = new List<string>();
        foreach (float value in values)
        {
            formatted.Add(FormatFloat(value));
        }
        return Join(formatted);
    }
}
