using UnityEngine;

public class HunterInteractionState : MonoBehaviour
{
    [SerializeField] private bool wounded;
    [SerializeField] private float healTimer;

    public bool IsWounded => wounded;
    public bool IsHealing => healTimer > 0f;

    public void SetWounded(bool value)
    {
        wounded = value;
    }

    public void StartHealing(float durationSeconds)
    {
        healTimer = Mathf.Max(0f, durationSeconds);
        if (healTimer == 0f)
        {
            wounded = false;
        }
    }

    private void Update()
    {
        if (healTimer <= 0f) return;
        healTimer -= Time.deltaTime;
        if (healTimer <= 0f)
        {
            healTimer = 0f;
            wounded = false;
        }
    }
}
