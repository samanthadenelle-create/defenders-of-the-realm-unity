# WORK ORDER 748 — RESULT

**Status:** DONE — gate-green, committed local `f5fcbde2` on `wip/village2-and-f8-tickets`. **Push HELD; owner felt-verify pending.**
**Implemented by:** Claude (CLI), 2026-07-19.

## What landed
Founding choice "Default Town vs Build Your Own", offered after PetSelect before first hub entry.
- **CREATED `Assets/_Modules/Onboarding/FoundingChoiceController.cs`** (DeNelle.Onboarding -> Core only) — code-built uGUI Obsidian screen (ElarionUiKit, no UXML), two buttons, ASCII-only, meaning by label not colour. `PresentOrContinue(onContinue)` self-gates to genuine fresh foundings.
- **Default Town** = `StrategicPlacementMigrated=false` + Save (Guard-wrapped) -> re-triggers the existing one-shot `StrategicPlacementMigration.RunIfNeeded`, which converts the LIVE baked ring into MOVABLE `PlacedStructureData` at live grid cells. Granted, no cost, `FreeBuildsUsed` untouched. **Build Your Own** = no-op (blank + FTUE).
- **EDITED `PetSelectController.cs`** — the two fresh-founding routes call `FoundingChoiceController.PresentOrContinue(GoCastle)` (marker set before the Castle-side migration writer runs).
- **EDITED `TutorialFlow.cs`** — `lumbermill`/`lumberyard` id alias so the `founding_stores` guided-build step auto-satisfies under Default Town.

## Landmines resolved
1. **Merged-world coordinate mismatch** — sidestepped by design: no coordinates authored; the migration reads live world positions and grid-quantises. 2. **lumbermill vs lumberyard** — aliased in TutorialFlow. 3. **Uncatalogued stations** (apothecary/jewelers-bench) — auto-omitted (no catalog row -> migration skips). Result set = 7 movable storefronts; jeweler stays player-placed.

## Gate
`COMPILE_GATE_OK` (compile + brace + NUL). `DataRegression.RunAll` — ZERO new red from this WO.

## Owner felt-verify (R1/R2/R3, not defects)
- **R2:** records replay as movable on the NEXT hub load (established one-shot-migration contract); on the founding load itself the player sees the baked ring. Confirm this UX is acceptable.
- **R3:** offered at the PetSelect->hub chokepoint; verify every fresh-founding path funnels through PetSelect (a Play-Intro straight to GoCastle would skip the offer).
- **R1:** confirm the 7 ring storefronts are active on the founding load.
