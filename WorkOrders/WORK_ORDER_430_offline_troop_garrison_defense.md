> ⚠ **NUMBER COLLISION — this document does not own WO-430; `WORK_ORDER_430_Handover_Triage_Detailed_Work_Orders.md` does.**
> Referred to hereafter as **WO-430-F (offline troop garrison defense)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> ownership decided by **first-on-disk** (`git log --follow --diff-filter=A`): the winner's file was created first.
> Banner only — nothing was renumbered or deleted.

# WORK ORDER 430 — Offline Troop Garrison Defense (RECONCILED)

**Status:** SPEC - queued post-V1 (reconciled 2026-08-09 - restates this file's own SPEC - queued (post V1 / post Pi-loop) line in the canonical vocabulary; no commit references WO-430. DUPLICATE NUMBER: six files claim 430)

Status: **SPEC — queued (post V1 / post Pi-loop).** Priority: V1 loop polish (NOT Pi critical path).
Origin: owner/Grok concept 2026-06-26. **Reconciled against real code 2026-06-26** (read-only pass).
WO number per `CLI_LANES_WO_NUMBERS.md` numbering authority (next free = 430).

## Concept (owner)
While offline, troops not on a raid auto-defend the base; Echoes add minor defense. On return:
deterministic fast-forward sim of N offline waves; rewards (resources/XP/loot) by waves cleared;
breaching waves leave structures BROKEN → a resource/time repair loop; return summary screen + VFX.

## ⚠️ RECONCILIATION — what's real vs new (do NOT trust the concept's assumptions)
| Piece | Status | Reuse / Build |
|---|---|---|
| Offline accrual clock | **EXISTING** | `OfflineHarvestService.ClaimAccrual()`, `GameState.LastHarvestClaimMs`, 10h cap. Reuse atomically. |
| Echo workforce + cadence | **EXISTING** | `EchoService` (EchoCount, ClaimOffline, unlock every 5 waves). Same clock. |
| Wave defs + difficulty | **EXISTING** | `WaveManager`/`WaveData`/`WaveScalingCurve` — but LIVE-only. |
| Rewards grant | **EXISTING** | `EconomyService.Grant(...)`; XP via Enemy.Died subscribers. |
| Repair loop (live) | **PARTIAL** | `WallRepairController` repairs LIVE structures (crystal cost scaled by damage). |
| Damage tracking | **PARTIAL** | `GameState.BuildingDamage` dict (v19) exists — maps buildingId→damage. |
| Troops have level+DPS | **PARTIAL** | `TroopDef` (MaxHp/AttackDamage/…), `TroopController`. Combat-only (Step 1). |
| **Troops garrison-vs-raid state** | **NEW** | Troops deploy mid-raid only; no idle garrison state. |
| **Echo defense bonus** | **NEW** | No echo-defense stat exists. |
| **Offline wave SIMULATION** | **NEW** | No fast-forward sim; WaveManager is live-only. |
| **DefensePower formula** | **NEW** | Σ(troopLevel×DPS)+echoBonus vs wave difficulty — new. |
| **Persisted broken/repair state on placed structures** | **NEW** | `PlacedStructureData` = itemId/cell/yaw/level ONLY — **NO damage field** (concept's "already supported" is FALSE). Needs new field + SaveSchema **v26**. |
| Return summary UI | **PARTIAL** | `WelcomeBackPopup` (harvest-only) — extend, don't greenfield. |

**Honest read: ~65% greenfield.** Biggest false assumption in the concept: *PlacedStructureData persists
damage/repair* — it does not. Real path = add a damage field to PlacedStructureData (or extend the
existing `BuildingDamage` dict with repair-in-progress) + SaveSchema v26 migration.

## Build order (when scheduled)
1. SaveSchema v26: damage/repair state on placed structures (additive-nullable, default-on-read).
2. Troop garrison state (idle-in-base, available-if-not-on-raid).
3. Deterministic offline wave simulator (seed/pure-math) reusing WaveData + WaveScalingCurve.
4. DefensePower formula + Echo defense bonus.
5. Reward grant on return (EconomyService) + extend WelcomeBackPopup → OfflineDefenseResult.
6. Offline repair state on top of WallRepairController's cost formula.
7. VFX montage = polish (see `NOTES_vfx_polish_seam_and_towers.md`); reuse existing VFX.

## Success criteria (owner) — unchanged
Zero-cost; logging-in feels rewarding not punishing; repair quick; no balance break; works with Echo cadence.

## Sequencing
After the four-side seam (DONE) + the Pi V1 critical path (VillageTier unlock + farm→build→level→raid).
Deterministic sim is the hard part — headless-testable (DataRegression). NOT a blind build.
