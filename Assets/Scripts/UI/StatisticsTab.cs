using UnityEngine;
using UnityEngine.UI;

public class StatisticsTab : MonoBehaviour
{
    [SerializeField] private Text statsText;

    public void Refresh()
    {
        if (statsText != null)
        {
            statsText.text = "Mission statistics will appear here in the full version.";
        }
    }
}
