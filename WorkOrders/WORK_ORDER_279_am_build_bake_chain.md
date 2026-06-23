# WORK ORDER 279 — AM Build + Bake Chain (ready-to-fire)

**Status: READY TO IMPLEMENT**
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
