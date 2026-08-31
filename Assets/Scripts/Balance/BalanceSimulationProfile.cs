using UnityEngine;

[CreateAssetMenu(fileName = "BalanceSimulationProfile", menuName = "Guild Manager/Balance Simulation Profile")]
public class BalanceSimulationProfile : ScriptableObject
{
    public enum RevealMode
    {
        All,
        RandomFraction,
        TraitsAndFamily,
        None
    }

    [Header("Identity")]
    public string profileName = "Simulation Profile";
    public RevealMode revealMode = RevealMode.RandomFraction;

    [Header("Investigation")]
    [Range(0f, 1f)] public float questionRevealFraction = 0.5f;
    [Tooltip("After this reputation rank, this profile uses Learned Question Reveal Fraction instead.")]
    public int learnedBestiaryAtReputation = 3;
    [Range(0f, 1f)] public float learnedQuestionRevealFraction = 0.5f;
    [Tooltip("Chance to select the correct monster when the profile has not revealed enough information.")]
    [Range(0f, 1f)] public float blindCorrectMonsterChance = 0.15f;

    [Header("Order Decisions")]
    [Tooltip("Refer orders below this predicted success chance.")]
    [Range(0f, 200f)] public float referBelowSuccessChance = 70f;
    [Tooltip("Only refer when the documented case quality is at least this value.")]
    [Range(0f, 1f)] public float minimumReferralCaseQuality = 0.45f;
    [Tooltip("Chance to refer when success is below the threshold and the case is good enough.")]
    [Range(0f, 1f)] public float referChanceBelowThreshold = 0.5f;
    [Tooltip("If current gold is below today's upkeep, this profile takes risky orders instead of referring.")]
    public bool sendRiskyOrdersWhenBroke = true;
    [Tooltip("Try to create a party that reaches at least this predicted success chance.")]
    [Range(0f, 200f)] public float targetDispatchSuccessChance = 100f;
    [Tooltip("Lower target used when gold is low, debt exists, or the active roster is small.")]
    [Range(0f, 200f)] public float economicPressureTargetDispatchSuccessChance = 120f;
    [Tooltip("If the selected party is below this projected score, wait/refer/decline instead of sending.")]
    [Range(0f, 200f)] public float minimumDispatchSuccessChance = 80f;
    [Tooltip("Lower projected score floor used when current gold is below today's upkeep.")]
    [Range(0f, 200f)] public float desperateMinimumDispatchSuccessChance = 50f;
    [Tooltip("If true, the simulator can pass time for a current mission to finish before deciding an order is too risky.")]
    public bool waitForHuntersBeforeUnsafeDispatch = true;
    [Tooltip("If true, high-quality unsafe cases are referred instead of declined while under economic pressure.")]
    public bool forceReferralForGoodUnsafeCasesUnderPressure = true;
    [Tooltip("Maximum hunters assigned to one order.")]
    [Min(1)] public int maxPartySize = 3;

    [Header("Roster")]
    [Tooltip("Minimum active hunters this profile tries to maintain.")]
    [Min(1)] public int targetRosterSize = 3;
    [Tooltip("Maximum active hunters this profile allows itself to maintain.")]
    [Min(1)] public int maxRosterSize = 6;
    [Tooltip("If true, ready hunters are leveled whenever affordable and reputation allows it.")]
    public bool autoLevelHunters = true;
}
