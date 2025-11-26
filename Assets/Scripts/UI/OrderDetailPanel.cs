using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class OrderDetailPanel : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text statsText;
    [SerializeField] private TMP_Text partyInfoText;
    [SerializeField] private TMP_Text timerText;

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

        if (timerText != null)
        {
            string timerLine = string.Empty;
            if (currentOrder.missionTimer != null)
            {
                timerLine = $"Mission: {currentOrder.missionTimer.GetFormattedRemainingTime()}";
            }
            else if (currentOrder.prepTimer != null)
            {
                timerLine = $"Prep: {currentOrder.prepTimer.GetFormattedRemainingTime()}";
            }
            timerText.text = timerLine;
        }
    }

    public void OnSendParty()
    {
        if (currentOrder == null || partyFormation == null) return;

        List<Hunter> party = partyFormation.GetParty();
        OrderManager manager = GameManager.Instance != null ? GameManager.Instance.GetOrderManager() : null;

        if (manager == null || !manager.StartMission(currentOrder, party))
        {
            UnityEngine.Debug.LogWarning("Failed to start mission. Check party size/state.");
            return;
        }

        partyFormation.ClearParty();
        UpdateUI();
    }

    // Helper: auto-fill with idle hunters up to max party size
    public void AutoFillParty()
    {
        if (partyFormation == null || currentOrder == null) return;

        HunterManager hunterManager = GameManager.Instance != null ? GameManager.Instance.GetHunterManager() : null;
        if (hunterManager == null) return;

        partyFormation.ClearParty();

        var available = hunterManager.GetAvailableHunters();
        int needed = Mathf.Min(currentOrder.maxPartySize, available.Count);
        for (int i = 0; i < needed; i++)
        {
            partyFormation.AddHunter(available[i]);
        }

        UpdateUI();
    }

    public void ClearParty()
    {
        if (partyFormation == null) return;
        partyFormation.ClearParty();
        UpdateUI();
    }
}
