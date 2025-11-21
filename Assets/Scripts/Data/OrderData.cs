using UnityEngine;

[CreateAssetMenu(fileName = "New Order Template", menuName = "Guild Manager/Order Template")]
public class OrderData : ScriptableObject
{
    [Header("Order Info")]
    public string orderTitle;
    [TextArea(3, 5)]
    public string description;
    
    [Header("Monster")]
    public MonsterType monsterType;
    
    [Header("Difficulty")]
    [Range(1, 100)]
    public int baseDifficulty = 10;
    [Range(1, 10)]
    public int difficultyVariance = 2; // Random variation
    
    [Header("Rewards")]
    public int baseGoldReward = 100;
    public int goldVariance = 20; // Random variation
    public int baseXPReward = 50;
    public int xpVariance = 10;
    
    [Header("Time")]
    public float baseMissionDuration = 300f; // In game seconds (5 minutes default)
    public float durationVariance = 60f; // Random variation
    public float basePrepTimeLimit = 180f; // Time to assign party (3 minutes default)
    public float prepTimeVariance = 60f;
    
    [Header("Party")]
    [Range(1, 5)]
    public int maxPartySize = 3;
    [Range(1, 5)]
    public int minPartySize = 1;
    
    // Generate a runtime order from this template
    public Order GenerateOrder()
    {
        Order order = new Order();
        order.orderTitle = orderTitle;
        order.description = description;
        order.monsterType = monsterType;
        order.difficulty = baseDifficulty + Random.Range(-difficultyVariance, difficultyVariance + 1);
        order.difficulty = Mathf.Clamp(order.difficulty, 1, 100);
        order.goldReward = baseGoldReward + Random.Range(-goldVariance, goldVariance + 1);
        order.goldReward = Mathf.Max(1, order.goldReward);
        order.xpReward = baseXPReward + Random.Range(-xpVariance, xpVariance + 1);
        order.xpReward = Mathf.Max(1, order.xpReward);
        order.missionDuration = baseMissionDuration + Random.Range(-durationVariance, durationVariance);
        order.missionDuration = Mathf.Max(60f, order.missionDuration); // Minimum 1 minute
        order.prepTimeLimit = basePrepTimeLimit + Random.Range(-prepTimeVariance, prepTimeVariance);
        order.prepTimeLimit = Mathf.Max(60f, order.prepTimeLimit); // Minimum 1 minute
        order.maxPartySize = maxPartySize;
        order.minPartySize = minPartySize;
        order.state = OrderState.Offered;
        
        return order;
    }
}

