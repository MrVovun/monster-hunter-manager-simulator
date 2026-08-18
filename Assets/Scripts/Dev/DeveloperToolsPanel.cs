#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class DeveloperToolsPanel : MonoBehaviour
{
    [Header("Visibility")]
    [SerializeField] private InputActionReference toggleAction;
    [SerializeField] private bool startVisible = false;

    [Header("Client/Order Presets")]
    [SerializeField] private List<ClientProfile> clientPresets = new List<ClientProfile>();
    [SerializeField] private List<MonsterData> monsterPresets = new List<MonsterData>();

    private HunterManager hunterManager;
    private OrderManager orderManager;
    private HunterRecruitmentManager recruitmentManager;
    private GuildConstructionManager constructionManager;
    private GoldManager goldManager;
    private ReputationManager reputationManager;
    private InvestigationManager investigationManager;
    private NotificationManager notificationManager;
    private MonsterSlainTracker slainTracker;
    private MonsterLibrary monsterLibrary;
    private GraveyardManager graveyardManager;
    private MissionBonusChestManager bonusChestManager;
    private CinematicCameraRig cinematicRig;

    private Rect windowRect = new Rect(30f, 30f, 420f, 640f);
    private bool visible;
    private Vector2 scrollPosition;

    private int selectedHunterIndex;
    private Hunter cachedHunter;
    private int hunterPowerInput;
    private int hunterLevelInput;
    private int hunterXPInput;
    private int hunterUpkeepInput;

    private string recruitmentHunterId = string.Empty;
    private int selectedRecruitDataIndex;

    private int selectedClientIndex;
    private int selectedMonsterIndex;
    private int debugOrderDifficulty = 25;
    private int debugOrderGold = 250;
    private int debugOrderXp = 100;
    private float debugOrderDuration = 120f;
    private float customResponseDelay = -1f;
    private string tutorialStepInput = "1";

    private int selectedTrophyMonsterIndex;
    private int addKillAmount = 1;

    private GUIStyle headerStyle;
    private bool cursorModified;
    private CursorLockMode cachedLockMode;
    private bool cachedCursorVisible;

    private void Awake()
    {
        CacheManagers();
        visible = startVisible;
        ApplyCursorState();
    }

    private void OnEnable()
    {
        if (toggleAction != null && toggleAction.action != null)
        {
            toggleAction.action.performed += HandleTogglePerformed;
            toggleAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (toggleAction != null && toggleAction.action != null)
        {
            toggleAction.action.performed -= HandleTogglePerformed;
            toggleAction.action.Disable();
        }
        RestoreCursorState();
    }

    private void CacheManagers()
    {
        var gm = GameManager.Instance;
        if (gm != null)
        {
            hunterManager = gm.GetHunterManager();
            orderManager = gm.GetOrderManager();
            goldManager = gm.GetGoldManager();
            reputationManager = gm.GetReputationManager();
            investigationManager = gm.GetInvestigationManager();
            constructionManager = gm.GetConstructionManager();
            notificationManager = gm.GetNotificationManager();
            graveyardManager = gm.GetGraveyardManager();
            monsterLibrary = gm.GetGameConfig() != null ? gm.GetGameConfig().monsterLibrary : null;
        }

        if (recruitmentManager == null)
        {
            recruitmentManager = SceneLookup.Find<HunterRecruitmentManager>();
        }

        if (slainTracker == null)
        {
            slainTracker = SceneLookup.Find<MonsterSlainTracker>();
        }

        if (bonusChestManager == null)
        {
            bonusChestManager = SceneLookup.Find<MissionBonusChestManager>();
        }

        if (cinematicRig == null)
        {
            cinematicRig = SceneLookup.Find<CinematicCameraRig>(true);
        }
    }

    private void HandleTogglePerformed(InputAction.CallbackContext ctx)
    {
        visible = !visible;
        ApplyCursorState();
    }

    private void OnGUI()
    {
        if (!visible) return;

        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold
            };
        }

        windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindowContents, "Developer Tools");
    }

    private void DrawWindowContents(int windowId)
    {
        if (GUILayout.Button("Close"))
        {
            visible = false;
        }

        scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, true);
        DrawGlobalSection();
        GUILayout.Space(8f);
        DrawCinematicSection();
        GUILayout.Space(8f);
        DrawHunterSection();
        GUILayout.Space(8f);
        DrawRecruitmentSection();
        GUILayout.Space(8f);
        DrawClientSection();
        GUILayout.Space(8f);
        DrawTrophyWallSection();
        GUILayout.EndScrollView();

        GUI.DragWindow(new Rect(0, 0, windowRect.width, 20f));
    }

    private void ApplyCursorState()
    {
        if (visible)
        {
            if (!cursorModified)
            {
                cachedLockMode = Cursor.lockState;
                cachedCursorVisible = Cursor.visible;
                cursorModified = true;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            RestoreCursorState();
        }
    }

    private void RestoreCursorState()
    {
        if (!cursorModified) return;
        Cursor.lockState = cachedLockMode;
        Cursor.visible = cachedCursorVisible;
        cursorModified = false;
    }

    private void DrawGlobalSection()
    {
        GUILayout.Label("Global State", headerStyle);
        if (goldManager == null || reputationManager == null)
        {
            CacheManagers();
        }

        if (goldManager != null)
        {
            GUILayout.Label($"Gold: {goldManager.GetGold()}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+100")) goldManager.AddGold(100);
            if (GUILayout.Button("+1000")) goldManager.AddGold(1000);
            if (GUILayout.Button("-100")) goldManager.SpendGold(100);
            if (GUILayout.Button("-1000")) goldManager.SpendGold(1000);
            GUILayout.EndHorizontal();
        }
        else
        {
            GUILayout.Label("GoldManager not found.");
        }

        if (reputationManager != null)
        {
            GUILayout.Label($"Reputation: {reputationManager.GetReputation():0.##} (Points: {reputationManager.GetReputationPointsPrecise():0.##})");
            GUILayout.Label(reputationManager.GetProgressText());
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+5 pts")) reputationManager.AddReputationPoints(5f);
            if (GUILayout.Button("+25 pts")) reputationManager.AddReputationPoints(25f);
            if (GUILayout.Button("-5 pts")) reputationManager.AddReputationPoints(-5f);
            if (GUILayout.Button("-25 pts")) reputationManager.AddReputationPoints(-25f);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Reset Reputation To Default"))
            {
                reputationManager.ResetToDefault();
            }
        }
        else
        {
            GUILayout.Label("ReputationManager not found.");
        }

        GUILayout.Space(4f);
        GUILayout.Label("Tutorial", headerStyle);
        TutorialManager tutorialManager = TutorialManager.Instance;
        if (tutorialManager != null)
        {
            GUILayout.Label(tutorialManager.IsTutorialDisabled() ? "Tutorial disabled" : "Tutorial enabled");
            GUILayout.Label($"Current Step: {(tutorialManager.CurrentStepIndex >= 0 ? tutorialManager.CurrentStepIndex + 1 : 0)} / {tutorialManager.StepCount}");
            if (tutorialManager.CurrentStepIndex >= 0)
            {
                GUILayout.Label(tutorialManager.GetStepLabel(tutorialManager.CurrentStepIndex));
            }

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset Tutorial"))
                {
                    tutorialManager.ResetTutorialProgress();
                }
                if (GUILayout.Button("Disable Tutorial"))
                {
                    tutorialManager.SetTutorialDisabled(true);
                }
                if (GUILayout.Button("Enable Tutorial"))
                {
                    tutorialManager.SetTutorialDisabled(false);
                }
            }

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Go To Step", GUILayout.Width(78f));
                tutorialStepInput = GUILayout.TextField(tutorialStepInput, GUILayout.Width(48f));
                if (GUILayout.Button("Activate"))
                {
                    if (int.TryParse(tutorialStepInput, out int stepNumber))
                    {
                        tutorialManager.DebugJumpToStep(stepNumber - 1);
                    }
                }
            }

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Previous Step"))
                {
                    int target = Mathf.Max(0, tutorialManager.CurrentStepIndex - 1);
                    tutorialStepInput = (target + 1).ToString();
                    tutorialManager.DebugJumpToStep(target);
                }
                if (GUILayout.Button("Next Step"))
                {
                    int target = Mathf.Min(Mathf.Max(0, tutorialManager.StepCount - 1), tutorialManager.CurrentStepIndex + 1);
                    tutorialStepInput = (target + 1).ToString();
                    tutorialManager.DebugJumpToStep(target);
                }
            }
        }
        else
        {
            GUILayout.Label("TutorialManager not found.");
        }

        if (constructionManager == null)
        {
            CacheManagers();
        }

        using (new GUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Revert All Constructions"))
            {
                if (constructionManager != null)
                {
                    constructionManager.ResetAllConstructions();
                }
                else
                {
                    Debug.LogWarning("DeveloperTools: ConstructionManager not found.");
                }
            }
        }

        using (new GUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Clear Notification History"))
            {
                if (notificationManager == null)
                {
                    notificationManager = GameManager.Instance != null
                        ? GameManager.Instance.GetNotificationManager()
                        : SceneLookup.Find<NotificationManager>();
                }

                if (notificationManager != null)
                {
                    notificationManager.ClearHistory();
                }
                else
                {
                    Debug.LogWarning("DeveloperTools: NotificationManager not found.");
                }
            }
        }

        using (new GUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Clear Graveyard"))
            {
                if (graveyardManager == null)
                {
                    graveyardManager = GameManager.Instance != null
                        ? GameManager.Instance.GetGraveyardManager()
                        : SceneLookup.Find<GraveyardManager>();
                }

                if (graveyardManager != null)
                {
                    graveyardManager.ClearGraveyard();
                }
                else
                {
                    Debug.LogWarning("DeveloperTools: GraveyardManager not found.");
                }
            }
        }

        GUILayout.Space(4f);
        GUILayout.Label("Bonus Chests", headerStyle);
        using (new GUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Spawn Chest"))
            {
                if (bonusChestManager == null) CacheManagers();
                if (bonusChestManager != null)
                {
                    bonusChestManager.DebugSpawnChest();
                }
                else
                {
                    Debug.LogWarning("DeveloperTools: MissionBonusChestManager not found.");
                }
            }

            if (GUILayout.Button("Spawn Mimic"))
            {
                if (bonusChestManager == null) CacheManagers();
                if (bonusChestManager != null)
                {
                    bonusChestManager.DebugSpawnMimic();
                }
                else
                {
                    Debug.LogWarning("DeveloperTools: MissionBonusChestManager not found.");
                }
            }
        }
    }

    private void DrawCinematicSection()
    {
        GUILayout.Label("Cinematics", headerStyle);
        if (cinematicRig == null)
        {
            CacheManagers();
        }

        if (cinematicRig == null)
        {
            GUILayout.Label("CinematicCameraRig not found.");
            return;
        }

        GUILayout.Label($"Shots: {cinematicRig.ShotCount} | Active: {(cinematicRig.ActiveShotIndex >= 0 ? cinematicRig.ActiveShotIndex + 1 : 0)}");
        using (new GUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Prev Shot"))
            {
                cinematicRig.PlayPreviousShot();
            }
            if (GUILayout.Button("Next Shot"))
            {
                cinematicRig.PlayNextShot();
            }
            if (GUILayout.Button("Exit Shot"))
            {
                cinematicRig.ExitCinematic();
            }
        }

        using (new GUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Toggle HUD"))
            {
                cinematicRig.ToggleHud();
            }
            if (GUILayout.Button("Toggle Notifications"))
            {
                cinematicRig.ToggleNotifications();
            }
            if (GUILayout.Button("Screenshot"))
            {
                cinematicRig.CaptureScreenshot();
            }
        }

        using (new GUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Pause"))
            {
                cinematicRig.TogglePause();
            }
            if (GUILayout.Button("Slow Motion"))
            {
                cinematicRig.ToggleSlowMotion();
            }
        }
    }

    private void DrawHunterSection()
    {
        GUILayout.Label("Hunters", headerStyle);
        if (hunterManager == null)
        {
            CacheManagers();
        }

        if (hunterManager == null)
        {
            GUILayout.Label("HunterManager not found.");
            return;
        }

        if (GUILayout.Button("Reset Hunters To Default"))
        {
            hunterManager.DebugResetRoster();
        }

        var hunters = hunterManager.GetAllHunters();
        if (hunters == null || hunters.Count == 0)
        {
            GUILayout.Label("No hunters available.");
            return;
        }

        selectedHunterIndex = Mathf.Clamp(selectedHunterIndex, 0, hunters.Count - 1);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<", GUILayout.Width(30f)))
        {
            selectedHunterIndex = (selectedHunterIndex - 1 + hunters.Count) % hunters.Count;
            cachedHunter = null;
        }

        var selected = hunters[selectedHunterIndex];
        GUILayout.Label(selected != null ? selected.name : "Unknown Hunter", GUILayout.ExpandWidth(true));

        if (GUILayout.Button(">", GUILayout.Width(30f)))
        {
            selectedHunterIndex = (selectedHunterIndex + 1) % hunters.Count;
            cachedHunter = null;
        }
        GUILayout.EndHorizontal();

        if (selected == null) return;
        if (selected != cachedHunter)
        {
            LoadHunterInputs(selected);
            cachedHunter = selected;
        }

        GUILayout.Label($"Current Level {selected.GetLevel()} | XP {selected.GetXP()} | Power {selected.GetStats()?.GetTotalPower() ?? 0}");
        hunterLevelInput = IntField("Set Level", hunterLevelInput);
        hunterXPInput = IntField("Set XP", hunterXPInput);
        if (GUILayout.Button("Apply Level & XP"))
        {
            selected.DebugSetLevelAndXP(hunterLevelInput, hunterXPInput);
        }

        hunterPowerInput = IntField("Power Override", hunterPowerInput);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply Power Override"))
        {
            selected.GetStats()?.SetDebugPowerOverride(hunterPowerInput);
        }
        if (GUILayout.Button("Clear Power Override", GUILayout.Width(150f)))
        {
            selected.GetStats()?.ClearDebugPowerOverride();
        }
        GUILayout.EndHorizontal();

        hunterUpkeepInput = IntField("Upkeep Override", hunterUpkeepInput);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply Upkeep Override"))
        {
            selected.SetDebugUpkeep(hunterUpkeepInput);
        }
        if (selected.HasDebugUpkeepOverride() && GUILayout.Button("Clear Upkeep Override", GUILayout.Width(150f)))
        {
            selected.ClearDebugUpkeep();
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Set Alive"))
        {
            var state = GetOrAddInteractionState(selected);
            if (state != null)
            {
                state.SetWounded(false);
            }
            selected.SetState(HunterState.Idle);
        }
        if (GUILayout.Button("Set Wounded"))
        {
            var state = GetOrAddInteractionState(selected);
            if (state != null)
            {
                state.SetWounded(true);
            }
            selected.SetState(HunterState.Idle);
        }
        if (GUILayout.Button("Set Dead"))
        {
            selected.SetState(HunterState.Dead);
        }
        GUILayout.EndHorizontal();
    }

    private void DrawRecruitmentSection()
    {
        GUILayout.Label("Recruitment", headerStyle);
        if (recruitmentManager == null)
        {
            recruitmentManager = SceneLookup.Find<HunterRecruitmentManager>();
        }

        if (recruitmentManager == null)
        {
            GUILayout.Label("RecruitmentManager not found.");
            return;
        }

        recruitmentHunterId = LabeledTextField("Hunter ID", recruitmentHunterId);
        if (GUILayout.Button("Spawn Candidate By ID"))
        {
            if (!recruitmentManager.DebugForceCandidate(recruitmentHunterId))
            {
                Debug.LogWarning($"DeveloperTools: Unable to spawn candidate for ID '{recruitmentHunterId}'.");
            }
        }

        var allData = hunterManager != null ? hunterManager.GetAllHunterData() : null;
        if (allData != null && allData.Count > 0)
        {
            selectedRecruitDataIndex = Mathf.Clamp(selectedRecruitDataIndex, 0, allData.Count - 1);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(30f)))
            {
                selectedRecruitDataIndex = (selectedRecruitDataIndex - 1 + allData.Count) % allData.Count;
            }
            GUILayout.Label(allData[selectedRecruitDataIndex]?.hunterName ?? "Unknown", GUILayout.ExpandWidth(true));
            if (GUILayout.Button(">", GUILayout.Width(30f)))
            {
                selectedRecruitDataIndex = (selectedRecruitDataIndex + 1) % allData.Count;
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Spawn Selected Candidate"))
            {
                if (!recruitmentManager.DebugForceCandidate(allData[selectedRecruitDataIndex]))
                {
                    Debug.LogWarning("DeveloperTools: Failed to spawn candidate from selection.");
                }
            }
        }
    }

    private void DrawClientSection()
    {
        GUILayout.Label("Clients & Orders", headerStyle);
        if (investigationManager == null)
        {
            CacheManagers();
        }

        if (investigationManager == null)
        {
            GUILayout.Label("InvestigationManager not found.");
            return;
        }

        if (monsterPresets == null || monsterPresets.Count == 0)
        {
            GUILayout.Label("Assign Monster Presets to spawn orders.");
            return;
        }

        selectedMonsterIndex = Mathf.Clamp(selectedMonsterIndex, 0, monsterPresets.Count - 1);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<", GUILayout.Width(30f)))
        {
            selectedMonsterIndex = (selectedMonsterIndex - 1 + monsterPresets.Count) % monsterPresets.Count;
        }
        GUILayout.Label(monsterPresets[selectedMonsterIndex] != null
            ? monsterPresets[selectedMonsterIndex].displayName
            : "Unknown Monster", GUILayout.ExpandWidth(true));
        if (GUILayout.Button(">", GUILayout.Width(30f)))
        {
            selectedMonsterIndex = (selectedMonsterIndex + 1) % monsterPresets.Count;
        }
        GUILayout.EndHorizontal();

        if (clientPresets != null && clientPresets.Count > 0)
        {
            selectedClientIndex = Mathf.Clamp(selectedClientIndex, 0, clientPresets.Count - 1);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(30f)))
            {
                selectedClientIndex = (selectedClientIndex - 1 + clientPresets.Count) % clientPresets.Count;
            }
            GUILayout.Label(clientPresets[selectedClientIndex] != null
                ? clientPresets[selectedClientIndex].name
                : "Client (null)", GUILayout.ExpandWidth(true));
            if (GUILayout.Button(">", GUILayout.Width(30f)))
            {
                selectedClientIndex = (selectedClientIndex + 1) % clientPresets.Count;
            }
            GUILayout.EndHorizontal();
        }
        else
        {
            GUILayout.Label("Assign Client Presets to control appearance.");
        }

        debugOrderDifficulty = IntField("Difficulty", debugOrderDifficulty);
        debugOrderGold = IntField("Gold Reward", debugOrderGold);
        debugOrderXp = IntField("XP Reward", debugOrderXp);
        debugOrderDuration = FloatField("Mission Duration (s)", debugOrderDuration);
        customResponseDelay = FloatField("Client Response Delay (-1 = default)", customResponseDelay);

        if (GUILayout.Button("Spawn Client & Order"))
        {
            var monster = monsterPresets[selectedMonsterIndex];
            var order = BuildDebugOrder(monster);
            ClientProfile profile = null;
            if (clientPresets != null && clientPresets.Count > 0)
            {
                profile = clientPresets[selectedClientIndex];
            }

            var runtimeProfile = BuildRuntimeProfile(profile, customResponseDelay);
            investigationManager.DebugStartInvestigation(order, runtimeProfile);
        }
    }

    private void DrawTrophyWallSection()
    {
        GUILayout.Label("Trophy Wall", headerStyle);
        if (slainTracker == null)
        {
            GUILayout.Label("MonsterSlainTracker not found.");
            return;
        }

        if (GUILayout.Button("Reset All Kills"))
        {
            slainTracker.ResetAll();
        }

        var monsters = monsterLibrary != null ? monsterLibrary.GetMonsters() : null;
        if (monsters == null || monsters.Count == 0)
        {
            GUILayout.Label("No monsters available.");
            return;
        }

        selectedTrophyMonsterIndex = Mathf.Clamp(selectedTrophyMonsterIndex, 0, monsters.Count - 1);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<", GUILayout.Width(30f)))
        {
            selectedTrophyMonsterIndex = (selectedTrophyMonsterIndex - 1 + monsters.Count) % monsters.Count;
        }
        GUILayout.Label(monsters[selectedTrophyMonsterIndex] != null ? monsters[selectedTrophyMonsterIndex].displayName : "<null>", GUILayout.ExpandWidth(true));
        if (GUILayout.Button(">", GUILayout.Width(30f)))
        {
            selectedTrophyMonsterIndex = (selectedTrophyMonsterIndex + 1) % monsters.Count;
        }
        GUILayout.EndHorizontal();

        addKillAmount = IntField("Add kills (can be negative)", addKillAmount);
        var selected = monsters[selectedTrophyMonsterIndex];
        int current = slainTracker.GetKillCount(selected);
        GUILayout.Label($"Current kills: {current}");

        if (GUILayout.Button("Apply"))
        {
            slainTracker.AddKills(selected, addKillAmount);
        }
    }

    private void LoadHunterInputs(Hunter hunter)
    {
        hunterPowerInput = hunter.GetStats()?.GetTotalPower() ?? 0;
        hunterLevelInput = hunter.GetLevel();
        hunterXPInput = hunter.GetXP();
        hunterUpkeepInput = hunter.GetUpkeepCost();
    }

    private HunterInteractionState GetOrAddInteractionState(Hunter hunter)
    {
        if (hunter == null) return null;
        var state = hunter.GetComponent<HunterInteractionState>();
        if (state == null)
        {
            state = hunter.gameObject.AddComponent<HunterInteractionState>();
        }
        return state;
    }

    private int IntField(string label, int value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(160f));
        string text = GUILayout.TextField(value.ToString(), GUILayout.ExpandWidth(true));
        GUILayout.EndHorizontal();
        if (int.TryParse(text, out int parsed))
        {
            return parsed;
        }
        return value;
    }

    private float FloatField(string label, float value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(160f));
        string text = GUILayout.TextField(value.ToString("0.##"), GUILayout.ExpandWidth(true));
        GUILayout.EndHorizontal();
        if (float.TryParse(text, out float parsed))
        {
            return parsed;
        }
        return value;
    }

    private string LabeledTextField(string label, string value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(160f));
        value = GUILayout.TextField(value ?? string.Empty, GUILayout.ExpandWidth(true));
        GUILayout.EndHorizontal();
        return value;
    }

    private Order BuildDebugOrder(MonsterData monster)
    {
        Order order = new Order
        {
            orderTitle = monster != null ? $"{monster.displayName} Debug Order" : "Debug Order",
            description = "Generated from Developer Tools.",
            monsterData = monster,
            difficulty = Mathf.Max(1, debugOrderDifficulty),
            goldReward = Mathf.Max(0, debugOrderGold),
            xpReward = Mathf.Max(0, debugOrderXp),
            reputationPointsReward = Mathf.Max(0f, debugOrderDifficulty / 5f),
            missionDuration = Mathf.Max(30f, debugOrderDuration),
            maxPartySize = 3,
            minPartySize = 1,
            state = OrderState.Offered
        };
        return order;
    }

    private ClientProfile BuildRuntimeProfile(ClientProfile source, float delayOverride)
    {
        if (source == null) return null;
        if (delayOverride < 0f)
        {
            return source;
        }

        ClientProfile instance = ScriptableObject.CreateInstance<ClientProfile>();
        instance.name = source.name + "_Runtime";
        instance.profileId = source.profileId;
        instance.categoryName = source.categoryName;
        instance.responseDelaySeconds = delayOverride;
        instance.visualPrefabs = new List<GameObject>(source.visualPrefabs);
        return instance;
    }
}
#else
public class DeveloperToolsPanel : MonoBehaviour
{
    private void Awake()
    {
        Destroy(gameObject);
    }
}
#endif
