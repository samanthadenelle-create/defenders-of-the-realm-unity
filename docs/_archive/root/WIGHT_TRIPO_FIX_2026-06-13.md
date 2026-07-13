# WIGHT — Tripo material + 90° rotation fix spec (2026-06-13)

Read-only investigation. Nothing changed. Branch `feat/tower-core-loop`.

---

## TL;DR (read this first)

**There is NO asset, code reference, catalog entry, or model named "wight" anywhere
in the project.** Case-insensitive grep across `Assets/_Modules`, `Assets/StreamingAssets`,
and a filesystem `find -iname "*wight*"` over all of `Assets/` returned **zero** hits
(the only "Dwight" hits are unrelated Yarn sample audio in `Packages/…snaaake`).

So "WIGHT" is the **owner's in-game name** for an on-screen creature that renders wrong,
not an asset id. By elimination it is one of the two **newest Tripo creature FBXs** that
were just wired into the spawn map on 2026-06-13 and that — unlike every working Tripo
enemy — got **neither a material extraction at import nor any rotation**:

- `Assets/Resources/Enemies/Demon.fbx`   (added Jun 5; wired 06-13 as `tiefling-cultist` + `demon`)
- `Assets/Resources/Enemies/OgreMage.fbx` (added Jun 5; wired 06-13 as `ogre` + `ogre-mage`)

Both have the EXACT two symptoms in the task: (a) embedded Phong material URP can't render
(magenta/unlit/white) and (b) no forward-rotation correction. **The fix below applies
identically to BOTH** — recommend fixing both in one pass. If the owner can confirm which
one they call "the wight," it is almost certainly **Demon** (gaunt undead-looking horned
silhouette reads as a wight far more than an ogre).

> ACTION FOR CLI: ask the owner "is the wight the Demon or the OgreMage?" if you want to
> scope to one — but the spec is the same for either, so fixing both is safe and cheap.

---

## Why these two render wrong — root cause (cite)

### Material (the magenta/unlit symptom)

Two facts combine:

1. **Enemies never get the runtime Tripo fixer.** `EnemyFactory.Build` skins via
   `SkinOptions.Enemy(height)` — `VisualFactory.cs:51`:
   ```
   public static SkinOptions Enemy(float height) =>
       new SkinOptions { FitHeight = height, StripColliders = true };
   ```
   `FixTripoMaterials` is **false** here. Only `SkinOptions.Structure` sets it true
   (`VisualFactory.cs:55`). So the runtime `TripoMaterialFixer` (attached via reflection in
   `VisualFactory.TryAddTripoFixer`, `VisualFactory.cs:145`) is **NOT** applied to any enemy.
   Enemies are expected to render correctly **because their material was fixed at IMPORT
   time**, not at runtime.

2. **The working Tripo enemies have their material extracted to a real URP `.mat` at import;
   Demon/OgreMage do NOT.**
   - `Assets/Resources/Enemies/Orc_Berserker.fbx.meta:6-11` has a populated `externalObjects:`
     block remapping the Tripo material `tripo_mat_f84a1f82` to an external URP material asset
     (`guid: b18ad3044f20eba4ab25077a1a16a3b1`). This is why orcs render correctly.
   - `Assets/Resources/Enemies/Demon.fbx.meta:6` and `OgreMage.fbx.meta:6` both have
     **`externalObjects: {}`** (empty) with `materials.materialImportMode: 2` /
     `materialLocation: 1` — i.e. the embedded **Phong** material is used as-is. That is
     exactly the `FbxSurfacePhong` case `TripoMaterialFixer.cs:6-12` documents URP can't render.

   Net: Demon/OgreMage ship their raw Tripo Phong material and nothing — import-side or
   runtime-side — converts it to URP. → magenta / unlit / white.

### Rotation (the "needs 90° like the other Tripo" symptom)

`EnemyFactory.Build` only applies the -90° yaw to the **OrcWarband** rig
(`EnemyFactory.cs:87-90`):
```
var skinOpts = SkinOptions.Enemy(height);
if (EnemyAnimatorFactory.RigFor(model) == EnemyRig.OrcWarband)
    skinOpts.LocalRotation = Quaternion.Euler(0f, -90f, 0f);
```
But `RigFor("Demon")` and `RigFor("OgreMage")` both return `EnemyRig.HumanoidLarge`
(`EnemyAnimatorFactory.cs:39-41`), **not** OrcWarband. So they get
`LocalRotation = null` → identity → no rotation. If their FBX exports facing +X (the
Tripo/AccuRIG convention — same as the orcs and the heroes), they face 90° off travel.

---

## The working-sibling pattern to match

| Concern | Working sibling | File:line | Exact value |
|---|---|---|---|
| Tripo material → URP (import-side) | `Orc_*.fbx` extracted material | `Orc_Berserker.fbx.meta:6-11` (`externalObjects` remap) | external URP `.mat` |
| Tripo material → URP (runtime-side, alt.) | `TripoMaterialFixer` via `FixTripoMaterials` | `VisualFactory.cs:38, 95, 145-155` | attach component |
| 90° forward yaw (the "like the other Tripo") | OrcWarband enemies | `EnemyFactory.cs:88-89` | `Quaternion.Euler(0f, -90f, 0f)` |
| Same yaw, hero proof of value | `HeroBodySwapper` | `HeroBodySwapper.cs:99-106` | `forwardYaw = -90f` (WO-326: "-90f is the proven value") |

**Canonical Euler for a +X-forward Tripo body = `Quaternion.Euler(0f, -90f, 0f)`** (yaw -90°,
applied via `SkinOptions.LocalRotation` so it lands BEFORE fit/seat — DEF-232,
`VisualFactory.cs:86-90`). Never set `localRotation` after `Skin()` (off-pivot swing bug).

---

## THE FIX — precise steps

This is a **code fix** (the factory), not a prefab/scene edit. It is the cleanest match to
how the orcs already work and keeps the single enemy-creation path authoritative.

### Step 1 — Material (choose ONE of 1A / 1B; 1A preferred)

**Option 1A (preferred — matches orcs exactly, import-side, zero runtime cost):**
Re-import `Demon.fbx` / `OgreMage.fbx` with their Tripo material **extracted to an external
URP material**, so `externalObjects` is populated like `Orc_Berserker.fbx.meta:6-11`.
The project already has the editor tooling for this — the magenta/Tripo material fixers under
`DeNelle.Editor` (see `docs/MASTER_CATALOG/editor-tools.md` "magenta material fixers"). Run the
same extract-and-remap the orcs went through. Verify success by confirming
`Demon.fbx.meta` / `OgreMage.fbx.meta` line 6 changes from `externalObjects: {}` to a
populated remap block. **Do NOT hand-edit the `.meta`** — let the importer/tool write it.

**Option 1B (runtime fallback — if 1A extraction is inconvenient):**
Make `EnemyFactory` attach the runtime `TripoMaterialFixer` for the Generic-rig Tripo brutes
(Troll/Demon/OgreMage), the same component `VisualFactory` already wires for structures.
In `Assets/_Modules/Village/Enemies/EnemyFactory.cs`, after the rig-rotation block
(`EnemyFactory.cs:87-90`) and before the `VisualFactory.Skin` call (`EnemyFactory.cs:90`),
set `skinOpts.FixTripoMaterials = true` for these models. Concretely, gate it on the same
`HumanoidLarge` Tripo set (Troll/Demon/OgreMage) — NOT the KayKit `Skeleton_Golem`, which is
also `HumanoidLarge` but is KayKit (already-URP) and must NOT be reprocessed. Cleanest guard
is by model name:
```
if (model == "Demon" || model == "OgreMage" || model == "Troll")
    skinOpts.FixTripoMaterials = true;
```
`VisualFactory.Skin` then calls `TryAddTripoFixer` (`VisualFactory.cs:95, 145`) which rebuilds
each material as `Universal Render Pipeline/Lit` carrying the basecolor.
> Note 1B caveat: `TripoMaterialFixer` runs in `Start()` (next frame) and its default has no
> fallback tint/texture — if the embedded Phong has no resolvable `_MainTex`/`_BaseMap` the
> body could render solid white. 1A (extraction) is the more reliable, orc-matching path.

### Step 2 — Rotation (-90° yaw, code)

In `Assets/_Modules/Village/Enemies/EnemyFactory.cs`, extend the rig-rotation condition at
`EnemyFactory.cs:88-89` so the Generic-rig Tripo brutes also receive the proven -90° yaw.
Current:
```
if (EnemyAnimatorFactory.RigFor(model) == EnemyRig.OrcWarband)
    skinOpts.LocalRotation = Quaternion.Euler(0f, -90f, 0f);
```
Change to also cover the +X-forward Tripo brutes (Demon/OgreMage — and Troll if it shows the
same off-facing; the existing code comment `EnemyFactory.cs:85-86` already flags "playtest if a
Tripo Troll lands"):
```
if (EnemyAnimatorFactory.RigFor(model) == EnemyRig.OrcWarband
    || model == "Demon" || model == "OgreMage")
    skinOpts.LocalRotation = Quaternion.Euler(0f, -90f, 0f);
```
- Exact Euler: **`Quaternion.Euler(0f, -90f, 0f)`** (yaw axis = world/local Y, -90°).
- WHERE it must be set: on `skinOpts.LocalRotation` BEFORE `VisualFactory.Skin`
  (`EnemyFactory.cs:90`) — it is applied pre-fit/seat inside Skin (`VisualFactory.cs:90`).
  Do NOT set the visual child's `localRotation` after Skin (DEF-232 off-pivot swing).
- If a body comes out 90° the WRONG way after testing, the alternate is `+90f` — but -90f is
  the project-proven value for this exact Tripo/AccuRIG export (orcs + all 4 heroes use -90f,
  `HeroBodySwapper.cs:96-99`). Start with -90f.

### Step 3 — Quality gate
Brace-balance `EnemyFactory.cs` after the edit (CLAUDE.md §1) and build-verify
(`COMPILE_GATE_OK`). No scene/prefab files are touched. Then playtest the Wildlands roamers
(`tiefling-cultist` → Demon, `ogre`/`ogre-mage` → OgreMage) to confirm both the texture and
the facing.

---

## Asset path summary

| Item | Path |
|---|---|
| Demon model | `Assets/Resources/Enemies/Demon.fbx` (+ `.meta` — `externalObjects: {}`, Phong) |
| OgreMage model | `Assets/Resources/Enemies/OgreMage.fbx` (+ `.meta` — `externalObjects: {}`, Phong) |
| Working orc sibling (extracted material) | `Assets/Resources/Enemies/Orc_Berserker.fbx.meta:6-11` |
| Factory (rotation + optional 1B material) | `Assets/_Modules/Village/Enemies/EnemyFactory.cs:70, 87-90` |
| Rig classifier (Demon/OgreMage → HumanoidLarge) | `Assets/_Modules/Village/Enemies/EnemyAnimatorFactory.cs:39-41` |
| Skin / LocalRotation mechanism (DEF-232) | `Assets/_Modules/Village/VisualFactory.cs:38, 51, 86-90, 145-155` |
| Runtime Tripo fixer | `Assets/_Modules/Core/TripoMaterialFixer.cs` |
| Hero -90f rotation proof | `Assets/_Modules/Village/Hero/HeroBodySwapper.cs:96-106` |

## Fix classification
- **Material:** preferred = **import setting** (extract Tripo material to external URP `.mat`,
  matching orcs — `Orc_Berserker.fbx.meta`); fallback = **code** (`FixTripoMaterials=true` in
  EnemyFactory).
- **Rotation:** **code** (one-line condition extension in `EnemyFactory.cs:88-89`,
  `Quaternion.Euler(0f, -90f, 0f)` via `SkinOptions.LocalRotation`).
