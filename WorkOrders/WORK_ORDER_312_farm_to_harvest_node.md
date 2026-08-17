<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK_ORDER_312 — Replace Farm building with a small harvestable food node

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 1 (World/Env — VillageSceneBuilder) + Lane 6 (Economy)
**Origin:** owner playtest 2026-06-06 · **Reconcile with:** HarvestSite/MineNode, EconomyService

## Problem
The oversized red "Farm" building dominates the square and is just decoration. It should be a smaller tile
treated as a **harvestable node** (food), consistent with the resource-gather loop.

## Goal
Swap the big farm prefab for a small farm-tile/plot that behaves as a Food **harvestable node** (player/pet
harvest → banks Food via EconomyService).

## Scope
- Builder (VillageSceneBuilder): replace the large Farm building with a small farm-plot tile prefab from the
  catalog at an appropriate spot (not dominating the plaza).
- Behavior: attach the existing harvest-node component (`HarvestSite`/`MineNode`) configured for **Food**;
  yield routes through `EconomyService.Grant` (no new economy). Pet auto-harvest (WO-119) can target it.
- Visual harvest feedback reuses WO-229.

## Acceptance criteria
- [ ] The big Farm building is replaced by a small farm-tile node, correctly scaled, not dominating the square.
- [ ] It is harvestable (player [F] / pet) and banks Food through EconomyService.
- [ ] Reuses HarvestSite/MineNode (no duplicate harvest logic); placed via the builder (no .unity hand-edit).
- [ ] Brace check; CompileGate OK; build SUCCESS.

## Root cause (triage 2026-06-06)
**Confidence: Likely (where-to-look correct).** Not a bug — additive builder + component-attach work. The
harvest-node machinery the WO wants to reuse already exists and is correct:
- `MineNode` is a self-contained harvestable that banks via `EconomyService.Grant` (one faucet), with
  Food support (`MineResource.Food`) and player [F] / pet auto-harvest
  (`Assets/_Modules/Village/World/MineNode.cs:28`, `BankYield` `:376-415`, interact `:318-334`).
- `HarvestSite` is the Economy-routed claimed variant.
**Suggested minimal fix:** in `VillageSceneBuilder`, replace the oversized Farm building prefab with a small
farm-plot tile and attach `MineNode` (Resource = Food, AutoBuildVisual off if the tile supplies its own art).
No new economy. Lane-1 single-writer — serialize with WO-311/313.

## Do NOT touch
- Never hand-edit `Village.unity`. Lane 1 single-writer (coordinate w/ WO-311/313). Don't fork EconomyService.
