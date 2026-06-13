using System;
using System.Collections.Generic;
using UnityEngine;

public class KitchenManager : MonoBehaviour
{
    public static KitchenManager Instance { get; private set; }

    [Serializable]
    private class KitchenSaveData
    {
        public int dayIndex;
        public string recipeId;
        public string counterTraitId;
        public List<string> fedHunterIds = new List<string>();
        public List<string> dirtySeatIds = new List<string>();
    }

    [Serializable]
    public class ServingPoint
    {
        public Transform point;
        [NonSerialized] public Hunter hunter;
    }

    private class QueueSlot
    {
        public Transform point;
        public Hunter hunter;
        public bool arrived;
    }

    [Serializable]
    public struct KitchenBuffAggregate
    {
        public float successChanceBonusPercent;
        public float woundChanceReductionPercent;
        public float deathChanceReductionPercent;
        public float missionTimeReductionPercent;
    }

    private const string SaveKey = "GuildKitchenState";

    [Header("Unlock")]
    [SerializeField] private GuildConstructionManager constructionManager;
    [SerializeField] private GuildConstructionDefinition kitchenConstruction;

    [Header("Recipes")]
    [SerializeField] private List<KitchenRecipe> recipes = new List<KitchenRecipe>();
    [SerializeField] private bool useMonsterLibraryAsCounterFallback = true;

    [Header("Visuals")]
    [SerializeField] private Transform potAnchor;
    [SerializeField] private GameObject defaultPotPrefab;
    [SerializeField] private GameObject dirtyPlatePrefab;

    [Header("Hunter Flow")]
    [SerializeField] private List<ServingPoint> servingPoints = new List<ServingPoint>();
    [Tooltip("Optional ordered line positions. Element 0 is the front of the line, closest to the pot.")]
    [SerializeField] private List<Transform> queuePoints = new List<Transform>();
    [SerializeField] private List<HunterSeat> diningSeats = new List<HunterSeat>();
    [SerializeField] private SharedCharacterAnimator.ClipEntry eatingClip;
    [SerializeField] private float fallbackEatingSeconds = 2f;
    [SerializeField] private float scanIntervalSeconds = 0.5f;
    [SerializeField] private List<GuildDoorController> routeDoorsToOpen = new List<GuildDoorController>();

    [Header("Caps")]
    [SerializeField] private float maxWoundReductionPercent = 75f;
    [SerializeField] private float maxDeathReductionPercent = 75f;
    [SerializeField] private float maxMissionTimeReductionPercent = 75f;

    private readonly HashSet<string> fedHunterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Hunter> feedingHunters = new HashSet<Hunter>();
    private readonly HashSet<string> dirtySeatIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Hunter, HunterSeat> returnSeats = new Dictionary<Hunter, HunterSeat>();
    private readonly Dictionary<string, KitchenRecipe> recipeLookup = new Dictionary<string, KitchenRecipe>(StringComparer.OrdinalIgnoreCase);
    private readonly List<QueueSlot> queueSlots = new List<QueueSlot>();

    private HunterManager hunterManager;
    private TimeManager timeManager;
    private TimeManager subscribedTimeManager;
    private KitchenRecipe currentRecipe;
    private MonsterTrait rolledCounterTrait;
    private GameObject potInstance;
    private float scanTimer;
    private bool loadedState;

    public event Action OnStateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple KitchenManager instances found. The newest one will replace the static instance.", this);
        }

        Instance = this;
        BuildRecipeLookup();
        RebuildQueueSlots();
        ResolveReferences();
    }

    private void OnValidate()
    {
        RebuildQueueSlots();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToTimeManager();
    }

    private void Start()
    {
        ResolveReferences();
        if (!loadedState)
        {
            LoadState();
        }
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (subscribedTimeManager != null)
        {
            subscribedTimeManager.OnDayStarted -= HandleDayStarted;
            subscribedTimeManager.OnDayStateChanged -= HandleDayStateChanged;
            subscribedTimeManager = null;
        }
    }

    private void Update()
    {
        if (!IsUnlocked()) return;
        if (!HasActiveRecipe()) return;
        if (timeManager == null || timeManager.GetDayState() != TimeManager.DayState.Active) return;

        scanTimer -= Time.deltaTime;
        if (scanTimer > 0f) return;
        scanTimer = Mathf.Max(0.1f, scanIntervalSeconds);

        CleanupInvalidFlowHunters();
        ProcessServingQueue();
        TryAssignHuntersToEat();
        ProcessServingQueue();
    }

    public IReadOnlyList<KitchenRecipe> GetRecipes()
    {
        return recipes;
    }

    public KitchenRecipe GetCurrentRecipe()
    {
        return currentRecipe;
    }

    public MonsterTrait GetRolledCounterTrait()
    {
        return rolledCounterTrait;
    }

    public bool HasActiveRecipe()
    {
        return currentRecipe != null;
    }

    public bool CanOpenRecipeUI()
    {
        ResolveReferences();
        return IsUnlocked();
    }

    public bool CanChooseRecipe()
    {
        ResolveReferences();
        return IsUnlocked()
            && currentRecipe == null
            && timeManager != null
            && timeManager.GetDayState() == TimeManager.DayState.Active;
    }

    public bool TryChooseRecipe(KitchenRecipe recipe)
    {
        if (recipe == null || !CanChooseRecipe()) return false;

        currentRecipe = recipe;
        rolledCounterTrait = RollCounterTrait(recipe);
        fedHunterIds.Clear();
        feedingHunters.Clear();
        returnSeats.Clear();
        ClearQueueSlots();

        SpawnPotVisual();

        var config = GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null;
        var tm = GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null;
        float cost = config != null ? config.actionTimeSettings.chooseKitchenRecipeSeconds : 0f;
        tm?.AdvanceTime(cost);

        SaveState();
        OnStateChanged?.Invoke();
        return true;
    }

    public bool TryCleanPlate(KitchenDirtyPlate plate)
    {
        if (plate == null) return false;

        ResolveReferences();
        if (timeManager == null || timeManager.GetDayState() != TimeManager.DayState.Active) return false;

        var config = GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null;
        float cost = config != null ? config.actionTimeSettings.cleanKitchenPlateSeconds : 0f;
        timeManager.AdvanceTime(cost);

        HunterSeat seat = plate.GetSeat();
        if (seat != null)
        {
            dirtySeatIds.Remove(seat.SeatId);
            seat.ClearDirtyPlate(plate);
        }

        Destroy(plate.gameObject);
        SaveState();
        OnStateChanged?.Invoke();
        return true;
    }

    public bool IsHunterFed(Hunter hunter)
    {
        string id = GetHunterId(hunter);
        return !string.IsNullOrEmpty(id) && fedHunterIds.Contains(id);
    }

    public KitchenRecipe GetRecipeForHunter(Hunter hunter)
    {
        return IsHunterFed(hunter) ? currentRecipe : null;
    }

    public IEnumerable<string> GetCounteredTraitKeysForHunter(Hunter hunter)
    {
        if (!IsHunterFed(hunter) || rolledCounterTrait == null) yield break;

        if (!string.IsNullOrWhiteSpace(rolledCounterTrait.traitId))
        {
            yield return rolledCounterTrait.traitId;
        }
        if (!string.IsNullOrWhiteSpace(rolledCounterTrait.displayName))
        {
            yield return rolledCounterTrait.displayName;
        }
    }

    public KitchenBuffAggregate GetBuffAggregateForParty(List<Hunter> party)
    {
        KitchenBuffAggregate aggregate = new KitchenBuffAggregate();
        if (party == null || currentRecipe == null) return aggregate;

        foreach (var hunter in party)
        {
            if (!IsHunterFed(hunter)) continue;
            aggregate.successChanceBonusPercent += currentRecipe.successChanceBonusPercent;
            aggregate.woundChanceReductionPercent += currentRecipe.woundChanceReductionPercent;
            aggregate.deathChanceReductionPercent += currentRecipe.deathChanceReductionPercent;
            aggregate.missionTimeReductionPercent += currentRecipe.missionTimeReductionPercent;
        }

        aggregate.woundChanceReductionPercent = Mathf.Clamp(aggregate.woundChanceReductionPercent, 0f, Mathf.Max(0f, maxWoundReductionPercent));
        aggregate.deathChanceReductionPercent = Mathf.Clamp(aggregate.deathChanceReductionPercent, 0f, Mathf.Max(0f, maxDeathReductionPercent));
        aggregate.missionTimeReductionPercent = Mathf.Clamp(aggregate.missionTimeReductionPercent, 0f, Mathf.Max(0f, maxMissionTimeReductionPercent));
        return aggregate;
    }

    public static KitchenRecipe GetActiveRecipe(Hunter hunter)
    {
        return Instance != null ? Instance.GetRecipeForHunter(hunter) : null;
    }

    public static KitchenBuffAggregate GetActiveBuffAggregate(List<Hunter> party)
    {
        return Instance != null ? Instance.GetBuffAggregateForParty(party) : new KitchenBuffAggregate();
    }

    public static IEnumerable<string> GetActiveCounteredTraitKeys(Hunter hunter)
    {
        return Instance != null ? Instance.GetCounteredTraitKeysForHunter(hunter) : Array.Empty<string>();
    }

    private void TryAssignHuntersToEat()
    {
        ResolveReferences();
        if (hunterManager == null || currentRecipe == null) return;

        foreach (var hunter in hunterManager.GetAllHunters())
        {
            if (!CanHunterStartEating(hunter)) continue;

            HunterSeat returnSeat = hunter.GetAssignedSeat();
            if (returnSeat == null || returnSeat.HasDirtyPlate) continue;

            OpenRouteDoors();
            if (!AssignHunterToFoodFlow(hunter, returnSeat))
            {
                return;
            }
        }
    }

    private bool AssignHunterToFoodFlow(Hunter hunter, HunterSeat returnSeat)
    {
        if (hunter == null || returnSeat == null) return false;

        feedingHunters.Add(hunter);
        returnSeats[hunter] = returnSeat;

        if (queueSlots.Count > 0)
        {
            QueueSlot slot = FindJoinQueueSlot();
            if (slot == null)
            {
                feedingHunters.Remove(hunter);
                returnSeats.Remove(hunter);
                return false;
            }

            slot.hunter = hunter;
            slot.arrived = false;
            bool walkingToQueue = hunter.WalkToKitchenPoint(slot.point, arrived => HandleHunterArrivedAtQueueSlot(arrived, slot));
            if (!walkingToQueue)
            {
                ClearQueueSlot(slot);
                feedingHunters.Remove(hunter);
                returnSeats.Remove(hunter);
                return false;
            }
            return true;
        }

        ServingPoint point = FindFreeServingPoint();
        if (point == null)
        {
            feedingHunters.Remove(hunter);
            returnSeats.Remove(hunter);
            return false;
        }

        point.hunter = hunter;
        bool walking = hunter.WalkToKitchenPoint(point.point, arrived => HandleHunterArrivedAtPot(arrived, point));
        if (!walking)
        {
            point.hunter = null;
            feedingHunters.Remove(hunter);
            returnSeats.Remove(hunter);
            return false;
        }

        return true;
    }

    private bool CanHunterStartEating(Hunter hunter)
    {
        if (hunter == null) return false;
        if (hunter.GetState() != HunterState.Idle) return false;
        if (!hunter.IsSeated()) return false;
        if (feedingHunters.Contains(hunter)) return false;
        if (IsHunterFed(hunter)) return false;

        HunterSeat seat = hunter.GetAssignedSeat();
        return seat != null && seat.CanUseForGuildHall && !seat.HasDirtyPlate;
    }

    private ServingPoint FindFreeServingPoint()
    {
        if (servingPoints == null) return null;
        foreach (var point in servingPoints)
        {
            if (point != null && point.point != null && point.hunter == null)
            {
                return point;
            }
        }
        return null;
    }

    private QueueSlot FindJoinQueueSlot()
    {
        RebuildQueueSlots();
        if (queueSlots.Count == 0) return null;

        int lastOccupiedIndex = -1;
        for (int i = 0; i < queueSlots.Count; i++)
        {
            if (queueSlots[i].hunter != null)
            {
                lastOccupiedIndex = i;
            }
        }

        int joinIndex = lastOccupiedIndex + 1;
        if (joinIndex < 0 || joinIndex >= queueSlots.Count) return null;
        return queueSlots[joinIndex].hunter == null ? queueSlots[joinIndex] : null;
    }

    private void ProcessServingQueue()
    {
        RebuildQueueSlots();
        if (queueSlots.Count == 0) return;

        MoveFrontHunterToServingPoint();
        AdvanceQueueForward();
        MoveFrontHunterToServingPoint();
    }

    private void MoveFrontHunterToServingPoint()
    {
        if (queueSlots.Count == 0) return;
        QueueSlot front = queueSlots[0];
        if (front.hunter == null || !front.arrived) return;

        ServingPoint point = FindFreeServingPoint();
        if (point == null) return;

        Hunter hunter = front.hunter;
        ClearQueueSlot(front);
        point.hunter = hunter;

        bool walking = hunter.WalkToKitchenPoint(point.point, arrived => HandleHunterArrivedAtPot(arrived, point));
        if (!walking)
        {
            point.hunter = null;
            ClearFeedingHunter(hunter);
            hunter.ReturnToGuildSeat();
        }
    }

    private void AdvanceQueueForward()
    {
        for (int i = 1; i < queueSlots.Count; i++)
        {
            QueueSlot current = queueSlots[i];
            QueueSlot previous = queueSlots[i - 1];
            if (previous.hunter != null || current.hunter == null || !current.arrived) continue;

            Hunter hunter = current.hunter;
            ClearQueueSlot(current);
            previous.hunter = hunter;
            previous.arrived = false;

            bool walking = hunter.WalkToKitchenPoint(previous.point, arrived => HandleHunterArrivedAtQueueSlot(arrived, previous));
            if (!walking)
            {
                ClearQueueSlot(previous);
                ClearFeedingHunter(hunter);
                hunter.ReturnToGuildSeat();
            }
        }
    }

    private void HandleHunterArrivedAtQueueSlot(Hunter hunter, QueueSlot slot)
    {
        if (slot == null || hunter == null) return;
        if (slot.hunter != hunter) return;
        slot.arrived = true;
        ProcessServingQueue();
    }

    private void HandleHunterArrivedAtPot(Hunter hunter, ServingPoint point)
    {
        if (point != null && point.hunter == hunter)
        {
            point.hunter = null;
        }

        if (hunter == null || !returnSeats.TryGetValue(hunter, out HunterSeat seat) || seat == null)
        {
            ClearFeedingHunter(hunter);
            return;
        }

        if (seat.HasDirtyPlate || !hunter.WalkToTemporarySeat(seat, HandleHunterReturnedToSeat))
        {
            ClearFeedingHunter(hunter);
            hunter?.ReturnToGuildSeat();
        }
    }

    private void HandleHunterReturnedToSeat(Hunter hunter)
    {
        if (hunter == null)
        {
            ClearFeedingHunter(null);
            return;
        }

        if (!returnSeats.TryGetValue(hunter, out HunterSeat seat) || seat == null || seat.HasDirtyPlate)
        {
            ClearFeedingHunter(hunter);
            return;
        }

        bool played = hunter.PlayCustomAnimation(eatingClip, eatingClip != null && eatingClip.loop ? null : () => CompleteHunterEating(hunter));
        if (!played)
        {
            StartCoroutine(CompleteEatingAfterDelay(hunter, fallbackEatingSeconds));
            return;
        }

        if (eatingClip != null && eatingClip.loop)
        {
            StartCoroutine(CompleteEatingAfterDelay(hunter, fallbackEatingSeconds));
        }
    }

    private System.Collections.IEnumerator CompleteEatingAfterDelay(Hunter hunter, float delay)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, delay));
        CompleteHunterEating(hunter);
    }

    private void CompleteHunterEating(Hunter hunter)
    {
        if (hunter == null)
        {
            ClearFeedingHunter(null);
            return;
        }

        hunter.StopCustomAnimation();

        string hunterId = GetHunterId(hunter);
        if (!string.IsNullOrEmpty(hunterId))
        {
            fedHunterIds.Add(hunterId);
        }

        if (returnSeats.TryGetValue(hunter, out HunterSeat seat) && seat != null)
        {
            SpawnDirtyPlate(seat);
        }

        ClearFeedingHunter(hunter);
        SaveState();
        OnStateChanged?.Invoke();
    }

    private void ClearFeedingHunter(Hunter hunter)
    {
        if (hunter == null)
        {
            feedingHunters.RemoveWhere(h => h == null);
            return;
        }

        feedingHunters.Remove(hunter);
        returnSeats.Remove(hunter);
        RemoveHunterFromQueue(hunter);
        RemoveHunterFromServingPoint(hunter);
    }

    private void CleanupInvalidFlowHunters()
    {
        foreach (var slot in queueSlots)
        {
            if (slot == null || slot.hunter == null) continue;
            if (slot.hunter.GetState() != HunterState.Idle || slot.hunter.GetState() == HunterState.Dead)
            {
                ClearFeedingHunter(slot.hunter);
                ClearQueueSlot(slot);
            }
        }

        foreach (var point in servingPoints)
        {
            if (point == null || point.hunter == null) continue;
            if (point.hunter.GetState() != HunterState.Idle || point.hunter.GetState() == HunterState.Dead)
            {
                ClearFeedingHunter(point.hunter);
                point.hunter = null;
            }
        }
    }

    private void RebuildQueueSlots()
    {
        if (!Application.isPlaying) return;
        if (queuePoints == null)
        {
            queuePoints = new List<Transform>();
        }

        queuePoints.RemoveAll(point => point == null);
        if (queueSlots.Count == queuePoints.Count)
        {
            bool matches = true;
            for (int i = 0; i < queueSlots.Count; i++)
            {
                if (queueSlots[i].point != queuePoints[i])
                {
                    matches = false;
                    break;
                }
            }

            if (matches) return;
        }

        queueSlots.Clear();
        foreach (var point in queuePoints)
        {
            if (point == null) continue;
            queueSlots.Add(new QueueSlot { point = point });
        }
    }

    private void ClearQueueSlots()
    {
        foreach (var slot in queueSlots)
        {
            ClearQueueSlot(slot);
        }
    }

    private static void ClearQueueSlot(QueueSlot slot)
    {
        if (slot == null) return;
        slot.hunter = null;
        slot.arrived = false;
    }

    private void RemoveHunterFromQueue(Hunter hunter)
    {
        if (hunter == null) return;
        foreach (var slot in queueSlots)
        {
            if (slot != null && slot.hunter == hunter)
            {
                ClearQueueSlot(slot);
            }
        }
    }

    private void RemoveHunterFromServingPoint(Hunter hunter)
    {
        if (hunter == null || servingPoints == null) return;
        foreach (var point in servingPoints)
        {
            if (point != null && point.hunter == hunter)
            {
                point.hunter = null;
            }
        }
    }

    private void SpawnPotVisual()
    {
        DestroyPotVisual();
        if (potAnchor == null || currentRecipe == null) return;

        GameObject prefab = currentRecipe.potPrefab != null ? currentRecipe.potPrefab : defaultPotPrefab;
        if (prefab == null) return;

        potInstance = Instantiate(prefab, potAnchor);
        potInstance.transform.localPosition = Vector3.zero;
        potInstance.transform.localRotation = Quaternion.identity;
        potInstance.transform.localScale = Vector3.one;
    }

    private void DestroyPotVisual()
    {
        if (potInstance == null) return;
        Destroy(potInstance);
        potInstance = null;
    }

    private void SpawnDirtyPlate(HunterSeat seat)
    {
        if (seat == null || seat.HasDirtyPlate || dirtyPlatePrefab == null) return;

        Transform spawn = seat.PlateSpawnPoint;
        GameObject plateObj = Instantiate(dirtyPlatePrefab, spawn.position, spawn.rotation);
        KitchenDirtyPlate plate = plateObj.GetComponent<KitchenDirtyPlate>();
        if (plate == null)
        {
            plate = plateObj.AddComponent<KitchenDirtyPlate>();
        }

        plate.Initialize(this, seat);
        seat.SetDirtyPlate(plate);
        dirtySeatIds.Add(seat.SeatId);
    }

    private void RestoreDirtyPlates()
    {
        if (dirtySeatIds.Count == 0 || dirtyPlatePrefab == null) return;
        RefreshDiningSeats();

        foreach (var seat in diningSeats)
        {
            if (seat == null || !dirtySeatIds.Contains(seat.SeatId) || seat.HasDirtyPlate) continue;
            SpawnDirtyPlate(seat);
        }
    }

    private void RefreshDiningSeats()
    {
        if (diningSeats == null)
        {
            diningSeats = new List<HunterSeat>();
        }

        diningSeats.RemoveAll(seat => seat == null);
        if (diningSeats.Count > 0) return;

        var seats = FindObjectsByType<HunterSeat>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var seat in seats)
        {
            if (seat != null && seat.CanUseForGuildHall)
            {
                diningSeats.Add(seat);
            }
        }
    }

    private MonsterTrait RollCounterTrait(KitchenRecipe recipe)
    {
        if (recipe == null || !recipe.counterOneRandomMonsterTrait) return null;

        List<MonsterTrait> pool = BuildCounterTraitPool(recipe);
        if (pool.Count == 0) return null;

        return pool[UnityEngine.Random.Range(0, pool.Count)];
    }

    private List<MonsterTrait> BuildCounterTraitPool(KitchenRecipe recipe)
    {
        List<MonsterTrait> pool = new List<MonsterTrait>();
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddTrait(MonsterTrait trait)
        {
            if (trait == null) return;
            string key = !string.IsNullOrWhiteSpace(trait.traitId) ? trait.traitId : trait.name;
            if (!seen.Add(key)) return;
            pool.Add(trait);
        }

        if (recipe.counterTraitPool != null)
        {
            foreach (var trait in recipe.counterTraitPool)
            {
                AddTrait(trait);
            }
        }

        if (pool.Count > 0 || !useMonsterLibraryAsCounterFallback) return pool;

        GameConfig config = GameManager.Instance != null ? GameManager.Instance.GetGameConfig() : null;
        var monsters = config != null && config.monsterLibrary != null ? config.monsterLibrary.GetMonsters() : null;
        if (monsters == null) return pool;

        foreach (var monster in monsters)
        {
            if (monster == null || monster.possibleTraits == null) continue;
            foreach (var trait in monster.possibleTraits)
            {
                AddTrait(trait);
            }
        }

        return pool;
    }

    private MonsterTrait FindCounterTraitById(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        foreach (var recipe in recipes)
        {
            if (recipe == null) continue;
            foreach (var trait in BuildCounterTraitPool(recipe))
            {
                if (trait == null) continue;
                if (string.Equals(trait.traitId, id, StringComparison.OrdinalIgnoreCase)) return trait;
            }
        }
        return null;
    }

    private void HandleDayStarted(int _)
    {
        ClearDailyRecipeState(clearDirtyPlates: false);
    }

    private void HandleDayStateChanged(TimeManager.DayState state)
    {
        if (state == TimeManager.DayState.Evening)
        {
            ClearDailyRecipeState(clearDirtyPlates: false);
        }
    }

    private void ClearDailyRecipeState(bool clearDirtyPlates)
    {
        currentRecipe = null;
        rolledCounterTrait = null;
        fedHunterIds.Clear();
        feedingHunters.Clear();
        returnSeats.Clear();
        ClearQueueSlots();
        foreach (var point in servingPoints)
        {
            if (point != null) point.hunter = null;
        }
        DestroyPotVisual();

        if (clearDirtyPlates)
        {
            ClearAllDirtyPlates();
        }

        SaveState();
        OnStateChanged?.Invoke();
    }

    private void ClearAllDirtyPlates()
    {
        var plates = FindObjectsByType<KitchenDirtyPlate>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var plate in plates)
        {
            if (plate != null)
            {
                Destroy(plate.gameObject);
            }
        }
        dirtySeatIds.Clear();
    }

    private void ResolveReferences()
    {
        if (GameManager.Instance != null)
        {
            if (constructionManager == null) constructionManager = GameManager.Instance.GetConstructionManager();
            if (hunterManager == null) hunterManager = GameManager.Instance.GetHunterManager();
            if (timeManager == null) timeManager = GameManager.Instance.GetTimeManager();
        }

        SubscribeToTimeManager();
    }

    private void SubscribeToTimeManager()
    {
        if (!isActiveAndEnabled || timeManager == null || subscribedTimeManager == timeManager) return;

        if (subscribedTimeManager != null)
        {
            subscribedTimeManager.OnDayStarted -= HandleDayStarted;
            subscribedTimeManager.OnDayStateChanged -= HandleDayStateChanged;
        }

        subscribedTimeManager = timeManager;
        subscribedTimeManager.OnDayStarted += HandleDayStarted;
        subscribedTimeManager.OnDayStateChanged += HandleDayStateChanged;
    }

    private bool IsUnlocked()
    {
        if (kitchenConstruction == null) return true;
        ResolveReferences();
        return constructionManager != null && constructionManager.IsBuilt(kitchenConstruction);
    }

    private void OpenRouteDoors()
    {
        if (routeDoorsToOpen == null) return;
        foreach (var door in routeDoorsToOpen)
        {
            if (door == null) continue;
            door.OpenForRoute();
        }
    }

    private void BuildRecipeLookup()
    {
        recipeLookup.Clear();
        if (recipes == null) return;
        foreach (var recipe in recipes)
        {
            if (recipe == null) continue;
            string id = recipe.GetRecipeId();
            if (!recipeLookup.ContainsKey(id))
            {
                recipeLookup.Add(id, recipe);
            }
        }
    }

    private KitchenRecipe FindRecipeById(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        BuildRecipeLookup();
        return recipeLookup.TryGetValue(id, out KitchenRecipe recipe) ? recipe : null;
    }

    private void LoadState()
    {
        loadedState = true;
        if (!PlayerPrefs.HasKey(SaveKey)) return;

        try
        {
            KitchenSaveData data = JsonUtility.FromJson<KitchenSaveData>(PlayerPrefs.GetString(SaveKey));
            if (data == null) return;

            int currentDay = timeManager != null ? timeManager.GetCurrentDayIndex() : data.dayIndex;
            bool canRestoreActiveRecipe = data.dayIndex == currentDay
                && timeManager != null
                && timeManager.GetDayState() == TimeManager.DayState.Active;
            if (canRestoreActiveRecipe)
            {
                currentRecipe = FindRecipeById(data.recipeId);
                rolledCounterTrait = FindCounterTraitById(data.counterTraitId);
                fedHunterIds.Clear();
                if (data.fedHunterIds != null)
                {
                    foreach (var id in data.fedHunterIds)
                    {
                        if (!string.IsNullOrWhiteSpace(id)) fedHunterIds.Add(id);
                    }
                }
                if (currentRecipe != null)
                {
                    SpawnPotVisual();
                }
            }

            dirtySeatIds.Clear();
            if (data.dirtySeatIds != null)
            {
                foreach (var id in data.dirtySeatIds)
                {
                    if (!string.IsNullOrWhiteSpace(id)) dirtySeatIds.Add(id);
                }
            }
            RestoreDirtyPlates();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"KitchenManager: Failed to load saved kitchen state: {ex.Message}", this);
        }
    }

    private void SaveState()
    {
        int dayIndex = timeManager != null ? timeManager.GetCurrentDayIndex() : 0;
        KitchenSaveData data = new KitchenSaveData
        {
            dayIndex = dayIndex,
            recipeId = currentRecipe != null ? currentRecipe.GetRecipeId() : string.Empty,
            counterTraitId = rolledCounterTrait != null ? rolledCounterTrait.traitId : string.Empty,
            fedHunterIds = new List<string>(fedHunterIds),
            dirtySeatIds = new List<string>(dirtySeatIds)
        };

        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    private static string GetHunterId(Hunter hunter)
    {
        return hunter != null && hunter.Data != null ? hunter.Data.hunterId : null;
    }
}
