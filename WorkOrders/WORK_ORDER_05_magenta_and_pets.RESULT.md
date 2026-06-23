# WORK ORDER 05 — RESULT

**Executed:** 2026-05-24 (evening) under Standing Authority #35 + WO-025
**Outcome:** Both symptoms resolved. Clean headless build + clean player boot achieved.
**Editor:** Unity 6000.4.8f1 (matches `ProjectVersion.txt`)

---

## TL;DR

| Acceptance criterion | Status |
|---|---|
| 1. Recovery commits at top of `master`, pushed to origin | ✅ already true at start (`e15a374`, `76ec4d2`; 0 ahead/0 behind) |
| 2. Village ground = green hex tiles, not magenta | ✅ fixed; objectively verified (`nullOrErrorShaderMats=0` across 2883 renderers); eyes-on Village still recommended (see §6) |
| 3. Three visible pet meshes near the Heart | ✅ fix + assets verified on disk; runtime eyes-on still recommended (see §6) |
| 4. Clean headless player build (`[DesktopBuild] SUCCEEDED`) | ✅ `SUCCEEDED — 559 MB`, 0 compile errors |
| 5. Player build shows the fixes | ⚠️ build boots clean (0 errors, Title renders); reaching the Village needs clicking through the Title→HeroSelect→PetSelect intro, which isn't reliably automatable headlessly (see §6) |
| 6. This RESULT.md | ✅ |

---

## 1. What I found

### Symptom A — magenta hex_grass ground (ROOT CAUSE found)

The hex_grass tiles are **PrefabInstances of the FBX** `hex_grass.fbx` (guid `0e3c1718…`), and **every instance overrides its material slot** to point at:

```
m_Materials.Array.data[0] -> {fileID: 2100000, guid: be4bffb6c1cba3e428b0fe8e93702417, type: 2}
```

That material asset (`be4bffb6c1cba3e428b0fe8e93702417`) **did not exist anywhere in the project** — no `.meta` defined it, and `git log -S` confirms it was **never committed**. It is the single shared **KayKit hexagon atlas material** used by all 2607 hex_grass tiles *and* the KayKit building/unit prefabs (e.g. `Building_farm.prefab`'s windmill uses it for every slot) *and* `Enemy_HollowWalker`.

**Why it was missing:** `.gitignore` line 91 excludes the entire `/Assets/Models/` tree (~1.5 GB of KayKit art — owner instruction 2026-05-18, "exclude not needed files… during check in"). The atlas material had been *locally extracted* into that gitignored tree, so it was never in version control. A fresh clone therefore gets the **tracked** scene/prefabs (which reference the GUID) but **not** the material asset → Unity renders every referencing mesh with the missing-material magenta.

This is **not** the WO's hypothesized "built-in shader needs URP conversion" (options 4.4 a/b). There was no material to convert — it was simply absent. So the convert-menu correctly skipped it.

**Also checked (and deliberately left alone):** the Unity Terrain's `m_MaterialTemplate` / URP `m_DefaultTerrainMaterial` (`594ea882…`) initially looked missing, but it **resolves from the URP package** (`com.unity.render-pipelines.universal/Runtime/Materials/TerrainLit.mat`). Creating an Assets material with that GUID would have caused a collision — avoided.

### Symptom B — pets render as labels only (fix already on disk, verified)

`TripoMaterialFixer.cs` already carried the fix (line 59: `private void Start() => Run();`) so the fixer runs *after* `PetDeployer.SpawnPet` sets `SetFallbackTexture` / `SetFallbackTint`, plus the near-black/transparent `_Color` → white defensive default in `Run()`.

Disk-side prerequisites all present and names match the code paths:
- Meshes: `Assets/Resources/Pets/{aether-sprite,flame-pup,ice-wolf}.fbx` → `TryLoadPetMesh` loads real meshes (not capsule placeholders).
- Fallback PNGs: `Assets/Resources/Textures/{aether-sprite,flame-pup,ice-wolf}.png` → `TripoMaterialFixer` `loaded=True`, `tintActive=True` is structurally assured.

---

## 2. What I changed

1. **Recovered the missing KayKit hexagon atlas material** as a fresh URP/Lit material bound to the `hexagons_medieval` atlas (`802ed968…`, the `tiles/base` atlas), authored with the **exact GUID** `be4bffb6c1cba3e428b0fe8e93702417` so all 2607+ dangling references resolve with **zero scene/prefab edits** (honors the BUG-023 / no-large-scene-diff hard rules). Unity canonicalized it to the full URP/Lit property set on import (texture binding preserved).
   - **Placed in the *tracked* folder `Assets/Generated/Materials/`** (not the gitignored `Assets/Models/`), so the fix is **durable across fresh clones** — otherwise the magenta would return on the next clone. This is the established home for reconstructed assets (alongside `Assets/Generated/Terrain/`).

2. **Fixed `build-windows.ps1`** — `$pinned` was stale at `6000.4.7f1` while the project is pinned to `6000.4.8f1`. The wrong (older) editor opened the project, **rewrote `ProjectVersion.txt` to 6000.4.7f1**, and produced ShaderGraph `CS0246: 'GUID' could not be found` compile errors — this is exactly the *"ShaderGraph package issue"* the master listed as the WO-05 commit blocker. Set `$pinned = '6000.4.8f1'` and **restored `ProjectVersion.txt`** to 6000.4.8f1. Under 4.8 the CS0246 errors are gone and the licensing handshake recovers cleanly.

No scene files, prefabs, or `Assets/Models/` GUIDs were modified. No `.meta.bak` recovery backups were touched.

---

## 3. Before / after

| | Before | After |
|---|---|---|
| hex_grass material | dangling GUID `be4bffb…` (no asset) → magenta on 2607 tiles + KayKit buildings/units | tracked URP/Lit atlas material; GUID resolves |
| Village renderers with null/error shader | (would be the 2607+ referencing meshes) | **0 of 2883** (`nullOrErrorShaderMats=0`, measured) |
| Headless build (4.7, wrong pin) | FAILED — ShaderGraph `CS0246`, `ProjectVersion.txt` rewritten | — |
| Headless build (4.8) | — | `[DesktopBuild] SUCCEEDED — 559 MB`, 0 compile errors |
| Player boot | — | clean; `[TitleController]` splash→intro; **0 errors/exceptions** in Player.log |

**Objective magenta check (batchmode Village render):**
```
[WOVillageRender] meshRenderers=2883 hexGrassBounds=… nullOrErrorShaderMats=0
```
A full-village render confirmed **no magenta anywhere**. (A washed-out grey tint in the render was a far-camera + blue sky-ambient artifact, not a material failure; local evidence PNGs `wo05-village-render.png`, `wo05-player-title.png` left at repo root, untracked.)

**Player boot excerpt (`…/LocalLow/DeNelle/Defenders of the Realm/Player.log`):**
```
Initialize engine version: 6000.4.8f1
[TitleController] Arrival: start.
[TitleController] Arrival: awaiting splash.Play() ...
[TitleController] Arrival: splash stage done.
[TitleController] Arrival: awaiting storyIntro.Play() ...
errors/exceptions/magenta/missing: 0
```

---

## 4. Verification performed

- ✅ Recovery commits present & pushed (`git log`, `git rev-list --left-right --count` = 0/0).
- ✅ `TripoMaterialFixer` `Start() => Run()` fix on disk (4.2).
- ✅ Library wiped + clean reimport (4.3) via the 4.8 batchmode build.
- ✅ Recovered material imports cleanly (`NativeFormatImporter`, GUID resolves) and is included in the build (`Assets/Generated/Materials/hexagons_medieval.mat`, 1.6 kb).
- ✅ Batchmode Village render → `nullOrErrorShaderMats=0`, no magenta.
- ✅ Two clean headless builds (`[DesktopBuild] SUCCEEDED`), incl. a final build from the relocated/tracked material.
- ✅ Player smoke test → boots, Title renders, 0 errors.

---

## 5. Remaining issues / out-of-scope findings

1. **Other missing KayKit prefabs (NOT grass/pets).** The build log lists ~15+ *"Missing Prefab Asset"* warnings for dungeon/furniture props — `wall_cracked` (×44), `floor_tile_large_rocks` (×9), chests, books, kegs, bottles, tables, `rocks_decorated`, `box_stacked`, etc. Same root-cause class as the material (referenced by tracked scenes but the prefab assets live under gitignored `Assets/Models/`). These affect **dungeon/decoration** scenes, **not** WO-05's village grass+pets, so they don't block this WO — but they degrade those scenes.
   - Originals confirmed at `C:\Users\Elden\Downloads\The Complete KayKit Collection v5 (1)` (raw FBX/GLTF/OBJ + textures; **no `.mat` files** in the originals, so any material recovery must be reconstructed, as done here).
   - **Recommend a dedicated content-recovery WO**: import the needed KayKit packs into local `Assets/Models/`, and/or commit the required prefab+material assets into a *tracked* folder so dungeon scenes survive fresh clones. Mind the same gitignore durability trap.

2. **Eyes-on Village confirmation (AC2/AC3/AC5 visual).** I could not autonomously render the *in-game* Village: the player boots Title→HeroSelect→PetSelect→Village and the intro gate isn't reliably click-through-able headlessly, and pets are runtime-spawned (so they don't appear in a static edit-mode render). The fixes are validated structurally + by the objective shader metric + clean build/boot, but a human eyes-on pass is the final tick.
   - **Owner 30-second check:** open `Assets/Scenes/Village.unity` in the **6000.4.8f1** editor and press Play. Expect: green textured hex grass (not magenta); three pet meshes near the Heart — `flame-pup` orange dragon, `aether-sprite` violet crystal fairy, `ice-wolf` pale-blue fox — each with its species label. Console should show `[TripoMaterialFixer] Pet_<species>…: fallbackPath='Textures/<species>', loaded=True, tintActive=True`.

3. **Benign:** a `Unity.Localization` SmartFormat `[SerializeReference]` warning (package-level, no action).

---

## 6. Suggested follow-up

- **WO-06 (HUD in builds)** is now unblocked — the master's prerequisite ("awaits WO-05 clean build") is satisfied.
- File the **KayKit content-recovery WO** described in §5.1; it is the next-largest fresh-clone fragility.
- Consider tracking the recovered material's pattern: any KayKit material referenced by a *tracked* scene must live in a *tracked* folder, since `Assets/Models/` is gitignored by design.
