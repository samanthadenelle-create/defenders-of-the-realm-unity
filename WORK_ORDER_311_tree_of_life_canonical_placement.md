# WORK_ORDER_311 — Tree of Life canonical placement (Heart of Elarion at 0,0,0)

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 1 (World/Env — VillageSceneBuilder, SINGLE-WRITER)
**Origin:** owner playtest 2026-06-06 · **Reconcile with:** WO-240 (heartwood living tree asset), CLAUDE.md §7 canon

## Problem
The central tree in-scene isn't the canonical **Heart of Elarion / Tree of Life** — wrong asset/position
(off-centre, plain), not the dominant glowing centerpiece at world origin.

## Goal
The Tree of Life is the dominant, emissive centerpiece at **exactly (0,0,0)**, per canon (CLAUDE.md §7).

## Scope (via VillageSceneBuilder only — never hand-edit Village.unity)
- Place the canonical tree asset (`Assets/Resources/Structures/tree_of_life.fbx`, or WO-240's heartwood asset)
  at (0,0,0), large scale (~12–15m), emissive violet glow + mound + stone ring, in `BuildElarion`/centerpiece.
- Remove/replace any stray placeholder tree. Central plaza around it.
- Rebake (bake goes in a CLI batchmode step; editor closed).

## Acceptance criteria
- [ ] Tree of Life is at (0,0,0), dominant scale, emissive glow, with mound + stone ring.
- [ ] No duplicate/placeholder tree remains; plaza reads as the village centre.
- [ ] Built via `VillageSceneBuilder.BuildVillage` (no hand-edited .unity); NavMesh rebaked.
- [ ] Brace check on any .cs touched; CompileGate OK; build SUCCESS.

## Do NOT touch
- Never hand-edit `Village.unity`. Lane 1 single-writer — coordinate with WO-312/313 (same builder).
