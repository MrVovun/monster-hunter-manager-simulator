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
        OnInteractionStart(player);

        if (bellAudio != null)
        {
            bellAudio.Play();
        }

        var manager = ResolveInvestigationManager();
        var generator = ResolveOrderGenerator();
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
