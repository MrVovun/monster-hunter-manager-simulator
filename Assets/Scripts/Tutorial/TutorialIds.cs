public static class TutorialIds
{
    public const string TalkHunter = "TalkHunter";
    public const string WarTable = "WarTable";
    public const string OrdersTab = "OrdersTab";
    public const string HuntersTab = "HuntersTab";
    public const string HiringTab = "HiringTab";
    public const string RingClientBell = "RingClientBell";
    public const string TalkClient = "TalkClient";
    public const string DialogueQuestions = "DialogueQuestions";
    public const string SelectMonster = "SelectMonster";
    public const string AcceptOrder = "AcceptOrder";
    public const string DeclineOrder = "DeclineOrder";
    public const string ReferOrder = "ReferOrder";
    public const string AssignHunter = "AssignHunter";
    public const string SendParty = "SendParty";
    public const string PassTime = "PassTime";
    public const string CloseMissionReport = "CloseMissionReport";
    public const string PostHiringAd = "PostHiringAd";
    public const string HireCandidate = "HireCandidate";
    public const string DeclineCandidate = "DeclineCandidate";

    public const string EventHunterDialogueOpened = "HunterDialogueOpened";
    public const string EventHunterDialogueClosed = "HunterDialogueClosed";
    public const string EventWarTableOpened = "WarTableOpened";
    public const string EventOrdersTabOpened = "OrdersTabOpened";
    public const string EventHuntersTabOpened = "HuntersTabOpened";
    public const string EventHiringTabOpened = "HiringTabOpened";
    public const string EventClientBellRung = "ClientBellRung";
    public const string EventClientArrived = "ClientArrived";
    public const string EventClientDialogueOpened = "ClientDialogueOpened";
    public const string EventClientQuestionAnswered = "ClientQuestionAnswered";
    public const string EventAllClientQuestionsAsked = "AllClientQuestionsAsked";
    public const string EventMonsterSelected = "MonsterSelected";
    public const string EventOrderAccepted = "OrderAccepted";
    public const string EventHunterAssignedToOrder = "HunterAssignedToOrder";
    public const string EventMissionStarted = "MissionStarted";
    public const string EventPassTimeConfirmed = "PassTimeConfirmed";
    public const string EventMissionReportClosed = "MissionReportClosed";
    public const string EventHiringAdPosted = "HiringAdPosted";
}

public enum TutorialActionKey
{
    None,
    TalkHunter,
    WarTable,
    OrdersTab,
    HuntersTab,
    HiringTab,
    RingClientBell,
    TalkClient,
    DialogueQuestions,
    SelectMonster,
    AcceptOrder,
    DeclineOrder,
    ReferOrder,
    AssignHunter,
    SendParty,
    PassTime,
    CloseMissionReport,
    PostHiringAd,
    HireCandidate,
    DeclineCandidate
}

public enum TutorialEventKey
{
    None,
    HunterDialogueOpened,
    HunterDialogueClosed,
    WarTableOpened,
    OrdersTabOpened,
    HuntersTabOpened,
    HiringTabOpened,
    ClientBellRung,
    ClientArrived,
    ClientDialogueOpened,
    ClientQuestionAnswered,
    AllClientQuestionsAsked,
    MonsterSelected,
    OrderAccepted,
    HunterAssignedToOrder,
    MissionStarted,
    PassTimeConfirmed,
    MissionReportClosed,
    HiringAdPosted
}

public static class TutorialKeyUtility
{
    public static string ToId(TutorialActionKey key)
    {
        switch (key)
        {
            case TutorialActionKey.TalkHunter:
                return TutorialIds.TalkHunter;
            case TutorialActionKey.WarTable:
                return TutorialIds.WarTable;
            case TutorialActionKey.OrdersTab:
                return TutorialIds.OrdersTab;
            case TutorialActionKey.HuntersTab:
                return TutorialIds.HuntersTab;
            case TutorialActionKey.HiringTab:
                return TutorialIds.HiringTab;
            case TutorialActionKey.RingClientBell:
                return TutorialIds.RingClientBell;
            case TutorialActionKey.TalkClient:
                return TutorialIds.TalkClient;
            case TutorialActionKey.DialogueQuestions:
                return TutorialIds.DialogueQuestions;
            case TutorialActionKey.SelectMonster:
                return TutorialIds.SelectMonster;
            case TutorialActionKey.AcceptOrder:
                return TutorialIds.AcceptOrder;
            case TutorialActionKey.DeclineOrder:
                return TutorialIds.DeclineOrder;
            case TutorialActionKey.ReferOrder:
                return TutorialIds.ReferOrder;
            case TutorialActionKey.AssignHunter:
                return TutorialIds.AssignHunter;
            case TutorialActionKey.SendParty:
                return TutorialIds.SendParty;
            case TutorialActionKey.PassTime:
                return TutorialIds.PassTime;
            case TutorialActionKey.CloseMissionReport:
                return TutorialIds.CloseMissionReport;
            case TutorialActionKey.PostHiringAd:
                return TutorialIds.PostHiringAd;
            case TutorialActionKey.HireCandidate:
                return TutorialIds.HireCandidate;
            case TutorialActionKey.DeclineCandidate:
                return TutorialIds.DeclineCandidate;
            default:
                return string.Empty;
        }
    }

    public static string ToId(TutorialEventKey key)
    {
        switch (key)
        {
            case TutorialEventKey.HunterDialogueOpened:
                return TutorialIds.EventHunterDialogueOpened;
            case TutorialEventKey.HunterDialogueClosed:
                return TutorialIds.EventHunterDialogueClosed;
            case TutorialEventKey.WarTableOpened:
                return TutorialIds.EventWarTableOpened;
            case TutorialEventKey.OrdersTabOpened:
                return TutorialIds.EventOrdersTabOpened;
            case TutorialEventKey.HuntersTabOpened:
                return TutorialIds.EventHuntersTabOpened;
            case TutorialEventKey.HiringTabOpened:
                return TutorialIds.EventHiringTabOpened;
            case TutorialEventKey.ClientBellRung:
                return TutorialIds.EventClientBellRung;
            case TutorialEventKey.ClientArrived:
                return TutorialIds.EventClientArrived;
            case TutorialEventKey.ClientDialogueOpened:
                return TutorialIds.EventClientDialogueOpened;
            case TutorialEventKey.ClientQuestionAnswered:
                return TutorialIds.EventClientQuestionAnswered;
            case TutorialEventKey.AllClientQuestionsAsked:
                return TutorialIds.EventAllClientQuestionsAsked;
            case TutorialEventKey.MonsterSelected:
                return TutorialIds.EventMonsterSelected;
            case TutorialEventKey.OrderAccepted:
                return TutorialIds.EventOrderAccepted;
            case TutorialEventKey.HunterAssignedToOrder:
                return TutorialIds.EventHunterAssignedToOrder;
            case TutorialEventKey.MissionStarted:
                return TutorialIds.EventMissionStarted;
            case TutorialEventKey.PassTimeConfirmed:
                return TutorialIds.EventPassTimeConfirmed;
            case TutorialEventKey.MissionReportClosed:
                return TutorialIds.EventMissionReportClosed;
            case TutorialEventKey.HiringAdPosted:
                return TutorialIds.EventHiringAdPosted;
            default:
                return string.Empty;
        }
    }
}
