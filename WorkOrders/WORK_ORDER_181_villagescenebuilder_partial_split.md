<!-- era-sweep-2026-08-17 -->
> ### ⛔ ERA SWEEP 2026-08-17 — SUPERSEDED
> **Superseded by:** work that has already shipped. **Git first-add:** 2026-06-22.
> **Evidence:** the split is DONE in tree: `Assets/Editor/VillageSceneBuilder.{Scene,Walls,NavMesh,Content,Dressing,Fortify,Helpers,Materials,Portals,Systems,Wiring,Characters,CityManifest,Village2Inject,Village3Recipe}.cs` all exist alongside `VillageSceneBuilder.cs`.
> Only the `**Status:**` line was rewritten. The body below is UNTOUCHED — CLAUDE.md §15, *"frozen, never rewrite"*.
> **TO REVIVE:** nothing was deleted and not one line of the body below was changed. If this work is still wanted, re-date the WO (add a `**Minted:** <today>` line), re-point it at the live scene/system, and set `**Status:** READY TO IMPLEMENT`.

# WORK ORDER 181 — Split VillageSceneBuilder.cs into partial-class files (<800 lines)

**Status:** SUPERSEDED by work that has already shipped (era sweep 2026-08-17)
**Priority:** Medium — maintainability / collision-risk reduction, not a gameplay bug.
**Date:** 2026-05-31
**Lane:** Architect / World — `Assets/Editor/VillageSceneBuilder.cs` (single-writer) + green-rebake equivalence gate. CLI implements.
**Source:** owner — *"a file 2,800 lines, shouldn't an architect refactor that into smaller chunks, no more than ~800 lines?"* Agreed.

---

## Why (and why carefully)
`VillageSceneBuilder.cs` is ~2,800 lines — 3.5× a healthy ceiling. But it is also (CLAUDE.md §9 + memory):
the **single-writer serialization bottleneck**, has **corruption-on-resave history**, and is the one file a
bad edit can use to **break the entire village bake**. So we split it the **zero-risk** way.

## Technique — C# `partial class`, NOT a logic rewrite
Same type, same namespace, same compile, **zero behavior change, zero call-site churn** — a pure source
reorganization. Move method groups into sibling files; change **no logic, no signatures, no constants**.

Proposed split (along seams that already exist):
| File | Contents |
|---|---|
| `VillageSceneBuilder.cs` | `BuildVillage` entry, `[MenuItem]`s, constants, shared fields |
| `VillageSceneBuilder.Walls.cs` | `BuildWallRing`, `BuildWallPerimeter`, wall fit helpers |
| `VillageSceneBuilder.Gates.cs` | `BuildGates`, gatehouse/pillar placement |
| `VillageSceneBuilder.Ramparts.cs` | `BuildRamparts`, WallBarrier boxes |
| `VillageSceneBuilder.Moat.cs` | `BuildMoat` |
| `VillageSceneBuilder.Heart.cs` | world-tree / Heart of Elarion build |
| `VillageSceneBuilder.Props.cs` | buildings, dressing, roads, nature |
| `VillageSceneBuilder.Helpers.cs` | reflection bridge, Make/Box/NormalizeProp/Strip* utilities |

Each file: `namespace DeNelle.Editor { internal static partial class VillageSceneBuilder { ... } }`.
Target every file < 800 lines. Keep the `.meta` for the original; new files get fresh `.meta`.

## Acceptance criteria (the equivalence gate)
1. **Compiles green** (brace balance every new file; no missing/duplicate members).
2. **Rebake equivalence:** rebake `BuildVillage` *before* (baseline already on disk) and *after* the split →
   the produced `Village.unity` is **functionally identical** (same roots/counts; the bake's own summary log
   `BuildVillage complete -- ...` reports the same wall/gate/prop counts). No new placeholders, no errors.
3. No method body, signature, constant, or execution order changed — diff is **moves only**.
4. Single-writer: done as its own pass, no other village edit in flight; editor-closed rebake.

## What NOT to touch
- No logic changes, no "while I'm in here" fixes — moves only. Any real fix is a separate WO.
- Don't change `BuildVillage`'s call order (324–342: Ring→Gates→Perimeter→Moat→Ramparts).
- No `.unity` hand-edit; rebuild via builder.

## Done checklist (CLAUDE.md §10)
- [ ] Split into partials; every file < 800 lines; moves-only diff
- [ ] Brace balance on every new file; compiles green
- [ ] Rebake equivalence verified (same summary counts, 0 errors, 0 new placeholders)
- [ ] `WORK_ORDER_181_villagescenebuilder_partial_split.RESULT.md` when complete
