using UnityEngine;

public static class OrderRewardUtility
{
    public static void ApplyTraitRewardScaling(Order order, GameConfig config)
    {
        if (order == null || order.traitRewardsScaled) return;

        float multiplier = GetTraitRewardMultiplier(order, config);
        if (Mathf.Abs(multiplier - 1f) > 0.0001f)
        {
            order.goldReward = Mathf.Max(0, Mathf.RoundToInt(order.goldReward * multiplier));
            order.xpReward = Mathf.Max(0, Mathf.RoundToInt(order.xpReward * multiplier));
        }

        order.traitRewardsScaled = true;
    }

    public static float GetTraitRewardMultiplier(Order order, GameConfig config)
    {
        if (order == null || config == null) return 1f;

        int traitCount = CountActualMonsterTraits(order);
        if (traitCount <= 0) return 1f;

        float bonusPerTrait = Mathf.Max(0f, config.rewardBonusPerMonsterTrait);
        return Mathf.Max(0f, 1f + traitCount * bonusPerTrait);
    }

    public static int CountActualMonsterTraits(Order order)
    {
        var traits = order?.investigationCase?.truthTraits;
        if (traits == null) return 0;

        int count = 0;
        foreach (var trait in traits)
        {
            if (trait != null) count++;
        }
        return count;
    }
}
