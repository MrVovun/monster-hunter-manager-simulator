using UnityEngine;

[DisallowMultipleComponent]
public class BroomCleaningVfxEvent : MonoBehaviour
{
    [Tooltip("Optional point used for spawned VFX. Put this near the broom bristles.")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Optional looping or burst particle system kept on the broom prefab.")]
    [SerializeField] private ParticleSystem sweepVfx;
    [Tooltip("Optional one-shot VFX prefab to instantiate at the far point of the sweep.")]
    [SerializeField] private GameObject sweepVfxPrefab;
    [Min(0.05f)] [SerializeField] private float spawnedVfxLifetime = 2f;

    public void PlaySweepVfx()
    {
        Transform origin = spawnPoint != null ? spawnPoint : transform;
        if (sweepVfxPrefab != null)
        {
            Destroy(Instantiate(sweepVfxPrefab, origin.position, origin.rotation), spawnedVfxLifetime);
        }

        if (sweepVfx != null)
        {
            sweepVfx.Play(true);
        }
    }
}
