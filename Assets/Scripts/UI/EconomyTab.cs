using UnityEngine;
using UnityEngine.UI;

public class EconomyTab : MonoBehaviour
{
    [SerializeField] private Text goldText;

    public void Refresh()
    {
        GoldManager gold = GameManager.Instance != null ? GameManager.Instance.GetGoldManager() : null;
        if (goldText != null && gold != null)
        {
            goldText.text = $"Gold: {gold.GetGold()}";
        }
    }
}
