<!-- era-sweep-2026-08-17 -->
> ### ⛔ ERA SWEEP 2026-08-17 — CLOSED as OBSOLETE (deleted system)
> **Dead thing:** OuterWorld.unity. **Git first-add:** 2026-06-22.
> **Evidence:** `Assets/Scenes/OuterWorld.unity` is absent from disk and from `git ls-files`; the chain's steps 2–4 are `OuterWorldBuilder.BuildOuterWorld` / `ExteriorTerrainBuilder.BuildExterior` / `BakeWorldNavMesh` against that scene, and its acceptance is "hero can exit a gate to OuterWorld". (Co-claimant of WO-279 — `WORK_ORDER_279_village2_generator_fixes.md` targets Village2, which is LIVE, and stays READY.)
> Only the `**Status:**` line was rewritten. The body below is UNTOUCHED — CLAUDE.md §15, *"frozen, never rewrite"*.
> **TO REVIVE:** nothing was deleted and not one line of the body below was changed. If this work is still wanted, re-date the WO (add a `**Minted:** <today>` line), re-point it at the live scene/system, and set `**Status:** READY TO IMPLEMENT`.

> ⚠ **UNRESOLVED NUMBER COLLISION — WO-279 is claimed by more than one file and OWNERSHIP IS NOT DECIDED.**
> Co-claimants: `WORK_ORDER_279_am_build_bake_chain.md`, `WORK_ORDER_279_village2_generator_fixes.md`
> Both files were added in the SAME commit (first-on-disk is a dead tie) and neither is cited by any other doc, RESULT file, or commit message — there is no evidence on either side.
> Flagged (not resolved) by the 2026-08-16 Sunday board-grooming pass — a wrong ownership call is worse
> than a flagged unknown, so this needs an **owner ruling**. Nothing renumbered or deleted. Until it is
> ruled, cite this WO by FILENAME, never by bare number.

# WORK ORDER 279 — AM Build + Bake Chain (ready-to-fire)

**Status:** CLOSED — OBSOLETE: OuterWorld.unity no longer exists (era sweep 2026-08-17)
**Owner seat:** CLI gatekeeper (or sole committer on watch) — controlled batchmode only.
**Created:** overnight watch, for the AM roundtable.

## Purpose
One-command path to a fresh, non-corrupt Windows + WebGL build for the roundtable.
Do NOT fire while the Unity **editor** (`Unity.exe`) is open — Hub being open is fine,
but a bake during an active editor session corrupts the village scene (MEMORY: village-scene-resave-corruption).

## Pre-flight (verify before firing)
1. `Unity.exe` NOT running (Task Manager). Unity Hub may be open.
2. No `.git/index.lock`.
3. On a clean-ish tree — do NOT let the bake's regenerated terrain/asset churn get
   swept into a `git add -A`. Stage build artifacts by explicit path only.

## The chain (in order — each must succeed before the next)
Use the fork-aware helper: `run-unity-method.ps1 -Method <X> -LogName <log>`.
Per CLI_GATEKEEPER_PLAYBOOK §3 — method names are EXACT:
1. `DeNelle.Editor.VillageSceneBuilder.BuildVillage`  ← verify log "[CityManifest] placed N buildings". GATED: only with explicit owner "go" on the village lane (safety classifier enforces).
2. `DeNelle.Editor.OuterWorldBuilder.BuildOuterWorld`  ← ⚠️ THIS WIPES THE TERRAIN.
3. `DeNelle.Editor.ExteriorTerrainBuilder.BuildExterior`  ← REBUILDS the terrain BuildOuterWorld just wiped. SKIPPING THIS = "world void" / can't-walk-out (the #1 recurring bug).
4. `DeNelle.Editor.OuterWorldBuilder.BakeWorldNavMesh`  ← must log "marked N terrain(s)", N ≥ 1 (hard-errors on 0).
5. `build-windows.ps1` — deletes `Builds/Windows` FIRST (exe-stub staleness → native crash), then builds. NOT the Build Profile button.
6. WebGL: `build-webgl.ps1` (separate, for the itch.io playtest drop).

Verify each: `[CityManifest] placed N buildings`, `marked ≥1 terrain`, `Build Finished, Result: Success`, `_Data/level0` present.
Note: the 505 license handshake prints every batchmode launch but is transient/non-fatal — judge by the success marker, not 505.

## Acceptance
- [ ] Bake log shows "marked ≥1 terrain" — world is walkable out of the village
- [ ] `Builds/Windows` freshly emitted (deleted-then-rebuilt) — no level3 crash on load
- [ ] Hero can exit a gate to OuterWorld (combined navmesh seam intact)
- [ ] WebGL build uploaded for Tricia with a plain-language playtest card

## What NOT to touch
- Do not hand-edit `Village.unity`
- Do not advance/merge DEF-243 Village2 here — that is WO-280, gated separately
