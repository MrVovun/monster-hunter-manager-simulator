using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Hunter Trait", menuName = "Guild Manager/Hunter Trait")]
public class HunterTrait : ScriptableObject
{
[Header("Identifiers")]
[Tooltip("Optional trait ID used to counter specific monster traits.")]
public string traitId;
[Tooltip("Display name shown in UI.")]
public string displayName;
[TextArea(2, 4)]
public string description;
[Header("Visuals")]
public Sprite icon;

    [Header("Stat Modifiers")]
    [Tooltip("Percent bonus expressed as decimal, e.g. 0.1 = +10%.")]
    public float powerModifier = 0f;
    public float defenseModifier = 0f;
    public float resolveModifier = 0f;
    
    [Header("Mission Modifiers")]
    [Tooltip("Flat success chance bonus in percent points.")]
    public float successChanceBonus = 0f;
    [Tooltip("Multiplier to injury risk. 1 = normal, 0.8 = safer, 1.2 = riskier.")]
    public float injuryRiskModifier = 1f;
    [Tooltip("Multiplier to death risk. 1 = normal.")]
    public float deathRiskModifier = 1f;

    [Header("Counters")]
    [Tooltip("Monster traits this hunter trait counters (e.g., FireDamage, Flying).")]
    public List<MonsterTrait> counters = new List<MonsterTrait>();

    private void OnEnable()
    {
        EnsureId();
    }

    private void OnValidate()
    {
        EnsureId();
    }

    private void EnsureId()
    {
        if (string.IsNullOrWhiteSpace(traitId))
        {
            traitId = Guid.NewGuid().ToString("N");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = traitId;
        }
    }
}
