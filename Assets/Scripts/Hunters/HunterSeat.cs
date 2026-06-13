using UnityEngine;

/// <summary>
/// Marks a transform as a hunter seat and exposes a precise anchor
/// where the hunter's hips should align when seated.
/// </summary>
public class HunterSeat : MonoBehaviour
{
    public enum SeatUsage
    {
        GuildHall,
        BriefingRoom
    }

    [SerializeField] private SeatUsage seatUsage = SeatUsage.GuildHall;
    [SerializeField] private string seatId;
    [SerializeField] private Transform seatAnchor;
    [SerializeField] private Transform approachPoint;
    [SerializeField] private Transform plateSpawnPoint;

    private Hunter occupant;
    private KitchenDirtyPlate dirtyPlate;

    /// <summary>World position a hunter should walk toward.</summary>
    public Vector3 ApproachPosition => (approachPoint != null ? approachPoint : Anchor).position;

    /// <summary>Transform used to align hunter position/rotation when seated.</summary>
    public Transform Anchor => seatAnchor != null ? seatAnchor : transform;

    public SeatUsage Usage => seatUsage;
    public bool CanUseForGuildHall => seatUsage == SeatUsage.GuildHall;
    public bool CanUseForBriefing => seatUsage == SeatUsage.BriefingRoom;
    public bool IsOccupied => occupant != null;
    public bool HasDirtyPlate => dirtyPlate != null;
    public Transform PlateSpawnPoint => plateSpawnPoint != null ? plateSpawnPoint : Anchor;
    public string SeatId => string.IsNullOrWhiteSpace(seatId) ? name : seatId;

    public bool TryAssign(Hunter hunter)
    {
        if (hunter == null) return false;
        if (occupant == hunter) return true;
        if (occupant != null) return false;
        occupant = hunter;
        return true;
    }

    public void Release(Hunter hunter)
    {
        if (occupant == hunter)
        {
            occupant = null;
        }
    }

    public void SetDirtyPlate(KitchenDirtyPlate plate)
    {
        dirtyPlate = plate;
    }

    public void ClearDirtyPlate(KitchenDirtyPlate plate)
    {
        if (dirtyPlate == plate)
        {
            dirtyPlate = null;
        }
    }
}
