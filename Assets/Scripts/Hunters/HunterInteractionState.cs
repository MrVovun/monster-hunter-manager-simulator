using UnityEngine;

public class HunterInteractionState : MonoBehaviour
{
    [SerializeField] private bool wounded;
    [SerializeField] private float healTimer;
    [SerializeField] private bool useRealTimeHealing = true;

    public bool IsWounded => wounded;
    public bool IsHealing => healTimer > 0f;

    public void SetWounded(bool value)
    {
        wounded = value;
    }

    public void StartHealing(float durationSeconds, bool realTimeHealing = true)
    {
        healTimer = Mathf.Max(0f, durationSeconds);
        useRealTimeHealing = realTimeHealing;
        if (healTimer == 0f)
        {
            wounded = false;
        }
    }

    public void AdvanceHealing(float durationSeconds)
    {
        if (healTimer <= 0f) return;
        healTimer = Mathf.Max(0f, healTimer - Mathf.Max(0f, durationSeconds));
        if (healTimer <= 0f)
        {
            CompleteHealing();
        }
    }

    public void CompleteHealing()
    {
        healTimer = 0f;
        wounded = false;
        useRealTimeHealing = true;
    }

    private void Update()
    {
        if (healTimer <= 0f) return;
        if (!useRealTimeHealing) return;
        healTimer -= Time.deltaTime;
        if (healTimer <= 0f)
        {
            CompleteHealing();
        }
    }
}
