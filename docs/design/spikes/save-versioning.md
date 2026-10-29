# Spike: save versioning (2026-10-29, branch vs/prep)

**Code:** `Assets/Prototypes/_Shared/Runtime/SaveStore.cs`, tests `_Shared/Tests/SaveStoreTests.cs` (6 tests, shared
assembly 42/42). Not wired into `SimClock`: P2–P6 keep their save as tested.

**What it does**
- Envelope `{ v, lastSeenUtc, state }`, `CurrentVersion = 1`; one migration per version (`Migrations[i]`: i → i+1).
  Prototype saves (`{lastSeenUtc, state}`) read as version 0 and migrate.
- A save from a newer build returns `TooNew` and is never overwritten (the caller should keep it and start nothing).
- Atomic write: `.tmp`, then `File.Replace` into place with the previous save kept as `.bak`; a broken main file loads
  from `.bak` (`LoadedFromBackup`).
- Time away comes from the file only; a clock moved backwards counts as 0 h, a jump beyond 30 days is capped and flagged.

**Recommendation for the slice:** use it as the save layer (vertical-slice-plan.md §5.2). Still open:
- `JsonUtility` can't do renames or type changes inside `state`; when a migration needs that, migrate on the raw JSON
  (or move to a JSON library that Unity ships, e.g. `com.unity.nuget.newtonsoft-json`; that's a package install, so ask).
- `File.Replace` on iOS: falls back to copy + move if it throws; verify on a device in the slice's first TestFlight.
- Log `LoadedFromBackup`, `TooNew` and capped clock jumps as events so we see them in feedback issues.
