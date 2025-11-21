using System.Text;
using UnityEngine;
using UnityEngine.UI;

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
            Destroy(child.gameObject);
        }

        foreach (var hunter in manager.GetAllHunters())
        {
            if (hunter == null) continue;
            CreateItem(hunter);
        }
    }

    private void CreateItem(Hunter hunter)
    {
        GameObject item = textItemPrefab != null
            ? Instantiate(textItemPrefab, listParent)
            : new GameObject("HunterItem");

        item.transform.SetParent(listParent, false);
        Text text = item.GetComponent<Text>();
        if (text == null)
        {
            text = item.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = Color.white;
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
