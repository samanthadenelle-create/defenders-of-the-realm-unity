# RESULT — WO-1184 lookout horde warnings (owner bounce 2026-08-27)

**Date:** 2026-08-27  **Seat:** CLI
**Status:** IMPLEMENTED — pending PO felt-verify. WO Status line left untouched (instruction).
**Not committed. No Unity batchmode.**

Owner bounce: *"red dot on screen should be a friendly way to let you know."*

## What changed

1. **Friendly on-screen tell, not a panic bang.** Removed the red `!` circle and "LOOKOUT REPORT / Raid incoming" copy. The live cue is a small parchment/gold **Lookout notice** chip (words, not colour-only; ASCII `--`; `ToastTone.Info`, never Danger).
2. **Off UIDocument, onto code-built uGUI.** `LookoutNoticeChip` is an ElarionUiKit overlay (same substrate as GearGrantToast / the rest of the HUD). HUD asmdef is untouched; `HudKitController` is untouched (WO-1221).
3. **`BestLookoutLevel` keys catalog id / role, not display name.** Matches `tower_ground_archer` and `StructureRole.Lookout`. Scans `GameState.BaseLayout` + live `PlacedStructure`. No `towerName.IndexOf("Archer"/"Watchtower")`.
4. **Phone half unchanged in contract.** Still one replaceable local notification, cancelled on return. Copy stays factual. No shield pairing. `SiegeScheduler` cadence is read-only (`SiegeIntervalMs`); WO-1179 not advanced.
5. **Oracle.** `[lookout-alert]` in `DataRegression.RunAll` pins the substrate, the catalog-id/role match, the friendly copy, and the no-shield / no-offline-combat fences.

On-screen notice is earned (`BestLookoutLevel() > 0`), matching the phone half. Level-3 still unlocks the force-size line.

## Player-facing strings

**On-screen (new):**
```
Lookout notice
Horde approaching -- the north gate in 5s.
```
Level-3 prefix example:
```
Lookout notice
A warband. Horde approaching -- the north gate in 5s.
```
(`the gates` / `all gates` when direction is mixed or unknown.)

**Phone (kept):**
- Title: `Lookout report`
- Body: `{size}Horde approaching. Expected at the town in {timing}. Return to defend live.`
- Size (L3 only): `A small raiding party.` / `A warband.` / `A large horde.` / `An unknown force.`

Neither surface claims the town is under attack, losing resources, or fighting offline. Neither offers a shield.

## Files

- `Assets/_Modules/Village/Waves/AlertIntelSystem.cs`
- `Assets/_Modules/Village/Waves/LookoutNoticeChip.cs` (+ `.meta`)
- `Assets/_Modules/Village/Siege/RoamingHordeNotifications.cs`
- `Assets/_Modules/Core/Catalog/StructureRole.cs` (`Lookout = "lookout"`)
- `Assets/Editor/Regression/LookoutAlertRegression.cs` (+ `.meta`)
- `Assets/Editor/Regression/DataRegression.cs` (register `[lookout-alert]`)
- `docs/MASTER_CATALOG/village-enemies-world.md` (AlertIntelSystem one-liner)

## Verification

- Brace-balance + NUL-clean on every touched `.cs`.
- Unity batchmode / compile gate **not run** (instruction). `[lookout-alert]` is authored, not executed this seat.
- `SiegeScheduler` not written. No shield offer. No HudKitController edit.

## PO felt-verify

- Seeker: the top-centre cue is a small **Lookout notice** chip (readable words, not a red bang), and it actually renders in a player build.
- Level-2 lookout: no force-size line. Level-3: size prefixes the approaching line.
- Backgrounding schedules one phone notice; returning cancels it. Copy stays "Horde approaching / return to defend live."
