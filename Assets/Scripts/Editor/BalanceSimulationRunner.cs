using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BalanceSimulationRunner
{
    private const string ReportsDirectory = "BalanceReports";

    [MenuItem("Tools/Balance Simulation/Run Selected Settings")]
    public static void RunSelectedSettings()
    {
        BalanceSimulationSettings settings = Selection.activeObject as BalanceSimulationSettings;
        if (settings == null)
        {
            Debug.LogWarning("Select a BalanceSimulationSettings asset first, or use Run Default Profiles.");
            return;
        }

        Run(settings);
    }

    [MenuItem("Tools/Balance Simulation/Run Default Profiles")]
    public static void RunDefaultProfiles()
    {
        Run(null);
    }

    public static void Run(BalanceSimulationSettings settings)
    {
        SimulationData data = SimulationData.Load(settings);
        if (!data.IsValid)
        {
            Debug.LogError(data.Error);
            return;
        }

        int sessionsPerProfile = settings != null ? Mathf.Max(1, settings.sessionsPerProfile) : 10;
        int daysPerSession = settings != null ? Mathf.Max(1, settings.daysPerSession) : 10;
        int seed = settings != null ? settings.randomSeed : 12345;
        List<ProfileSpec> profiles = BuildProfiles(settings);
        List<SessionResult> results = new List<SessionResult>();

        try
        {
            for (int profileIndex = 0; profileIndex < profiles.Count; profileIndex++)
            {
                ProfileSpec profile = profiles[profileIndex];
                for (int sessionIndex = 0; sessionIndex < sessionsPerProfile; sessionIndex++)
                {
                    int sessionSeed = seed + profileIndex * 10000 + sessionIndex;
                    UnityEngine.Random.InitState(sessionSeed);
                    BalanceSimulator simulator = new BalanceSimulator(data, settings, profile, sessionSeed);
                    results.Add(simulator.Run(daysPerSession));
                }
            }
        }
        finally
        {
            BalanceSimulator.CleanupSimulationHunters();
        }

        Directory.CreateDirectory(ReportsDirectory);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string sessionsPath = Path.Combine(ReportsDirectory, $"balance_sessions_{stamp}.csv");
        string summaryPath = Path.Combine(ReportsDirectory, $"balance_summary_{stamp}.csv");
        File.WriteAllText(sessionsPath, BuildSessionCsv(results), Encoding.UTF8);
        File.WriteAllText(summaryPath, BuildSummaryCsv(results), Encoding.UTF8);
        AssetDatabase.Refresh();

        Debug.Log($"Balance simulation complete. Sessions: {sessionsPath}. Summary: {summaryPath}");
    }

    private static List<ProfileSpec> BuildProfiles(BalanceSimulationSettings settings)
    {
        List<ProfileSpec> profiles = new List<ProfileSpec>();
        if (settings != null && settings.profiles != null)
        {
            foreach (var profile in settings.profiles)
            {
                if (profile == null) continue;
                profiles.Add(ProfileSpec.FromAsset(profile));
            }
        }

        if (profiles.Count > 0)
        {
            return profiles;
        }

        profiles.Add(new ProfileSpec
        {
            Name = "Investigator",
            RevealMode = BalanceSimulationProfile.RevealMode.All,
            QuestionRevealFraction = 1f,
            LearnedBestiaryAtReputation = 3,
            LearnedQuestionRevealFraction = 0.5f,
            BlindCorrectMonsterChance = 0.75f,
            ReferBelowSuccessChance = 100f,
            MinimumReferralCaseQuality = 0.6f,
            ReferChanceBelowThreshold = 0.9f,
            SendRiskyOrdersWhenBroke = true,
            TargetDispatchSuccessChance = 150f,
            EconomicPressureTargetDispatchSuccessChance = 120f,
            MinimumDispatchSuccessChance = 100f,
            DesperateMinimumDispatchSuccessChance = 100f,
            WaitForHuntersBeforeUnsafeDispatch = true,
            ForceReferralForGoodUnsafeCasesUnderPressure = true,
            MaxPartySize = 3,
            TargetRosterSize = 4,
            MaxRosterSize = 6,
            AutoLevelHunters = true
        });
        profiles.Add(new ProfileSpec
        {
            Name = "Middle-ground",
            RevealMode = BalanceSimulationProfile.RevealMode.RandomFraction,
            QuestionRevealFraction = 0.5f,
            LearnedBestiaryAtReputation = 3,
            LearnedQuestionRevealFraction = 0.5f,
            BlindCorrectMonsterChance = 0.35f,
            ReferBelowSuccessChance = 35f,
            MinimumReferralCaseQuality = 0.45f,
            ReferChanceBelowThreshold = 0.35f,
            SendRiskyOrdersWhenBroke = true,
            TargetDispatchSuccessChance = 130f,
            EconomicPressureTargetDispatchSuccessChance = 120f,
            MinimumDispatchSuccessChance = 85f,
            DesperateMinimumDispatchSuccessChance = 60f,
            WaitForHuntersBeforeUnsafeDispatch = true,
            ForceReferralForGoodUnsafeCasesUnderPressure = false,
            MaxPartySize = 3,
            TargetRosterSize = 3,
            MaxRosterSize = 5,
            AutoLevelHunters = true
        });
        profiles.Add(new ProfileSpec
        {
            Name = "Traiter",
            RevealMode = BalanceSimulationProfile.RevealMode.TraitsAndFamily,
            QuestionRevealFraction = 0.5f,
            LearnedBestiaryAtReputation = 99,
            LearnedQuestionRevealFraction = 0.5f,
            BlindCorrectMonsterChance = 0.3f,
            ReferBelowSuccessChance = 45f,
            MinimumReferralCaseQuality = 0.55f,
            ReferChanceBelowThreshold = 0.5f,
            SendRiskyOrdersWhenBroke = true,
            TargetDispatchSuccessChance = 115f,
            EconomicPressureTargetDispatchSuccessChance = 100f,
            MinimumDispatchSuccessChance = 80f,
            DesperateMinimumDispatchSuccessChance = 55f,
            WaitForHuntersBeforeUnsafeDispatch = true,
            ForceReferralForGoodUnsafeCasesUnderPressure = false,
            MaxPartySize = 3,
            TargetRosterSize = 3,
            MaxRosterSize = 5,
            AutoLevelHunters = true
        });
        profiles.Add(new ProfileSpec
        {
            Name = "Lazy",
            RevealMode = BalanceSimulationProfile.RevealMode.None,
            QuestionRevealFraction = 0f,
            LearnedBestiaryAtReputation = 99,
            LearnedQuestionRevealFraction = 0f,
            BlindCorrectMonsterChance = 0.1f,
            ReferBelowSuccessChance = 0f,
            MinimumReferralCaseQuality = 0f,
            ReferChanceBelowThreshold = 0f,
            SendRiskyOrdersWhenBroke = false,
            TargetDispatchSuccessChance = 90f,
            EconomicPressureTargetDispatchSuccessChance = 80f,
            MinimumDispatchSuccessChance = 50f,
            DesperateMinimumDispatchSuccessChance = 25f,
            WaitForHuntersBeforeUnsafeDispatch = false,
            ForceReferralForGoodUnsafeCasesUnderPressure = false,
            MaxPartySize = 3,
            TargetRosterSize = 3,
            MaxRosterSize = 4,
            AutoLevelHunters = false
        });

        return profiles;
    }

    private static string BuildSessionCsv(List<SessionResult> results)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("profile,session,seed,daysPlayed,gameOver,finalGold,finalDebt,finalReputationRank,finalReputationPoints,reputationEarned,trustStreak,dayReachedRep2,dayReachedRep3,dayReachedRep4,endOfDayRepRank[],endOfDayReputationPoints[],totalIncome,totalUpkeep,totalLevelUpSpend,totalHiringSpend,totalSurplus,totalXpGranted,levelUpsBought,ordersGenerated,ordersSent,ordersCompleted,cleanSuccesses,messySuccesses,ordersFailed,ordersReferred,ordersDeclined,wounds,deaths,huntersHired,huntersDismissed,endingHunters,averageEndingHunterLevel,averageSuccessChance,averagePartySize,averagePartyPower,averageRequiredPower,averageKnownTraitRatio,averageCaseQuality");
        foreach (var r in results)
        {
            AppendCsvLine(sb,
                r.ProfileName,
                r.SessionIndex,
                r.Seed,
                r.DaysPlayed,
                r.GameOver,
                r.FinalGold,
                r.FinalDebt,
                r.FinalReputationRank,
                F(r.FinalReputationPoints),
                F(r.ReputationEarned),
                r.TrustStreak,
                r.DayReachedRep2,
                r.DayReachedRep3,
                r.DayReachedRep4,
                JoinInts(r.EndOfDayRepRanks),
                JoinFloats(r.EndOfDayReputationPoints),
                r.TotalIncome,
                r.TotalUpkeep,
                r.TotalLevelUpSpend,
                r.TotalHiringSpend,
                r.TotalSurplus,
                r.TotalXpGranted,
                r.LevelUpsBought,
                r.OrdersGenerated,
                r.OrdersSent,
                r.OrdersCompleted,
                r.CleanSuccesses,
                r.MessySuccesses,
                r.OrdersFailed,
                r.OrdersReferred,
                r.OrdersDeclined,
                r.Wounds,
                r.Deaths,
                r.HuntersHired,
                r.HuntersDismissed,
                r.EndingHunters,
                F(r.AverageEndingHunterLevel),
                F(r.AverageSuccessChance),
                F(r.AveragePartySize),
                F(r.AveragePartyPower),
                F(r.AverageRequiredPower),
                F(r.AverageKnownTraitRatio),
                F(r.AverageCaseQuality));
        }
        return sb.ToString();
    }

    private static string BuildSummaryCsv(List<SessionResult> results)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("profile,sessions,gameOverRate,medianFinalGold,averageFinalGold,medianSurplus,averageSurplus,medianReputationRank,averageReputationRank,averageReputationEarned,averageDayReachedRep2,averageDayReachedRep3,averageDayReachedRep4,averageIncome,averageUpkeep,averageLevelUpSpend,averageXpGranted,averageLevelUpsBought,averageOrdersCompleted,averageCleanSuccesses,averageMessySuccesses,averageOrdersFailed,averageOrdersReferred,averageWounds,averageDeaths,averageEndingHunterLevel,averageSuccessChance,averagePartySize,averagePartyPower,averageRequiredPower,averageCaseQuality");
        foreach (var group in results.GroupBy(r => r.ProfileName))
        {
            List<SessionResult> rows = group.ToList();
            AppendCsvLine(sb,
                group.Key,
                rows.Count,
                F(rows.Count(r => r.GameOver) / (float)Mathf.Max(1, rows.Count)),
                F(Median(rows.Select(r => (float)r.FinalGold))),
                F(rows.Average(r => r.FinalGold)),
                F(Median(rows.Select(r => (float)r.TotalSurplus))),
                F(rows.Average(r => r.TotalSurplus)),
                F(Median(rows.Select(r => (float)r.FinalReputationRank))),
                F(rows.Average(r => r.FinalReputationRank)),
                F(rows.Average(r => r.ReputationEarned)),
                F(AverageReachedDay(rows, r => r.DayReachedRep2)),
                F(AverageReachedDay(rows, r => r.DayReachedRep3)),
                F(AverageReachedDay(rows, r => r.DayReachedRep4)),
                F(rows.Average(r => r.TotalIncome)),
                F(rows.Average(r => r.TotalUpkeep)),
                F(rows.Average(r => r.TotalLevelUpSpend)),
                F(rows.Average(r => r.TotalXpGranted)),
                F(rows.Average(r => r.LevelUpsBought)),
                F(rows.Average(r => r.OrdersCompleted)),
                F(rows.Average(r => r.CleanSuccesses)),
                F(rows.Average(r => r.MessySuccesses)),
                F(rows.Average(r => r.OrdersFailed)),
                F(rows.Average(r => r.OrdersReferred)),
                F(rows.Average(r => r.Wounds)),
                F(rows.Average(r => r.Deaths)),
                F(rows.Average(r => r.AverageEndingHunterLevel)),
                F(rows.Average(r => r.AverageSuccessChance)),
                F(rows.Average(r => r.AveragePartySize)),
                F(rows.Average(r => r.AveragePartyPower)),
                F(rows.Average(r => r.AverageRequiredPower)),
                F(rows.Average(r => r.AverageCaseQuality)));
        }
        return sb.ToString();
    }

    private static float Median(IEnumerable<float> values)
    {
        List<float> sorted = values.OrderBy(v => v).ToList();
        if (sorted.Count == 0) return 0f;
        int mid = sorted.Count / 2;
        if (sorted.Count % 2 == 1) return sorted[mid];
        return (sorted[mid - 1] + sorted[mid]) * 0.5f;
    }

    private static string F(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string F(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string JoinInts(List<int> values)
    {
        return values == null || values.Count == 0 ? string.Empty : string.Join("|", values);
    }

    private static string JoinFloats(List<float> values)
    {
        if (values == null || values.Count == 0) return string.Empty;
        return string.Join("|", values.Select(F));
    }

    private static float AverageReachedDay(List<SessionResult> rows, Func<SessionResult, int> selector)
    {
        if (rows == null || selector == null) return 0f;
        var reached = rows.Select(selector).Where(day => day > 0).ToList();
        return reached.Count > 0 ? (float)reached.Average() : 0f;
    }

    private static void AppendCsvLine(StringBuilder sb, params object[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0) sb.Append(',');
            string text = values[i]?.ToString() ?? string.Empty;
            bool quote = text.Contains(",") || text.Contains("\"") || text.Contains("\n");
            if (quote)
            {
                sb.Append('"').Append(text.Replace("\"", "\"\"")).Append('"');
            }
            else
            {
                sb.Append(text);
            }
        }
        sb.AppendLine();
    }

    private sealed class BalanceSimulator
    {
        private static readonly List<GameObject> TemporaryHunterObjects = new List<GameObject>();

        private readonly SimulationData data;
        private readonly BalanceSimulationSettings settings;
        private readonly ProfileSpec profile;
        private readonly int seed;
        private readonly SessionResult result;
        private readonly List<SimHunter> hunters = new List<SimHunter>();
        private readonly List<SimMission> activeMissions = new List<SimMission>();
        private readonly HashSet<string> usedHunterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private int gold;
        private int debt;
        private int currentDayGrossIncome;
        private int previousDayGrossIncome;
        private int unpaidUpkeepStreak;
        private float debtSuccessPenalty;
        private float reputationPoints;
        private int trustStreak;
        private int referralsToday;
        private int currentDay;

        public BalanceSimulator(SimulationData data, BalanceSimulationSettings settings, ProfileSpec profile, int seed)
        {
            this.data = data;
            this.settings = settings;
            this.profile = profile;
            this.seed = seed;
            gold = settings != null ? Mathf.Max(0, settings.startingGold) : 100;
            reputationPoints = settings != null ? Mathf.Max(0f, settings.startingReputationPoints) : 0f;

            result = new SessionResult
            {
                ProfileName = profile.Name,
                Seed = seed,
                SessionIndex = Mathf.Abs(seed % 10000)
            };

            int initialHunters = settings != null ? Mathf.Max(1, settings.startingHunterCount) : 3;
            for (int i = 0; i < initialHunters; i++)
            {
                TryHireRandomHunter(free: true);
            }
        }

        public static void CleanupSimulationHunters()
        {
            foreach (var go in TemporaryHunterObjects)
            {
                if (go != null)
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }
            TemporaryHunterObjects.Clear();
        }

        public SessionResult Run(int days)
        {
            for (currentDay = 1; currentDay <= days; currentDay++)
            {
                if (!StartDay())
                {
                    break;
                }

                RunDay();
                ResolveAllActiveMissions();
                HealSurvivorsOvernight();
                result.DaysPlayed = currentDay;
                RecordEndOfDayReputationProgress();
            }

            result.GameOver = unpaidUpkeepStreak >= 3;
            result.FinalGold = gold;
            result.FinalDebt = debt;
            result.FinalReputationPoints = reputationPoints;
            result.FinalReputationRank = GetCurrentReputation();
            result.TrustStreak = ClampTrust(trustStreak);
            result.TotalSurplus = result.TotalIncome - result.TotalUpkeep - result.TotalLevelUpSpend - result.TotalHiringSpend;
            result.EndingHunters = hunters.Count(h => !h.Dead);
            result.AverageEndingHunterLevel = result.EndingHunters > 0
                ? (float)hunters.Where(h => !h.Dead).Average(h => h.Hunter.GetLevel())
                : 0f;
            result.AverageSuccessChance = result.OrdersSent > 0 ? result.SuccessChanceTotal / result.OrdersSent : 0f;
            result.AveragePartySize = result.OrdersSent > 0 ? result.PartySizeTotal / result.OrdersSent : 0f;
            result.AveragePartyPower = result.OrdersSent > 0 ? result.PartyPowerTotal / result.OrdersSent : 0f;
            result.AverageRequiredPower = result.OrdersSent > 0 ? result.RequiredPowerTotal / result.OrdersSent : 0f;
            result.AverageKnownTraitRatio = result.OrdersGenerated > 0 ? result.KnownTraitRatioTotal / result.OrdersGenerated : 0f;
            result.AverageCaseQuality = result.OrdersGenerated > 0 ? result.CaseQualityTotal / result.OrdersGenerated : 0f;
            return result;
        }

        private bool StartDay()
        {
            referralsToday = 0;
            previousDayGrossIncome = currentDayGrossIncome;
            currentDayGrossIncome = 0;
            int upkeep = hunters.Where(h => !h.Dead).Sum(h => h.Hunter.GetUpkeepCost());
            result.TotalUpkeep += upkeep;

            if (upkeep <= 0 || gold >= upkeep)
            {
                gold -= Mathf.Min(gold, upkeep);
                unpaidUpkeepStreak = 0;
                debtSuccessPenalty = 0f;
                debt = 0;
                return true;
            }

            unpaidUpkeepStreak++;
            int unpaidAmount = Mathf.Max(0, upkeep - gold);
            debt += unpaidAmount;
            gold = 0;

            GameConfig.DebtSettings debtSettings = data.Config.debtSettings ?? new GameConfig.DebtSettings();
            if (unpaidUpkeepStreak >= 3)
            {
                return false;
            }

            bool secondDay = unpaidUpkeepStreak >= 2;
            debtSuccessPenalty = secondDay
                ? Mathf.Max(0f, debtSettings.unpaidDay2SuccessPenaltyPercent)
                : Mathf.Max(0f, debtSettings.unpaidDay1SuccessPenaltyPercent);

            float pointLossPercent = secondDay
                ? Mathf.Clamp(debtSettings.unpaidDay2ReputationPointLossPercent, 0f, 100f)
                : Mathf.Clamp(debtSettings.unpaidDay1ReputationPointLossPercent, 0f, 100f);
            LoseReputationPointsPercent(pointLossPercent);

            if (secondDay && debtSettings.dismissHuntersUntilUpkeepFitsPreviousIncome)
            {
                DismissHuntersUntilUpkeepFits(previousDayGrossIncome, Mathf.Max(0, debtSettings.minimumHuntersAfterDebtDismissal));
            }

            return true;
        }

        private void RunDay()
        {
            float timeRemaining = Mathf.Max(1f, data.Config.dayLengthSeconds);
            int maxClients = settings != null ? Mathf.Max(1, settings.maxClientsPerDay) : 8;
            float keepSeconds = settings != null ? Mathf.Max(0f, settings.minimumSecondsToKeepBeforeNewClient) : 0f;

            TryMaintainRoster(ref timeRemaining);
            TryLevelHunters(ref timeRemaining);

            for (int i = 0; i < maxClients && timeRemaining > keepSeconds; i++)
            {
                if (!hunters.Any(h => h.CanAct))
                {
                    AdvanceToNextMissionCompletion(ref timeRemaining);
                    if (!hunters.Any(h => h.CanAct)) break;
                }

                AdvanceTime(ref timeRemaining, data.Config.actionTimeSettings.ringBellSeconds);
                Order order = GenerateOrder();
                if (order == null) break;
                result.OrdersGenerated++;

                Investigate(order, ref timeRemaining);
                float knownRatio = GetKnownTraitRatio(order);
                float caseQuality = CalculateReferralCaseQuality(order);
                result.KnownTraitRatioTotal += knownRatio;
                result.CaseQualityTotal += caseQuality;

                List<SimHunter> party = SelectParty(order);
                if (party.Count == 0)
                {
                    result.OrdersDeclined++;
                    continue;
                }

                float projectedDispatchTimeRemaining = Mathf.Max(0f, timeRemaining - data.Config.actionTimeSettings.acceptOrderSeconds);
                float predictedSuccess = CalculatePreviewSuccess(order, party, projectedDispatchTimeRemaining);
                if (!MeetsDispatchFloor(predictedSuccess))
                {
                    TryWaitForSaferParty(order, ref timeRemaining, keepSeconds, ref party, ref predictedSuccess);
                }

                if (!MeetsDispatchFloor(predictedSuccess))
                {
                    if (ShouldRefer(order, predictedSuccess, caseQuality))
                    {
                        int referral = CalculateReferralFee(order);
                        AddGold(referral);
                        ApplyReferralReputation(order);
                        referralsToday++;
                        result.OrdersReferred++;
                        AdvanceTime(ref timeRemaining, data.Config.actionTimeSettings.referOrderSeconds);
                    }
                    else
                    {
                        result.OrdersDeclined++;
                    }
                    continue;
                }

                if (ShouldRefer(order, predictedSuccess, caseQuality))
                {
                    int referral = CalculateReferralFee(order);
                    AddGold(referral);
                    ApplyReferralReputation(order);
                    referralsToday++;
                    result.OrdersReferred++;
                    AdvanceTime(ref timeRemaining, data.Config.actionTimeSettings.referOrderSeconds);
                    continue;
                }

                AdvanceTime(ref timeRemaining, data.Config.actionTimeSettings.acceptOrderSeconds);
                order.state = OrderState.Accepted;
                order.assignedHunters.Clear();
                order.assignedHunters.AddRange(party.Select(h => h.Hunter));
                order.lateDispatch = timeRemaining <= data.Config.lateDispatchWindowSeconds;
                MissionOutcomeResult outcome = MissionOutcomeCalculator.Evaluate(order, order.assignedHunters, BuildMissionConfig());
                float duration = Mathf.Max(1f, order.missionDuration * Mathf.Max(0.01f, outcome.MissionTimeMultiplier));
                foreach (var hunter in party)
                {
                    hunter.OnMission = true;
                }
                activeMissions.Add(new SimMission(order, party, duration));
            result.OrdersSent++;
            result.SuccessChanceTotal += outcome.SuccessChancePercent;
            result.PartySizeTotal += party.Count;
            result.PartyPowerTotal += outcome.PartyPower;
            result.RequiredPowerTotal += outcome.RequiredPower;
                AdvanceTime(ref timeRemaining, data.Config.actionTimeSettings.sendPartySeconds);
                ResolveCompletedMissions();

                TryMaintainRoster(ref timeRemaining);
                TryLevelHunters(ref timeRemaining);
            }
        }

        private void Investigate(Order order, ref float timeRemaining)
        {
            var caseData = order.investigationCase;
            if (caseData == null) return;

            List<InvestigationQuestion> questions = data.Questions;
            int questionCount = GetQuestionCountToAsk(questions.Count);
            for (int i = 0; i < questionCount && i < questions.Count; i++)
            {
                var question = PickQuestionForProfile(questions, caseData);
                if (question == null) break;
                RevealQuestion(order, question);
                float clientDelay = caseData.clientProfile != null ? Mathf.Max(0f, caseData.clientProfile.responseDelaySeconds) : 0f;
                AdvanceTime(ref timeRemaining, Mathf.Max(0f, question.askDurationSeconds) + clientDelay);
            }

            if (profile.RevealMode == BalanceSimulationProfile.RevealMode.All && !HasLearnedBestiary())
            {
                RevealAll(order);
            }
            else if (profile.RevealMode == BalanceSimulationProfile.RevealMode.TraitsAndFamily)
            {
                RevealFamily(order);
                RevealAllTraits(order);
            }

            order.declaredMonster = ChooseDeclaredMonster(order);
            caseData.declaredMonster = order.declaredMonster;
        }

        private int GetQuestionCountToAsk(int totalQuestions)
        {
            if (totalQuestions <= 0) return 0;
            switch (profile.RevealMode)
            {
                case BalanceSimulationProfile.RevealMode.All:
                    if (HasLearnedBestiary())
                    {
                        return Mathf.Clamp(Mathf.RoundToInt(totalQuestions * Mathf.Clamp01(profile.LearnedQuestionRevealFraction)), 0, totalQuestions);
                    }
                    return totalQuestions;
                case BalanceSimulationProfile.RevealMode.TraitsAndFamily:
                    return Mathf.Max(1, Mathf.CeilToInt(totalQuestions * 0.35f));
                case BalanceSimulationProfile.RevealMode.RandomFraction:
                    float fraction = GetCurrentReputation() >= profile.LearnedBestiaryAtReputation
                        ? profile.LearnedQuestionRevealFraction
                        : profile.QuestionRevealFraction;
                    return Mathf.Clamp(Mathf.RoundToInt(totalQuestions * Mathf.Clamp01(fraction)), 0, totalQuestions);
                default:
                    return 0;
            }
        }

        private InvestigationQuestion PickQuestionForProfile(List<InvestigationQuestion> questions, InvestigationCase caseData)
        {
            if (questions == null || questions.Count == 0) return null;
            List<InvestigationQuestion> candidates = questions
                .Where(q => q != null && !HasQuestionBeenAsked(caseData, q))
                .ToList();
            if (candidates.Count == 0) return null;

            if (profile.RevealMode == BalanceSimulationProfile.RevealMode.TraitsAndFamily)
            {
                var targeted = candidates.FirstOrDefault(RevealsTraitOrFamily);
                if (targeted != null) return targeted;
            }

            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        private bool HasLearnedBestiary()
        {
            return GetCurrentReputation() >= profile.LearnedBestiaryAtReputation;
        }

        private bool RevealsTraitOrFamily(InvestigationQuestion question)
        {
            if (question == null) return false;
            if (question.revealedTraits != null && question.revealedTraits.Count > 0) return true;
            if (question.revealedCategories == null) return false;
            foreach (var category in question.revealedCategories)
            {
                string name = category.GetCategoryName(data.Config.evidenceTagLibrary);
                if (IsFamilyCategory(name)) return true;
            }
            return false;
        }

        private bool HasQuestionBeenAsked(InvestigationCase caseData, InvestigationQuestion question)
        {
            if (caseData == null || question == null || caseData.history == null) return false;
            return caseData.history.Any(h => h != null && string.Equals(h.summary, question.questionId, StringComparison.OrdinalIgnoreCase));
        }

        private void RevealQuestion(Order order, InvestigationQuestion question)
        {
            if (order?.investigationCase == null || question == null) return;
            InvestigationCase caseData = order.investigationCase;
            caseData.history.Add(new InvestigationCase.EvidenceRecord { summary = question.questionId });

            if (question.revealedCategories != null)
            {
                foreach (var reveal in question.revealedCategories)
                {
                    string category = reveal.GetCategoryName(data.Config.evidenceTagLibrary);
                    if (string.IsNullOrWhiteSpace(category)) continue;
                    string value = order.monsterData != null ? order.monsterData.GetTagValue(category) : null;
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        caseData.RevealTag(category, value, null);
                    }
                }
            }

            if (question.revealedTraits != null)
            {
                foreach (var trait in question.revealedTraits)
                {
                    if (TraitIsOnOrder(order, trait))
                    {
                        caseData.ConfirmTrait(trait);
                    }
                }
            }

            if (profile.RevealMode == BalanceSimulationProfile.RevealMode.RandomFraction && UnityEngine.Random.value < 0.25f)
            {
                RevealOneRandomTrait(order);
            }
        }

        private void RevealAll(Order order)
        {
            if (order?.monsterData == null || order.investigationCase == null) return;
            foreach (var tag in order.monsterData.evidenceTags)
            {
                if (tag == null) continue;
                order.investigationCase.RevealTag(tag.categoryName, tag.valueName, null);
            }
            RevealAllTraits(order);
        }

        private void RevealFamily(Order order)
        {
            if (order?.monsterData == null || order.investigationCase == null) return;
            foreach (var tag in order.monsterData.evidenceTags)
            {
                if (tag == null || !IsFamilyCategory(tag.categoryName)) continue;
                order.investigationCase.RevealTag(tag.categoryName, tag.valueName, null);
                return;
            }
        }

        private void RevealAllTraits(Order order)
        {
            if (order?.investigationCase?.truthTraits == null) return;
            foreach (var trait in order.investigationCase.truthTraits)
            {
                order.investigationCase.ConfirmTrait(trait);
            }
        }

        private void RevealOneRandomTrait(Order order)
        {
            var traits = order?.investigationCase?.truthTraits;
            if (traits == null || traits.Count == 0) return;
            var hidden = traits.Where(t => t != null && !order.investigationCase.confirmedTraitIds.Contains(t.traitId)).ToList();
            if (hidden.Count == 0) return;
            order.investigationCase.ConfirmTrait(hidden[UnityEngine.Random.Range(0, hidden.Count)]);
        }

        private MonsterData ChooseDeclaredMonster(Order order)
        {
            if (order == null || order.monsterData == null) return null;
            if (profile.RevealMode == BalanceSimulationProfile.RevealMode.All && !HasLearnedBestiary())
            {
                return order.monsterData;
            }

            if (UnityEngine.Random.value <= profile.BlindCorrectMonsterChance)
            {
                return order.monsterData;
            }

            string knownFamily = GetKnownFamily(order);
            if (!string.IsNullOrWhiteSpace(knownFamily))
            {
                var familyPool = data.Monsters.Where(m => m != null && SameText(GetFamily(m), knownFamily)).ToList();
                if (familyPool.Count > 0)
                {
                    return familyPool[UnityEngine.Random.Range(0, familyPool.Count)];
                }
            }

            return data.Monsters[UnityEngine.Random.Range(0, data.Monsters.Count)];
        }

        private List<SimHunter> SelectParty(Order order)
        {
            List<SimHunter> available = hunters.Where(h => h.CanAct).ToList();
            if (available.Count == 0) return new List<SimHunter>();
            int maxParty = Mathf.Min(Mathf.Max(1, profile.MaxPartySize), order.maxPartySize, available.Count);

            if (profile.RevealMode == BalanceSimulationProfile.RevealMode.None)
            {
                return available.OrderBy(_ => UnityEngine.Random.value).Take(maxParty).ToList();
            }

            List<SimHunter> party = new List<SimHunter>();
            List<SimHunter> pool = available
                .OrderByDescending(h => h.Hunter.GetStats() != null ? h.Hunter.GetStats().GetTotalPower() : 0)
                .ToList();

            for (int i = 0; i < pool.Count && party.Count < maxParty; i++)
            {
                party.Add(pool[i]);
                float chance = MissionOutcomeCalculator.EvaluatePreview(order, party.Select(h => h.Hunter).ToList(), BuildMissionConfig()).SuccessChancePercent;
                if (chance >= GetCurrentTargetDispatchSuccessChance())
                {
                    break;
                }
            }

            return party;
        }

        private float CalculatePreviewSuccess(Order order, List<SimHunter> party, float projectedTimeRemainingAfterAccept)
        {
            if (order == null || party == null || party.Count == 0) return 0f;
            float score = MissionOutcomeCalculator.EvaluatePreview(order, party.Select(h => h.Hunter).ToList(), BuildMissionConfig()).SuccessChancePercent;
            if (data.Config.lateDispatchSuccessPenaltyPercent > 0f
                && data.Config.lateDispatchWindowSeconds > 0f
                && projectedTimeRemainingAfterAccept <= data.Config.lateDispatchWindowSeconds)
            {
                score -= data.Config.lateDispatchSuccessPenaltyPercent;
            }
            return Mathf.Clamp(score, 0f, MissionOutcomeCalculator.MaxSuccessChance);
        }

        private bool MeetsDispatchFloor(float predictedSuccess)
        {
            return predictedSuccess >= GetCurrentDispatchFloor();
        }

        private float GetCurrentDispatchFloor()
        {
            float floor = IsEconomicallyDesperate()
                ? profile.DesperateMinimumDispatchSuccessChance
                : profile.MinimumDispatchSuccessChance;
            return Mathf.Clamp(floor, 0f, MissionOutcomeCalculator.MaxSuccessChance);
        }

        private float GetCurrentTargetDispatchSuccessChance()
        {
            float target = profile.TargetDispatchSuccessChance;
            if (IsUnderEconomicPressure())
            {
                target = Mathf.Min(target, profile.EconomicPressureTargetDispatchSuccessChance);
            }
            return Mathf.Clamp(target, 0f, MissionOutcomeCalculator.MaxSuccessChance);
        }

        private bool IsEconomicallyDesperate()
        {
            return gold < GetDailyUpkeep();
        }

        private bool IsUnderEconomicPressure()
        {
            return IsEconomicallyDesperate() || debt > 0 || hunters.Count(h => !h.Dead) <= 2;
        }

        private void TryWaitForSaferParty(Order order, ref float timeRemaining, float keepSeconds, ref List<SimHunter> party, ref float predictedSuccess)
        {
            if (!profile.WaitForHuntersBeforeUnsafeDispatch) return;
            if (activeMissions.Count == 0) return;

            while (!MeetsDispatchFloor(predictedSuccess) && activeMissions.Count > 0 && timeRemaining > keepSeconds)
            {
                float previousTimeRemaining = timeRemaining;
                AdvanceToNextMissionCompletion(ref timeRemaining);
                if (Mathf.Approximately(previousTimeRemaining, timeRemaining))
                {
                    break;
                }

                party = SelectParty(order);
                float projectedDispatchTimeRemaining = Mathf.Max(0f, timeRemaining - data.Config.actionTimeSettings.acceptOrderSeconds);
                predictedSuccess = CalculatePreviewSuccess(order, party, projectedDispatchTimeRemaining);
            }
        }

        private bool ShouldRefer(Order order, float predictedSuccess, float caseQuality)
        {
            if (profile.ReferBelowSuccessChance <= 0f) return false;
            if (profile.RevealMode == BalanceSimulationProfile.RevealMode.None) return false;
            if (caseQuality < profile.MinimumReferralCaseQuality) return false;

            if (profile.ForceReferralForGoodUnsafeCasesUnderPressure
                && IsUnderEconomicPressure()
                && predictedSuccess < GetCurrentDispatchFloor()
                && CalculateReferralFee(order) > 0)
            {
                return true;
            }

            if (predictedSuccess >= profile.ReferBelowSuccessChance) return false;
            if (profile.SendRiskyOrdersWhenBroke && IsEconomicallyDesperate()) return false;

            return UnityEngine.Random.value <= profile.ReferChanceBelowThreshold;
        }

        private Order GenerateOrder()
        {
            DifficultyEntry difficulty = PickDifficulty();
            if (difficulty == null) return null;
            MonsterData monster = PickMonster(difficulty.difficultyValue);
            if (monster == null) return null;

            Order order = new Order
            {
                orderTitle = string.IsNullOrWhiteSpace(monster.displayName) ? "Simulated Order" : $"{monster.displayName} Order",
                description = "Simulated order",
                monsterData = monster,
                difficulty = Mathf.Max(1, difficulty.difficultyValue),
                goldReward = Mathf.Max(0, difficulty.goldReward),
                xpReward = Mathf.Max(0, difficulty.xpReward),
                reputationPointsReward = Mathf.Max(0f, difficulty.reputationPointsReward),
                reputationTier = Mathf.Max(0, difficulty.minReputation),
                missionDuration = Mathf.Max(1f, difficulty.missionTimeSeconds),
                maxPartySize = 3,
                minPartySize = 1,
                state = OrderState.Offered
            };
            order.investigationCase = new InvestigationCase
            {
                truthMonster = monster,
                clientProfile = PickClientProfile(),
                truthTraits = RollMonsterTraits(monster)
            };
            OrderRewardUtility.ApplyTraitRewardScaling(order, data.Config);
            return order;
        }

        private DifficultyEntry PickDifficulty()
        {
            int reputation = GetCurrentReputation();
            List<DifficultyEntry> eligible = data.Difficulty.entries
                .Where(e => e != null && reputation >= e.minReputation && reputation <= e.maxReputation && e.weight > 0)
                .ToList();
            if (eligible.Count == 0) return null;

            return WeightedPick(eligible, e =>
            {
                float weight = Mathf.Max(0f, e.weight);
                int tierDelta = Mathf.Max(0, reputation - e.minReputation);
                if (tierDelta > 0)
                {
                    float decay = Mathf.Pow(Mathf.Clamp01(data.Config.lowerOrderDecay), tierDelta);
                    weight *= Mathf.Max(Mathf.Clamp01(data.Config.minOldOrderMultiplier), decay);
                }
                else if (e.minReputation == reputation)
                {
                    weight *= Mathf.Max(0f, data.Config.currentTierOrderMultiplier);
                }
                return weight;
            });
        }

        private MonsterData PickMonster(int difficultyValue)
        {
            int reputation = GetCurrentReputation();
            List<MonsterData> pool = data.Monsters
                .Where(m => m != null && reputation >= m.requiredReputation && difficultyValue >= m.minimumDifficulty)
                .ToList();
            if (pool.Count == 0)
            {
                pool = data.Monsters.Where(m => m != null && reputation >= m.requiredReputation).ToList();
            }
            if (pool.Count == 0) return null;

            return WeightedPick(pool, m => Mathf.Max(1, m.weight) * m.GetDifficultySelectionMultiplier(difficultyValue));
        }

        private List<MonsterTrait> RollMonsterTraits(MonsterData monster)
        {
            List<MonsterTrait> resultTraits = new List<MonsterTrait>();
            if (monster == null || monster.possibleTraits == null || monster.possibleTraits.Count == 0) return resultTraits;

            int min = Mathf.Max(0, monster.traitCountRange.x);
            int max = Mathf.Max(min, monster.traitCountRange.y);
            int count = data.Config.RollTraitCount(min, max);
            List<MonsterTrait> pool = monster.possibleTraits.Where(t => t != null).OrderBy(_ => UnityEngine.Random.value).ToList();
            for (int i = 0; i < count && i < pool.Count; i++)
            {
                resultTraits.Add(pool[i]);
            }
            return resultTraits;
        }

        private ClientProfile PickClientProfile()
        {
            if (data.Config.defaultClientProfiles == null || data.Config.defaultClientProfiles.Count == 0) return null;
            var pool = data.Config.defaultClientProfiles.Where(p => p != null).ToList();
            return pool.Count > 0 ? pool[UnityEngine.Random.Range(0, pool.Count)] : null;
        }

        private void AdvanceTime(ref float timeRemaining, float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            float actual = Mathf.Min(timeRemaining, seconds);
            timeRemaining = Mathf.Max(0f, timeRemaining - actual);
            foreach (var mission in activeMissions)
            {
                mission.RemainingSeconds -= actual;
            }
            ResolveCompletedMissions();
        }

        private void AdvanceToNextMissionCompletion(ref float timeRemaining)
        {
            if (activeMissions.Count == 0 || timeRemaining <= 0f) return;
            float next = Mathf.Max(0f, activeMissions.Min(m => m.RemainingSeconds));
            if (next <= 0f) next = 1f;
            AdvanceTime(ref timeRemaining, next);
        }

        private void ResolveCompletedMissions()
        {
            for (int i = activeMissions.Count - 1; i >= 0; i--)
            {
                if (activeMissions[i].RemainingSeconds > 0f) continue;
                ResolveMission(activeMissions[i]);
                activeMissions.RemoveAt(i);
            }
        }

        private void ResolveAllActiveMissions()
        {
            for (int i = activeMissions.Count - 1; i >= 0; i--)
            {
                ResolveMission(activeMissions[i]);
                activeMissions.RemoveAt(i);
            }
        }

        private void ResolveMission(SimMission mission)
        {
            List<Hunter> party = mission.Party.Select(h => h.Hunter).ToList();
            MissionOutcomeResult outcome = MissionOutcomeCalculator.Evaluate(mission.Order, party, BuildMissionConfig());
            bool success = outcome.GuaranteedSuccessFromChance || UnityEngine.Random.Range(0f, 100f) < Mathf.Clamp(outcome.SuccessRollThreshold, 0f, 100f);
            int deaths = 0;
            int wounds = 0;

            foreach (var simHunter in mission.Party)
            {
                simHunter.OnMission = false;
                if (simHunter.Dead) continue;

                bool wasWoundedBeforeMission = simHunter.Wounded;
                bool injured = false;
                if (!success && outcome.FinalInjuryChance > 0f)
                {
                    injured = RollNegative(outcome.FinalInjuryChance, HasRerollNegativeRolls(simHunter.Hunter));
                }
                else if (outcome.InjuriesGuaranteed && !outcome.InjuryPreventionActive)
                {
                    injured = true;
                }
                else if (!outcome.InjuryProtectionFromSuccess && !outcome.InjuryPreventionActive)
                {
                    injured = RollNegative(outcome.FinalInjuryChance, HasRerollNegativeRolls(simHunter.Hunter));
                }
                injured = wasWoundedBeforeMission || injured;

                bool canDie = !outcome.DeathProtectionFromSuccess && !outcome.DeathPreventionActive && (outcome.AllowDeathWithoutInjury || wasWoundedBeforeMission);
                bool died = canDie && RollNegative(outcome.FinalDeathChance, HasRerollNegativeRolls(simHunter.Hunter));
                if (died)
                {
                    simHunter.Dead = true;
                    if (!outcome.AllowDeathWithoutInjury)
                    {
                        injured = false;
                    }
                    deaths++;
                    continue;
                }

                simHunter.Wounded = injured;
                if (injured) wounds++;

                int xp = success
                    ? Mathf.Max(0, Mathf.RoundToInt(mission.Order.xpReward + Mathf.Max(0f, outcome.AdditionalSuccessXP)))
                    : Mathf.Max(0, mission.Order.xpReward / 2);
                simHunter.Hunter.GainXP(xp);
                result.TotalXpGranted += xp;
            }

            int goldEarned = success ? mission.Order.goldReward : mission.Order.goldReward / 2;
            AddGold(goldEarned);
            ApplyTrustAndReputation(mission.Order, success, deaths, wounds);

            if (success) result.OrdersCompleted++;
            else result.OrdersFailed++;
            result.Wounds += wounds;
            result.Deaths += deaths;
        }

        private void ApplyTrustAndReputation(Order order, bool success, int deaths, int wounds)
        {
            if (order == null) return;
            float baseRep = Mathf.Max(0f, order.reputationPointsReward);
            bool clean = success && SameMonster(order.declaredMonster, order.monsterData) && deaths <= 0 && wounds <= 0;
            bool eligible = order.reputationTier >= Mathf.Max(0, GetCurrentReputation() - Mathf.Max(0, data.Config.trustEligibleTierBelowCurrentReputation));
            int trustForReward = deaths > 0 ? 0 : ClampTrust(trustStreak);
            float qualityMultiplier = success
                ? (clean ? Mathf.Clamp01(data.Config.cleanSuccessReputationMultiplier) : Mathf.Clamp01(data.Config.messySuccessReputationMultiplier))
                : Mathf.Clamp01(data.Config.failureReputationMultiplier);
            float earned = baseRep * qualityMultiplier * (1f + trustForReward * Mathf.Max(0f, data.Config.trustReputationBonusPerStreak));
            result.ReputationEarned += Mathf.Max(0f, earned);

            if (deaths > 0)
            {
                trustStreak = 0;
            }
            else if (success)
            {
                if (clean && eligible)
                {
                    result.CleanSuccesses++;
                    trustStreak = ClampTrust(trustStreak + Mathf.Max(0, data.Config.cleanSuccessTrustGain));
                }
                else
                {
                    result.MessySuccesses++;
                }
            }
            else
            {
                trustStreak = data.Config.resetTrustOnFailedOrder
                    ? 0
                    : ClampTrust(trustStreak - Mathf.Max(0, data.Config.failedOrderTrustLoss));
            }

            reputationPoints += Mathf.Max(0f, earned);
        }

        private void RecordEndOfDayReputationProgress()
        {
            int rank = GetCurrentReputation();
            result.EndOfDayRepRanks.Add(rank);
            result.EndOfDayReputationPoints.Add(reputationPoints);

            if (rank >= 2 && result.DayReachedRep2 <= 0)
            {
                result.DayReachedRep2 = currentDay;
            }
            if (rank >= 3 && result.DayReachedRep3 <= 0)
            {
                result.DayReachedRep3 = currentDay;
            }
            if (rank >= 4 && result.DayReachedRep4 <= 0)
            {
                result.DayReachedRep4 = currentDay;
            }
        }

        private int ClampTrust(int value)
        {
            return Mathf.Clamp(value, 0, data.Config != null ? Mathf.Max(0, data.Config.maxTrustStreak) : 5);
        }

        private void AddGold(int amount)
        {
            if (amount <= 0) return;
            currentDayGrossIncome += amount;
            result.TotalIncome += amount;
            if (debt > 0)
            {
                int paid = Mathf.Min(debt, amount);
                debt -= paid;
                amount -= paid;
            }
            gold += amount;
        }

        private int CalculateReferralFee(Order order)
        {
            float fee = order.goldReward * Mathf.Clamp01(data.Config.referralRate) * CalculateReferralCaseQuality(order) * GetDailyReferralMultiplier();
            return Mathf.Max(0, Mathf.RoundToInt(fee));
        }

        private void ApplyReferralReputation(Order order)
        {
            if (order == null || data.Config == null) return;
            float earned = Mathf.Max(0f, order.reputationPointsReward) * CalculateReferralCaseQuality(order) * Mathf.Clamp01(data.Config.referralReputationMultiplier);
            if (earned <= 0f) return;
            result.ReputationEarned += earned;
            reputationPoints += earned;
        }

        private float CalculateReferralCaseQuality(Order order)
        {
            if (order == null || order.monsterData == null || order.declaredMonster == null) return 0f;
            if (!SameText(GetFamily(order.monsterData), GetFamily(order.declaredMonster))) return 0f;

            bool correctMonster = SameMonster(order.declaredMonster, order.monsterData);
            int actualTraitCount = order.investigationCase?.truthTraits?.Count(t => t != null) ?? 0;
            if (actualTraitCount <= 0)
            {
                return Mathf.Clamp01(0.45f + (correctMonster ? 0.55f : 0f));
            }

            int revealedTraits = CountRevealedCorrectTraits(order);
            return Mathf.Clamp01(0.40f + (correctMonster ? 0.35f : 0f) + 0.25f * (revealedTraits / (float)actualTraitCount));
        }

        private float GetDailyReferralMultiplier()
        {
            return Mathf.Max(0.25f, 1f - 0.2f * Mathf.Max(0, referralsToday));
        }

        private int CountRevealedCorrectTraits(Order order)
        {
            var caseData = order?.investigationCase;
            if (caseData?.truthTraits == null || caseData.confirmedTraitIds == null) return 0;
            HashSet<string> truth = new HashSet<string>(caseData.truthTraits.Where(t => t != null).Select(t => t.traitId), StringComparer.OrdinalIgnoreCase);
            return caseData.confirmedTraitIds.Count(id => !string.IsNullOrWhiteSpace(id) && truth.Contains(id));
        }

        private float GetKnownTraitRatio(Order order)
        {
            int count = order?.investigationCase?.truthTraits?.Count(t => t != null) ?? 0;
            if (count <= 0) return 1f;
            return CountRevealedCorrectTraits(order) / (float)count;
        }

        private void TryMaintainRoster(ref float timeRemaining)
        {
            int aliveCount = hunters.Count(h => !h.Dead);
            if (aliveCount >= profile.TargetRosterSize || aliveCount >= profile.MaxRosterSize) return;
            if (unpaidUpkeepStreak >= 2) return;

            int postingCost = data.Config.hunterConfig != null ? data.Config.hunterConfig.GetBasePostingFee() : 0;
            if (gold < postingCost) return;
            gold -= postingCost;
            result.TotalHiringSpend += postingCost;
            AdvanceTime(ref timeRemaining, data.Config.actionTimeSettings.postAdSeconds);
            TryHireRandomHunter(free: true);
        }

        private int GetDailyUpkeep()
        {
            return hunters.Where(h => !h.Dead).Sum(h => h.Hunter.GetUpkeepCost());
        }

        private bool TryHireRandomHunter(bool free)
        {
            int reputation = GetCurrentReputation();
            List<HunterData> candidates = data.Hunters
                .Where(h => h != null && h.minReputation <= reputation && !usedHunterIds.Contains(h.hunterId))
                .ToList();
            if (candidates.Count == 0) return false;

            HunterData picked = WeightedPick(candidates, h =>
            {
                float weight = 1f;
                GlobalHunterConfig.RarityEntry rarity = data.Config.hunterConfig != null ? data.Config.hunterConfig.GetRarity(h.rarity) : null;
                if (rarity != null) weight = Mathf.Max(0.01f, rarity.recruitmentWeight);
                int tierDelta = Mathf.Max(0, reputation - h.minReputation);
                if (tierDelta > 0)
                {
                    weight *= Mathf.Pow(Mathf.Clamp01(data.Config.lowerRecruitDecay), tierDelta);
                }
                return weight;
            });

            GameObject go = new GameObject($"SimHunter_{picked.hunterName}");
            go.hideFlags = HideFlags.HideAndDontSave;
            TemporaryHunterObjects.Add(go);
            Hunter hunter = go.AddComponent<Hunter>();
            hunter.InitializeForSimulation(picked);
            hunters.Add(new SimHunter(hunter));
            usedHunterIds.Add(picked.hunterId);
            result.HuntersHired++;
            return true;
        }

        private void TryLevelHunters(ref float timeRemaining)
        {
            if (!profile.AutoLevelHunters) return;
            int reputation = GetCurrentReputation();
            foreach (var hunter in hunters.Where(h => h.CanAct).OrderBy(h => h.Hunter.GetLevel()).ToList())
            {
                int nextLevel = hunter.Hunter.GetLevel() + 1;
                if (hunter.Hunter.IsAtMaxLevel()) continue;
                if (!hunter.Hunter.HasEnoughXPForNextLevel()) continue;
                if (hunter.Hunter.Data.GetRequiredReputationForLevel(nextLevel) > reputation) continue;
                int cost = hunter.Hunter.GetLevelUpCost();
                if (gold < cost) continue;
                gold -= cost;
                result.TotalLevelUpSpend += cost;
                int xpAfter = Mathf.Max(0, hunter.Hunter.GetXP() - hunter.Hunter.GetXPToNextLevel());
                hunter.Hunter.DebugSetLevelAndXP(nextLevel, xpAfter);
                result.LevelUpsBought++;
                AdvanceTime(ref timeRemaining, data.Config.actionTimeSettings.levelUpSeconds);
            }
        }

        private void DismissHuntersUntilUpkeepFits(int targetUpkeep, int minimumHunters)
        {
            while (hunters.Count(h => !h.Dead) > minimumHunters && hunters.Where(h => !h.Dead).Sum(h => h.Hunter.GetUpkeepCost()) > targetUpkeep)
            {
                var dismiss = hunters
                    .Where(h => !h.Dead)
                    .OrderByDescending(h => h.Hunter.GetUpkeepCost())
                    .FirstOrDefault();
                if (dismiss == null) return;
                dismiss.Dead = true;
                result.HuntersDismissed++;
            }
        }

        private void HealSurvivorsOvernight()
        {
            foreach (var hunter in hunters)
            {
                hunter.Wounded = false;
                hunter.OnMission = false;
            }
        }

        private MissionOutcomeConfig BuildMissionConfig()
        {
            MissionOutcomeConfig config = MissionOutcomeConfig.FromGameConfig(data.Config);
            config.debtSuccessPenaltyPercent = Mathf.Max(0f, debtSuccessPenalty);
            return config;
        }

        private int GetCurrentReputation()
        {
            int level = 0;
            if (data.Config.orderLimitByReputation == null) return level;
            foreach (var tier in data.Config.orderLimitByReputation)
            {
                if (tier == null) continue;
                if (reputationPoints >= Mathf.Max(0, tier.requiredReputationPoints))
                {
                    level = Mathf.Max(level, tier.requiredReputation);
                }
            }
            return level;
        }

        private void LoseReputationPointsPercent(float percent)
        {
            percent = Mathf.Clamp(percent, 0f, 100f);
            if (percent <= 0f || reputationPoints <= 0f) return;
            reputationPoints = Mathf.Max(0f, reputationPoints - reputationPoints * (percent / 100f));
        }

        private bool TraitIsOnOrder(Order order, MonsterTrait trait)
        {
            if (order?.investigationCase?.truthTraits == null || trait == null) return false;
            return order.investigationCase.truthTraits.Any(t => t != null && (t == trait || string.Equals(t.traitId, trait.traitId, StringComparison.OrdinalIgnoreCase)));
        }

        private bool HasRerollNegativeRolls(Hunter hunter)
        {
            var traits = hunter?.Data?.traits;
            if (traits == null) return false;
            foreach (var trait in traits)
            {
                if (trait == null || trait.bonusEffects == null) continue;
                if (trait.bonusEffects.Any(e => e != null && e.bonusType == HunterTrait.BonusEffectType.RerollNegativeRolls))
                {
                    return true;
                }
            }
            return false;
        }

        private bool RollNegative(float chance01, bool rerollOnce)
        {
            float roll = UnityEngine.Random.value;
            if (rerollOnce)
            {
                roll = Mathf.Max(roll, UnityEngine.Random.value);
            }
            return roll < Mathf.Clamp01(chance01);
        }

        private string GetKnownFamily(Order order)
        {
            var known = order?.investigationCase?.knownTags;
            if (known == null) return null;
            foreach (var tag in known)
            {
                if (tag != null && IsFamilyCategory(tag.categoryName))
                {
                    return tag.valueName;
                }
            }
            return null;
        }

        private string GetFamily(MonsterData monster)
        {
            if (monster == null || monster.evidenceTags == null) return null;
            var tag = monster.evidenceTags.FirstOrDefault(t => t != null && IsFamilyCategory(t.categoryName));
            return tag?.valueName;
        }

        private bool IsFamilyCategory(string category)
        {
            return !string.IsNullOrWhiteSpace(category) && category.IndexOf("family", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool SameMonster(MonsterData a, MonsterData b)
        {
            if (a == null || b == null) return false;
            if (!string.IsNullOrWhiteSpace(a.monsterId) && !string.IsNullOrWhiteSpace(b.monsterId))
            {
                return string.Equals(a.monsterId, b.monsterId, StringComparison.OrdinalIgnoreCase);
            }
            return a == b;
        }

        private bool SameText(string a, string b)
        {
            return !string.IsNullOrWhiteSpace(a) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private T WeightedPick<T>(List<T> items, Func<T, float> weightFunc)
        {
            float total = 0f;
            foreach (var item in items)
            {
                total += Mathf.Max(0f, weightFunc(item));
            }

            if (total <= 0f)
            {
                return items[UnityEngine.Random.Range(0, items.Count)];
            }

            float roll = UnityEngine.Random.Range(0f, total);
            foreach (var item in items)
            {
                roll -= Mathf.Max(0f, weightFunc(item));
                if (roll <= 0f) return item;
            }
            return items[items.Count - 1];
        }
    }

    private sealed class SimHunter
    {
        public readonly Hunter Hunter;
        public bool Dead;
        public bool Wounded;
        public bool OnMission;

        public SimHunter(Hunter hunter)
        {
            Hunter = hunter;
        }

        public bool CanAct => Hunter != null && !Dead && !Wounded && !OnMission;
    }

    private sealed class SimMission
    {
        public readonly Order Order;
        public readonly List<SimHunter> Party;
        public float RemainingSeconds;

        public SimMission(Order order, List<SimHunter> party, float duration)
        {
            Order = order;
            Party = party;
            RemainingSeconds = duration;
        }
    }

    private sealed class SimulationData
    {
        public GameConfig Config;
        public DifficultyTable Difficulty;
        public List<MonsterData> Monsters = new List<MonsterData>();
        public List<HunterData> Hunters = new List<HunterData>();
        public List<InvestigationQuestion> Questions = new List<InvestigationQuestion>();
        public string Error;
        public bool IsValid => string.IsNullOrEmpty(Error);

        public static SimulationData Load(BalanceSimulationSettings settings)
        {
            SimulationData data = new SimulationData();
            data.Config = settings != null && settings.gameConfig != null
                ? settings.gameConfig
                : LoadFirstAsset<GameConfig>();
            data.Difficulty = settings != null && settings.difficultyTable != null
                ? settings.difficultyTable
                : LoadFirstAsset<DifficultyTable>();
            data.Hunters = settings != null && settings.hunterPool != null && settings.hunterPool.Count > 0
                ? settings.hunterPool.Where(h => h != null).ToList()
                : LoadAssets<HunterData>();

            if (data.Config != null && data.Config.monsterLibrary != null)
            {
                data.Monsters = data.Config.monsterLibrary.GetMonsters().Where(m => m != null).ToList();
            }
            if (data.Monsters.Count == 0)
            {
                data.Monsters = LoadAssets<MonsterData>();
            }

            if (data.Config != null && data.Config.defaultInvestigationQuestions != null && data.Config.defaultInvestigationQuestions.Count > 0)
            {
                data.Questions = data.Config.defaultInvestigationQuestions.Where(q => q != null).ToList();
            }
            if (data.Questions.Count == 0)
            {
                data.Questions = LoadAssets<InvestigationQuestion>();
            }

            if (data.Config == null) data.Error = "Balance simulation cannot run: no GameConfig asset found.";
            else if (data.Difficulty == null || data.Difficulty.entries == null || data.Difficulty.entries.Count == 0) data.Error = "Balance simulation cannot run: no DifficultyTable with entries found.";
            else if (data.Monsters.Count == 0) data.Error = "Balance simulation cannot run: no MonsterData assets found.";
            else if (data.Hunters.Count == 0) data.Error = "Balance simulation cannot run: no HunterData assets found.";
            return data;
        }

        private static T LoadFirstAsset<T>() where T : UnityEngine.Object
        {
            return LoadAssets<T>().FirstOrDefault();
        }

        private static List<T> LoadAssets<T>() where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            List<T> assets = new List<T>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    assets.Add(asset);
                }
            }
            return assets;
        }
    }

    private sealed class ProfileSpec
    {
        public string Name;
        public BalanceSimulationProfile.RevealMode RevealMode;
        public float QuestionRevealFraction;
        public int LearnedBestiaryAtReputation;
        public float LearnedQuestionRevealFraction;
        public float BlindCorrectMonsterChance;
        public float ReferBelowSuccessChance;
        public float MinimumReferralCaseQuality;
        public float ReferChanceBelowThreshold;
        public bool SendRiskyOrdersWhenBroke;
        public float TargetDispatchSuccessChance;
        public float EconomicPressureTargetDispatchSuccessChance;
        public float MinimumDispatchSuccessChance;
        public float DesperateMinimumDispatchSuccessChance;
        public bool WaitForHuntersBeforeUnsafeDispatch;
        public bool ForceReferralForGoodUnsafeCasesUnderPressure;
        public int MaxPartySize = 3;
        public int TargetRosterSize = 3;
        public int MaxRosterSize = 6;
        public bool AutoLevelHunters;

        public static ProfileSpec FromAsset(BalanceSimulationProfile asset)
        {
            return new ProfileSpec
            {
                Name = string.IsNullOrWhiteSpace(asset.profileName) ? asset.name : asset.profileName,
                RevealMode = asset.revealMode,
                QuestionRevealFraction = asset.questionRevealFraction,
                LearnedBestiaryAtReputation = asset.learnedBestiaryAtReputation,
                LearnedQuestionRevealFraction = asset.learnedQuestionRevealFraction,
                BlindCorrectMonsterChance = asset.blindCorrectMonsterChance,
                ReferBelowSuccessChance = asset.referBelowSuccessChance,
                MinimumReferralCaseQuality = asset.minimumReferralCaseQuality,
                ReferChanceBelowThreshold = asset.referChanceBelowThreshold,
                SendRiskyOrdersWhenBroke = asset.sendRiskyOrdersWhenBroke,
                TargetDispatchSuccessChance = asset.targetDispatchSuccessChance,
                EconomicPressureTargetDispatchSuccessChance = asset.economicPressureTargetDispatchSuccessChance,
                MinimumDispatchSuccessChance = asset.minimumDispatchSuccessChance,
                DesperateMinimumDispatchSuccessChance = asset.desperateMinimumDispatchSuccessChance,
                WaitForHuntersBeforeUnsafeDispatch = asset.waitForHuntersBeforeUnsafeDispatch,
                ForceReferralForGoodUnsafeCasesUnderPressure = asset.forceReferralForGoodUnsafeCasesUnderPressure,
                MaxPartySize = Mathf.Max(1, asset.maxPartySize),
                TargetRosterSize = Mathf.Max(1, asset.targetRosterSize),
                MaxRosterSize = Mathf.Max(1, asset.maxRosterSize),
                AutoLevelHunters = asset.autoLevelHunters
            };
        }
    }

    private sealed class SessionResult
    {
        public string ProfileName;
        public int SessionIndex;
        public int Seed;
        public int DaysPlayed;
        public bool GameOver;
        public int FinalGold;
        public int FinalDebt;
        public int FinalReputationRank;
        public float FinalReputationPoints;
        public int TrustStreak;
        public int DayReachedRep2;
        public int DayReachedRep3;
        public int DayReachedRep4;
        public List<int> EndOfDayRepRanks = new List<int>();
        public List<float> EndOfDayReputationPoints = new List<float>();
        public int TotalIncome;
        public int TotalUpkeep;
        public int TotalLevelUpSpend;
        public int TotalHiringSpend;
        public int TotalSurplus;
        public int TotalXpGranted;
        public int LevelUpsBought;
        public float ReputationEarned;
        public int OrdersGenerated;
        public int OrdersSent;
        public int OrdersCompleted;
        public int CleanSuccesses;
        public int MessySuccesses;
        public int OrdersFailed;
        public int OrdersReferred;
        public int OrdersDeclined;
        public int Wounds;
        public int Deaths;
        public int HuntersHired;
        public int HuntersDismissed;
        public int EndingHunters;
        public float AverageEndingHunterLevel;
        public float SuccessChanceTotal;
        public float PartySizeTotal;
        public float PartyPowerTotal;
        public float RequiredPowerTotal;
        public float KnownTraitRatioTotal;
        public float CaseQualityTotal;
        public float AverageSuccessChance;
        public float AveragePartySize;
        public float AveragePartyPower;
        public float AverageRequiredPower;
        public float AverageKnownTraitRatio;
        public float AverageCaseQuality;
    }
}
