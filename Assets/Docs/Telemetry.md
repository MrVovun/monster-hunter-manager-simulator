# Local Telemetry

Local telemetry is controlled from `GameConfig` under the `Telemetry` header.

Files are written under `Application.persistentDataPath/<Local Telemetry Folder Name>`.
In the editor on Windows this is usually:

`%USERPROFILE%\AppData\LocalLow\<CompanyName>\<ProductName>\Telemetry`

## Files

- `live_events_<sessionId>.csv` contains one row per gameplay event.
- `live_sessions.csv` contains one compact summary row per closed play session.

## Currently Tracked

- Session start and scene loads.
- Day start, day state changes, and action-time advances.
- Gold changes, failed spends, debt changes, and game over.
- Reputation rank increases and Trust streak snapshots.
- Orders generated, accepted, referred, declined, canceled, mission started, and mission resolved.
- Mission success, failure, wounds, deaths, gold, XP, reputation, true monster, declared monster, and assigned hunters.
- Investigation and hunter dialogue questions answered, including known tag/trait counts after the answer.
- Hiring campaign start/end, candidate arrival/review/hire/decline, and hunter hire/fire/debt dismissal.
- Hunter level ups.
- Construction builds.
- Main hall floor dirt changes.

## Notes

This is intentionally local-only for internal balance testing. Later, the same event hooks can feed Unity Analytics or a custom endpoint without changing gameplay systems again.
