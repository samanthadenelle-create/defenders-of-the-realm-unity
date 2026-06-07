# WORK_ORDER_321 — Missing gate on the side exit (near Pet House)

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 1 (World/Env — VillageSceneBuilder, SINGLE-WRITER)
**Origin:** owner playtest 2026-06-06 (screenshot) · **Reconcile with:** `VillageSceneBuilder` wall/gate build (BuildGates/Walls), canon §7 (exactly 4 cardinal gates)

## Problem
The side wall exiting town near the **Pet House** has an arch **opening with no gate** (and an adjacent arch
looks bare too). A gate/gatehouse is missing on that side — the perimeter isn't complete/consistent with the
canonical 4 cardinal gates.

## Goal
The side exit near the Pet House has a proper gate/gatehouse like the others — consistent geometry, passable
opening, aligned spawn point.

## Scope (via VillageSceneBuilder only — never hand-edit Village.unity)
- In the wall/gate build, ensure the gate on that side is actually placed (gatehouse piers + arch/doors), not
  just a bare opening. Match the other gates' prefab/scale/alignment.
- Confirm the opening is passable (NavMesh carved) and the WaveSpawnPoint sits 12–15m outside, aligned.
- Verify all **four** cardinal gates are present + consistent (no missing/duplicate).
- Rebake (CLI batchmode, editor closed).

## Acceptance criteria
- [ ] The Pet-House-side exit has a complete gate/gatehouse matching the other gates.
- [ ] Opening is passable (NavMesh) and its spawn point is aligned outside.
- [ ] All 4 cardinal gates present + consistent; no bare arches.
- [ ] Built via `VillageSceneBuilder.BuildVillage` (no hand-edited .unity); NavMesh rebaked.
- [ ] Brace check on any .cs touched; CompileGate OK; build SUCCESS.

## Do NOT touch
- Never hand-edit `Village.unity`. Lane 1 single-writer — coordinate with WO-311/312/313 (same builder).
