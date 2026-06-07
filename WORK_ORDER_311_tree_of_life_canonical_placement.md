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

## Root cause (triage 2026-06-06)
**Confidence: Likely.** The central tree is NOT the canonical asset. `VillageSceneBuilder`
(`Assets/Editor/VillageSceneBuilder.cs`) builds "Elarion" from the **Hexagon pack's generic large tree mesh**
(`decoration/nature/trees_A_large.fbx` scaled up — see the builder's own header note `:40-41`), not
`Resources/Structures/tree_of_life.fbx`. The builder header also describes "two centerpieces side-by-side"
(`:16`), implying it is off-origin / not the single dominant emissive Heart at exactly (0,0,0).
**Suggested minimal fix:** in the centerpiece/`BuildElarion` step, place the canonical tree_of_life asset (or
WO-240 heartwood) at exactly (0,0,0) at ~12–15 m with emissive glow + mound + stone ring, and remove the
generic Hexagon tree stand-in. Builder is the Lane-1 single-writer — serialize with WO-312/313. Bake via CLI
batchmode (editor closed). This is additive builder work, not a code-logic bug.

## Do NOT touch
- Never hand-edit `Village.unity`. Lane 1 single-writer — coordinate with WO-312/313 (same builder).
