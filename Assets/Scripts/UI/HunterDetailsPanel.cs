using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HunterDetailsPanel : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private bool hideRootWhenEmpty = true;
    [SerializeField] private GlobalHunterConfig globalConfig;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text rarityText;
    [SerializeField] private TMP_Text upkeepText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text currentXPText;
    [SerializeField] private TMP_Text nextXPText;
    [SerializeField] private TMP_Text bioText;
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text powerText;
    [Header("Traits UI")]
    [SerializeField] private Transform traitListParent;
    [SerializeField] private GameObject traitItemPrefab;
    [SerializeField] private TMP_Text traitFallbackText;
    [SerializeField] private TraitTooltipPanel traitTooltipPanel;
    [SerializeField] private Image traitIconPrototype;
    [Header("XP Progress UI")]
    [SerializeField] private Image expProgressFillImage;
    [SerializeField] private RectTransform expProgressTrack;
    [SerializeField] private RectTransform expMarker;
    [SerializeField] private TMP_Text expStartValueText;
    [SerializeField] private TMP_Text expCurrentValueText;
    [SerializeField] private TMP_Text expEndValueText;

    private readonly List<GameObject> spawnedTraitItems = new List<GameObject>();

    private void Awake()
    {
        Clear();
    }

    public void ShowHunter(Hunter hunter)
    {
        if (hunter == null)
        {
            Clear();
            return;
        }

        if (panelRoot != null && hideRootWhenEmpty)
        {
            panelRoot.SetActive(true);
        }

        HunterData data = hunter.Data;
        HunterStats stats = hunter.GetStats();

        if (nameText != null) nameText.text = hunter.name;
        if (rarityText != null)
        {
            var rarity = GetRarityEntry(data);
            rarityText.text = rarity != null ? rarity.displayName : "-";
            rarityText.color = rarity != null ? rarity.color : Color.white;
        }

        if (upkeepText != null)
        {
            int upkeepValue = hunter != null ? hunter.GetUpkeepCost() : (data != null ? data.dailyUpkeepCost : 0);
            upkeepText.text = hunter != null ? upkeepValue.ToString() : "-";
        }

        if (levelText != null)
        {
            levelText.text = $"Level {hunter.GetLevel()}";
        }

        if (currentXPText != null)
        {
            currentXPText.text = hunter.GetXP().ToString();
        }

        int xpToNext = hunter.GetXPToNextLevel();
        if (nextXPText != null)
        {
            nextXPText.text = xpToNext == int.MaxValue ? "MAX" : xpToNext.ToString();
        }

        if (bioText != null)
        {
            bioText.text = data != null ? data.bio : string.Empty;
        }

        if (portraitImage != null)
        {
            Sprite portrait = data != null ? data.portrait : null;
            portraitImage.sprite = portrait;
            portraitImage.enabled = portrait != null;
        }

        if (powerText != null)
        {
            powerText.text = stats != null ? $"{stats.GetTotalPower()}" : "Power: -";
        }

        PopulateTraitList(data);
        UpdateExpProgressUI(hunter);
    }

    public void Clear()
    {
        if (nameText != null) nameText.text = "Select a Hunter";
        if (rarityText != null)
        {
            rarityText.text = "-";
            rarityText.color = Color.white;
        }
        if (upkeepText != null) upkeepText.text = "-";
        if (levelText != null) levelText.text = "-";
        if (currentXPText != null) currentXPText.text = "-";
        if (nextXPText != null) nextXPText.text = "-";
        if (bioText != null) bioText.text = string.Empty;
        if (portraitImage != null)
        {
            portraitImage.sprite = null;
            portraitImage.enabled = false;
        }
        if (powerText != null) powerText.text = "Power: -";

        ClearTraitList();
        UpdateExpProgressUI(null);

        if (panelRoot != null && hideRootWhenEmpty)
        {
            panelRoot.SetActive(false);
        }
    }

    private GlobalHunterConfig.RarityEntry GetRarityEntry(HunterData data)
    {
        if (globalConfig == null || data == null) return null;
        return globalConfig.GetRarity(data.rarity);
    }

    private void PopulateTraitList(HunterData data)
    {
        ClearTraitList();

        if (traitListParent == null)
        {
            return;
        }

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
            if (trait == null) continue;

            GameObject traitItem = traitItemPrefab != null
                ? Instantiate(traitItemPrefab, traitListParent)
                : new GameObject("Trait");

            RectTransform traitRect = traitItem.GetComponent<RectTransform>();
            if (traitRect == null)
            {
                traitRect = traitItem.AddComponent<RectTransform>();
            }

            spawnedTraitItems.Add(traitItem);

            TMP_Text text = traitItem.GetComponentInChildren<TMP_Text>();
            Image icon = traitItem.GetComponentInChildren<Image>();

            if (text != null)
            {
                text.text = string.Empty;
                text.gameObject.SetActive(false);
            }

            if (icon == null && traitIconPrototype != null)
            {
                icon = Instantiate(traitIconPrototype, traitItem.transform);
            }

            if (icon != null)
            {
                icon.sprite = trait.icon;
                icon.enabled = trait.icon != null;
            }

            if (traitTooltipPanel != null)
            {
                TraitTooltipTrigger trigger = traitItem.GetComponent<TraitTooltipTrigger>();
                if (trigger == null)
                {
                    trigger = traitItem.AddComponent<TraitTooltipTrigger>();
                }

                string tooltipName = trait.displayName;
                string tooltipDescription = trait.description;
                trigger.Initialize(traitTooltipPanel, traitRect, tooltipName, tooltipDescription);
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
        bool isMaxLevel = hunter == null || xpToNext == int.MaxValue || xpToNext <= 0;

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
}
