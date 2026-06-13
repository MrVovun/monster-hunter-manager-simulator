using UnityEngine;

public class DormitoryBed : Interactable
{
    [Header("Bed Identity")]
    [SerializeField] private string bedId;

    [Header("Visual States")]
    [SerializeField] private GameObject cleanVisual;
    [SerializeField] private GameObject dirtyVisual;
    [SerializeField] private GameObject staleVisual;
    [SerializeField] private GameObject unusableVisual;

    private DormitoryManager dormitoryManager;
    private int dirtyDayCount;
    private int staleDirtyDayCount = 2;
    private int unusableDirtyDayCount = 3;

    public string BedId => string.IsNullOrWhiteSpace(bedId) ? gameObject.name : bedId;
    public int DirtyDayCount => dirtyDayCount;
    public bool IsDirty => dirtyDayCount > 0;

    private void Reset()
    {
        interactionPrompt = "[E] Change Sheets";
        interactionType = InteractionType.Trigger;
        locksPlayer = false;
    }

    private void Awake()
    {
        RefreshVisuals();
    }

    public void Initialize(DormitoryManager manager, int staleThreshold, int unusableThreshold)
    {
        dormitoryManager = manager;
        staleDirtyDayCount = Mathf.Max(1, staleThreshold);
        unusableDirtyDayCount = Mathf.Max(staleDirtyDayCount + 1, unusableThreshold);
        interactionPrompt = "[E] Change Sheets";
        interactionType = InteractionType.Trigger;
        locksPlayer = false;
        RefreshVisuals();
    }

    public void SetDirtyDayCount(int value)
    {
        dirtyDayCount = Mathf.Max(0, value);
        RefreshVisuals();
    }

    public void MarkSleptIn()
    {
        dirtyDayCount = Mathf.Max(1, dirtyDayCount + 1);
        RefreshVisuals();
    }

    public void Clean()
    {
        dirtyDayCount = 0;
        RefreshVisuals();
    }

    public bool IsUsable()
    {
        return dirtyDayCount < unusableDirtyDayCount;
    }

    public override bool IsInteractionAvailable()
    {
        var tm = GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null;
        return base.IsInteractionAvailable()
            && dirtyDayCount > 0
            && tm != null
            && tm.GetDayState() == TimeManager.DayState.Active;
    }

    public override void Interact(PlayerInteraction player)
    {
        if (dormitoryManager == null)
        {
            dormitoryManager = DormitoryManager.Instance;
        }

        dormitoryManager?.TryCleanBed(this);
    }

    private void RefreshVisuals()
    {
        GameObject target = cleanVisual;
        if (dirtyDayCount >= unusableDirtyDayCount)
        {
            target = unusableVisual != null ? unusableVisual : staleVisual != null ? staleVisual : dirtyVisual;
        }
        else if (dirtyDayCount >= staleDirtyDayCount)
        {
            target = staleVisual != null ? staleVisual : dirtyVisual;
        }
        else if (dirtyDayCount > 0)
        {
            target = dirtyVisual;
        }

        SetVisualActive(cleanVisual, cleanVisual != null && target == cleanVisual);
        SetVisualActive(dirtyVisual, dirtyVisual != null && target == dirtyVisual);
        SetVisualActive(staleVisual, staleVisual != null && target == staleVisual);
        SetVisualActive(unusableVisual, unusableVisual != null && target == unusableVisual);
    }

    private static void SetVisualActive(GameObject visual, bool active)
    {
        if (visual != null && visual.activeSelf != active)
        {
            visual.SetActive(active);
        }
    }
}
