using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum OrderState
{
    Offered,      // Currently shown via client interaction
    Accepted,     // In Orders tab, awaiting party assignment
    InProgress,   // Party assigned, mission timer running
    Completed,    // Resolved successfully
    Failed,       // Resolved unsuccessfully
    Expired       // Not sent before PrepTimeLimit
}

[System.Serializable]
public class Order
{
    public string orderTitle;
    public string description;
    [Tooltip("Token replaced when generating player-facing descriptions (defaults to <monster_name>).")]
    public string monsterNamePlaceholder = DefaultMonsterPlaceholder;
    public MonsterData monsterData;
    [Tooltip("Monster selected by the player when committing the order.")]
    public MonsterData declaredMonster;
    [Tooltip("Investigation data collected before accepting the order.")]
    public InvestigationCase investigationCase;
    public int difficulty;
    public int goldReward;
    public int xpReward;
    public float reputationReward;
    public float missionDuration; // In game seconds
    public int maxPartySize;
    public int minPartySize;
    public OrderState state;
    
    // Runtime data
    public List<Hunter> assignedHunters = new List<Hunter>();
    public MissionTimer missionTimer;
    public System.Guid orderId;

    public enum DescriptionAudience
    {
        Client,
        DeclaredMonster,
        TrueMonster
    }

    public const string DefaultMonsterPlaceholder = "<monster_name>";
    
    public Order()
    {
        orderId = System.Guid.NewGuid();
        assignedHunters = new List<Hunter>();
    }
    
    public bool IsActive()
    {
        return state == OrderState.Accepted || state == OrderState.InProgress;
    }
    
    public bool CanAssignParty()
    {
        return state == OrderState.Accepted && assignedHunters.Count < maxPartySize;
    }
    
    public int GetAssignedPartySize()
    {
        return assignedHunters.Count;
    }

    public string GetMonsterName()
    {
        if (declaredMonster != null && !string.IsNullOrWhiteSpace(declaredMonster.displayName))
        {
            return declaredMonster.displayName;
        }

        if (monsterData != null && !string.IsNullOrWhiteSpace(monsterData.displayName))
        {
            return monsterData.displayName;
        }

        return "Unknown Monster";
    }

    public string GetDeclaredOrGenericMonsterName()
    {
        if (declaredMonster != null && !string.IsNullOrWhiteSpace(declaredMonster.displayName))
        {
            return declaredMonster.displayName;
        }

        return "monster";
    }

    public string GetDescriptionFor(DescriptionAudience audience)
    {
        string replacement = "monster";
        switch (audience)
        {
            case DescriptionAudience.DeclaredMonster:
                replacement = declaredMonster != null && !string.IsNullOrWhiteSpace(declaredMonster.displayName)
                    ? declaredMonster.displayName
                    : "monster";
                break;
            case DescriptionAudience.TrueMonster:
                replacement = monsterData != null && !string.IsNullOrWhiteSpace(monsterData.displayName)
                    ? monsterData.displayName
                    : "monster";
                break;
            default:
                replacement = "monster";
                break;
        }

        return BuildDescriptionFromTemplate(replacement);
    }

    private string BuildDescriptionFromTemplate(string replacement)
    {
        if (string.IsNullOrEmpty(description))
        {
            return string.Empty;
        }

        string token = string.IsNullOrWhiteSpace(monsterNamePlaceholder) ? DefaultMonsterPlaceholder : monsterNamePlaceholder;
        string safeReplacement = string.IsNullOrWhiteSpace(replacement) ? "monster" : replacement;

        if (!string.IsNullOrEmpty(token) && description.Contains(token, StringComparison.Ordinal))
        {
            return description.Replace(token, safeReplacement);
        }

        string truthName = monsterData != null ? monsterData.displayName : null;
        if (!string.IsNullOrWhiteSpace(truthName) && !string.Equals(truthName, safeReplacement, StringComparison.OrdinalIgnoreCase))
        {
            return ReplaceInsensitive(description, truthName, safeReplacement);
        }

        return description;
    }

    private string ReplaceInsensitive(string source, string oldValue, string newValue)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(oldValue))
        {
            return source;
        }

        StringBuilder builder = new StringBuilder();
        int position = 0;
        while (true)
        {
            int index = source.IndexOf(oldValue, position, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                builder.Append(source, position, source.Length - position);
                break;
            }

            builder.Append(source, position, index - position);
            builder.Append(newValue);
            position = index + oldValue.Length;
        }

        return builder.ToString();
    }
}
