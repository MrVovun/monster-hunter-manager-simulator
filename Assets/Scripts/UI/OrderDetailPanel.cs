using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OrderDetailPanel : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Text titleText;
    [SerializeField] private Text descriptionText;
    [SerializeField] private Text statsText;
    [SerializeField] private Text partyInfoText;

    [Header("Systems")]
    [SerializeField] private PartyFormation partyFormation;

    private Order currentOrder;

    public void ShowOrder(Order order)
    {
        currentOrder = order;
        if (partyFormation != null)
        {
            partyFormation.Initialize(order);
        }
        UpdateUI();
    }

    public void UpdateUI()
    {
        if (currentOrder == null) return;

        if (titleText != null) titleText.text = currentOrder.orderTitle;
        if (descriptionText != null) descriptionText.text = currentOrder.description;
        if (statsText != null)
        {
            statsText.text =
                $"Monster: {currentOrder.monsterType}\n" +
                $"Difficulty: {currentOrder.difficulty}\n" +
                $"Reward: {currentOrder.goldReward}g / {currentOrder.xpReward}xp\n" +
                $"Prep: {currentOrder.prepTimeLimit:0}s  Mission: {currentOrder.missionDuration:0}s";
        }

        if (partyInfoText != null && partyFormation != null)
        {
            var chance = partyFormation.CalculateSuccessChance();
            partyInfoText.text = $"Party: {partyFormation.GetPartySize()}/{partyFormation.GetMaxPartySize()}  Success: {chance:0}%";
        }
    }

    public void OnSendParty()
    {
        if (currentOrder == null || partyFormation == null) return;

        List<Hunter> party = partyFormation.GetParty();
        OrderManager manager = GameManager.Instance != null ? GameManager.Instance.GetOrderManager() : null;

        if (manager == null || !manager.StartMission(currentOrder, party))
        {
            Debug.LogWarning("Failed to start mission. Check party size/state.");
            return;
        }

        UpdateUI();
    }
}
