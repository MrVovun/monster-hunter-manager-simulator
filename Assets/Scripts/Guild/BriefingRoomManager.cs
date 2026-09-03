using System.Collections.Generic;
using UnityEngine;

public class BriefingRoomManager : MonoBehaviour
{
    public static BriefingRoomManager Instance { get; private set; }

    [Header("Seating")]
    [SerializeField] private List<HunterSeat> briefingChairs = new List<HunterSeat>();
    [SerializeField] private List<GuildDoorController> routeDoorsToOpen = new List<GuildDoorController>();

    [Header("Drawing Thresholds")]
    [SerializeField] private float clapThresholdSeconds = 20f;
    [SerializeField] private float cheerThresholdSeconds = 40f;
    [SerializeField] private float clapSuccessBonusPercent = 5f;
    [SerializeField] private float cheerSuccessBonusPercent = 10f;

    [Header("Reaction Clips")]
    [SerializeField] private SharedCharacterAnimator.ClipEntry booClip;
    [SerializeField] private SharedCharacterAnimator.ClipEntry clapClip;
    [SerializeField] private SharedCharacterAnimator.ClipEntry cheerClip;

    [Header("Reaction Audio")]
    [SerializeField] private AudioSource reactionAudioSource;
    [SerializeField] private AudioClip booAudioClip;
    [SerializeField] private AudioClip clapAudioClip;
    [SerializeField] private AudioClip cheerAudioClip;

    [Header("Boards")]
    [SerializeField] private List<BriefingChalkboard> chalkboards = new List<BriefingChalkboard>();

    [Header("Flow")]
    [Tooltip("Delay after the player finishes drawing before hunters stand up, react, and reaction audio plays.")]
    [SerializeField] private float reactionDelaySeconds = 0.5f;
    [SerializeField] private float releaseAfterReactionSeconds = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    private readonly List<Hunter> assembledHunters = new List<Hunter>();
    private readonly Dictionary<Hunter, float> dailyBonuses = new Dictionary<Hunter, float>();
    private HunterManager hunterManager;
    private TimeManager timeManager;
    private TimeManager subscribedTimeManager;
    private Coroutine releaseRoutine;
    private Coroutine reactionRoutine;
    private bool briefingCalledToday;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple BriefingRoomManager instances found. The newest one will replace the static instance.", this);
        }

        Instance = this;
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToTimeManager();
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
            subscribedTimeManager = null;
        }

        if (releaseRoutine != null)
        {
            StopCoroutine(releaseRoutine);
            releaseRoutine = null;
        }

        if (reactionRoutine != null)
        {
            StopCoroutine(reactionRoutine);
            reactionRoutine = null;
        }
    }

    private void Start()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (subscribedTimeManager == null)
        {
            ResolveReferences();
        }
    }

    private void ResolveReferences()
    {
        if (GameManager.Instance != null)
        {
            if (hunterManager == null) hunterManager = GameManager.Instance.GetHunterManager();
            if (timeManager == null) timeManager = GameManager.Instance.GetTimeManager();
        }

        SubscribeToTimeManager();
    }

    private void SubscribeToTimeManager()
    {
        if (!isActiveAndEnabled || timeManager == null) return;
        if (subscribedTimeManager == timeManager) return;

        if (subscribedTimeManager != null)
        {
            subscribedTimeManager.OnDayStarted -= HandleDayStarted;
        }

        subscribedTimeManager = timeManager;
        subscribedTimeManager.OnDayStarted += HandleDayStarted;
    }

    private void HandleDayStarted(int _)
    {
        briefingCalledToday = false;
        dailyBonuses.Clear();
        ReleaseAssembledHunters();
        ClearBoards();
    }

    public bool CanCallHunters()
    {
        ResolveReferences();
        return !briefingCalledToday && timeManager != null && timeManager.GetDayState() == TimeManager.DayState.PreBell;
    }

    public void CallHuntersToBriefing()
    {
        ResolveReferences();
        if (!CanCallHunters())
        {
            if (debugLogs) Debug.Log("BriefingRoomManager: Hunters can only be called before the workday starts.", this);
            return;
        }

        if (hunterManager == null || briefingChairs == null || briefingChairs.Count == 0)
        {
            Debug.LogWarning("BriefingRoomManager: Missing HunterManager or briefing chairs.", this);
            return;
        }

        if (assembledHunters.Count > 0)
        {
            if (debugLogs) Debug.Log("BriefingRoomManager: Hunters are already assembled.", this);
            return;
        }

        briefingCalledToday = true;
        OpenRouteDoors();
        assembledHunters.Clear();

        foreach (var hunter in hunterManager.GetAllHunters())
        {
            if (hunter == null || !hunter.IsAvailableForOrders()) continue;
            if (assembledHunters.Contains(hunter)) continue;

            HunterSeat chair = FindFreeChair();
            if (chair == null) break;

            if (hunter.WalkToTemporarySeat(chair))
            {
                assembledHunters.Add(hunter);
            }
        }

        hunterManager.NotifyRosterChanged();
    }

    public void CompleteDrawing(float drawingSeconds)
    {
        ResolveReferences();
        if (reactionRoutine != null)
        {
            StopCoroutine(reactionRoutine);
            reactionRoutine = null;
        }

        List<Hunter> huntersToReact = new List<Hunter>(assembledHunters);
        huntersToReact.RemoveAll(hunter => hunter == null);
        assembledHunters.Clear();
        bool hasAudience = huntersToReact.Count > 0;

        bool canGrantBonus = timeManager != null && timeManager.GetDayState() == TimeManager.DayState.PreBell;
        SharedCharacterAnimator.ClipEntry reaction = booClip;
        AudioClip reactionAudio = booAudioClip;
        float bonus = 0f;

        if (drawingSeconds >= cheerThresholdSeconds)
        {
            reaction = cheerClip;
            reactionAudio = cheerAudioClip;
            bonus = cheerSuccessBonusPercent;
        }
        else if (drawingSeconds >= clapThresholdSeconds)
        {
            reaction = clapClip;
            reactionAudio = clapAudioClip;
            bonus = clapSuccessBonusPercent;
        }

        reactionRoutine = StartCoroutine(PlayReactionAfterDelay(
            huntersToReact,
            reaction,
            reactionAudio,
            canGrantBonus,
            bonus,
            hasAudience));

        if (debugLogs)
        {
            Debug.Log($"BriefingRoomManager: Drawing lasted {drawingSeconds:0.0}s. Bonus={(canGrantBonus ? bonus : 0f):0.#}%.", this);
        }
    }

    public void HandlePlayerLeftRoom()
    {
        ClearBoards();
        ReleaseAssembledHunters();
    }

    public void ReleaseAssembledHunters()
    {
        if (releaseRoutine != null)
        {
            StopCoroutine(releaseRoutine);
            releaseRoutine = null;
        }

        if (reactionRoutine != null)
        {
            StopCoroutine(reactionRoutine);
            reactionRoutine = null;
        }

        if (assembledHunters.Count == 0) return;

        foreach (var hunter in assembledHunters)
        {
            if (hunter == null) continue;
            if (hunter.GetState() == HunterState.Idle)
            {
                hunter.ReturnToGuildSeat();
            }
        }

        assembledHunters.Clear();
        hunterManager?.NotifyRosterChanged();
    }

    public void ClearBoards()
    {
        RefreshChalkboardList();
        if (chalkboards == null) return;
        foreach (var board in chalkboards)
        {
            if (board == null) continue;
            board.ClearBoard();
        }
    }

    public float GetDailyBonusForHunter(Hunter hunter)
    {
        if (hunter == null) return 0f;
        return dailyBonuses.TryGetValue(hunter, out float bonus) ? bonus : 0f;
    }

    public static float GetActiveDailyBonus(Hunter hunter)
    {
        return Instance != null ? Instance.GetDailyBonusForHunter(hunter) : 0f;
    }

    public bool ContainsBriefingChair(HunterSeat seat)
    {
        return seat != null && briefingChairs != null && briefingChairs.Contains(seat);
    }

    public IReadOnlyList<HunterSeat> GetBriefingChairs()
    {
        return briefingChairs;
    }

    public void RegisterChalkboard(BriefingChalkboard board)
    {
        if (board == null) return;
        if (chalkboards == null)
        {
            chalkboards = new List<BriefingChalkboard>();
        }
        if (!chalkboards.Contains(board))
        {
            chalkboards.Add(board);
        }
    }

    public void UnregisterChalkboard(BriefingChalkboard board)
    {
        if (chalkboards == null || board == null) return;
        chalkboards.Remove(board);
    }

    private System.Collections.IEnumerator ReleaseAfterReaction()
    {
        float delay = Mathf.Max(0f, releaseAfterReactionSeconds);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        releaseRoutine = null;
        ReleaseAssembledHunters();
    }

    private System.Collections.IEnumerator PlayReactionAfterDelay(
        List<Hunter> huntersToReact,
        SharedCharacterAnimator.ClipEntry reaction,
        AudioClip reactionAudio,
        bool canGrantBonus,
        float bonus,
        bool hasAudience)
    {
        float delay = Mathf.Max(0f, reactionDelaySeconds);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (hasAudience)
        {
            PlayReactionAudio(reactionAudio);
        }

        foreach (var hunter in huntersToReact)
        {
            if (hunter == null) continue;

            hunter.PlayBriefingReactionThenReturn(reaction, releaseAfterReactionSeconds);
            if (canGrantBonus && bonus > 0f)
            {
                dailyBonuses[hunter] = bonus;
            }
        }

        reactionRoutine = null;
        hunterManager?.NotifyRosterChanged();
    }

    private HunterSeat FindFreeChair()
    {
        foreach (var chair in briefingChairs)
        {
            if (chair != null && !chair.IsOccupied)
            {
                return chair;
            }
        }

        return null;
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

    private void RefreshChalkboardList()
    {
        if (chalkboards == null)
        {
            chalkboards = new List<BriefingChalkboard>();
        }

        chalkboards.RemoveAll(board => board == null);
        var boardsInScene = SceneLookup.FindAll<BriefingChalkboard>();
        foreach (var board in boardsInScene)
        {
            if (board != null && !chalkboards.Contains(board))
            {
                chalkboards.Add(board);
            }
        }
    }

    private Vector3 GetRoomCenter()
    {
        Vector3 total = Vector3.zero;
        int count = 0;

        if (briefingChairs != null)
        {
            foreach (var chair in briefingChairs)
            {
                if (chair == null) continue;
                total += chair.transform.position;
                count++;
            }
        }

        if (chalkboards != null)
        {
            foreach (var board in chalkboards)
            {
                if (board == null) continue;
                total += board.transform.position;
                count++;
            }
        }

        return count > 0 ? total / count : transform.position;
    }

    private void PlayReactionAudio(AudioClip clip)
    {
        if (clip == null) return;

        if (reactionAudioSource != null)
        {
            reactionAudioSource.PlayOneShot(clip);
            return;
        }

        AudioSource.PlayClipAtPoint(clip, GetRoomCenter());
    }
}
