using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class HunterRecruitmentManager : MonoBehaviour
{
    [Serializable]
    public class RecruitmentCandidate
    {
        public HunterData hunter;
        public CandidateStatus status;
        public DateTime timestamp;
        [NonSerialized] public Hunter spawnedHunter;
        [NonSerialized] public HunterCandidateController controller;
        [NonSerialized] public Transform assignedSpot;
    }

    [Serializable]
    public struct AdSettings
    {
        public int targetPower;
        public int maxUpkeep;
        public float durationSeconds;
        public List<string> prioritizedTraitIds;
    }

    [Serializable]
    private class RecruitmentSaveData
    {
        public List<string> hiredHunterIds = new List<string>();
        public bool campaignActive;
        public float campaignTimeRemaining;
        public float nextArrivalTimer;
        public float currentCampaignCost;
        public float burnAccumulator;
        public AdSettingsData adSettings;
        public List<CandidateSaveData> candidates = new List<CandidateSaveData>();
        public List<string> seenCandidateIds = new List<string>();
        public List<HunterManager.HunterSaveState> hunterSaveStates = new List<HunterManager.HunterSaveState>();
    }

    [Serializable]
    private class AdSettingsData
    {
        public int targetPower;
        public int maxUpkeep;
        public float durationSeconds;
        public List<string> prioritizedTraitIds = new List<string>();
    }

    [Serializable]
    private class CandidateSaveData
    {
        public string hunterId;
        public CandidateStatus status;
    }

    public enum CandidateStatus
    {
        Pending,
        Hired,
        Declined
    }

    [Header("References")]
    [SerializeField] private HunterManager hunterManager;
    [SerializeField] private GoldManager goldManager;
    [SerializeField] private ReputationManager reputationManager;
    [SerializeField] private GlobalHunterConfig hunterConfig;
    [Header("Candidate Spawning")]
    [SerializeField] private Transform candidateSpawnPoint;
    [SerializeField] private Transform candidateExitPoint;
    [SerializeField] private List<Transform> candidateWaitingSpots = new List<Transform>();
    [SerializeField] private CandidateProfilePanel candidateProfilePanel;
    [SerializeField] private Camera candidateInteractionCamera;
    [SerializeField] private float candidateCameraTransitionDuration = 0.5f;

    [Header("Scoring Weights")]
    [SerializeField] private float traitPriorityWeight = 2f;
    [SerializeField] private float powerPriorityWeight = 1f;
    [SerializeField] private float upkeepPriorityWeight = 1f;

    public event Action OnStateChanged;
    public event Action<RecruitmentCandidate> OnCandidateArrived;
    public event Action<string> OnCampaignEnded;

    private readonly List<RecruitmentCandidate> candidateQueue = new List<RecruitmentCandidate>();
    private readonly Dictionary<Transform, RecruitmentCandidate> occupiedSpots = new Dictionary<Transform, RecruitmentCandidate>();
    private readonly HashSet<string> seenThisCampaign = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private AdSettings activeSettings;
    private bool campaignActive;
    private float campaignTimeRemaining;
    private float nextArrivalTimer;
    private float burnAccumulator;
    private float campaignCost;
    private int baseFee;
    private int costPerMinute;
    private Vector2 arrivalInterval = new Vector2(45f, 60f);
    private int maxQueueSize = 3;
    private string savePath;
    private Vector3 candidateCameraHomePosition;
    private Quaternion candidateCameraHomeRotation;
    private bool candidateCameraCached;
    private Coroutine candidateCameraRoutine;
    private TimeManager timeManager;

    private void Awake()
    {
        if (hunterManager == null)
        {
            hunterManager = FindObjectOfType<HunterManager>();
        }
        if (goldManager == null && GameManager.Instance != null)
        {
            goldManager = GameManager.Instance.GetGoldManager();
        }
        if (reputationManager == null && GameManager.Instance != null)
        {
            reputationManager = GameManager.Instance.GetReputationManager();
        }
        if (hunterConfig == null)
        {
            hunterConfig = HunterData.GetGlobalConfig();
        }

        if (hunterConfig != null)
        {
            baseFee = hunterConfig.GetBasePostingFee();
            costPerMinute = hunterConfig.GetCostPerMinute();
            arrivalInterval = hunterConfig.GetArrivalIntervalSeconds();
            maxQueueSize = hunterConfig.GetMaxCandidateQueueSize();
        }
        if (GameManager.Instance != null)
        {
            timeManager = GameManager.Instance.GetTimeManager();
        }
        if (candidateProfilePanel != null)
        {
            candidateProfilePanel.Initialize(this);
        }

        candidateWaitingSpots.RemoveAll(t => t == null);
        CacheCandidateCameraHome();

        savePath = Path.Combine(Application.persistentDataPath, "recruitment_state.json");
        EnsureActiveSettingsInitialized();
        LoadState();
    }

    private void Update()
    {
        if (timeManager != null && timeManager.IsActionBasedTime())
        {
            // Driven by time manager events
            return;
        }
        if (!campaignActive) return;
        if (hunterManager != null && hunterManager.IsAtHunterLimit())
        {
            StopCampaign("Hunter limit reached");
            return;
        }
        float delta = Time.deltaTime;
        if (campaignTimeRemaining > 0f)
        {
            campaignTimeRemaining = Mathf.Max(0f, campaignTimeRemaining - delta);
            HandleBurn(delta);
            HandleArrivals(delta);
            if (campaignTimeRemaining <= 0f)
            {
                StopCampaign("Campaign time elapsed");
            }
        }
    }

    private void OnEnable()
    {
        if (timeManager == null && GameManager.Instance != null)
        {
            timeManager = GameManager.Instance.GetTimeManager();
        }
        if (timeManager != null)
        {
            timeManager.OnTimeUpdate += HandleTimeAdvanced;
        }
    }

    private void OnDisable()
    {
        if (timeManager != null)
        {
            timeManager.OnTimeUpdate -= HandleTimeAdvanced;
        }
    }

    private void HandleTimeAdvanced(float deltaSeconds)
    {
        if (!campaignActive) return;
        if (hunterManager != null && hunterManager.IsAtHunterLimit())
        {
            StopCampaign("Hunter limit reached");
            return;
        }

        if (deltaSeconds <= 0f) return;
        if (campaignTimeRemaining > 0f)
        {
            campaignTimeRemaining = Mathf.Max(0f, campaignTimeRemaining - deltaSeconds);
            HandleBurn(deltaSeconds);
            HandleArrivals(deltaSeconds);
            if (campaignTimeRemaining <= 0f)
            {
                StopCampaign("Campaign time elapsed");
            }
        }
    }

    private void HandleBurn(float deltaTime)
    {
        if (costPerMinute <= 0 || goldManager == null) return;
        burnAccumulator += (costPerMinute / 60f) * deltaTime;
        int burnInt = Mathf.FloorToInt(burnAccumulator);
        if (burnInt <= 0) return;

        if (!goldManager.SpendGold(burnInt))
        {
            StopCampaign("Insufficient gold");
            return;
        }

        burnAccumulator -= burnInt;
        campaignCost += burnInt;
        OnStateChanged?.Invoke();
    }

    private void HandleArrivals(float deltaTime)
    {
        nextArrivalTimer -= deltaTime;
        if (nextArrivalTimer > 0f) return;

        if (TryGenerateCandidate())
        {
            float min = Mathf.Max(5f, arrivalInterval.x);
            float max = Mathf.Max(min, arrivalInterval.y);
            nextArrivalTimer = UnityEngine.Random.Range(min, max);
            SaveState();
            OnStateChanged?.Invoke();
        }
        else
        {
            nextArrivalTimer = 15f;
        }
    }

    private bool TryGenerateCandidate()
    {
        if (hunterManager == null) return false;
        if (maxQueueSize > 0 && candidateQueue.Count >= maxQueueSize) return false;
        int reputation = reputationManager != null ? reputationManager.GetReputation() : 0;
        var pool = hunterManager.GetRecruitableHunters(reputation);
        if (pool == null || pool.Count == 0) return false;

        List<HunterData> candidates = new List<HunterData>();
        List<float> weights = new List<float>();
        foreach (var data in pool)
        {
            if (data == null) continue;
            if (seenThisCampaign.Contains(data.hunterId)) continue;
            float weight = EvaluateCandidateWeight(data);
            if (weight <= 0f) continue;
            candidates.Add(data);
            weights.Add(weight);
        }

        if (candidates.Count == 0) return false;

        HunterData selected = WeightedPick(candidates, weights);
        if (selected == null) return false;

        var entry = new RecruitmentCandidate
        {
            hunter = selected,
            status = CandidateStatus.Pending,
            timestamp = DateTime.UtcNow
        };

        seenThisCampaign.Add(selected.hunterId);
        candidateQueue.Add(entry);
        TrySpawnCandidate(entry);
        OnCandidateArrived?.Invoke(entry);
        return true;
    }

    private float EvaluateCandidateWeight(HunterData data)
    {
        if (data == null) return 0f;
        EnsureActiveSettingsInitialized();
        float rarityWeight = 1f;
        var rarityEntry = hunterConfig != null ? hunterConfig.GetRarity(data.rarity) : null;
        if (rarityEntry != null)
        {
            rarityWeight = Mathf.Max(0.01f, rarityEntry.recruitmentWeight);
        }
        if (hunterManager != null)
        {
            rarityWeight *= hunterManager.GetRecruitmentRarityWeightMultiplier(data.rarity);
        }

        float powerScore = 1f;
        if (activeSettings.targetPower > 0)
        {
            int power = data.GetTotalPower(data.startingLevel);
            float diff = Mathf.Abs(activeSettings.targetPower - power);
            powerScore = Mathf.Clamp01(1f - diff / Mathf.Max(1f, activeSettings.targetPower));
        }

        float upkeepScore = 1f;
        if (activeSettings.maxUpkeep > 0)
        {
            float diff = data.dailyUpkeepCost - activeSettings.maxUpkeep;
            upkeepScore = diff <= 0 ? 1f : Mathf.Clamp01(1f - diff / Mathf.Max(1f, activeSettings.maxUpkeep));
        }

        float traitScore = 0f;
        if (activeSettings.prioritizedTraitIds != null && activeSettings.prioritizedTraitIds.Count > 0)
        {
            int matches = 0;
            var set = new HashSet<string>(activeSettings.prioritizedTraitIds, StringComparer.OrdinalIgnoreCase);
            foreach (var trait in data.traits)
            {
                if (trait == null) continue;
                if (!string.IsNullOrEmpty(trait.traitId) && set.Contains(trait.traitId))
                {
                    matches++;
                }
            }
            traitScore = (float)matches / activeSettings.prioritizedTraitIds.Count;
        }

        float weighted =
            0.5f +
            traitScore * traitPriorityWeight +
            powerScore * powerPriorityWeight +
            upkeepScore * upkeepPriorityWeight;

        return Mathf.Max(0.01f, weighted) * rarityWeight;
    }

    private HunterData WeightedPick(List<HunterData> candidates, List<float> weights)
    {
        if (candidates.Count == 0) return null;
        float total = 0f;
        foreach (var w in weights) total += Mathf.Max(0f, w);
        if (total <= 0f) return candidates[UnityEngine.Random.Range(0, candidates.Count)];

        float roll = UnityEngine.Random.Range(0f, total);
        float cumulative = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            cumulative += Mathf.Max(0f, weights[i]);
            if (roll <= cumulative)
            {
                return candidates[i];
            }
        }
        return candidates[candidates.Count - 1];
    }

    private RecruitmentCandidate FindCandidateByHunter(Hunter hunterInstance)
    {
        if (hunterInstance == null) return null;
        foreach (var candidate in candidateQueue)
        {
            if (candidate != null && candidate.spawnedHunter == hunterInstance)
            {
                return candidate;
            }
        }
        return null;
    }

    private bool TrySpawnCandidate(RecruitmentCandidate candidate)
    {
        if (candidate == null || candidate.spawnedHunter != null) return true;
        if (hunterManager == null) return false;

        Transform spot = GetAvailableWaitingSpot();
        if (spot == null) return false;

        Hunter instance = hunterManager.CreateCandidateInstance(candidate.hunter, candidateSpawnPoint);
        if (instance == null)
        {
            return false;
        }

        candidate.spawnedHunter = instance;
        candidate.assignedSpot = spot;
        occupiedSpots[spot] = candidate;

        HunterCandidateController controller = instance.GetComponent<HunterCandidateController>();
        if (controller == null)
        {
            controller = instance.gameObject.AddComponent<HunterCandidateController>();
        }
        controller.Initialize(this, candidate, candidateSpawnPoint, spot, candidateExitPoint);
        candidate.controller = controller;

        HunterInteractable interactable = instance.GetComponentInChildren<HunterInteractable>();
        if (interactable == null)
        {
            interactable = instance.gameObject.AddComponent<HunterInteractable>();
        }
        interactable.Initialize(this, candidate, candidateInteractionCamera);
        interactable.SetInteractionEnabled(false);

        return true;
    }

    private Transform GetAvailableWaitingSpot()
    {
        foreach (var spot in candidateWaitingSpots)
        {
            if (spot == null) continue;
            if (!occupiedSpots.ContainsKey(spot))
            {
                return spot;
            }
        }
        return null;
    }

    private void ReleaseWaitingSpot(RecruitmentCandidate candidate)
    {
        if (candidate == null || candidate.assignedSpot == null) return;
        if (occupiedSpots.TryGetValue(candidate.assignedSpot, out var stored) && stored == candidate)
        {
            occupiedSpots.Remove(candidate.assignedSpot);
        }
        candidate.assignedSpot = null;
    }

    private void RemoveCandidateObjectLinks(RecruitmentCandidate candidate, bool keepHunterAlive)
    {
        if (candidate == null) return;

        if (candidate.controller != null)
        {
            Destroy(candidate.controller);
            candidate.controller = null;
        }

        if (candidate.spawnedHunter != null)
        {
            if (!keepHunterAlive && hunterManager != null)
            {
                hunterManager.DestroyCandidateInstance(candidate.spawnedHunter);
            }
        }

        candidate.spawnedHunter = null;
    }

    public void HandleCandidateExited(RecruitmentCandidate candidate)
    {
        if (candidate == null) return;

        ReleaseWaitingSpot(candidate);
        RemoveCandidateObjectLinks(candidate, keepHunterAlive: false);
        candidateQueue.Remove(candidate);
        SaveState();
        OnStateChanged?.Invoke();
        TryActivatePendingCandidates();
    }

    private void ClearAllCandidates()
    {
        foreach (var candidate in candidateQueue)
        {
            ReleaseWaitingSpot(candidate);
            RemoveCandidateObjectLinks(candidate, keepHunterAlive: false);
        }
        candidateQueue.Clear();
        occupiedSpots.Clear();
    }

    private void TryActivatePendingCandidates()
    {
        foreach (var candidate in candidateQueue)
        {
            if (candidate == null) continue;
            if (candidate.status != CandidateStatus.Pending) continue;
            if (candidate.spawnedHunter != null) continue;
            if (!TrySpawnCandidate(candidate))
            {
                break;
            }
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool DebugForceCandidate(string hunterId)
    {
        if (string.IsNullOrWhiteSpace(hunterId) || hunterManager == null) return false;
        var data = hunterManager.GetHunterDataById(hunterId);
        return DebugForceCandidate(data);
    }

    public bool DebugForceCandidate(HunterData data)
    {
        if (data == null) return false;
        var entry = new RecruitmentCandidate
        {
            hunter = data,
            status = CandidateStatus.Pending,
            timestamp = DateTime.UtcNow
        };

        candidateQueue.Add(entry);
        if (maxQueueSize > 0 && candidateQueue.Count > maxQueueSize)
        {
            var removed = candidateQueue[0];
            candidateQueue.RemoveAt(0);
            ReleaseWaitingSpot(removed);
            RemoveCandidateObjectLinks(removed, keepHunterAlive: false);
        }

        bool spawned = TrySpawnCandidate(entry);
        SaveState();
        OnStateChanged?.Invoke();
        return spawned;
    }
#endif

    public bool IsCampaignActive => campaignActive;
    public float CampaignTimeRemaining => campaignTimeRemaining;
    public float CurrentCampaignCost => campaignCost;
    public float GetEstimatedCost(float durationSeconds)
    {
        float minutes = Mathf.Max(0f, durationSeconds) / 60f;
        return Mathf.Max(0f, baseFee + costPerMinute * minutes);
    }

    public void PostAd(AdSettings settings)
    {
        if (campaignActive)
        {
            StopCampaign(reason: null, notify: false);
        }

        if (hunterManager != null && hunterManager.IsAtHunterLimit())
        {
            Debug.LogWarning("HunterRecruitment: Hunter limit reached. Cannot start campaign.");
            return;
        }

        ClearAllCandidates();

        if (goldManager != null && baseFee > 0 && !goldManager.SpendGold(baseFee))
        {
            return;
        }

        EnsureActiveSettingsInitialized();
        float durationSeconds = Mathf.Max(30f, settings.durationSeconds);
        activeSettings.targetPower = settings.targetPower;
        activeSettings.maxUpkeep = settings.maxUpkeep;
        activeSettings.durationSeconds = durationSeconds;
        activeSettings.prioritizedTraitIds = settings.prioritizedTraitIds != null
            ? new List<string>(settings.prioritizedTraitIds)
            : new List<string>();
        campaignActive = true;
        campaignTimeRemaining = durationSeconds;
        campaignCost = baseFee;
        burnAccumulator = 0f;
        candidateQueue.Clear();
        seenThisCampaign.Clear();

        float min = Mathf.Max(5f, arrivalInterval.x);
        float max = Mathf.Max(min, arrivalInterval.y);
        nextArrivalTimer = UnityEngine.Random.Range(min, max);

        SaveState();
        OnStateChanged?.Invoke();

        var config = GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null;
        var tm = GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null;
        float cost = config != null ? config.actionTimeSettings.postAdSeconds : 0f;
        tm?.AdvanceTime(cost);
    }

    public void StopCampaign()
    {
        StopCampaign(reason: null, notify: true);
    }

    public void StopCampaign(string reason, bool notify = true)
    {
        bool wasActive = campaignActive;
        campaignActive = false;
        campaignTimeRemaining = 0f;
        burnAccumulator = 0f;
        campaignCost = 0f;
        SaveState();
        OnStateChanged?.Invoke();
        if (wasActive && notify)
        {
            OnCampaignEnded?.Invoke(reason);
        }
    }

    public void HireCandidate(RecruitmentCandidate candidate)
    {
        if (!CanModifyCandidate(candidate)) return;
        if (hunterManager == null) return;
        if (candidate.spawnedHunter == null) return;

        candidate.status = CandidateStatus.Hired;
        candidateProfilePanel?.HandleCandidateResolved(candidate);
        candidate.controller?.CancelNavigation();

        if (hunterManager.TryHireCandidate(candidate.spawnedHunter))
        {
            ReleaseWaitingSpot(candidate);
            RemoveCandidateObjectLinks(candidate, keepHunterAlive: true);
            candidateQueue.Remove(candidate);
            SaveState();
            OnStateChanged?.Invoke();
            TryActivatePendingCandidates();
        }
        else
        {
            candidate.status = CandidateStatus.Pending;
        }

        var config = GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null;
        var tm = GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null;
        float cost = config != null ? config.actionTimeSettings.hireOrDeclineSeconds : 0f;
        tm?.AdvanceTime(cost);
    }

    public void DeclineCandidate(RecruitmentCandidate candidate)
    {
        if (!CanModifyCandidate(candidate)) return;
        candidate.status = CandidateStatus.Declined;
        candidateProfilePanel?.HandleCandidateResolved(candidate);

        if (candidate.controller != null)
        {
            candidate.controller.LeaveGuild();
        }
        else
        {
            HandleCandidateExited(candidate);
        }

        OnStateChanged?.Invoke();

        var config = GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null;
        var tm = GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null;
        float cost = config != null ? config.actionTimeSettings.hireOrDeclineSeconds : 0f;
        tm?.AdvanceTime(cost);
    }

    public bool ShowCandidateProfile(Hunter hunterInstance, Action onClosed, bool onlyIfPending = true)
    {
        if (hunterInstance == null || candidateProfilePanel == null) return false;
        var candidate = FindCandidateByHunter(hunterInstance);
        if (candidate == null) return false;
        if (onlyIfPending && candidate.status != CandidateStatus.Pending) return false;
        candidateProfilePanel.ShowCandidate(candidate, onClosed);

        var config = GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null;
        var tm = GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null;
        float cost = config != null ? config.actionTimeSettings.reviewCandidateSeconds : 0f;
        tm?.AdvanceTime(cost);
        return true;
    }

    private bool CanModifyCandidate(RecruitmentCandidate candidate)
    {
        return candidate != null && candidate.status == CandidateStatus.Pending;
    }

    public List<HunterTrait> GetAvailableTraits()
    {
        List<HunterTrait> traits = new List<HunterTrait>();
        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (hunterManager == null) return traits;
        int rep = reputationManager != null ? reputationManager.GetReputation() : 0;
        var pool = hunterManager.GetRecruitableHunters(rep);
        foreach (var data in pool)
        {
            if (data == null || data.traits == null) continue;
            foreach (var trait in data.traits)
            {
                if (trait == null || string.IsNullOrEmpty(trait.traitId)) continue;
                if (ids.Add(trait.traitId))
                {
                    traits.Add(trait);
                }
            }
        }
        return traits;
    }

    private void OnApplicationQuit()
    {
        SaveState();
    }

    private void OnDestroy()
    {
        SaveState();
    }

    private void SaveState()
    {
        RecruitmentSaveData data = new RecruitmentSaveData
        {
            hiredHunterIds = hunterManager != null ? hunterManager.GetHiredHunterIds() : new List<string>(),
            hunterSaveStates = hunterManager != null ? hunterManager.GetHunterSaveStates() : new List<HunterManager.HunterSaveState>(),
            campaignActive = campaignActive,
            campaignTimeRemaining = campaignTimeRemaining,
            nextArrivalTimer = nextArrivalTimer,
            currentCampaignCost = campaignCost,
            burnAccumulator = burnAccumulator,
            adSettings = new AdSettingsData
            {
                targetPower = activeSettings.targetPower,
                maxUpkeep = activeSettings.maxUpkeep,
                durationSeconds = activeSettings.durationSeconds,
                prioritizedTraitIds = activeSettings.prioritizedTraitIds != null
                    ? new List<string>(activeSettings.prioritizedTraitIds)
                    : new List<string>()
            }
        };

        foreach (var entry in candidateQueue)
        {
            if (entry?.hunter == null) continue;
            if (entry.status != CandidateStatus.Pending) continue;
            data.candidates.Add(new CandidateSaveData
            {
                hunterId = entry.hunter.hunterId,
                status = entry.status
            });
        }

        data.seenCandidateIds.AddRange(seenThisCampaign);

        string json = JsonUtility.ToJson(data);
        File.WriteAllText(savePath, json);
    }

    private void LoadState()
    {
        if (!File.Exists(savePath))
        {
            return;
        }

        string json = File.ReadAllText(savePath);
        if (string.IsNullOrEmpty(json))
        {
            return;
        }

        RecruitmentSaveData data = JsonUtility.FromJson<RecruitmentSaveData>(json);
        if (data == null)
        {
            return;
        }

        hunterManager?.LoadHunterSaveStates(data.hunterSaveStates);
        hunterManager?.LoadHiredHunters(data.hiredHunterIds);

        candidateQueue.Clear();
        occupiedSpots.Clear();
        seenThisCampaign.Clear();

        foreach (var id in data.seenCandidateIds)
        {
            if (!string.IsNullOrEmpty(id))
            {
                seenThisCampaign.Add(id);
            }
        }

        if (data.adSettings != null)
        {
            activeSettings.targetPower = data.adSettings.targetPower;
            activeSettings.maxUpkeep = data.adSettings.maxUpkeep;
            activeSettings.durationSeconds = data.adSettings.durationSeconds;
            activeSettings.prioritizedTraitIds = data.adSettings.prioritizedTraitIds != null
                ? new List<string>(data.adSettings.prioritizedTraitIds)
                : new List<string>();
        }
        else
        {
            EnsureActiveSettingsInitialized();
        }

        campaignActive = data.campaignActive;
        campaignTimeRemaining = data.campaignTimeRemaining;
        nextArrivalTimer = data.nextArrivalTimer;
        campaignCost = data.currentCampaignCost;
        burnAccumulator = data.burnAccumulator;

        if (campaignActive && campaignTimeRemaining <= 0f)
        {
            campaignActive = false;
        }

        if (data.candidates != null)
        {
            foreach (var candidate in data.candidates)
            {
                if (string.IsNullOrEmpty(candidate.hunterId)) continue;
                var hunter = hunterManager != null
                    ? hunterManager.GetHunterDataById(candidate.hunterId)
                    : null;
                if (hunter == null) continue;
                candidateQueue.Add(new RecruitmentCandidate
                {
                    hunter = hunter,
                    status = candidate.status,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        TryActivatePendingCandidates();
    }

    private void CacheCandidateCameraHome()
    {
        if (candidateInteractionCamera == null || candidateCameraCached) return;
        candidateCameraHomePosition = candidateInteractionCamera.transform.position;
        candidateCameraHomeRotation = candidateInteractionCamera.transform.rotation;
        candidateCameraCached = true;
        candidateInteractionCamera.gameObject.SetActive(false);
    }

    public void ToggleCandidateCamera(bool entering, Camera playerCamera)
    {
        if (candidateInteractionCamera == null) return;
        CacheCandidateCameraHome();
        if (candidateCameraRoutine != null)
        {
            StopCoroutine(candidateCameraRoutine);
        }
        candidateCameraRoutine = StartCoroutine(HandleCandidateCameraTransition(entering, playerCamera));
    }

    private System.Collections.IEnumerator HandleCandidateCameraTransition(bool entering, Camera playerCamera)
    {
        CacheCandidateCameraHome();
        float duration = Mathf.Max(0.05f, candidateCameraTransitionDuration);
        Camera sourceCamera = playerCamera != null ? playerCamera : Camera.main;

        if (entering)
        {
            Vector3 startPos = sourceCamera != null ? sourceCamera.transform.position : candidateCameraHomePosition;
            Quaternion startRot = sourceCamera != null ? sourceCamera.transform.rotation : candidateCameraHomeRotation;
            Vector3 endPos = candidateCameraHomePosition;
            Quaternion endRot = candidateCameraHomeRotation;

            if (sourceCamera != null)
            {
                sourceCamera.enabled = false;
            }

            candidateInteractionCamera.transform.SetPositionAndRotation(startPos, startRot);
            candidateInteractionCamera.gameObject.SetActive(true);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                candidateInteractionCamera.transform.position = Vector3.Lerp(startPos, endPos, t);
                candidateInteractionCamera.transform.rotation = Quaternion.Slerp(startRot, endRot, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            candidateInteractionCamera.transform.position = endPos;
            candidateInteractionCamera.transform.rotation = endRot;
        }
        else
        {
            Vector3 startPos = candidateInteractionCamera.transform.position;
            Quaternion startRot = candidateInteractionCamera.transform.rotation;
            Vector3 endPos = sourceCamera != null ? sourceCamera.transform.position : candidateCameraHomePosition;
            Quaternion endRot = sourceCamera != null ? sourceCamera.transform.rotation : candidateCameraHomeRotation;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                candidateInteractionCamera.transform.position = Vector3.Lerp(startPos, endPos, t);
                candidateInteractionCamera.transform.rotation = Quaternion.Slerp(startRot, endRot, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            candidateInteractionCamera.transform.position = candidateCameraHomePosition;
            candidateInteractionCamera.transform.rotation = candidateCameraHomeRotation;
            candidateInteractionCamera.gameObject.SetActive(false);

            if (sourceCamera != null)
            {
                sourceCamera.enabled = true;
            }
        }
        candidateCameraRoutine = null;
    }

    public AdSettings GetCurrentSettings()
    {
        EnsureActiveSettingsInitialized();
        return new AdSettings
        {
            targetPower = activeSettings.targetPower,
            maxUpkeep = activeSettings.maxUpkeep,
            durationSeconds = activeSettings.durationSeconds,
            prioritizedTraitIds = activeSettings.prioritizedTraitIds != null
                ? new List<string>(activeSettings.prioritizedTraitIds)
                : new List<string>()
        };
    }

    private void EnsureActiveSettingsInitialized()
    {
        if (activeSettings.prioritizedTraitIds == null)
        {
            activeSettings.prioritizedTraitIds = new List<string>();
        }

        if (activeSettings.durationSeconds <= 0f)
        {
            activeSettings.durationSeconds = 120f;
        }
    }
}
