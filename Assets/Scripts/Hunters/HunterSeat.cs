using UnityEngine;

/// <summary>
/// Marks a transform as a hunter seat and exposes a precise anchor
/// where the hunter's hips should align when seated.
/// </summary>
public class HunterSeat : MonoBehaviour
{
    [SerializeField] private Transform seatAnchor;
    [SerializeField] private Transform approachPoint;

    private Hunter occupant;

    /// <summary>World position a hunter should walk toward.</summary>
    public Vector3 ApproachPosition => (approachPoint != null ? approachPoint : Anchor).position;

    /// <summary>Transform used to align hunter position/rotation when seated.</summary>
    public Transform Anchor => seatAnchor != null ? seatAnchor : transform;

    public bool IsOccupied => occupant != null;

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
}
