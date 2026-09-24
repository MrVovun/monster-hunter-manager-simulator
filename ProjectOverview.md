# Adventurers' Guild Manager — Project Overview

Reviewed 22 September 2026 against the working tree.

This is a source, configuration, content, and documentation review. Implementation below means supporting code/assets were found; it does not certify that every feature works end to end in a release build. No Unity build or gameplay performance benchmark was run for this review.

## Game identity and loop

A first-person fantasy guild management game built around investigating monster reports, choosing suitable hunters, and maintaining a functioning guild.

The player walks around the guild, talks to clients and hunters, consults a bestiary, assigns parties, and manages facilities. Missions resolve through a simulation of party power, traits, preparation, and risk. The inspected mission path resolves outcomes rather than loading a player-controlled combat encounter.

The central loop is:

1. Prepare the guild before opening: review the roster, use facilities, and manually upgrade reputation when eligible.
2. Ring the client bell to begin the active workday.
3. Question clients, gather evidence, identify the suspected monster, and reveal relevant traits.
4. Accept a contract, refer the case for a quality-dependent fee, or decline it.
5. Assign a party and dispatch it, balancing power, counters, preparation, and remaining time.
6. Resolve missions, collect rewards, handle injuries/deaths, and reinvest in hunters and construction.
7. End the day, rest, and meet the next day's upkeep obligations.

The live scene enables action-based time. The configured workday has 600 action-time seconds; this is a decision budget, not a fixed ten-minute real-time session. Mission timers advance with actions, and unfinished missions resolve when evening begins. Generated orders allow parties of one to three hunters.

Sources: [TimeManager](Assets/Scripts/Core/TimeManager.cs), [OrderGenerator](Assets/Scripts/Orders/OrderGenerator.cs), [OrderManager](Assets/Scripts/Orders/OrderManager.cs), [MissionResolver](Assets/Scripts/Missions/MissionResolver.cs).

## Implemented systems found

| Area | Existing implementation |
|---|---|
| First-person guild interaction | Movement, mouse look, interaction prompts, clients, hunter conversations, bells, beds, workbench, and facility interactions. |
| Investigation | Evidence categories, conditional questions, client response timing, hidden monster identity/traits, suspected-monster selection, and bestiary UI. |
| Contracts | Reputation-weighted generation, difficulty tables, flavor text, acceptance, referrals, party assignment, mission timing, and reports. |
| Mission outcomes | Shared outcome calculator for previews/resolution, trait counters and conditional bonuses, success thresholds, wounds, death, rewards, rescue/sacrifice effects, and bonus chests. |
| Hunters | Named data assets, rarity, recruitment campaigns, candidate review, hiring/firing, XP, purchased level upgrades, upkeep, dialogue, equipment appearance, and movement between guild activities. |
| Reputation and trust | Six configured ranks, manual pre-bell rank upgrades, clean/messy result distinctions, and a capped trust streak. |
| Economy and failure | Daily upkeep, income paying debt first, escalating penalties, hiring restrictions, forced dismissals, and game over after three consecutive unpaid upkeep days. |
| Construction | Costs, reputation/prerequisite gates, saved construction state, scene activation, and hunter-capacity increases. |
| Kitchen | Recipes, serving queues, hunters eating, daily mission buffs/counters, and dirty plate cleanup. |
| Dormitory | Bed assignment, sleeping/recovery, dirty/stale beds, and missed-sleep penalties. |
| Infirmary | Wounded hunters walking to treatment points and healing through action-time treatment. |
| Briefing room | Gathering hunters, chalkboard drawing, timed drawing reaction tiers, and daily success bonuses. Drawing duration determines the inspected reward logic. |
| Armory | Selecting/displaying hunters and changing saved weapon appearance. The inspected weapon-selection path changes visual presets; combat-stat equipment bonuses were not established. |
| Guild atmosphere | Floor dirt affecting rewards, trophy displays, monster kill tracking, and persistent grave records. |
| Card minigame | Draw/stand play against a hunter, side cards, rounds, and match resolution. Production stakes/rewards and action-time costs remain backlog items. |
| Front end and persistence | Main menu, new/continue flows, pause/settings, separate JSON save files, PlayerPrefs-backed systems, and game-over backup/restore utilities. |
| Tutorial | A 13-step first-session sequence with Tasha, voice-clip references, event completion, action gates, and a forced introductory order. |
| Developer tools | Local event/session telemetry, configurable balance simulation, CSV reports, developer UI, visual/material editors, and cinematic tools. |

Relevant folders: [gameplay scripts](Assets/Scripts), [content definitions](Assets/Resources), [telemetry documentation](Assets/Docs/Telemetry.md).

## Content inventory

Counts below are serialized asset definitions under `Assets/Resources`, classified by their script GUID. They are not a claim that every definition is reachable, fully balanced, or included in the intended demo progression.

| Definition type | Count |
|---|---:|
| Monsters | 43 |
| Hunters | 25 |
| Shared hunter stat blocks | 3 |
| Hunter traits | 55 |
| Monster traits | 6 |
| Guild constructions/upgrades | 15 |
| Kitchen recipes | 5 |
| Client profiles | 3 |
| Investigation questions | 7 |

There are 166 C# files under `Assets/Scripts`, including editor and developer tools. The 28 assets inside the Hunters folder comprise 25 hunter definitions and three stat blocks.

## Runtime structure and scenes

- Engine: Unity 6000.3.9f1; C# gameplay; Universal Render Pipeline 17.3.0.
- Supporting packages include Input System, AI Navigation, Cinemachine, Shader Graph, and TextMesh Pro/uGUI. The repository also contains third-party visual, animation, and cloth tools; package presence alone does not prove runtime usage.
- Current enabled build scenes: `MainMenu` and `TrailerScene`. `TestScene2` is listed but disabled.
- The serialized main-menu destination is `TrailerScene`, overriding the `TestScene2` default in `MainMenuUI.cs`.
- `TrailerScene` contains the game, hunter, recruitment, investigation, construction, kitchen, dormitory, infirmary, briefing, armory, graveyard, tutorial, and card-game objects.
- The scene's GameManager loads `Resources/GameConfig` when its serialized config reference is empty. Its scene starting values are 100 gold and a starting-reputation input of 1.
- Most systems use MonoBehaviour managers, events, serialized references, and ScriptableObject content. `GameManager` initializes central systems and supplies cross-system access.
- Persistence is spread across subsystem files and PlayerPrefs. That makes new-game, continue, day transitions, and game-over restoration useful integration-test targets.

Sources: [project version](ProjectSettings/ProjectVersion.txt), [packages](Packages/manifest.json), [build scenes](ProjectSettings/EditorBuildSettings.asset), [GameManager](Assets/Scripts/Core/GameManager.cs), [save utilities](Assets/Scripts/System/GameSaveUtility.cs).

## Documentation drift found

The existing [balance reference](BalanceReference.md) and [backlog](DocumentationBacklog.md) are valuable but contain statements that lag behind the current files.

| Documentation statement | Current source/configuration |
|---|---|
| Base injury/death chances are 50% / 25%. | `GameConfig.asset` has 25% / 5%. These are base inputs, not unconditional final casualty rates. |
| Unpaid upkeep removes one reputation rank. | GameManager applies percentage losses to current reputation points: 10% on the first unpaid day and 25% on the second. |
| Zero difficulty weight does not disable an entry because it is clamped to one. | The current difficulty selector gives nonpositive weights zero probability. The monster base-weight path still clamps to at least one. |
| A hunter must be wounded to die, without specifying when. | The normal resolver death gate checks whether the hunter was wounded before this mission; traits can permit death without prior injury. |
| Candidate arrivals take 10–20 action seconds. | `GlobalHunterConfig.asset` has 10–30. |
| Floor-wash action cost still needs serialization. | `washFloorSeconds: 5` is already serialized in `GameConfig.asset`. |
| Move OrderListItem out of OrdersTab. | `Assets/Scripts/UI/OrderListItem.cs` already exists as the class's separate source file. |
| Talent Scout, Earplugs, Last Stand, and Overprepared need explicit balance values. | All four now contain configured bonus effects. Balance tuning may still be outstanding. |

The reference also needs to account for the current trust cap of five, the 0.65 messy-success reputation multiplier, and manual pre-bell reputation advancement using the highest-earned points threshold.

Sources: [live game config](Assets/Resources/GameConfig.asset), [hunter config](Assets/Resources/GlobalHunterConfig.asset), [reputation](Assets/Scripts/Core/ReputationManager.cs), [mission resolver](Assets/Scripts/Missions/MissionResolver.cs), [order generation](Assets/Scripts/Orders/OrderGenerator.cs).

## Backlog interpretation

The explicit demo backlog is focused: repair spreadsheet import wiring and teach manual reputation upgrades in the tutorial. The broader backlog covers construction registration, trait tuning, configuration cleanup, UI restructuring/wiring, and richer facility interactions.

Construction registration remains relevant: `GuildConstructionManager` registers definitions from GameConfig and scene instances. Merely placing a construction asset in Resources does not register it. Fifteen construction definitions exist, while GameConfig directly references ten; scene-instance registration can supply others, so this difference alone does not prove five missing constructions.

The Dungeon Keys Forge has a construction definition with an empty description and zero gold cost. This review did not establish a corresponding dungeon gameplay system; treat that asset as insufficient evidence for a finished feature.

The full-game additions explicitly listed include a last-chance loan/debt option and deeper card-game rewards/progression. Extra briefing tiers, saved drawings, richer dormitory upgrades, more kitchen identity, and facility status UI are expansion/polish items rather than evidence that the base facilities are absent.

## Existing validation and release gaps

`BalanceReports` contains nine pairs of session/summary CSV files. The latest named summary, dated 30 August 2026, records ten simulated sessions for each of four behavior profiles. It reports economy, progression, mission outcomes, wounds, and deaths. These are balance simulations, not graphics or hardware benchmarks, and their historical results should not be assumed to describe today's configuration.

Local telemetry is enabled in the current GameConfig and records gameplay events and session summaries. The performance-test information asset has an empty results list and no populated hardware measurements. No project-owned automated test files were identified in the inspected script inventory.

This review did not establish Steam achievements/API integration, Steam Cloud configuration, controller coverage, or tested macOS/Linux support. Those need separate evidence before being promised on the store page. Steamworks web settings, including any Auto-Cloud setup, were not inspected.

## Implications for Steam system requirements

The earlier proposed CPU/GPU/RAM table remains an untested candidate specification. The broader project review does not turn those suggested models into measured requirements.

The repository establishes a Windows 64-bit target in its performance-test metadata and explicit DirectX 11/12 graphics configuration. The PC rendering assets include HDR, shadows, SSAO, decals, and VolFx. Their actual cost depends on the active scene, materials, cameras, resolution, and NPC load. Feature count and source asset size cannot determine minimum hardware or installed storage.

To finalize the Steam fields:

1. Produce the intended standalone Windows release build with development profiling options disabled.
2. Measure its complete installed folder, plus an allowance for saves and planned release content.
3. Measure frame time, RAM, and VRAM during busy guild scenes, facility queues, UI transitions, and later progression.
4. Test the candidate minimum/recommended machines at explicit resolutions and quality settings, including cold start and save/load.
5. Set the published hardware tiers from those results.

The evidence supports describing this as a substantially implemented management game with remaining demo integration, tuning, and polish work. Release readiness and minimum hardware remain unverified by this static review.
