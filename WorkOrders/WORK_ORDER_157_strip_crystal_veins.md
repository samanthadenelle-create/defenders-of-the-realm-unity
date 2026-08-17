<!-- era-sweep-2026-08-17 -->
> ### ⛔ ERA SWEEP 2026-08-17 — CLOSED as OBSOLETE (deleted system)
> **Dead thing:** Village.unity. **Git first-add:** 2026-06-22.
> **Evidence:** `Assets/Scenes/Village.unity` is absent from disk and from `git ls-files`; scope is stripping crystal-vein decoration from the Village bake.
> Only the `**Status:**` line was rewritten. The body below is UNTOUCHED — CLAUDE.md §15, *"frozen, never rewrite"*.
> **TO REVIVE:** nothing was deleted and not one line of the body below was changed. If this work is still wanted, re-date the WO (add a `**Minted:** <today>` line), re-point it at the live scene/system, and set `**Status:** READY TO IMPLEMENT`.

# WORK ORDER 157 — Strip Crystal Veins from the Village (deleted content, magenta)

**Status:** CLOSED — OBSOLETE: Village.unity no longer exists (era sweep 2026-08-17)
**Priority:** Medium — visible magenta shards in the village; deleted content re-spawned
**Date:** 2026-05-30
**Lane:** Architect / World — `Assets/Editor/VillageSceneBuilder.cs` (single-writer) + rebake. CLI implements; UI spec only.
**Source:** owner playtest — the magenta/purple vertical shards by the world-tree "list as veins" → **needs deleted.**

---

## What & why

The magenta vertical shards in the village are the **crystal veins** decoration. They render magenta
because they're re-spawned deleted content with no valid material (same class of issue as the WO-150
magenta blob / Crystal Mine). **Crystals relocated to world nodes** (WO-150/153/154), so the village
veins should be **removed**, not material-fixed.

This is the **same skip-guard pattern** CLI already applied for orchard/trees/portals/Crystal Mine/
OuterWorldRoot in WO-150 — extend the deleted-content strip to the crystal-vein generator.

## Implementation

- Grep `BuildVillage` (and the WO-150 strip block) for the crystal-vein generator / objects (likely
  named `Vein`/`CrystalVein`/`Crystals` or placed near the Heart/world-tree). Add to the existing
  skip-guard / `DestroyImmediate` strip list — mirror the `go.name == "OuterWorldRoot"` line CLI added.
- Full `BuildVillage` rebake (editor closed) so the veins are gone.

## Acceptance criteria
1. No magenta crystal-vein shards anywhere in the village after rebake.
2. Veins do not re-spawn on subsequent bakes (generator guarded, not just deleted from scene).
3. WO-136 castle/parapet/wall-collision, the 5 buildings, world-tree (Heart), moat — all intact.
4. Scene integrity: 0 dup ids, 0 junk; NavMesh rebuilt; spawn→Heart path valid.
5. Brace balance on `VillageSceneBuilder.cs`.

## What NOT to touch
- Don't material-fix the veins — **remove** them (crystals are world nodes now).
- Don't touch the world-tree/Heart itself (it's supposed to be there — only the vein shards go).
- No `.unity` hand-edit; rebuild via builder, editor closed.

## Done checklist (CLAUDE.md §10)
- [ ] Crystal-vein generator guarded/stripped; no magenta veins post-bake
- [ ] Castle/buildings/Heart/moat intact; NavMesh rebuilt; path verified
- [ ] Brace balance; no hand-edit; editor closed for bake
- [ ] `WORK_ORDER_157_strip_crystal_veins.RESULT.md` when complete
