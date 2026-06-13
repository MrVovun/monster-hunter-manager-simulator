using System.Collections.Generic;
using System.Text;
using UnityEngine;

[CreateAssetMenu(fileName = "KitchenRecipe", menuName = "Guild Manager/Kitchen Recipe")]
public class KitchenRecipe : ScriptableObject
{
    [Header("Info")]
    public string recipeId;
    public string displayName;
    [TextArea(2, 5)] public string description;
    public Sprite icon;

    [Header("Visuals")]
    [Tooltip("Optional pot visual for this recipe. Falls back to the KitchenManager default pot prefab when empty.")]
    public GameObject potPrefab;

    [Header("Daily Effects Per Fed Hunter")]
    [Tooltip("Flat success chance bonus in percent. Use 5 for +5%.")]
    public float successChanceBonusPercent;
    [Tooltip("Wound chance reduction in percent. Use 10 for -10%.")]
    public float woundChanceReductionPercent;
    [Tooltip("Death chance reduction in percent. Use 10 for -10%.")]
    public float deathChanceReductionPercent;
    [Tooltip("Mission completion time reduction in percent. Use 10 for -10%.")]
    public float missionTimeReductionPercent;

    [Header("Monster Trait Counter")]
    [Tooltip("If enabled, this recipe rolls one trait for the day. Fed hunters counter that one trait on future orders.")]
    public bool counterOneRandomMonsterTrait;
    [Tooltip("Optional trait pool for the random counter. If empty, KitchenManager can fall back to the global monster library.")]
    public List<MonsterTrait> counterTraitPool = new List<MonsterTrait>();

    private void OnEnable()
    {
        EnsureId();
    }

    private void OnValidate()
    {
        EnsureId();
    }

    public string GetDisplayName()
    {
        return string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    }

    public string GetRecipeId()
    {
        EnsureId();
        return recipeId;
    }

    public string BuildEffectSummary(MonsterTrait rolledTrait = null)
    {
        StringBuilder sb = new StringBuilder();
        AppendEffect(sb, successChanceBonusPercent, "% success");
        AppendEffect(sb, woundChanceReductionPercent, "% wound chance reduction");
        AppendEffect(sb, deathChanceReductionPercent, "% death chance reduction");
        AppendEffect(sb, missionTimeReductionPercent, "% faster orders");

        if (counterOneRandomMonsterTrait)
        {
            string traitName = rolledTrait != null
                ? (!string.IsNullOrWhiteSpace(rolledTrait.displayName) ? rolledTrait.displayName : rolledTrait.name)
                : "one random monster trait";
            if (sb.Length > 0) sb.AppendLine();
            sb.Append("Counters ").Append(traitName);
        }

        return sb.Length > 0 ? sb.ToString() : "No effects configured.";
    }

    private static void AppendEffect(StringBuilder sb, float value, string suffix)
    {
        if (Mathf.Abs(value) <= 0.01f) return;
        if (sb.Length > 0) sb.AppendLine();
        sb.Append(value >= 0f ? "+" : string.Empty).Append(value.ToString("0.#")).Append(suffix);
    }

    private void EnsureId()
    {
        if (string.IsNullOrWhiteSpace(recipeId))
        {
            recipeId = System.Guid.NewGuid().ToString("N");
        }
    }
}
