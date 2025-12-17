using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "Guild Manager/Game Config")]
public class GameConfig : ScriptableObject
{
    [Header("Core Data")]
    public GlobalHunterConfig hunterConfig;
    public MonsterLibrary monsterLibrary;
    public EvidenceTagLibrary evidenceTagLibrary;
    public List<InvestigationQuestion> defaultInvestigationQuestions = new List<InvestigationQuestion>();
    public List<ClientProfile> defaultClientProfiles = new List<ClientProfile>();

    [Header("Time")]
    [Tooltip("Length of an in-game day in real-time seconds.")]
    public float dayLengthSeconds = 600f;

    [Tooltip("If true, investigation UI pauses global time (accessibility/testing).")]
    public bool allowInvestigationPauseToggle = true;

    [Header("Order Limits")]
    public List<OrderLimitTier> orderLimitByReputation = new List<OrderLimitTier>()
    {
        new OrderLimitTier{ requiredReputation = 0, orderLimit = 3 },
        new OrderLimitTier{ requiredReputation = 50, orderLimit = 4 },
        new OrderLimitTier{ requiredReputation = 100, orderLimit = 5 },
    };

    [Header("Mission Balance")]
    [Range(0f, 1f)] public float baseInjuryChance = 0.2f;
    [Range(0f, 1f)] public float baseDeathChance = 0.05f;

    public int GetOrderLimit(int reputation)
    {
        int limit = 0;
        foreach (var tier in orderLimitByReputation)
        {
            if (reputation >= tier.requiredReputation)
            {
                limit = Mathf.Max(limit, tier.orderLimit);
            }
        }
        return limit;
    }

    [Serializable]
    public class OrderLimitTier
    {
        public int requiredReputation;
        public int orderLimit = 3;
    }
}
