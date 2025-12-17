using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MonsterTrait", menuName = "Guild Manager/Monster Trait")]
public class MonsterTrait : ScriptableObject
{
    [Header("Identifiers")]
    public string traitId;
    public string displayName;

    [Header("Description")]
    [TextArea(2, 4)] public string description;
    [TextArea(1, 3)] public string dialogueRevealText;
    [Header("Visuals")]
    public Sprite icon;
    [Header("Mission Effects")]
    public List<MissionEffect> missionEffects = new List<MissionEffect>();

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
    }

    [Serializable]
    public class MissionEffect
    {
        public MissionEffectType effectType = MissionEffectType.None;
        [Tooltip("For multiplier effects use 1.0 = no change. For additive effects use decimal (0.15 = +15%).")]
        public float value = 1f;
    }

    public enum MissionEffectType
    {
        None = 0,
        RequiredPowerMultiplier = 1,
        PartyPowerMultiplier = 2,
        GuaranteeInjury = 3,
        AllowDeathWithoutInjury = 4,
        InjuryChanceAdd = 5,
        InjuryChanceMultiplier = 6,
        DeathChanceAdd = 7,
        DeathChanceMultiplier = 8,
    }
}
