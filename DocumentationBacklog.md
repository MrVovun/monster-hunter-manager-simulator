# Documentation Backlog

## Polishing Backlog

- Revisit UI click VFX rendering. Click VFX currently spawn under the Canvas, but assigned prefabs may still be invisible depending on Canvas/rendering setup.
- Investigate remaining FPS drops after sending hunters on orders. Profiler points to render/editor hitches and follow-up physics catch-up frames; dialogue cameras and URP shadow/render settings are likely suspects.
- Add a reputation progress bar to show current progress toward the next reputation level, using the same reputation points and thresholds as the reputation progress text.
- Audit construction registration and remove remaining double-wiring. `GuildConstructionManager` still reads `GameConfig.guildConstructions` and scene instances; construction assets that are only in `Resources/Constructions` will not appear unless referenced by one of those paths.
- Tune and finish newly added trait assets that need explicit balance values: Talent Scout, Earplugs, Last Stand, and Overprepared.
- Document real evidence tag categories/values for trait setup: `family`, `size`, `sound`, `tail`, `winged`, and `movement`.
- Review old/stale serialized config data after field removals or renames, such as old `hunterLimit` entries under `orderLimitByReputation`.
- Replace deprecated Unity object lookup APIs (`FindObjectOfType`, `FindObjectsOfType`) with the Unity 6 alternatives during a technical cleanup pass.
- Decide whether class traits with no bonus effects are intentionally descriptive/counter-only, or should be wired with effects.
- Future Briefing Room expansions:
  - More drawing result tiers.
  - Different buff types beyond success chance.
  - Better hunter reactions / per-hunter personality reactions.
  - Saving chalkboard drawings.
  - UI summary showing who got the briefing buff.
  - Multiple briefing room upgrades.
- Future Dormitory expansions:
  - Better sleep scheduling.
  - Bed quality bonuses.
  - Morale/rested buff after sleep.
  - Dormitory upgrade tiers with different beds/limits.
  - UI showing who has a bed / who slept.
- Future Kitchen expansions:
  - More recipe types and stronger recipe identity.
  - Kitchen upgrade tiers with better recipes or more serving capacity.
  - More guild activities around upkeep beyond dirty plates.
  - UI showing who has eaten and who still needs food.

## Notification Message Templates

Document the editable notification message templates in `NotificationManager`, including available placeholders:

- `{order}`
- `{hunter}`
- `{level}`
- `{gold}`
- `{xp}`
- `{dead}`
- `{wounded}`
- `{casualties}`
- `{hunter_count}`
- `{hunter_plural}`
- `{requested_gold}`
- `{current_gold}`
- `{client_label}`
- `{client_category}`
- `{candidate}`
- `{reason}`
- `{reason_suffix}`
- `{construction}`
- `{day}`
