using System.Text;
using TMPro;
using UnityEngine;

public class HuntersTab : MonoBehaviour
{
    [SerializeField] private Transform listParent;
    [SerializeField] private GameObject textItemPrefab;

    public void Refresh()
    {
        HunterManager manager = GameManager.Instance != null ? GameManager.Instance.GetHunterManager() : null;
        if (manager == null || listParent == null) return;

        foreach (Transform child in listParent)
        {
            Object.Destroy(child.gameObject);
        }

        foreach (var hunter in manager.GetAllHunters())
        {
            if (hunter == null) continue;
            CreateItem(hunter);
        }
    }

    public void PayAndLevelUpAffordable()
    {
        HunterManager manager = GameManager.Instance != null ? GameManager.Instance.GetHunterManager() : null;
        GoldManager gold = GameManager.Instance != null ? GameManager.Instance.GetGoldManager() : null;
        if (manager == null || gold == null) return;

        foreach (var hunter in manager.GetAllHunters())
        {
            if (hunter != null && hunter.CanLevelUp())
            {
                manager.TryPayLevelUp(hunter, gold);
            }
        }

        Refresh();
    }

    private void CreateItem(Hunter hunter)
    {
        GameObject item = textItemPrefab != null
            ? Object.Instantiate(textItemPrefab, listParent)
            : new GameObject("HunterItem");

        item.transform.SetParent(listParent, false);
        TMP_Text text = item.GetComponent<TMP_Text>();
        if (text == null)
        {
            text = item.AddComponent<TextMeshProUGUI>();
            text.color = Color.white;
            text.fontSize = 14;
        }

        var stats = hunter.GetStats();
        StringBuilder sb = new StringBuilder();
        sb.Append(hunter.name);
        sb.Append("  Lv").Append(hunter.GetLevel());
        sb.Append("  State: ").Append(hunter.GetState());
        if (stats != null)
        {
            sb.Append($"  P{stats.GetPower()} D{stats.GetDefense()} R{stats.GetResolve()}");
        }

        text.text = sb.ToString();
    }
}
