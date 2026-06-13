using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class MissionReportPanel : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text rewardsGoldText;
    [SerializeField] private TMP_Text rewardsXPText;
    [SerializeField] private TMP_Text casualtiesText;
    [Header("Order Details")]
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text monsterPowerText;
    [SerializeField] private TMP_Text declaredMonsterText;
    [SerializeField] private Image declaredMonsterPortrait;
    [SerializeField] private TMP_Text trueMonsterText;
    [SerializeField] private Image trueMonsterPortrait;
    [SerializeField] private TMP_Text mismatchNoticeText;
    [Header("Traits")]
    [SerializeField] private Transform traitsParent;
    [SerializeField] private GameObject traitItemPrefab;
    [SerializeField] private TraitTooltipPanel traitTooltipPanel;
    [Header("Party")]
    [SerializeField] private Transform partyParent;
    [SerializeField] private GameObject partyItemPrefab;
    [Header("Controls")]
    [SerializeField] private Button closeButton;
    [Header("Visibility Blocking")]
    [Tooltip("If any of these roots are active, the report will wait until they close.")]
    [SerializeField] private List<GameObject> blockWhileActive = new List<GameObject>();

    private OrderManager trackedManager;
    private CursorLockMode previousLockState;
    private bool previousCursorVisible;
    private readonly List<GameObject> spawnedTraitItems = new List<GameObject>();
    private readonly List<GameObject> spawnedPartyItems = new List<GameObject>();
    private bool subscribed;
    private CanvasGroup canvasGroup;
    private bool cursorUnlockedForPanel;
    private bool movementLocked;
    private FirstPersonController cachedController;
    private PlayerInteraction cachedInteraction;
    private InvestigationManager cachedInvestigationManager;
    private GraveyardManager cachedGraveyardManager;
    private bool interactionDisabledByPanel;
    private readonly Queue<MissionReport> pendingReports = new Queue<MissionReport>();
    private MissionReport currentReport;
    private bool isVisible;

    private void OnEnable()
    {
        TrySubscribe();
        WireCloseButton();
        ShowVisuals(false); // keep hidden until a report arrives
    }

    private void OnDisable()
    {
        Unsubscribe();
        if (cursorUnlockedForPanel)
        {
            RestoreCursor();
            cursorUnlockedForPanel = false;
        }
        if (movementLocked || interactionDisabledByPanel)
        {
            UnlockPlayer();
        }
    }

    private void Update()
    {
        if (!subscribed)
        {
            TrySubscribe();
        }
    }

    private void TrySubscribe()
    {
        if (subscribed) return;
        trackedManager = GameManager.Instance != null ? GameManager.Instance.GetOrderManager() : null;
        if (trackedManager != null)
        {
            trackedManager.OnMissionResolved += HandleMissionResolved;
            subscribed = true;
        }
    }

    private void Unsubscribe()
    {
        if (trackedManager != null)
        {
            trackedManager.OnMissionResolved -= HandleMissionResolved;
            trackedManager = null;
        }
        subscribed = false;
    }

    private void HandleMissionResolved(MissionReport report)
    {
        if (report == null) return;

        pendingReports.Enqueue(report);
        // Mission resolution can happen inside another UI's button callback. Let that
        // UI finish closing before this report takes ownership of the player controls.
    }

    public void ShowReport(MissionReport report)
    {
        if (report == null) return;

        currentReport = report;
        WireCloseButton();
        ShowVisuals(true);
        RememberCursor();
        UnlockCursor();
        cursorUnlockedForPanel = true;
        LockPlayer();

        ClearTraitItems();
        ClearPartyItems();

        if (titleText != null)
        {
            titleText.text = report.order != null ? report.order.orderTitle : "Mission";
        }

        if (resultText != null)
        {
            resultText.text = report.success ? "Result: Success" : "Result: Failure";
            resultText.color = report.success ? Color.green : Color.red;
        }

        if (rewardsGoldText != null)
        {
            int totalGold = report.goldEarned;
            rewardsGoldText.text = $"Gold: {totalGold}";
        }

        if (rewardsXPText != null)
        {
            int totalXp = report.GetTotalXP();
            rewardsXPText.text = $"XP: {totalXp}";
        }

        if (mismatchNoticeText != null)
        {
            mismatchNoticeText.text = string.Empty;
            mismatchNoticeText.gameObject.SetActive(false);
        }

        UpdateOrderDetails(report.order);
        UpdateTraits(report.order);
        UpdateParty(report.hunterResults);
    }

    public void Close()
    {
        ResolveGraveyardManager()?.HandleMissionReportClosed(currentReport);
        ShowVisuals(false);
        if (cursorUnlockedForPanel)
        {
            RestoreCursor();
            cursorUnlockedForPanel = false;
        }
        UnlockPlayer();
        currentReport = null;
        TryShowNextPending();
    }

    private void RememberCursor()
    {
        previousLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestoreCursor()
    {
        Cursor.lockState = previousLockState;
        Cursor.visible = previousCursorVisible;
    }

    private void WireCloseButton()
    {
        if (closeButton == null) return;
        closeButton.onClick.RemoveListener(Close);
        closeButton.onClick.AddListener(Close);
        closeButton.interactable = true;
    }

    private void UpdateOrderDetails(Order order)
    {
        if (order == null) return;

        if (descriptionText != null)
        {
            descriptionText.text = order.GetDescriptionFor(Order.DescriptionAudience.DeclaredMonster);
        }

        if (monsterPowerText != null)
        {
            monsterPowerText.text = $"Power: {order.difficulty}";
        }

        if (declaredMonsterText != null)
        {
            declaredMonsterText.text = order.declaredMonster != null ? order.declaredMonster.displayName : "monster";
        }
        if (declaredMonsterPortrait != null)
        {
            declaredMonsterPortrait.sprite = order.declaredMonster != null ? order.declaredMonster.portrait : null;
            declaredMonsterPortrait.enabled = declaredMonsterPortrait.sprite != null;
        }

        if (trueMonsterText != null)
        {
            trueMonsterText.text = order.monsterData != null ? order.monsterData.displayName : "Unknown";
        }
        if (trueMonsterPortrait != null)
        {
            trueMonsterPortrait.sprite = order.monsterData != null ? order.monsterData.portrait : null;
            trueMonsterPortrait.enabled = trueMonsterPortrait.sprite != null;
        }

        UpdateMismatchNotice(order);
    }

    private void UpdateMismatchNotice(Order order)
    {
        if (mismatchNoticeText == null) return;

        bool hasDeclared = order != null && order.declaredMonster != null;
        bool hasTrue = order != null && order.monsterData != null;
        bool mismatch = hasDeclared && hasTrue && order.declaredMonster != order.monsterData;

        if (mismatch)
        {
            mismatchNoticeText.text = "Hunters report: wrong monster was identified for this contract.";
            mismatchNoticeText.gameObject.SetActive(true);
            return;
        }

        mismatchNoticeText.text = string.Empty;
        mismatchNoticeText.gameObject.SetActive(false);
    }

    private void UpdateTraits(Order order)
    {
        if (traitsParent == null || order == null) return;

        var caseData = order.investigationCase;
        var truthTraits = caseData?.truthTraits;

        if (truthTraits == null || truthTraits.Count == 0)
        {
            traitsParent.gameObject.SetActive(false);
            return;
        }

        traitsParent.gameObject.SetActive(true);
        var confirmed = new HashSet<string>(caseData?.confirmedTraitIds ?? new List<string>(), System.StringComparer.OrdinalIgnoreCase);

        foreach (var trait in truthTraits)
        {
            if (trait == null) continue;
            var item = CreateTraitItem(trait, confirmed.Contains(trait.traitId));
            if (item == null) continue;
            item.transform.SetParent(traitsParent, false);
            spawnedTraitItems.Add(item);
        }
    }

    private GameObject CreateTraitItem(MonsterTrait trait, bool revealed)
    {
        GameObject item = traitItemPrefab != null ? Instantiate(traitItemPrefab) : new GameObject("Trait");
        var rect = item.GetComponent<RectTransform>();
        if (rect == null) rect = item.AddComponent<RectTransform>();

        TMP_Text text = item.GetComponentInChildren<TMP_Text>();
        if (text != null)
        {
            text.text = $"{trait.displayName} ({(revealed ? "Revealed" : "Unrevealed")})";
        }

        Image icon = item.GetComponentInChildren<Image>();
        if (icon == null)
        {
            icon = item.GetComponent<Image>();
            if (icon == null)
            {
                icon = item.AddComponent<Image>();
            }
        }
        icon.sprite = trait.icon;
        icon.enabled = trait.icon != null;

        if (traitTooltipPanel != null)
        {
            var tooltip = item.GetComponent<TraitTooltipTrigger>();
            if (tooltip == null) tooltip = item.AddComponent<TraitTooltipTrigger>();
            tooltip.Initialize(traitTooltipPanel, rect, trait.displayName, trait.description);
        }

        return item;
    }

    private void UpdateParty(List<MissionReport.HunterResult> hunterResults)
    {
        if (partyParent == null || hunterResults == null) return;

        foreach (var result in hunterResults)
        {
            if (result == null || result.hunter == null) continue;
            var item = partyItemPrefab != null ? Instantiate(partyItemPrefab, partyParent) : new GameObject("PartyMember", typeof(RectTransform));
            spawnedPartyItems.Add(item);

            TMP_Text label = item.GetComponentInChildren<TMP_Text>();
            Image portrait = item.GetComponentsInChildren<Image>(true).FirstOrDefault();

            if (portrait != null)
            {
                var data = result.hunter.Data;
                portrait.sprite = data != null ? data.portrait : null;
                portrait.enabled = portrait.sprite != null;
            }

            if (label != null)
            {
                string status = result.died ? "Dead" : result.injured ? "Wounded" : "Healthy";
                label.text = BuildHunterResultText(result, status);
            }
        }
    }

    private string BuildHunterResultText(MissionReport.HunterResult result, string status)
    {
        if (result == null || result.hunter == null)
        {
            return status;
        }

        StringBuilder builder = new StringBuilder();
        builder.Append(result.hunter.name);
        builder.Append(" - ");
        builder.Append(status);

        if (result.xpGained > 0)
        {
            builder.Append(" - +");
            builder.Append(result.xpGained);
            builder.Append(" XP");
        }

        if (result.died)
        {
            return builder.ToString();
        }

        if (result.leveledUp)
        {
            builder.Append(" - Level Up!");
            return builder.ToString();
        }

        if (result.hunter.CanLevelUp())
        {
            builder.Append(" - Ready to level up");
            return builder.ToString();
        }

        int xpNeeded = result.hunter.GetXPToNextLevel();
        if (xpNeeded != int.MaxValue)
        {
            int remaining = Mathf.Max(0, xpNeeded - result.hunter.GetXP());
            builder.Append(" - ");
            builder.Append(remaining);
            builder.Append(" XP to level up");
        }

        return builder.ToString();
    }

    private void ClearTraitItems()
    {
        foreach (var item in spawnedTraitItems)
        {
            if (item != null) Destroy(item);
        }
        spawnedTraitItems.Clear();
        if (traitsParent != null)
        {
            foreach (Transform child in traitsParent)
            {
                Destroy(child.gameObject);
            }
        }
    }

    private void ClearPartyItems()
    {
        foreach (var item in spawnedPartyItems)
        {
            if (item != null) Destroy(item);
        }
        spawnedPartyItems.Clear();
        if (partyParent != null)
        {
            foreach (Transform child in partyParent)
            {
                Destroy(child.gameObject);
            }
        }
    }

    private void LateUpdate()
    {
        if (isVisible)
        {
            EnsureModalState();
            return;
        }

        // In case a blocking UI just closed, try to show pending reports.
        if (pendingReports.Count > 0)
        {
            TryShowNextPending();
        }
    }

    private void TryShowNextPending()
    {
        if (isVisible) return;
        if (IsBlockedByOtherUI()) return;
        if (pendingReports.Count == 0) return;

        var next = pendingReports.Dequeue();
        ShowReport(next);
    }

    private bool IsBlockedByOtherUI()
    {
        var investigationManager = ResolveInvestigationManager();
        if (investigationManager != null && investigationManager.IsHunterDialogueActive)
        {
            return true;
        }

        if (blockWhileActive != null)
        {
            foreach (var go in blockWhileActive)
            {
                if (go != null && go.activeInHierarchy)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void LockPlayer()
    {
        var controller = ResolvePlayerController();
        if (controller != null)
        {
            controller.LockMovement();
            movementLocked = true;
        }

        var interaction = ResolvePlayerInteraction();
        if (interaction != null && interaction.enabled)
        {
            interaction.enabled = false;
            interactionDisabledByPanel = true;
        }
    }

    private void UnlockPlayer()
    {
        if (movementLocked)
        {
            var controller = ResolvePlayerController();
            if (controller != null)
            {
                controller.UnlockMovement();
            }
            movementLocked = false;
        }

        var interaction = ResolvePlayerInteraction();
        if (interaction != null && interactionDisabledByPanel && !interaction.enabled)
        {
            interaction.enabled = true;
        }
        interactionDisabledByPanel = false;
    }

    private void EnsureModalState()
    {
        var controller = ResolvePlayerController();
        if (controller != null && !controller.IsMovementLocked())
        {
            controller.LockMovement();
            movementLocked = true;
        }

        var interaction = ResolvePlayerInteraction();
        if (interaction != null && interaction.enabled)
        {
            interaction.enabled = false;
            interactionDisabledByPanel = true;
        }

        if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
        {
            UnlockCursor();
            cursorUnlockedForPanel = true;
        }
    }

    private FirstPersonController ResolvePlayerController()
    {
        if (cachedController != null) return cachedController;
        cachedController = FindObjectOfType<FirstPersonController>();
        return cachedController;
    }

    private PlayerInteraction ResolvePlayerInteraction()
    {
        if (cachedInteraction != null) return cachedInteraction;
        cachedInteraction = FindObjectOfType<PlayerInteraction>();
        return cachedInteraction;
    }

    private InvestigationManager ResolveInvestigationManager()
    {
        if (cachedInvestigationManager != null) return cachedInvestigationManager;
        cachedInvestigationManager = GameManager.Instance != null
            ? GameManager.Instance.GetInvestigationManager()
            : FindObjectOfType<InvestigationManager>();
        return cachedInvestigationManager;
    }

    private GraveyardManager ResolveGraveyardManager()
    {
        if (cachedGraveyardManager != null) return cachedGraveyardManager;
        cachedGraveyardManager = GameManager.Instance != null
            ? GameManager.Instance.GetGraveyardManager()
            : FindObjectOfType<GraveyardManager>();
        return cachedGraveyardManager;
    }

    private void ShowVisuals(bool visible)
    {
        // If a dedicated panel root is provided and differs from this GameObject, toggle it normally.
        if (panelRoot != null && panelRoot != gameObject)
        {
            panelRoot.SetActive(visible);
            isVisible = visible;
            return;
        }

        // Fall back to a CanvasGroup so we can hide without disabling this component (keeps subscriptions alive).
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
        isVisible = visible;
    }
}
