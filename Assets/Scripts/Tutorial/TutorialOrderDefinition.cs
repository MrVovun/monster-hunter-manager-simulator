using UnityEngine;

[CreateAssetMenu(menuName = "Guild Manager/Tutorial Order", fileName = "TutorialOrder")]
public class TutorialOrderDefinition : ScriptableObject
{
    public MonsterData monster;
    public string orderTitle = "Giant Rat Trouble";
    [TextArea(2, 5)] public string description = "A Giant Rat is causing trouble.";
    public int difficulty = 5;
    public int goldReward = 25;
    public int xpReward = 25;
    public float reputationPointsReward = 0.1f;
    public float missionDurationSeconds = 60f;
    public int minPartySize = 1;
    public int maxPartySize = 1;

    public Order CreateOrder()
    {
        Order order = new Order
        {
            orderTitle = orderTitle,
            description = description,
            monsterData = monster,
            difficulty = Mathf.Max(1, difficulty),
            goldReward = Mathf.Max(0, goldReward),
            xpReward = Mathf.Max(0, xpReward),
            reputationPointsReward = Mathf.Max(0f, reputationPointsReward),
            missionDuration = Mathf.Max(1f, missionDurationSeconds),
            minPartySize = Mathf.Max(1, minPartySize),
            maxPartySize = Mathf.Max(Mathf.Max(1, minPartySize), maxPartySize),
            state = OrderState.Offered
        };

        return order;
    }
}
