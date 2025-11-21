using UnityEngine;

[CreateAssetMenu(fileName = "New Hunter Trait", menuName = "Guild Manager/Hunter Trait")]
public class HunterTrait : ScriptableObject
{
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
}
