using UnityEngine;

public class ClientBell : Interactable
{
    [SerializeField] private AudioSource bellAudio;
    [SerializeField] private InvestigationManager investigationManager;
    [SerializeField] private OrderGenerator orderGenerator;

    private void Reset()
    {
        interactionPrompt = "[E] Ring Bell";
        interactionType = InteractionType.Trigger;
        locksPlayer = false;
    }

    public override void Interact(PlayerInteraction player)
    {
        var manager = ResolveInvestigationManager();
        var generator = ResolveOrderGenerator();
        if (manager != null && manager.HasActiveClientInvestigation())
        {
            Debug.LogWarning("ClientBell: Cannot call another client while a client is already active.");
            return;
        }

        var timeManager = GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null;
        if (timeManager != null)
        {
            var state = timeManager.GetDayState();
            if (state == TimeManager.DayState.Evening)
            {
                Debug.LogWarning("ClientBell: Cannot ring the bell in the evening.");
                return;
            }
            timeManager.StartDayCountdown();
        }

        OnInteractionStart(player);

        if (bellAudio != null)
        {
            bellAudio.Play();
        }

        if (manager == null || generator == null)
        {
            Debug.LogWarning("ClientBell: Missing InvestigationManager or OrderGenerator reference.");
            OnInteractionEnd(player);
            return;
        }

        Order newOrder = generator.GenerateRandomOrder();
        if (newOrder == null)
        {
            Debug.LogWarning("ClientBell: Failed to generate a new order.");
            OnInteractionEnd(player);
            return;
        }

        manager.StartInvestigation(newOrder);

        // Advance action-based time for ringing the bell
        var config = GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null;
        var timeCost = config != null ? config.actionTimeSettings.ringBellSeconds : 0f;
        var tm2 = GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null;
        tm2?.AdvanceTime(timeCost);

        OnInteractionEnd(player);
    }

    private InvestigationManager ResolveInvestigationManager()
    {
        if (investigationManager != null) return investigationManager;
        investigationManager = GameManager.Instance != null ? GameManager.Instance.GetInvestigationManager() : null;
        return investigationManager;
    }

    private OrderGenerator ResolveOrderGenerator()
    {
        if (orderGenerator != null) return orderGenerator;
        orderGenerator = GameManager.Instance != null ? GameManager.Instance.GetOrderGenerator() : null;
        return orderGenerator;
    }
}
