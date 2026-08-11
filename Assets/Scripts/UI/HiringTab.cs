using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HiringTab : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HunterRecruitmentManager recruitmentManager;
    [SerializeField] private GlobalHunterConfig hunterConfig;
    [SerializeField] private TMP_Text powerValueText;
    [SerializeField] private TMP_Text powerStepText;
    [SerializeField] private TMP_Text upkeepValueText;
    [SerializeField] private TMP_Text upkeepStepText;
    [SerializeField] private TMP_Text durationValueText;
    [SerializeField] private TMP_Text durationStepText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button postAdButton;
    [SerializeField] private Button stopAdButton;
    [Header("Traits")]
    [SerializeField] private Transform traitListParent;
    [SerializeField] private TraitPriorityItem traitItemPrefab;
    [SerializeField] private TraitTooltipPanel traitTooltipPanel;
    [Header("Increments")]
    [SerializeField] private int powerStepAmount = 5;
    [SerializeField] private int upkeepStepAmount = 5;
    [SerializeField] private int durationStepMinutes = 1;
    [SerializeField] private int minPowerValue = 0;
    [SerializeField] private int minUpkeepValue = 0;
    [SerializeField] private int minDurationMinutes = 1;

    private readonly Dictionary<string, TraitPriorityItem> traitItems = new Dictionary<string, TraitPriorityItem>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> prioritizedTraits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private int currentPowerTarget;
    private int currentUpkeepTarget;
    private int currentDurationMinutes = 2;

    private void Awake()
    {
        if (recruitmentManager == null)
        {
            recruitmentManager = FindObjectOfType<HunterRecruitmentManager>();
        }
        if (hunterConfig == null)
        {
            hunterConfig = HunterData.GetGlobalConfig();
        }
    }

    private void OnEnable()
    {
        if (recruitmentManager != null)
        {
            recruitmentManager.OnStateChanged += HandleStateChanged;
        }
        SyncSettingsFromManager();
        RefreshTraitsList();
        Refresh();
    }

    private void OnDisable()
    {
        if (recruitmentManager != null)
        {
            recruitmentManager.OnStateChanged -= HandleStateChanged;
        }
    }

    private void HandleStateChanged()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (recruitmentManager == null) return;

        UpdateScalarUI();
        UpdateCostUI();

        if (timerText != null)
        {
            if (recruitmentManager.IsCampaignActive)
            {
                TimeSpan span = TimeSpan.FromSeconds(recruitmentManager.CampaignTimeRemaining);
                timerText.text = $"Ad Active: {span.Minutes:00}:{span.Seconds:00}";
            }
            else
            {
                timerText.text = "Ad Inactive";
            }
        }

        if (statusText != null)
        {
            statusText.text = recruitmentManager.IsCampaignActive ? "Receiving applicants..." : "Idle";
        }

        bool active = recruitmentManager.IsCampaignActive;
        if (postAdButton != null)
        {
            postAdButton.gameObject.SetActive(!active);
            bool canPostAd = !active && CanPostHiringAd();
            postAdButton.interactable = canPostAd;
            if (canPostAd)
            {
                UnavailableReasonButton.ClearReason(postAdButton);
            }
            else
            {
                UnavailableReasonButton.SetReason(postAdButton, GetPostHiringAdUnavailableReason());
            }
            var visualFeedback = postAdButton.GetComponent<UIButtonVisualFeedback>();
            if (visualFeedback != null)
            {
                visualFeedback.RefreshVisualState(true);
            }
        }

        if (stopAdButton != null)
        {
            stopAdButton.gameObject.SetActive(active);
        }
    }

    public void OnPostAdPressed()
    {
        if (recruitmentManager == null) return;
        if (!TutorialManager.IsActionAllowed(TutorialIds.PostHiringAd)) return;

        float durationSeconds = Mathf.Max(30f, currentDurationMinutes * 60f);
        bool freeCampaign = false;
        if (TutorialManager.TryGetForcedHiringAd(out float forcedDurationSeconds, out bool forcedFree))
        {
            if (forcedDurationSeconds > 0f)
            {
                durationSeconds = forcedDurationSeconds;
            }
            freeCampaign = forcedFree;
        }

        var settings = new HunterRecruitmentManager.AdSettings
        {
            targetPower = Mathf.Max(0, currentPowerTarget),
            maxUpkeep = Mathf.Max(0, currentUpkeepTarget),
            durationSeconds = durationSeconds,
            prioritizedTraitIds = new List<string>(prioritizedTraits)
        };

        recruitmentManager.PostAd(settings, freeCampaign);
    }

    public void OnStopAdPressed()
    {
        recruitmentManager?.StopCampaign();
    }

    private void RefreshTraitsList()
    {
        if (recruitmentManager == null || traitListParent == null || traitItemPrefab == null)
        {
            return;
        }

        foreach (Transform child in traitListParent)
        {
            Destroy(child.gameObject);
        }
        traitItems.Clear();

        var traits = recruitmentManager.GetAvailableTraits();
        foreach (var trait in traits)
        {
            if (trait == null) continue;
            TraitPriorityItem item = Instantiate(traitItemPrefab, traitListParent);
            bool active = prioritizedTraits.Contains(trait.traitId);
            item.Initialize(trait, active, HandleTraitToggled, traitTooltipPanel);
            traitItems[trait.traitId] = item;
        }
    }

    private void HandleTraitToggled(string traitId, bool enabled)
    {
        if (string.IsNullOrEmpty(traitId)) return;

        if (enabled)
        {
            prioritizedTraits.Add(traitId);
        }
        else
        {
            prioritizedTraits.Remove(traitId);
        }
    }

    public void ForceRefreshTraitsFromManager()
    {
        RefreshTraitsList();
    }

    private void UpdateScalarUI()
    {
        if (powerValueText != null)
        {
            powerValueText.text = $"{currentPowerTarget} Power";
        }
        if (powerStepText != null)
        {
            powerStepText.text = $"+/- {Mathf.Max(1, powerStepAmount)} per tap";
        }

        if (upkeepValueText != null)
        {
            upkeepValueText.text = $"{currentUpkeepTarget} Gold";
        }
        if (upkeepStepText != null)
        {
            upkeepStepText.text = $"+/- {Mathf.Max(1, upkeepStepAmount)} per tap";
        }

        if (durationValueText != null)
        {
            durationValueText.text = $"{currentDurationMinutes} min";
        }
        if (durationStepText != null)
        {
            durationStepText.text = $"+/- {Mathf.Max(1, durationStepMinutes)} min per tap";
        }
    }

    public void IncreasePower() => AdjustPower(Mathf.Max(1, powerStepAmount));
    public void DecreasePower() => AdjustPower(-Mathf.Max(1, powerStepAmount));
    private void AdjustPower(int delta)
    {
        currentPowerTarget = Mathf.Max(minPowerValue, currentPowerTarget + delta);
        UpdateScalarUI();
    }

    public void IncreaseUpkeep() => AdjustUpkeep(Mathf.Max(1, upkeepStepAmount));
    public void DecreaseUpkeep() => AdjustUpkeep(-Mathf.Max(1, upkeepStepAmount));
    private void AdjustUpkeep(int delta)
    {
        currentUpkeepTarget = Mathf.Max(minUpkeepValue, currentUpkeepTarget + delta);
        UpdateScalarUI();
    }

    public void IncreaseDuration() => AdjustDuration(Mathf.Max(1, durationStepMinutes));
    public void DecreaseDuration() => AdjustDuration(-Mathf.Max(1, durationStepMinutes));
    private void AdjustDuration(int deltaMinutes)
    {
        currentDurationMinutes = Mathf.Max(minDurationMinutes, currentDurationMinutes + deltaMinutes);
        UpdateScalarUI();
        UpdateCostUI();
    }

    private void UpdateCostUI()
    {
        if (recruitmentManager == null || costText == null) return;

        if (recruitmentManager.IsCampaignActive)
        {
            costText.text = $"Spent: {recruitmentManager.CurrentCampaignCost:0}";
        }
        else
        {
            float durationSeconds = Mathf.Max(30f, currentDurationMinutes * 60f);
            float estimate = recruitmentManager.GetEstimatedCost(durationSeconds);
            costText.text = $"Estimated: {estimate:0}";
        }
    }

    private void SyncSettingsFromManager()
    {
        if (recruitmentManager == null) return;
        var settings = recruitmentManager.GetCurrentSettings();
        currentPowerTarget = Mathf.Max(minPowerValue, settings.targetPower);
        currentUpkeepTarget = Mathf.Max(minUpkeepValue, settings.maxUpkeep);
        currentDurationMinutes = settings.durationSeconds > 0f
            ? Mathf.Max(minDurationMinutes, Mathf.RoundToInt(settings.durationSeconds / 60f))
            : Mathf.Max(minDurationMinutes, currentDurationMinutes);

        prioritizedTraits.Clear();
        if (settings.prioritizedTraitIds != null)
        {
            foreach (var id in settings.prioritizedTraitIds)
            {
                if (!string.IsNullOrEmpty(id))
                {
                    prioritizedTraits.Add(id);
                }
            }
        }
        UpdateScalarUI();
    }

    private bool CanPostHiringAd()
    {
        if (recruitmentManager == null) return false;
        if (!TutorialManager.IsActionAllowed(TutorialIds.PostHiringAd)) return false;

        var gm = GameManager.Instance;
        var tm = gm != null ? gm.GetTimeManager() : null;
        if (tm != null && tm.GetDayState() != TimeManager.DayState.Active) return false;
        if (gm != null && gm.GetUnpaidUpkeepStreak() >= 2) return false;

        HunterManager hunterManager = gm != null ? gm.GetHunterManager() : null;
        if (hunterManager != null && hunterManager.IsAtHunterLimit()) return false;

        return true;
    }

    private string GetPostHiringAdUnavailableReason()
    {
        if (recruitmentManager == null) return "Hiring is not ready.";
        if (recruitmentManager.IsCampaignActive) return "A hiring ad is already active.";
        if (!TutorialManager.IsActionAllowed(TutorialIds.PostHiringAd)) return "Unavailable during the current tutorial step.";

        var gm = GameManager.Instance;
        var tm = gm != null ? gm.GetTimeManager() : null;
        if (tm != null && tm.GetDayState() != TimeManager.DayState.Active) return "Hiring ads can only be posted during the workday.";
        if (gm != null && gm.GetUnpaidUpkeepStreak() >= 2) return "Critical upkeep debt blocks new hiring ads.";

        HunterManager hunterManager = gm != null ? gm.GetHunterManager() : null;
        if (hunterManager != null && hunterManager.IsAtHunterLimit()) return "Hunter limit reached.";

        return "Cannot post a hiring ad right now.";
    }
}
