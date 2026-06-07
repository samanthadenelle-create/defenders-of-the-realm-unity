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

## Do NOT touch
- Never hand-edit `Village.unity`. Lane 1 single-writer (coordinate w/ WO-311/313). Don't fork EconomyService.
