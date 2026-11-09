# Demo Release Plan

Audited 4 November 2026 against the first-party documentation, enabled build scenes, gameplay scripts, release-scene wiring, and content definitions. Vendor documentation and third-party sample content were excluded unless they affect the release.

## Recommended demo scope

The demo should prove one complete, polished loop: investigate a client request, build the case, accept or refer it, prepare and dispatch hunters, watch mission progress, resolve the result, collect a reward, improve the guild, and begin another day.

Lock these content targets before production continues:

- 30 demo monsters selected from the 43 existing definitions.
- 37 hunters total. There are 25 definitions now, so 12 more are required.
- One equipment catalog sized for the demo progression, with at least one useful choice for every supported slot type and reputation tier.
- A short story arc with a defined beginning, escalation, and demo ending.
- A defined demo duration or end condition, followed by an end-of-demo screen.

Do not expand the card game, add the loan system, or build the future facility upgrade ideas until the complete demo loop passes its release gate.

## Current content snapshot

### Monsters

There are 43 `MonsterData` definitions in `Assets/Resources/Monsters`, and all 43 currently have:

- a portrait and description;
- at least one possible monster trait;
- all six evidence categories (`family`, `size`, `sound`, `tail`, `winged`, and `movement`);
- six investigation responses;
- a unique evidence-tag combination.

This means the 30-monster target is primarily a curation, balance, and visual-completion task. Choose the 30 that create the clearest difficulty and reputation progression, then remove the other 13 from the demo `MonsterLibrary` rotation without deleting their assets.

Only 15 monsters currently have trophy-head prefabs. Twenty-eight lack them. Even if all 15 complete monsters are selected, a 30-monster demo needs at least 15 more trophy visuals if the trophy wall must represent every demo monster.

### Hunters

There are 25 `HunterData` definitions. All 25 have portraits, visual-prefab references, bios, and at least one trait. Content gaps remain:

- 12 additional hunter definitions are needed to reach 37.
- 19 existing hunters have no optional dialogue questions.
- 12 existing hunters have no healing line.
- All 25 have empty conditional morning-dialogue lists, even though the supporting system is implemented.
- Twenty-four hunters use visual prefabs from the old `Assets/Prefabs/HunterVisualPrefabs` pipeline.
- One new custom-character prefab exists under `Assets/Content/3d/Characters`; it is temporarily assigned to Sister Ysolde for testing.

If all 37 demo hunters are meant to use the new custom art style, 36 additional production character prefabs remain after the current reference model. The new characters should use the shared Blender/FBX/prefab pipeline instead of character-specific code.

### Traits and supporting content

- 55 hunter-trait assets exist. All have configured mechanics; `Talent Scout` is the only hunter trait missing an icon.
- Six monster-trait assets exist, all with icons.
- Five kitchen recipes, fifteen construction definitions, seven investigation questions, three client profiles, and a thirteen-step voiced tutorial exist.
- All thirteen tutorial steps have voice clips assigned.

## Priority 0: lock scope and unblock production

### 1. Freeze the demo roster and progression

Select the 30 monsters and define which reputation tier introduces each one. Define the 12 new hunters and the final 37-hunter rarity/reputation distribution. Create one content checklist per selected monster, hunter, and equipment item so “associated content” has a measurable meaning.

Exit criteria:

- the demo monster library contains exactly the approved 30;
- the 37-hunter roster, traits, rarity, unlock tier, dialogue needs, model status, and portrait status are tracked;
- the demo's first day, expected progression length, and end condition are written down.

### 2. Finish the spreadsheet/content import pipeline

The project currently has a manual `MonsterFamilyImporter` that reads a chosen CSV and updates part of `MonsterData`. It is not a general spreadsheet pipeline and does not cover hunters, traits, equipment, economy, reputation tiers, or validation.

Build a versioned import workflow with stable IDs, a preview/dry-run report, unmatched-row errors, duplicate-ID checks, enum/tag validation, and a post-import validation summary. Treat the exported CSV files as inputs committed to the project so a balance state can be reproduced. Start with monsters, hunters, traits, equipment, and the main balance tables.

Exit criteria:

- designers can export the agreed sheets and update Unity assets without hand-editing every asset;
- imports never silently create duplicate IDs or accept unknown references;
- a clean re-import produces no unexpected asset changes.

### 3. Finalize the reusable character-production gate

The template and reference source exist, but the documented shared Unity importer preset and generic prefab validator do not. The reference FBX folder also differs from the documented `Model` layout.

Finish and approve:

- the Blender export skeleton and reference pose;
- the standard folder layout and mesh/material names;
- the shared Humanoid model-import preset;
- a generic validator for Avatar validity, scale, materials, UV0, Animator, face meshes, and MagicaCloth prerequisites;
- the reference prefab's face animation and coat/hair cloth tuning.

Exit criteria:

- a second character can be exported, validated, assigned through `HunterData.visualPrefab`, and animated without any C# change;
- FBX reimport preserves the Avatar, material remaps, prefab, face setup, and cloth data.

## Priority 1: implement the missing demo pillars

### 4. Build equipment as a data-driven system

The current Armory only swaps P09 weapon-part IDs and saves that visual choice. It has no owned inventory, typed slots, equipment traits, duplicate quantities, discovery state, loot tables, or equipment list.

Implement the feature in this order:

1. Add designer-authored equipment types, slot definitions, and `EquipmentData` assets containing stable ID, category, icon, description, world/character model, granted traits, and drop rules.
2. Add inventory and discovery state with duplicate quantities.
3. Add per-hunter typed slots, two by default, with equip/unequip/move operations and save/load support.
4. Merge equipped traits into the hunter's active mission traits and remove them cleanly when unequipped.
5. Replace the Armory's P09 weapon picker with hunter slots, owned-equipment drag/drop or click assignment, trait hints, and the warning shown when moving an already equipped copy.
6. Add the Equipment List with category grouping, owned-first alphabetical sorting, undiscovered entries, duplicate counts, detail view, and trait descriptions.
7. Attach equipment models through Humanoid bones or prefab-defined sockets so custom hunters do not need name-specific logic.

Exit criteria:

- inventory quantity and equipped instances remain correct across save/load, firing, death, and new-game reset;
- mission previews and resolution use equipment traits consistently;
- the same physical copy cannot remain equipped by two hunters;
- duplicate copies can be equipped independently up to the owned quantity.

### 5. Turn chests and mimics into actual rewards

`MissionBonusChestManager` can spawn a chest or mimic after a high-success mission, and the mimic chase/catch interaction works. `BonusChestInteractable` currently plays presentation and destroys or resets the object without granting a reward.

Connect both outcomes to equipment loot tables. Define separate normal-chest, caught-mimic, and escaped-mimic results. Show a reward popup and record the inventory change. Add deterministic developer controls for testing each rarity and outcome.

Exit criteria:

- opening a chest always resolves a configured reward;
- catching a mimic grants its configured reward and escape resolves cleanly;
- reward state cannot be duplicated through repeated interaction or save/reload.

### 6. Add the mission-progress map

The underlying progress data already exists: in-progress orders have `MissionTimer.GetProgress()` and `GetRemainingTime()`, and the Orders list already refreshes mission progress. The missing work is the map presentation.

Build the map as a UI screen driven by active orders rather than a second mission simulation. Give each order a route or destination definition, interpolate its party marker from timer progress, show remaining time and party portraits, and open the existing order detail when selected.

Exit criteria:

- every dispatched party appears and advances as action time advances;
- progress survives save/load and matches the Orders tab;
- multiple parties, evening resolution, cancellation, success, and failure leave no stale markers.

### 7. Add a small data-driven story layer and NPC pipeline

The project has hunter/client dialogue, a voiced tutorial, and a developer cinematic camera rig, but no story/chapter/quest state system. `CinematicCameraRig` is currently a trailer capture tool with hotkeys and screenshots, not a runtime narrative controller.

For the demo, implement story beats as data with conditions, one-time flags, dialogue/cinematic actions, and saved completion state. Add a generic NPC data/prefab path. Reuse existing dialogue presentation where practical, while keeping story state separate from hunter and client investigation data.

Scope the story before building it: identify the named NPCs, required scenes, trigger conditions, rewards/unlocks, and the final demo beat.

Exit criteria:

- story state saves and cannot replay or skip beats accidentally;
- NPCs use shared systems and prefabs;
- the complete short arc can be played from a clean save to the demo ending.

### 8. Give hunters individual seats and poses

The current seating system already has stable `HunterSeat.seatId` values, but `HunterManager` assigns the next free guild seat in round-robin order. Seating clips are shared on `SharedCharacterAnimator`.

Add a preferred seat ID and optional seated idle/entry/exit clip data to each hunter. Try the preferred seat first and retain the current free-seat fallback. Import Mixamo clips as Humanoid, in-place, looping where appropriate, and verify them against the shared Avatar. Keep briefing, kitchen, dormitory, infirmary, and Armory temporary-seat behavior intact.

Exit criteria:

- each configured hunter returns to the intended guild spot and pose;
- two hunters never claim one seat;
- departures, facilities, firing, death, and save/load restore seat occupancy correctly.

## Priority 2: presentation and content production

### 9. Rework the UI as one system

Establish a UI style guide first: typography, panel hierarchy, spacing, colors, button states, tooltips, portraits, icons, and motion/audio feedback. Then rebuild the highest-frequency flow in order:

1. HUD and interaction prompts;
2. client dialogue and Case Builder;
3. Orders, party assignment, map, and reports;
4. hunters, hiring, Armory, and Equipment List;
5. economy, construction, facilities, bestiary, settings, and menus.

Replace temporary runtime-created UI where final layout needs designer control, especially the Case Builder and generated order-list elements. Retain one shared prefab/component per repeated pattern so enabled and test scenes cannot drift.

### 10. Complete content in production batches

Produce content in vertical batches rather than finishing all models before integration. Each batch should include several hunters, their portraits/models/materials/face atlases/cloth/poses/dialogue, relevant equipment, and the monsters encountered at the same progression tier.

For hunters, complete optional questions, healing lines, and conditional morning dialogue as part of the character checklist. For monsters, finish trophy visuals for the selected 30 and verify the bestiary, Case Builder, client answers, mission reports, and trophy wall together.

### 11. Complete audio and clear licensing

The release scene already has music and general interaction/UI clips, and the tutorial is fully voiced. Concrete gaps found in current wiring include:

- construction build SFX;
- notification clips for info, success, and warning variants;
- default hunter and client footsteps;
- chest open/reward, mimic catch/escape, equipment, map, and story/NPC presentation;
- final broom sweep audio.

Several music filenames appear to be commercial soundtrack material. Confirm written distribution rights for every music/SFX/voice asset or replace it before any public build. Maintain a release audio/license manifest.

## Priority 3: integration, balance, and release hardening

### 12. Balance only after the equipment vertical slice is real

Once the spreadsheet import, selected roster, and equipment traits are integrated, rerun balance simulations and live telemetry. Validate progression by player behavior profile, economy, recruitment, equipment acquisition, referrals, mission success, wounds, deaths, and time pressure.

Remove or update stale backlog statements during this pass. `OrderListItem` is already separate, the four named traits already have mechanics, the old serialized `hunterLimit` entry is absent, and floor-wash action time is already configured.

### 13. Finish tutorial and onboarding

Add the documented manual reputation-upgrade lesson. Extend onboarding only for mechanics that are required in the demo: Case Builder, equipment/loot, the mission map, and the story's first objective. Verify the tutorial with both tutorial-enabled and tutorial-disabled new games.

### 14. Polish implemented systems

- Tune face gaze, blinking, and dialogue mouth timing.
- Finish and stress-test coat/hair MagicaCloth on the reference and production characters.
- Polish broom framing, sweep VFX/audio, and timing.
- Give the Case Builder an authored final layout and playtest clue/filter clarity.
- Resolve the five unregistered construction assets: explicitly add, defer, or remove them from demo content.
- Decide whether the card game appears in the demo. If it does, it needs production stakes/rewards and a reason to spend time on it.

### 15. Run the demo release gate

Required test passes:

- clean new game through the demo ending;
- save/load at every day state and during active missions;
- all referral, failure, wound, death, debt, firing, equipment, chest/mimic, construction, and game-over paths;
- repeated UI open/close and scene transitions;
- keyboard/mouse coverage and an explicit decision on controller support;
- performance, RAM, and VRAM on the intended minimum and recommended PCs;
- installed size, cold start, build version, credits, licenses, and privacy/telemetry disclosure;
- a non-development Windows build with no missing references, Console errors, or editor-only dependencies.

The project currently has local telemetry and balance simulation, but no project-owned automated test suite or populated hardware benchmark results. Do not publish system requirements until the release build is measured.

## Incomplete, unused, and legacy inventory

### Incomplete systems that affect the demo

- Equipment is visual weapon selection only.
- Bonus chests and caught mimics do not grant rewards.
- The mission map is absent, although progress data exists.
- No saved story/chapter/quest system exists.
- Hunter dialogue content is sparse, and all morning-dialogue lists are empty.
- The character pipeline lacks the documented importer preset and generic validator.
- Twenty-eight monsters lack trophy-head prefabs.
- Five construction definitions are not registered in `GameConfig` or `TrailerScene`: Dormitory 4, Dormitory 5, Kitchen Upgrade 1, Main Hall Upgrade 2, and Dungeon Keys Forge.
- Dungeon Keys Forge has an empty description, zero cost, and no established dungeon gameplay.
- The card game works as a minigame prototype but has no production stakes/rewards or action-time cost.
- The manual reputation-upgrade tutorial step is missing.
- Audio coverage and licensing are incomplete.
- Performance requirements and release-platform support remain unverified.

### Legacy or unused code/assets with high-confidence evidence

- The P09 modular humanoid pipeline, presets, visual applier, and old hunter visual prefabs are legacy and still required by current content. Remove them only after all hunters and equipment have migrated.
- `OrderData` is an older order-template ScriptableObject with no assets or current runtime references; live orders come from `OrderGenerator`.
- `HunterClass` is an unused enum; current hunter identity is trait/stat based.
- `CinematicActorMarker` has no references. The active `CinematicCameraRig` is developer/trailer tooling rather than story gameplay.
- `TutorialGateButton` and `TutorialSettingsToggle` have no scene, prefab, or code references.
- `MeshCombiner` is a context-menu authoring utility attached to several scenes, including `TrailerScene`; it has no runtime gameplay role and should move to editor tooling or be removed from release objects after confirming the combined meshes are baked.
- `TestScene`, `TestScene2`, `Test_Interior`, and `Experimental_Scene` are development/legacy scenes. Only `MainMenu` and `TrailerScene` are enabled in build settings.
- `Tools/Blender/__pycache__` is generated tooling debris and should not be part of the release source set.

### Backlog entries that are stale or lower priority

- Splitting `OrderListItem` into its own source file is already done.
- Talent Scout, Earplugs, Last Stand, and Overprepared already have configured effects; they need balance review, not initial implementation.
- The previously mentioned old `hunterLimit` serialization is not present in the live `GameConfig.asset`.
- Floor cleaning is implemented and documented; remaining work is presentation polish.
- The proposed loan system, deeper card-game progression, saved briefing drawings, richer dormitory scheduling, kitchen expansion, and extra facility tiers are full-game work unless deliberately promoted into demo scope.

