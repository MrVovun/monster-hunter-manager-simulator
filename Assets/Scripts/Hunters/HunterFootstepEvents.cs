using UnityEngine;

/// <summary>
/// Receives animation events fired from imported clips
/// (FootL / FootR) and plays optional audio hooks.
/// Attach this to the root of each visual prefab so the
/// animation event errors go away immediately.
/// </summary>
public class HunterFootstepEvents : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip defaultFootstepClip;
    [SerializeField, Range(0f, 0.3f)] private float pitchVariance = 0.05f;

    /// <summary>
    /// Called via animation event when the left foot hits the ground.
    /// </summary>
    public void FootL()
    {
        HandleFootstep();
    }

    /// <summary>
    /// Called via animation event when the right foot hits the ground.
    /// </summary>
    public void FootR()
    {
        HandleFootstep();
    }

    private void HandleFootstep()
    {
        if (audioSource == null || defaultFootstepClip == null)
        {
            return;
        }

        float originalPitch = audioSource.pitch;
        if (pitchVariance > 0f)
        {
            audioSource.pitch = Random.Range(1f - pitchVariance, 1f + pitchVariance);
        }

        audioSource.PlayOneShot(defaultFootstepClip);
        audioSource.pitch = originalPitch;
    }
}
