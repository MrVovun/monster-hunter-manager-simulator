using UnityEngine;

public class ClientBell : Interactable
{
    [SerializeField] private OrderOfferPanel orderOfferPanel;
    [SerializeField] private AudioSource bellAudio;

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

        OrderManager manager = GameManager.Instance != null ? GameManager.Instance.GetOrderManager() : null;
        if (manager != null)
        {
            Order newOrder = manager.GenerateAndOfferOrder();
            if (orderOfferPanel != null)
            {
                orderOfferPanel.Show(newOrder);
            }
        }
        else
        {
            Debug.LogWarning("ClientBell: No OrderManager found.");
        }

        OnInteractionEnd(player);
    }
}
