using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BriefingRoomExitTrigger : MonoBehaviour
{
    [SerializeField] private BriefingRoomManager briefingRoomManager;
    [SerializeField] private PlayerInteraction player;

    private Collider triggerCollider;
    private bool playerWasInside;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (player == null)
        {
            player = FindObjectOfType<PlayerInteraction>();
        }
    }

    private void Reset()
    {
        var triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void Update()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider>();
        }
        if (player == null)
        {
            player = FindObjectOfType<PlayerInteraction>();
        }
        if (triggerCollider == null || player == null) return;

        bool playerInside = triggerCollider.bounds.Contains(player.transform.position);
        if (playerWasInside && !playerInside)
        {
            NotifyPlayerLeft();
        }

        playerWasInside = playerInside;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        PlayerInteraction enteringPlayer = other.GetComponentInParent<PlayerInteraction>();
        if (enteringPlayer == null) return;

        player = enteringPlayer;
        playerWasInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null || other.GetComponentInParent<PlayerInteraction>() == null) return;

        playerWasInside = false;
        NotifyPlayerLeft();
    }

    private void NotifyPlayerLeft()
    {
        ResolveManager()?.HandlePlayerLeftRoom();
    }

    private BriefingRoomManager ResolveManager()
    {
        if (briefingRoomManager != null) return briefingRoomManager;
        briefingRoomManager = BriefingRoomManager.Instance;
        if (briefingRoomManager == null)
        {
            briefingRoomManager = FindObjectOfType<BriefingRoomManager>();
        }
        return briefingRoomManager;
    }
}
