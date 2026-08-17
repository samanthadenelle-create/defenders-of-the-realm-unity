> ⚠ **UNRESOLVED NUMBER COLLISION — WO-280 is claimed by more than one file and OWNERSHIP IS NOT DECIDED.**
> Co-claimants: `WORK_ORDER_280_go_live_blockers.md`, `WORK_ORDER_280_village2_wiring_gate.md`
> Both added in the SAME commit (first-on-disk is a dead tie) and each is cited by exactly one other doc — the cross-reference tiebreak is also a tie.
> Flagged (not resolved) by the 2026-08-16 Sunday board-grooming pass — a wrong ownership call is worse
> than a flagged unknown, so this needs an **owner ruling**. Nothing renumbered or deleted. Until it is
> ruled, cite this WO by FILENAME, never by bare number.

# WORK ORDER 280 — Village2 Gameplay Wiring Gate (DEF-243)

**Status: READY TO IMPLEMENT — but GATED. Do not merge to feat/tower-core-loop until all 6 pass.**
**Source of truth:** `VILLAGE2_WIRING_NOTES.md` (repo root).
**Linear:** DEF-243 (In Progress). Deprecation map already posted as a comment there.

## Why gated
Village2 = a pretty generated town with NO gameplay layer yet. Merging it half-wired
walls the hero inside the town and breaks the lose condition. The unpushed commit
6c8af4c is deliberately held until this passes.

## The 6 checks (all must pass)
1. **Heart of Elarion** — `HeartController` on the Tree of Life at (0,0,0). Lose condition + HP + HUD bridge.
2. **Gates + spawns** — `Gate` + `GateProximityOpener` at the 4 cardinal wall gaps; `SpawnPoint`-tagged markers ~12 m OUTSIDE each gate.
3. **NavMesh two-scene seam** (owner-flagged P0) — Village2 bakes into the COMBINED Village+OuterWorld navmesh via `OuterWorldBuilder.BakeWorldNavMesh` + gate/seam `NavMeshLink` bridge. Reuse the `StairNavLink`/`RampartNavLinkInstaller` patterns. Without it the hero is walled in.
4. **Interactable buildings** — `Building` + `BuildingInteractable` on Forge/Market/Workshop/PetHouse/Farm/Lumbermill/Armorer so panels route (PanelRouter). v1 reuses houses, re-skin later.
5. **Scene-loader swap** — point `Assets/_Modules/Village/World/WorldSceneLoader.cs` at Village2 as the village content root. Preserve two-scene additive load.
6. **Pack trim** — `Assets/Quaternius` (675 MB CC0) untracked; LFS-track or bake the used pieces into a trimmed folder. Don't ship 675 MB to WebGL.

## On pass (sole committer executes)
- Merge 6c8af4c + wiring to feat/tower-core-loop (branch, gated, no force-push).
- Then retire the 14 `Assets/Editor/VillageSceneBuilder.*.cs` partials in ONE commit
  (PORT FORWARD `.NavMesh.cs` link logic first — Village2 needs the same seam).
- Close as superseded: DEF-245 / DEF-238 / DEF-237 (bugs in code that no longer ships),
  plus the set already listed in DEF-243 (DEF-191/195/220/240/etc).

## What NOT to touch
- VillageSceneBuilder partials stay as fallback until ALL 6 pass.
- No bake while the editor is open (see WO-279 pre-flight).
