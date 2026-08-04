using UnityEngine;

public class CinematicActorMarker : MonoBehaviour
{
    [SerializeField] private string markerId = "Marker";
    [SerializeField] private bool faceMarkerForward = true;

    public string MarkerId => markerId;
    public bool FaceMarkerForward => faceMarkerForward;
    public Vector3 Position => transform.position;
    public Quaternion Rotation => transform.rotation;

    public void Place(Transform actor)
    {
        if (actor == null) return;

        actor.position = transform.position;
        if (faceMarkerForward)
        {
            actor.rotation = transform.rotation;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.25f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.75f);
    }
}
