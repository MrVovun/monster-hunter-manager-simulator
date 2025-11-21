using System.Collections.Generic;
using UnityEngine;

public class OrderGenerator : MonoBehaviour
{
    [Header("Order Templates")]
    [SerializeField] private List<OrderData> orderTemplates = new List<OrderData>();
    
    [Header("Generation Settings")]
    [SerializeField] private int minDifficulty = 1;
    [SerializeField] private int maxDifficulty = 100;
    [SerializeField] private float difficultyScaling = 1f; // Scales with reputation
    
    private void Awake()
    {
        // Load all order templates from Resources if list is empty
        if (orderTemplates.Count == 0)
        {
            LoadOrderTemplates();
        }
    }
    
    private void LoadOrderTemplates()
    {
        // Try to load from Resources folder
        OrderData[] loaded = Resources.LoadAll<OrderData>("Orders");
        if (loaded != null && loaded.Length > 0)
        {
            orderTemplates.AddRange(loaded);
        }
    }
    
    public Order GenerateRandomOrder()
    {
        if (orderTemplates.Count == 0)
        {
            Debug.LogWarning("No order templates available. Creating default order.");
            return GenerateDefaultOrder();
        }
        
        // Select random template
        OrderData template = orderTemplates[Random.Range(0, orderTemplates.Count)];
        Order order = template.GenerateOrder();
        
        // Adjust difficulty based on reputation
        int reputation = GameManager.Instance != null ? 
            GameManager.Instance.GetReputation() : 0;
        order.difficulty = Mathf.Clamp(
            order.difficulty + Mathf.RoundToInt(reputation * difficultyScaling),
            minDifficulty,
            maxDifficulty
        );
        
        return order;
    }
    
    private Order GenerateDefaultOrder()
    {
        Order order = new Order();
        order.orderTitle = "Monster Hunt";
        order.description = "A dangerous creature needs to be dealt with.";
        order.monsterType = (MonsterType)Random.Range(0, System.Enum.GetValues(typeof(MonsterType)).Length);
        order.difficulty = Random.Range(5, 15);
        order.goldReward = order.difficulty * 10;
        order.xpReward = order.difficulty * 5;
        order.missionDuration = 300f;
        order.prepTimeLimit = 180f;
        order.maxPartySize = 3;
        order.minPartySize = 1;
        order.state = OrderState.Offered;
        
        return order;
    }
    
    public void AddOrderTemplate(OrderData template)
    {
        if (template != null && !orderTemplates.Contains(template))
        {
            orderTemplates.Add(template);
        }
    }
}

