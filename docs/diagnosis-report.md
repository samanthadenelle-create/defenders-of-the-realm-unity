# Village Recovery — Diagnosis Report

**Date:** 2026-05-22
**Investigators:** Agents 1 (GUID/.meta history), 2 (KayKit reimport strategy), 7 (architecture review)
**Repo state at time of diagnosis:** `master` up to date with `origin/master` (`2dffc44`), ~57 uncommitted modified files, untracked `.meta` files present.

---

## Headline

The village is **not** broken by GUID drift or a botched merge. The two suspicions that drove the original work order — "missing GUID references" and "cookie-cutter exterior merge broke the village" — both turned out to be wrong on the evidence. What is actually broken is the **Tripo asset pipeline** (Cathedral, CastleGate, hero/pet meshes) plus a few uncommitted `.meta` files that need to land in git. The KayKit hex buildings (homes, towers, walls, gates, etc.) are intact on disk and the procedural builder's paths all resolve.

This is a much smaller, safer fix than the work order anticipated. No destructive reimport is needed. No rewrite of `VillageController` or `VillageSceneBuilder` is needed.

---

## Root cause

Three things, in order of impact:

**1. Tripo texture extraction never completed.** The `TripoAssetPostprocessor` (Editor script at `Assets/Editor/TripoAssetPostprocessor.cs`) is supposed to extract embedded textures from Tripo-exported FBXs into sibling `Textures/` folders and write a `.tripo-extracted` marker. Across the project there are **zero `.tripo-extracted` marker files** and **no sibling `Textures/` folders** next to `Cathedral.fbx`, `castle+ballast+Tower.fbx`, `Resources/Heroes/*.fbx`, or `Resources/Pets/*.fbx`. Every Tripo target FBX *and* every companion PNG in `Resources/Textures/` is dirty in the working tree, confirming Unity recently re-imported them — but the extraction step never landed. Source materials are still legacy `FbxSurfacePhong` with null `_MainTex`.

**2. `TripoMaterialFixer` fallback is masking the breakage as solid color blobs.** At runtime, when the fixer (`Assets/_Modules/Core/TripoMaterialFixer.cs`) finds a material with no `_MainTex` and can't resolve the fallback texture, it applies only `_BaseColor = _fallbackTint`. Cathedral was given an explicit stone-grey fallback, which matches the "untextured stone-coloured Cathedral" symptom exactly. Other targets fall back to white. **This is why buildings appear "invisible/unstyled" — they're actually rendering as solid-color tinted geometry.**

**3. Three untracked `.meta` files are floating with freshly-generated GUIDs:**
- `Assets/Resources/Heroes/Mage.fbx.meta`
- `Assets/Resources/Pets/aether-sprite.fbx.meta`
- `Assets/Editor/TripoAssetPostprocessor.cs.meta`

These have **never existed in any commit** (`git log --all -p -- ...` returns empty). The parent commit `0d8bf60` ("Tripo asset pipeline: import postprocessor + new Mage/aether-sprite FBXs") landed the `.fbx` and `.cs` files **without** their `.meta` companions — a classic Unity GUID-drift trigger. If left untracked, the next clean clone will regenerate them with different GUIDs and the scene references will silently rot.

## What is NOT broken

- **KayKit asset packs.** All four KayKit folders (`dungeon/`, `medieval/`, `KayKit Dungeon Remastered 1.1/`, `KayKit Medieval Hexagon Pack 1.0.1/`) have 1-to-1 `.meta` coverage. No partial import. The shared atlas (`hexagons_medieval.png`) and its URP material are intact.
- **VillageSceneBuilder asset paths.** Every `LoadModel(...)` target in `VillageSceneBuilder.cs` (27 hex building FBXs, tiles/base, tiles/roads, decoration/nature, decoration/props, plus Mystery-Series-5 protagonists and Skeleton FBXs) resolves to a file on disk.
- **Village ↔ exterior "merge".** There is no cookie-cutter overlay. The integration is a heightmap seam blend in `ExteriorTerrainBuilder.SeamWeight()`. There is no GameObject linkage between the two builders, so no references could have broken across the seam. The visible problems the merge caused (Z-fighting, missing tree scatter, black terrain) have already been fixed in commits `0db1c3a`, `07ac2e2`, `8074dd9`.
- **`VillageController.cs`.** Skeleton class, wired via reflection by the Editor builder. Do not rewrite — would silently break every `BuildVillage` run.

## Recommended fix path

Lowest risk first. Each step is reversible.

### Step 1 — Lock in the floating GUIDs (5 minutes, do this first)

```
git add Assets/Resources/Heroes/Mage.fbx.meta
git add Assets/Resources/Pets/aether-sprite.fbx.meta
git add Assets/Editor/TripoAssetPostprocessor.cs.meta
git commit -m "Lock untracked Tripo .meta GUIDs before further work"
```

Do **not** delete these `.meta` files — that would force Unity to regenerate fresh GUIDs on next open, making the problem worse.

### Step 2 — Re-run Tripo texture extraction in the Editor

Open Unity. From the menu: **Defenders ▸ Tripo ▸ Force re-extract all textures** (menu defined at `TripoAssetPostprocessor.cs:236`). Verify Console shows `[TripoAssetPostprocessor] Extracted embedded textures from ...` lines for Cathedral, CastleGate, Heroes, and Pets. After this runs, these folders should exist:

- `Assets/Models/Cathedral/Textures/`
- `Assets/Resources/Heroes/Textures/`
- `Assets/Resources/Pets/Textures/`

…each with `.tripo-extracted` markers and populated `.mat` files with real `_BaseMap` references.

Run this **interactively first**, not in batchmode. The postprocessor's per-file ordering exists to survive a prior crash-loop in batchmode (see comments at `TripoAssetPostprocessor.cs:103-110, 152-159`).

### Step 3 — Rebuild the village scene

Menu: **Defenders ▸ Week 3 ▸ Build Village Scene** (`VillageSceneBuilder.BuildVillage`). The builder is idempotent (`VillageSceneBuilder.cs:24-27`) and uses path-based `AssetDatabase.LoadAssetAtPath` — it will pick up the now-correct materials automatically.

### Step 4 — Run KayKit material repair (belt-and-braces)

Menu: **Tools ▸ DeNelle ▸ Fix KayKit Materials**. This re-creates one URP material per KayKit subfolder against the local atlas and remaps the folder's FBX importer references. Cheap, idempotent, and the documented prerequisite at `VillageSceneBuilder.cs:2430`.

### Step 5 — Commit the repair

Single commit:

```
git add Assets/Models/Cathedral/Textures
git add Assets/Resources/Heroes/Textures
git add Assets/Resources/Pets/Textures
git add Assets/**/.tripo-extracted
git add Assets/Scenes/Village.unity
git add Assets/Resources/Textures/*.png
git add Assets/**/*.fbx
git commit -m "Repair Tripo materials + rebuild village scene"
```

After this commit, the textures survive a clean clone and the scene references resolve through the path-based builder.

### Step 6 — Visual verification

Hit Play in the Village scene. Confirm:
- Cathedral renders with stone texture (not solid grey)
- Castle gate / ballast tower renders with its basecolor (not solid white)
- Knight / Mage / Ranger heroes render textured (not solid colors)
- Hex buildings render with the medieval color palette (not pink/magenta error)

If any building still renders solid pink/magenta after these steps, the issue is `ForceHexMaterial` at `VillageSceneBuilder.cs:1965` — capture Unity Console output (look for `[VillageSceneBuilder]` warnings and `ForceHexMaterial` errors) before doing anything further.

---

## Optional cleanup (defer until after the fix is verified)

**Delete two orphan KayKit folders** to reclaim ~600MB:
- `Assets/Models/KayKit/dungeon/` (848 files, ~600MB; superseded by `KayKit Dungeon Remastered 1.1/`)
- `Assets/Models/KayKit/medieval/` (146 files, ~6MB; gltf-only, imported with wrong importer, never referenced)

`grep -rlF` of every GUID under those folders across `Assets/Scenes`, `Assets/Prefabs`, `Assets/Resources`, `Assets/Editor` returns **zero matches**. Only `docs/dungeons-3d-unity-layout-spec.md` mentions the bare `dungeon/` path as historical text.

**Important:** `.gitignore` lines 87-95 exclude `/Assets/Models/` entirely, so deletions are not recoverable from git. Zip both folders to a backup before deletion. Do this only **after** the Tripo fix is verified working, so a rollback path stays clean.

---

## Architecture recommendation (Agent 7)

### Short term — keep status quo

The current architecture (single `Village.unity` scene with two sibling roots `VillageRoot/` and `ExteriorRoot/`, built by two separate Editor scripts) works. Static batching is correctly applied. Terrain has sensible mobile defaults (`treeDistance=280`, `treeBillboardDistance=110`, `drawInstanced=true`). The exterior is already rendering correctly post-SeamFalloff fix.

Three small safety improvements worth doing whenever convenient:

1. Extract `VillageHalfX/Z` (currently hard-coded as 150/120 in both builders) into a shared `WallLayout` constant.
2. Gate the trailing `ExteriorTerrainBuilder.BuildExterior()` call in `VillageSceneBuilder` line 391 behind a bool param, so the village can be re-built without redoing the 2-3 minute terrain pass.
3. Add `[MenuItem("Defenders/Week 3/Rebuild Exterior Only")]` for terrain-only refresh.

### Long term — additive scene split

Move the exterior into `Assets/Scenes/VillageExterior.unity`, loaded additively at runtime from `VillageController` (or a small `WorldLoader` MonoBehaviour) once Village.unity finishes Awake. Justification:

- Halves headless `BuildVillage` time.
- Reduces merge conflict surface on `Village.unity`.
- Saves ~10–30MB of mobile memory on Title/HeroSelect (terrain + splats don't need to load until village entry).
- Unloadable on dungeon entry.

### Premature — Addressables chunking

A 300×300u world with 320 trees does not justify Addressables chunk streaming. Chunking pays off above ~1km² or with hundreds of unique props. Defer until the wilderness expands materially or enemy spawn area grows beyond the seam plateau.

---

## Do-not-touch list

- `VillageController.cs` — skeleton, wired via reflection. Do not rewrite.
- `WallLayout.Segments/Gates` — both builders hard-code the half-extents (150/120). Don't change one without the other.
- `ExteriorRoot` GameObject — don't delete manually; `BuildExterior` expects to be the sole author of that subtree.
- `TerrainBaseDepth = 0.5` — documented to prevent hex-tile Z-fighting (comment at `ExteriorTerrainBuilder.cs:105`).
- Per-instance color recoloring on dressing materials — breaks instanced batches (comment at `VillageSceneBuilder.cs:3476`).
- Village content → Addressables — defer; no perf payoff yet, forces a content-build pipeline change.

---

## Confidence and unknowns

**High confidence:** Tripo pipeline is the root cause (direct file-level evidence: missing markers, untracked `.meta` files, dirty FBX+PNG pairs, code path in `TripoMaterialFixer`). KayKit packs are intact (1-to-1 `.meta` coverage verified). Village/exterior integration is a heightmap blend with no broken references (architecture confirmed via reading both builders).

**Medium confidence:** Whether hex buildings specifically render correctly post-fix. Static analysis can't open Unity; if `ForceHexMaterial` is failing to load the shared URP material at runtime, every hex building renders as pink/magenta. The fix is `Tools ▸ DeNelle ▸ Fix KayKit Materials` (Step 4 above) — confirmed by reading `KayKitMaterials.cs` — but the verification has to happen in the Editor.

**Unknowns:**
- Whether the village's `m_PrefabSource` GUIDs in `Village.unity` (88k lines) still resolve to KayKit FBXs. Not feasible to grep blindly. **Not blocking** — the next `BuildVillage` run heals scene-side GUID references for free because the builder uses `AssetDatabase.LoadAssetAtPath` (path-based).
- NavMesh coverage of the exterior terrain. If enemy spawns move into the exterior, this becomes the next P0. Currently spawns sit in the seam plateau, so it's not blocking.
- Mobile frame budget on the target Android device. No profiler captures in repo.

---

## Status of the original 7-agent work order

| # | Agent | Status after diagnosis |
|---|---|---|
| 1 | GUID / .meta history | **Done.** Root cause identified — not GUIDs, Tripo pipeline. |
| 2 | KKit reimport strategy | **Done.** No reimport needed. Two orphans safe to delete. |
| 3 | Dungeon portal sub-map loading | Untouched. Defer to repair phase. |
| 4 | HUD top-left status repair | Untouched. Defer to repair phase. |
| 5 | Build Button UI flow repair | Untouched. Defer to repair phase. |
| 6 | Master volume toggle restoration | Untouched. Defer to repair phase. |
| 7 | Village + exterior architecture | **Done.** Status-quo OK short-term; additive scene split recommended long-term. |

Agents 3-6 are best handled in the repair phase with Unity Editor running — recommend moving to Claude Code in a terminal alongside Unity for those.
