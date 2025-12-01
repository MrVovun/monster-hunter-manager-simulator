using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HuntersTab : MonoBehaviour
{
    [SerializeField] private Transform listParent;
    [SerializeField] private HunterRosterItem hunterRosterItemPrefab;

    [Header("Details Panel")]
    [SerializeField] private GameObject detailsPanelRoot;
    [SerializeField] private GlobalHunterConfig globalConfig;
    [SerializeField] private TMP_Text detailNameText;
    [SerializeField] private TMP_Text detailRarityText;
    [SerializeField] private TMP_Text detailUpkeepText;
    [SerializeField] private TMP_Text detailLevelText;
    [SerializeField] private TMP_Text detailCurrentXPText;
    [SerializeField] private TMP_Text detailNextXPText;
    [SerializeField] private TMP_Text detailBioText;
    [SerializeField] private Image detailPortraitImage;
    [SerializeField] private Transform traitListParent;
    [SerializeField] private GameObject traitItemPrefab;
    [SerializeField] private TMP_Text traitFallbackText;
    [SerializeField] private TraitTooltipPanel traitTooltipPanel;
    [SerializeField] private Image traitIconPrototype;
    [SerializeField] private List<StatDisplayConfig> statDisplays = new List<StatDisplayConfig>();
    [Header("XP Progress UI")]
    [SerializeField] private Image expProgressFillImage;
    [SerializeField] private RectTransform expProgressTrack;
    [SerializeField] private RectTransform expMarker;
    [SerializeField] private TMP_Text expStartValueText;
    [SerializeField] private TMP_Text expCurrentValueText;
    [SerializeField] private TMP_Text expEndValueText;

    private Hunter selectedHunter;
    private readonly List<GameObject> spawnedTraitItems = new List<GameObject>();

    private void Awake()
    {
        ClearSelection();
    }

    public void Refresh()
    {
        HunterManager manager = GameManager.Instance != null ? GameManager.Instance.GetHunterManager() : null;
        if (manager == null || listParent == null || hunterRosterItemPrefab == null) return;

        var hunters = manager.GetAllHunters();

        foreach (Transform child in listParent)
        {
            Destroy(child.gameObject);
        }

        if (selectedHunter != null && !hunters.Contains(selectedHunter))
        {
            selectedHunter = null;
        }

        foreach (var hunter in hunters)
        {
            if (hunter == null) continue;
            HunterRosterItem item = Instantiate(hunterRosterItemPrefab, listParent);
            item.InitializeForHuntersTab(hunter, this, HandleHunterSelected);
        }

        UpdateDetails(selectedHunter);
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

    private void HandleHunterSelected(Hunter hunter)
    {
        selectedHunter = hunter;
        UpdateDetails(hunter);
    }

    private GlobalHunterConfig.RarityEntry GetRarityEntry(HunterData data)
    {
        if (globalConfig == null || data == null) return null;
        return globalConfig.GetRarity(data.rarity);
    }

    private void UpdateDetails(Hunter hunter)
    {
        bool hasHunter = hunter != null;
        if (detailsPanelRoot != null)
        {
            detailsPanelRoot.SetActive(hasHunter);
        }

        if (!hasHunter)
        {
            ClearDetails();
            return;
        }

        HunterData data = hunter.GetHunterData();
        HunterStats stats = hunter.GetStats();

        if (detailNameText != null) detailNameText.text = hunter.name;
        if (detailRarityText != null)
        {
            var rarity = GetRarityEntry(data);
            detailRarityText.text = rarity != null ? rarity.displayName : "-";
            detailRarityText.color = rarity != null ? rarity.color : Color.white;
        }
        if (detailUpkeepText != null) detailUpkeepText.text = data != null ? data.dailyUpkeepCost.ToString() : "-";
        if (detailLevelText != null) detailLevelText.text = "Level " + hunter.GetLevel().ToString();
        if (detailCurrentXPText != null) detailCurrentXPText.text = hunter.GetXP().ToString();

        int xpToNext = hunter.GetXPToNextLevel();
        if (detailNextXPText != null)
        {
            detailNextXPText.text = xpToNext == int.MaxValue ? "MAX" : xpToNext.ToString();
        }

        if (detailBioText != null)
        {
            detailBioText.text = data != null ? data.bio : string.Empty;
        }

        if (detailPortraitImage != null)
        {
            Sprite portrait = data != null ? data.portrait : null;
            detailPortraitImage.sprite = portrait;
            detailPortraitImage.enabled = portrait != null;
        }

        UpdateStatDisplays(stats);
        PopulateTraitList(data);
        UpdateExpProgressUI(hunter);
    }

    private void ClearDetails()
    {
        if (detailNameText != null) detailNameText.text = "Select a Hunter";
        if (detailRarityText != null)
        {
            detailRarityText.text = "-";
            detailRarityText.color = Color.white;
        }
        if (detailUpkeepText != null) detailUpkeepText.text = "-";
        if (detailLevelText != null) detailLevelText.text = "-";
        if (detailCurrentXPText != null) detailCurrentXPText.text = "-";
        if (detailNextXPText != null) detailNextXPText.text = "-";
        if (detailBioText != null) detailBioText.text = string.Empty;
        if (detailPortraitImage != null)
        {
            detailPortraitImage.sprite = null;
            detailPortraitImage.enabled = false;
        }

        UpdateStatDisplays(null);
        ClearTraitList();
        UpdateExpProgressUI(null);
    }

    public void ClearSelection()
    {
        selectedHunter = null;
        UpdateDetails(null);
    }

    private void UpdateStatDisplays(HunterStats stats)
    {
        if (statDisplays == null) return;

        foreach (var config in statDisplays)
        {
            if (config == null || config.valueText == null) continue;
            string valueText = "-";
            if (stats != null)
            {
                int statValue = 0;
                switch (config.statType)
                {
                    case HunterStatField.Power:
                        statValue = stats.GetPower();
                        break;
                    case HunterStatField.Defense:
                        statValue = stats.GetDefense();
                        break;
                    case HunterStatField.Resolve:
                        statValue = stats.GetResolve();
                        break;
                    case HunterStatField.TotalPower:
                        statValue = stats.GetTotalPower();
                        break;
                }
                valueText = statValue.ToString();
            }

            if (string.IsNullOrEmpty(config.label))
            {
                config.valueText.text = valueText;
            }
            else
            {
                config.valueText.text = $"{config.label}: {valueText}";
            }
        }
    }

    private void PopulateTraitList(HunterData data)
    {
        ClearTraitList();

        if (traitListParent == null) return;

        bool hasTraits = data != null && data.traits != null && data.traits.Count > 0;
        if (!hasTraits)
        {
            if (traitFallbackText != null)
            {
                traitFallbackText.text = "No traits";
            }
            return;
        }

        if (traitFallbackText != null)
        {
            traitFallbackText.text = string.Empty;
        }

        foreach (var trait in data.traits)
        {
            GameObject traitItem = traitItemPrefab != null
                ? Instantiate(traitItemPrefab, traitListParent)
                : new GameObject("Trait");

            var traitRect = traitItem.GetComponent<RectTransform>();
            if (traitRect == null)
            {
                traitRect = traitItem.AddComponent<RectTransform>();
            }

            traitRect.SetParent(traitListParent, false);
            spawnedTraitItems.Add(traitItem);

            TMP_Text text = traitItem.GetComponentInChildren<TMP_Text>();
            Image icon = traitItem.GetComponentInChildren<Image>();
            if (text == null || icon == null)
            {
                if (traitItemPrefab == null)
                {
                    GameObject container = new GameObject("TraitContainer", typeof(RectTransform));
                    container.transform.SetParent(traitItem.transform, false);
                    traitRect = container.GetComponent<RectTransform>();
                    if (text == null)
                    {
                        GameObject textObj = new GameObject("TraitName", typeof(RectTransform));
                        textObj.transform.SetParent(container.transform, false);
                        text = textObj.AddComponent<TextMeshProUGUI>();
                    }
                    if (icon == null)
                    {
                        GameObject iconObj = new GameObject("TraitIcon", typeof(RectTransform));
                        iconObj.transform.SetParent(container.transform, false);
                        icon = iconObj.AddComponent<Image>();
                    }
                }
                else
                {
                    if (text == null)
                    {
                        GameObject textObj = new GameObject("TraitName", typeof(RectTransform));
                        textObj.transform.SetParent(traitItem.transform, false);
                        text = textObj.AddComponent<TextMeshProUGUI>();
                    }
                    if (icon == null && traitIconPrototype != null)
                    {
                        icon = Instantiate(traitIconPrototype, traitItem.transform);
                    }
                }
            }

            if (text != null)
            {
            if (text != null)
            {
                text.text = string.Empty;
                text.gameObject.SetActive(false);
            }
            }

            if (icon != null)
            {
                Sprite sprite = trait != null ? trait.icon : null;
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }

            if (traitTooltipPanel != null)
            {
                var tooltip = traitItem.GetComponent<TraitTooltipTrigger>();
                if (tooltip == null)
                {
                    tooltip = traitItem.AddComponent<TraitTooltipTrigger>();
                }

                RectTransform anchor = traitRect;
                string tooltipName = trait != null ? trait.displayName : "Trait";
                string tooltipDescription = trait != null ? trait.description : string.Empty;
                tooltip.Initialize(traitTooltipPanel, anchor, tooltipName, tooltipDescription);
            }
        }
    }

    private void ClearTraitList()
    {
        foreach (var traitObj in spawnedTraitItems)
        {
            if (traitObj != null)
            {
                Destroy(traitObj);
            }
        }
        spawnedTraitItems.Clear();

        if (traitFallbackText != null)
        {
            traitFallbackText.text = string.Empty;
        }

        if (traitListParent != null)
        {
            foreach (Transform child in traitListParent)
            {
                Destroy(child.gameObject);
            }
        }
    }

    private void UpdateExpProgressUI(Hunter hunter)
    {
        int currentXP = hunter != null ? hunter.GetXP() : 0;
        int xpToNext = hunter != null ? hunter.GetXPToNextLevel() : 0;
        bool isMaxLevel = xpToNext == int.MaxValue || xpToNext <= 0;

        if (expStartValueText != null)
        {
            expStartValueText.text = "0";
        }

        if (expCurrentValueText != null)
        {
            expCurrentValueText.text = hunter != null ? currentXP.ToString() : "-";
        }

        if (expEndValueText != null)
        {
            expEndValueText.text = isMaxLevel ? "MAX" : xpToNext.ToString();
        }

        float normalized = isMaxLevel ? 1f : Mathf.Clamp01((float)currentXP / xpToNext);

        if (expProgressFillImage != null)
        {
            expProgressFillImage.fillAmount = normalized;
        }

        if (expMarker != null && expProgressTrack != null)
        {
            float width = expProgressTrack.rect.width;
            Vector2 anchored = expMarker.anchoredPosition;
            anchored.x = -0.5f * width + normalized * width;
            expMarker.anchoredPosition = anchored;
        }
    }

    [System.Serializable]
    private class StatDisplayConfig
    {
        public string label;
        public HunterStatField statType = HunterStatField.Power;
        public TMP_Text valueText;
    }

    private enum HunterStatField
    {
        Power,
        Defense,
        Resolve,
        TotalPower
    }
}
