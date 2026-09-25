# Floor cleaning

The current `TrailerScene` player has `PlayerFloorCleaning` and a camera child instance of `Assets/Prefabs/PlayerBroom.prefab`. The broom uses the mesh/material from `P_PROP_broom_interior`, without its prop colliders or static flags. `Assets/Animations/BroomSweep.anim` supplies a looping 0.8-second sweep.

## Player flow

Look at an active dirt decal volume within the normal interaction range and press the interaction key (E by default). The broom appears and the sweep repeats for three real seconds. The player can keep moving and looking unless the scene's wash interaction has `Locks Player` enabled. The pause menu freezes both animation and cleaning progress.

At completion, all floor dirt is cleared, the broom is hidden, any player lock owned by this interaction is restored, and the action-time cost is charged once using the existing dirt manager formula. Ten dirt points cost 15 action seconds; thirty points cost 35. Both take the same three seconds of animation. Cleaning may finish the workday if the action cost exhausts the remaining day.

Cleaning can start only during the active workday. It cannot be restarted by repeated input. Leaving the scene or disabling the cleaning component cancels it without a charge. Dirt is included in new-game reset and game-over backup/restore.

## Inspector setup

- `MainHallFloorDirtManager.visualThresholds`: keep the existing stage objects assigned. Decal Projectors may be on those objects or their children. No colliders or interaction scripts need to be added to each decal. The manager caches the stage projectors when it awakens.
- The manager tests the actual projector bounds, including pivot, rotation, size, and scale mode. Only enabled projectors under active stages can be targeted. Targeting covers the rectangular decal volume, including transparent texture areas; it does not sample PNG alpha.
- Normal physics obstructions limit the targeting distance. Existing interactables retain priority. Projector layers must be included in the player's interaction mask.
- `PlayerFloorCleaning.cleaningDurationSeconds`: fixed real-time cleaning duration, independent of action time. Default: 3.
- `broomRoot`: the camera child to show during cleaning. Its prefab is inactive by default.
- `broomAnimator`: the Animator on the broom root. It uses `Assets/Animations/BroomAnimator.controller`, which plays `Assets/Animations/BroomSweep.anim`.
- `BroomSweep.anim`: a looping 0.8-second clip. It animates the child named `Broom` from the player outward on local Z, with pitch/roll rotation for the scrub motion. Keep that child name when replacing the mesh, or update the animation bindings.
- `BroomCleaningVfxEvent`: on `Assets/Prefabs/PlayerBroom.prefab`. The clip calls `PlaySweepVfx` at 0.4 seconds, when the broom is farthest from the player. Assign either `Sweep Vfx` for a particle system kept on the broom, or `Sweep Vfx Prefab` for a one-shot effect. `SweepVfxPoint` is a child marker near the bristles.
- Reposition the `PlayerBroom` root to adjust the first-person placement without changing the clip. Adjust its child scale for the model size. The initial framing should be checked in the main scene at the intended FOV.
- To edit the motion, open the `PlayerBroom` prefab, temporarily activate the root for preview, select the root Animator in the Animation window, and edit `BroomSweep`. Do not animate the root placement; animate the `Broom` child and leave the root inactive when saving the prefab.
- To add VFX, put a Particle System under `Broom/SweepVfxPoint` and assign it to `BroomCleaningVfxEvent.sweepVfx`, or assign an effect prefab to `sweepVfxPrefab`. For short puffs, keep the Particle System stopped by default with Play On Awake disabled; the animation event will play it at the far point of every stroke.

## Implementation

`PlayerInteraction` checks visible dirt volumes after its normal physics query. `MainHallFloorDirtManager` stores a scene `WashFloorInteractable` on the same object, so its prompt and `Locks Player` flag are editable in the Inspector. The interaction starts `PlayerFloorCleaning` on the player; dirt stage deactivation therefore does not stop the cleaning owner midway through completion.

The current first visual threshold is five dirt points. Below that, there is no visible stage to target. Higher stages replace lower ones, and washing clears all stages together.

## Developer tools

In editor or development builds, press F9 to open Developer Tools. The `Floor Dirt` section shows current dirt, max dirt, and reward penalty. `Add dirt`, `Deduct dirt`, and `Clear dirt` update saved dirt state and visuals without spending action time.

## Play Mode verification

1. Start the active workday and accumulate enough dirt for the first visible stage.
2. Look at the decal within interaction range; confirm the cleaning prompt appears. Look beyond its volume or behind an obstruction; confirm it does not.
3. Press E; verify broom framing, visible looping outward motion, hidden prompt, and the configured movement lock behavior.
4. Pause halfway through; resume and verify the remaining sequence completes.
5. Confirm dirt is cleared, the broom disappears, movement returns, and action time advances once by `5 + dirtPoints` with the current settings.
6. Repeat at a higher dirt amount: real duration stays the same while action time increases.
7. Leave the scene during a wash: dirt should remain and no cleaning action time should be charged.
8. Start a new game and verify no floor dirt carries over from the previous guild.
