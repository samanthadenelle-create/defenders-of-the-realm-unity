# WO-2011 RESULT — Unified Item / Upgrade / Action State Model

**Status:** FIXED (commit a6bbc523d; COMPILE_GATE_OK [Builds/c26, 2026-09-06 11:18], REGRESSION_OK 400/400 suites [Builds/r24, 11:19], CATALOG_FALLBACK_GEN_OK [Builds/catgen2]) *(was: READY)*

**Date:** 2026-09-06

**Files shipped:**
- `Assets/_Modules/Core/Manage/ManageStateModel.cs` — **NEW** — separates ownership / upgrade-track state / action-availability with explicit enum fields; no contradictory combinations rendered.
- `Assets/_Modules/Core/Manage/ManageStateInvariants.cs` — **NEW** — contracts and guards on state transitions.
- `Assets/_Modules/Barracks/BarracksProgression.cs` — single accessor `EffectiveBarracksLevelOf()` serving both `TroopUnlock` and `BarracksService.IsTroopUnlocked` paths; resolves split-brain defect where barracks tier and unlock state disagreed on troop reachability.

**Data changes:**
- Migration: `MAX(BarracksLevel, buildingTier)` applied on read; save key preserved, no schema bump.
- Cross-checked building tiers / barracks / troops: ZERO disagreements on any row.

**New regression suites:**
- `TroopReachabilityRegression` — verifies unlock state across all 9 troops given barracks + building progression.
- `ManageStateModelRegression` — state invariants and contradictory-combination guards.

**Markers on fresh logs:**
- `COMPILE_GATE_OK` (c26, 11:18)
- `REGRESSION_OK 400/400 suites` (r24, 11:19)

## What was fixed

The split-brain diagnosis uncovered during this work: `TroopUnlock.EffectiveBarracksTier` already took `MAX(BarracksLevel, buildingTier)` while `BarracksService.IsTroopUnlocked` computed only barracks tier without the max. The Manage ARMY tab ANDs them, so the permanently-1 half of the split won: **7 of 9 troops were hard-locked** even when progression gates should have unlocked them. One authoritative accessor now serves both paths.

## Known gaps and parked items

None identified in this specialist pass. Acceptance criteria met.

---

*This is Wave 0 of the Manage redesign (commit a6bbc523d) — three pilots launched end-to-end on 2026-09-06 09:xx-11:xx, each shipping distinct state contracts and data reconciliation across the core loop. See WO-2003, WO-2005 for the parallel Heart progression and inventory filters.*
