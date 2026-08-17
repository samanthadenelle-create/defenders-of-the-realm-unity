# WORK ORDER 1101 — IMPLEMENTATION PLAN (ground textures + simple aesthetics)

**Status:** PLAN — READY FOR AN IMPLEMENTATION AGENT
**Date:** 2026-08-17
**Parent WO:** `WorkOrders/WORK_ORDER_1101_biome_maps_and_grass_texture_variety.md` (owner APPROVED 2026-08-17)
**Constraining canon:** `WorkOrders/WORK_ORDER_1044_biome_identity.md` — all eleven rulings APPROVED 2026-08-17.
Biome identity is CANON and is **not** re-opened here. This document is EXECUTION only.
**Trigger:** owner, 2026-08-17 — *"i want the textures for the world added. grass and simple aesthetics."*

> Design thinking is already done. Read WO-1044 §1 for the four authored palettes and
> `docs/ECHOES_OF_ELARION_NARRATIVE.md` §3-5b / `docs/regions-narrative-and-npcs.md` §2-5 for the lore.
> Do not re-derive them. This plan says *which files change, which bake runs, and how we prove it*.

---

## 1. THE DECISION — (b) RE-USE existing pack textures per biome

**Verdict: (b), with a mandatory curation step. No new texture art is authored or purchased.**

### Evidence that decided it

**E1 — The world has no ground texture art at all today. Not "bad textures" — none.**
`Assets/Editor/ExteriorTerrainBuilder.cs:793-806` (`MakeLayer`) builds each terrain layer with
`diffuseTexture = MakeSolidTexture(tint)`. `MakeSolidTexture` at `:809-840` synthesises a **64×64 RGB24
solid colour** with a ±22% Perlin mottle and ±2.5% grain, then `AssetDatabase.AddObjectToAsset(tex,
TerrainDataPath)` embeds it inside `ExteriorTerrainData.asset`.
Confirmed at source: all five `.terrainlayer` files in `Assets/Generated/Terrain/` carry
`m_DiffuseTexture: {guid: 71af267a01f8af14e851d47985fb6877}` — and that guid **is
`ExteriorTerrainData.asset.meta`**, not a PNG. Every layer also has `m_NormalMapTexture: {fileID: 0}`.
**Zero normal maps, zero albedo art, five 64-pixel colour swatches.** That is the whole reason the ground
reads flat. The owner asking for "textures for the world" is asking for something that has never existed.

**E2 — 160 authored, PBR-complete terrain layers already exist on disk.**
`Assets/Blink/Art/Textures/` (Blink RPG Art ULTIMATE Bundle) ships ~160 `.terrainlayer` assets across nine
themed packs, each with `_BaseColor` + `_Normal` + `_Height` + `_Roughness` + `_AmbientOcclusion` PNGs
imported at 2048. `StylizedForestTextures` alone covers grass (10), mud/dirt (7), cliff (4) plus `Roots`,
`Sand`, `Village_Stone_Path`. `StylizedIceTextures` covers snow; `StylizedVikingTextures` covers dead
leaf-litter. Spot-verified: `StylizedForestTextures/Grass_1/Grass_1.terrainlayer` →
`m_DiffuseTexture guid 699bdf6b…` → `Grass_1_BaseColor.png`, `m_NormalMapTexture guid 41c380f8…`, tileSize 2×2.

**E3 — The repo already does exactly this, in tracked files.**
`Assets/Resources/Arena/Grass_1.mat` is **git-tracked at HEAD** (`git show HEAD:…` confirms) and its
`_BaseMap` is guid `699bdf6be4304094db33dda64e887b3b` — Blink's `Grass_1_BaseColor.png` — with `_BumpMap`
guid `5e6f875f…`. `Assets/Resources/Arena/Materials/ArenaGround.mat` uses the same base map at 4×4 tiling.
So Blink ground textures are already the de-facto ground art of this project. Re-using them for terrain is
consistent, not novel.

**(a) is rejected** — importing new textures is unjustifiable when 160 authored PBR sets sit on disk.
**(c) is rejected as the primary approach** — material variants over one texture set is what we have TODAY
(one procedural swatch generator, five tints) and it is precisely the thing that fails. Material/tiling
variants remain a *secondary* tool (§4 step 3), layered on top of real textures, not instead of them.

### ⚠ The one blocker on (b), and its resolution

`Assets/Blink/` is **gitignored** (`.gitignore:350 /Assets/Blink/`) with **zero tracked files**
(`git ls-files Assets/Blink` → 0). `Assets/polyperfect/` is likewise gitignored (`.gitignore:182`).
Both are physically present on this machine; `Assets/Quaternius/` is **absent entirely** — so any code
still expecting `Floor_WoodDark` is already referencing a missing asset here.

A tracked `.terrainlayer` pointing at a Blink guid **works on this machine and breaks on a fresh clone**
(missing texture → the exact colourless/pink ground failure CLAUDE.md §12 was written about). The tracked
Arena materials above are *already* in that broken state on a clean clone — precedent, not permission.

**Resolution — mandated by WO-1101 §0.6** ("prefer tracked sources or new small textures under
`Assets/Generated/Terrain/` so a fresh clone still builds"):
**CURATE.** Copy only the eight BaseColor + eight Normal PNGs named in §3 out of Blink into a new tracked
folder `Assets/Generated/Terrain/Layers/`, re-imported at **1024** max size (mobile budget; 2048 × 16 is
32 MB of VRAM we do not need for ground). `Assets/Generated/Terrain/` is fully git-tracked and **not** LFS
(`git check-attr filter` → unspecified), so the copies survive a fresh clone. Sixteen 1024 PNGs is roughly
8-12 MB — acceptable, and it is the only way this feature exists for anyone but this machine.

---

## 2. WHICH BUILDER OWNS THE GROUND, AND WHICH BAKE

**Owner of the hub ground: `Assets/Editor/ExteriorTerrainBuilder.cs`.**
- Builds the 1000×1000 origin-centred Unity Terrain for `Main_Castle_Overworld`, writing
  `Assets/Generated/Terrain/ExteriorTerrainData.asset`.
- Terrain material assigned at `:340` — `terrain.materialTemplate = EnsureTerrainMaterial()`;
  `EnsureTerrainMaterial()` `:1661-1688` resolves
  `Shader.Find("Universal Render Pipeline/Terrain/Lit") ?? Shader.Find("Nature/Terrain/Standard")` (`:1665-1666`).
  **Terrain is URP/TerrainLit, not URP/Lit** — splat blending of up to 8 layers is natively available; no
  custom shader work is required.
- Layer set authored at `:701-710`; splat painted at `:712-771`; roads stamped by `PaintNaturalPaths`.
- `EnsureTerrainShaderIncluded()` `:1698-1726` pins TerrainLit into GraphicsSettings
  `m_AlwaysIncludedShaders` — **without it the terrain renders BLACK in the player** (URP shader stripping).
  Any layer-count change must keep this call intact.

**BAKE COMMAND (attended, editor must be closed):**
```
DeNelle.Editor.ExteriorTerrainBuilder.BuildExterior
```

✅ **This is NOT `VillageSceneBuilder.cs`.** The §9 serialization bottleneck does **not** apply to the hub
terrain lane, so this work can run parallel to any VillageSceneBuilder work. Village2 (§5) is the one
sub-task that touches the Village2 lane — sequence it after, or hand it to the same agent.

**Second authority — the runtime repaint (DEF-108).** `Assets/_Modules/Village/World/WorldSceneLoader.cs:94-136`
detects an all-zero alphamap at runtime and **repaints the entire splat in code**, per quadrant:
E Goldfields=layer 0, W Stoneback=1, S Mirewood=2, N Ashwood=4 (`:117-126`). Assembly `DeNelle.Village`.
**On device this is the only splat the player ever sees** — a bake-only change is invisible. Both authorities
must be updated in the same change, and the layer index contract (§6 risk 2) must stay identical between them.

---

## 3. PER-BIOME GROUND TREATMENT — VALUE / TEXTURE / LIGHT

Sourced from WO-1044 §1 (canon). **Never differentiate by hue** — the owner is red/green colourblind
(memory `owner-colorblind-delegate-visual-creative`), and hue-only variety fails the AC-1 greyscale gate.
Target values are Rec.709 luminance of the base map, measured at bake and asserted by the oracle in §7.

> **Today's ground fails this gate and it is measurable.** Current tints (`ExteriorTerrainBuilder.cs:704-709`):
> Grass `(0.28,0.52,0.22)` → **L=0.447**; Stone `(0.55,0.52,0.45)` → **L=0.521**. **ΔL = 0.074.**
> Goldfields and Stoneback are *adjacent* marches and are near-indistinguishable in greyscale right now.
> Mud L=0.274, Snow L=0.919, Dead L=0.176. **Minimum acceptable ΔL between any two march layers: 0.15.**

| March | Value target | Texture | Light | Blink source (curate → `Generated/Terrain/Layers/`) |
|---|---|---|---|---|
| **Goldfields** (E, tier 1) | **HIGH, ~0.72**; the brightest ground and the *lowest internal contrast* — a pale page | Fine, dry, small-grain; **large tileSize (14-16)** so no repeat is legible; the flattest normal map of the four | Low raking sun so every stalk throws a long soft shadow; ground reads as texture, not colour | `StylizedForest/Grass_3`, `Grass_Flowers` (+ `Sand` as the dry-edge second layer) |
| **Stoneback** (W, tier 2) | **MID, ~0.50**, but the **highest LOCAL contrast** — faceted, white cut into the crevices | Matte, dusty, hard-edged; **strongest normal map** of the four (rock faceting does all the modelling); tileSize 8-10 | Flattest **global** light — overcast, near shadowless; sky brighter than ground; snow patches are the only true whites (~0.90) | `StylizedForest/Cliff_1` + `Cliff_2`; `StylizedIce/Snow_2` for patches |
| **Mirewood** (S, tier 3) | **LOW and CRUSHED, 0.22-0.32** — the narrowest value range in the game; no highlights, no true blacks | Wet, sheened, slick — **raise `m_Smoothness` / lower roughness**, this is the one biome where the ground is specular; tileSize 5-7 | Light only in vertical shafts; **the water is the brightest thing in frame** so the eye is pulled down at the drowned town; fog eats distance past ~20 m | `StylizedForest/Mud_1`, `Mud_Roots`, `Grass_Mud` |
| **Ashwood** (N, tier 4) | **PALE ground, ~0.68**, against near-black trunks — **the highest contrast in the game, almost no mid-tones** | Dry, matte, dead, powdery; flat normal; tileSize 12-14 | Flat and shadowless — silhouette does all the work; only the corruption-fog and ward-stones *emit*, and they must read brighter than everything around them so they survive greyscale without hue | `StylizedViking/Mud_Autumn_Leaves` (value-lifted) or `StylizedForest/Sand` re-tinted ash |

### ⚠ Ashwood's ground value is INVERTED today — canon overtakes the existing code

WO-1044 §1 authors Ashwood as **"near-black trunks standing on a pale powdery ground, like ink on ash …
Greyscale test: two values only, plus two glows."** The shipped `Exterior_Dead` layer is
`(0.20,0.17,0.16)`, **L=0.176 — dark ground** (`ExteriorTerrainBuilder.cs:709`), and
`WorldSceneLoader.cs:126` paints that dark layer across the entire north quadrant.
**The current implementation is the opposite of ratified canon.** Ashwood's ground layer must be
**lifted to ~0.68** and the darkness moved into the trunks/props. Do not preserve the existing value.
This also fixes a second defect: Ashwood (0.176) vs Mirewood (0.274) is ΔL 0.098 today — two dark
quadrants that also fail to separate. Lifting Ashwood to 0.68 separates it from Mirewood by 0.40.

---

## 4. FILES TO CHANGE

Every `.cs` below is checked against its `.asmdef` (CLAUDE.md §5 — the table there is a subset, not the map).

| # | File | Assembly | Change |
|---|---|---|---|
| 1 | `Assets/Generated/Terrain/Layers/*.png` **(NEW, 16 files)** | — (asset) | Curated copies of 8 BaseColor + 8 Normal PNGs from `Assets/Blink/Art/Textures/`, re-imported at max size **1024**. Tracked, so a fresh clone builds. |
| 2 | `Assets/Generated/Terrain/*.terrainlayer` **(regenerated, 5 → 8)** | — (asset) | Written by the builder, not by hand. |
| 3 | `Assets/Editor/ExteriorTerrainBuilder.cs` | `DeNelle.Editor` | **The core change.** (a) `MakeLayer` (`:793-806`) takes a texture path pair instead of a `Color`; **`MakeSolidTexture` (`:809-840`) is retired as the diffuse source** — keep the method (it is the fallback, §6 risk 1) but call it only when a curated PNG is missing, with `Debug.LogWarning`, never an error (CLAUDE.md §4). (b) Layer table `:701-710` grows 5 → 8 with the §3 sources, per-layer `tileSize` and `normalMapTexture`. (c) Splat rules `:712-771` blend 2-3 layers per quadrant by slope + height + low-frequency Perlin (the machinery already exists — `SteepnessAt` `:778-790`, `PerlinFbm` `:632`). (d) One-line layer-manifest `Debug.Log` so a capture diff proves a re-bake ran. **Do NOT touch heightmaps — heights are the navmesh** (WO-1101 "What NOT to touch"). Keep `EnsureTerrainShaderIncluded()` `:1698-1726`. |
| 4 | `Assets/_Modules/Village/World/WorldSceneLoader.cs` | `DeNelle.Village` | Update the DEF-108 runtime repaint (`:94-136`) to the same 8-layer index contract and the same blend rules, so bake and runtime agree. Add `FlowTrace.Step("World", …)` naming which splat authority ran + per-layer coverage % (this is AC-2's proving line). **Keep `DiagTerrain` in place** — never strip (CLAUDE.md §12). |
| 5 | `Assets/Resources/Data/Canonical/biomes.json` **(NEW)** + byte-identical `Assets/StreamingAssets/Data/Canonical/biomes.json` | — (data) | WO-1101 Phase 2. `areaId → { groundLayers[], blendRules, propSet }`. **No `ambientFx` field** — "No VFX" ruling. |
| 6 | `Assets/_Modules/Core/World/BiomeCatalog.cs` **(NEW)** | `DeNelle.Core` (`Assets/_Modules/Core/DeNelle.Core.asmdef` — nearest ancestor; there is no asmdef inside `World/`) | Mirror `RealmMapCatalog.cs` **exactly**: `const string RelativePath`, `EnsureLoaded()` using `Guard.Try("Biome", …, () => CanonicalJson.Read(RelativePath), null)` then `Guard.Try(… JsonConvert.DeserializeObject …)`, `FlowTrace.Fail` + **empty catalog on failure, never throw**, `Reload()` test hook. DTOs `[Serializable] sealed class` with `[JsonProperty]`. |
| 7 | `Assets/Editor/Regression/TerrainLayerRegression.cs` **(NEW)** | `DeNelle.EditorRegression` | §7. |
| 8 | `Assets/Editor/Regression/DataRegression.cs` | `DeNelle.EditorRegression` | Two registration lines, §7. |
| 9 | `Assets/Editor/OverworldCaptureTool.cs` **(NEW)** | `DeNelle.Editor` | §8 — the compass capture entry point. **It does not exist today and AC-1 cannot be met without it.** |
| 10 | `Assets/Editor/Village2Playable.cs` | `DeNelle.Editor` | §5 — Village2 re-uses the same layer assets and blend code. Do it **second**. |

**Not touched:** `VillageSceneBuilder.cs` (bottleneck, and not the hub-terrain owner), `ArenaBiomeDressing.cs`
behaviour, `realm-map.json` structure, `StructureFactory.cs` / `VisualFactory.cs` / any town building or
placeable item (owner: *"leave all town buildings alone. No VFX."*), `CastleHubBuilder.cs` nav planes,
any `.unity` file.

---

## 5. "SIMPLE AESTHETICS" — the cheapest high-impact additions

The owner's second phrase. Ranked by impact-per-hour. **No VFX** (WO-1101 binding ruling).

**A1 — Ground detail grass. The single highest-impact item, and it is entirely unbuilt.**
`ExteriorTerrainBuilder.cs:397` calls `td.SetDetailResolution(512, 16)` and then **never sets a single
`detailPrototype`**. There is no grass clutter anywhere in the world. Adding detail prototypes is native
TerrainData work — GPU-instanced, mobile-cheap, and it is literally the "grass" the owner asked for.

**A2 — Prop scatter already exists and already works; only the biome weighting is missing.**
`PaintTrees()` `:1025-1186` (budget `TreeTargetCount = 2400`, `:244`) and `ScatterRocks()` `:1205-1275`
(140 boulders) are live. **Weight the existing prototypes per quadrant** — Goldfields sparse lone trees,
Stoneback rock clusters, Mirewood dense, Ashwood bare + debris (densities per
`docs/WORLD_BIOME_SCATTER_DIRECTION.md:52-61`). No new spawner.

**A3 — Per-march fog/light.** `WorldFeelInjector.cs:288-291` sets one global
`FogMode.ExponentialSquared`, `fogColor (0.78,0.66,0.58)`, `fogDensity 0.0012`. Drive density per quadrant
only: Mirewood dense (distance eaten past ~20 m), Goldfields near-clear, Ashwood flat and shadowless.
Density is a *value* cue, not a hue cue — it survives greyscale. Cheapest mood win in the plan.

### Prefab verification — checked against disk, which is the only truth

⚠ **`docs/polyperfect-asset-catalog.md` cannot be used as the verification source.** It does not enumerate
individual Nature prefab names, and `docs/WORLD_BIOME_SCATTER_DIRECTION.md:3-5` states outright that the
catalog "had stale/nonexistent names" and that the scatter doc was re-verified against real prefab files.
**Verify against disk. I did.**

**The builder's own KayKit prefabs — VERIFIED PRESENT, and this is a correction.**
An earlier read of this tree concluded the KayKit nature packs were missing and that the world was rendering
placeholder cubes. **That is wrong.** `Assets/Models/KayKit/KayKit Forest Nature Pack 1.0/Assets/fbx(unity)/Color1/`
exists and holds **198 FBX**; `Tree_1_A_Color1.fbx` and the `Rock_*` set resolve; the Hexagon-pack fallback
`trees_A_large.fbx` resolves under `Assets/Models/KayKit/KayKit Medieval Hexagon Pack 1.0.1/Assets/fbx(unity)/decoration/nature/`.
The builder's path constants (`ForestPackColor1` `:88-89`, `HexDecoNature` `:93-94`) are correct.
**Prefer these for scatter — KayKit is git-tracked, polyperfect is not.**
Verified ground-cover available there: `Grass_1_A..D`, `Grass_2_A..D` (plus `_Singlesided` variants — use
these for detail billboards), `Bush_1_A..G`, `Bush_2_A..F`, `Bush_3_A..C`, `Bush_4_A..F`,
`Rock_1_A..Q`, `Rock_2_A..H`, `Rock_3_A..R`, `Rock_4_A..H`, `Rock_5_A..H`, `Rock_6_A..H`,
`Tree_1..Tree_7`, `Tree_Bare_1`, `Tree_Bare_2`, plus a `Hill_Cliff*` kit.

**Polyperfect names — verified on disk (16/16 spot-check passed), but the pack is gitignored.**
`Wheat_Plant`, `Cotton`, `Sunflower`, `Lotus` → `Nature_M/Flowers_M/`. `Rock_Large`, `Rock_Sharp`,
`Rock_Pillar`, `Rock_Terrasse`, `Stone_Flat`, `Rocks_Tiny` → `Nature_M/Stones_M/`.
`Tree_Dead`, `Tree_Dead_Broken`, `Tree_Bare` → `Nature_M/Trees_M/Trees_Dead_M/`.
`Fern_Prehistoric` → `Nature_M/Prehistoric_M/`. `Stump`, `Log` → `Nature_M/Trees_M/`.
`Grass`, `Grass_Basic`, `Grass_Clumb`, `Grass_Long`, `Grass_Tall` → `Nature_M/Grass_M/`.

⚠ **Two errors in `WORLD_BIOME_SCATTER_DIRECTION.md` that will produce null prefabs — do not copy them:**
1. Its stated base `…/Nature_M/` is **one level short for every Nature name**. Real layout is
   `Nature_M/<Sub>_M/<Name>.prefab`. Concatenating base + name resolves null for all of them.
2. **`Stones_Small` does not exist.** The real prefab is `Stone_Small` (`Nature_M/Stones_M/Stone_Small.prefab`).
   Also mis-homed: `Bone_Pile_Prehistoric` is in `Tribal_M/`, and `Rubble_Stone` / `Skull_Human` /
   `Gravestone` / `Gravestone_Round` are in `Fantasy_M/` — **not** `Nature_M/`.

⚠ **That doc's §"Tints = the elemental realization" (`:63-68`) is STALE and must not be followed.** It
prescribes differentiating the biomes by colour tint over shared prefabs. WO-1044's ratified palettes
mandate **value, texture and light**, and hue-only differentiation fails the AC-1 greyscale gate outright.
Use the tint hooks only to shift *value*, never as the differentiator.

`Assets/Quaternius/` **does not exist on this machine.** Any path expecting `Floor_WoodDark` is already
broken here. Warn, never error (CLAUDE.md §4).

---

## 6. TOP RISKS

**R1 — Gitignored source packs silently break a fresh clone (the pink-floor failure class).**
`Assets/Blink/` (`.gitignore:350`) and `Assets/polyperfect/` (`.gitignore:182`) have **zero tracked files**;
`Assets/Quaternius/` is gone. A tracked `.terrainlayer` pointing at a Blink guid renders correctly here and
colourless everywhere else — and the "pink floor" lesson (CLAUDE.md §12) was exactly colourless URP floor
tiles, found by capture rather than by reading. *Mitigation:* the §1 curation step (copy 16 PNGs into tracked
`Assets/Generated/Terrain/Layers/`) is **not optional**, plus keep `MakeSolidTexture` as a warn-and-continue
fallback so a missing curated PNG degrades to today's flat tint instead of to null.

**R2 — The layer-index contract is duplicated across an editor file and a runtime file.**
`ExteriorTerrainBuilder.cs` owns `LayerCount` / `LayerGrass` / `LayerStone` / `LayerMud` / `LayerSnow` /
`LayerDead`; `WorldSceneLoader.cs:117-126` **hardcodes indices 0, 1, 2 and 4** with no shared constant.
Growing 5 → 8 in one file and not the other paints the wrong ground on device and only on device.
*Mitigation:* land both in the same change; ideally hoist the indices into `BiomeCatalog` (file #6) so there
is one authority — which is WO-1101 Phase 1 item 5's "data-driven from day one" intent.

**R3 — There is no way to screenshot the overworld today, so AC-1/AC-2 cannot be met as written.**
`DeNelle.Editor.UICaptureLaunch.RunCaptureHeadless` (`Assets/Editor/UICaptureLaunch.cs:479`, marker
`UI_CAPTURE_OK <count>`, output `Builds/ui-capture/`) shoots **19 named uGUI panels** — it never opens a
scene, never makes a scene camera, never touches Terrain. `VfxProofCapture` positions a camera
(`FrameCamera` `:1121-1145`) but stages prefabs on a synthetic plane. **No compass/overworld capture tool
exists anywhere** under `Assets/Editor`, `tools/`, or `.claude/`. And the AutoPilot fleet runs `-nographics`
— `.claude/skills/run-defenders/SKILL.md:93-94`: *"`-nographics` = NO pixels … never screenshots."*
*Mitigation:* file #9 must be built **first**, or this change ships unproven. See §8.

*Secondary, capture-affecting:* `DeNelle-URP.asset:57` sets `m_ShadowDistance: 30` — short for wide
overworld shots, and Goldfields' identity is *long raking shadows*. `WorldFeelInjector.cs:126-135` adds
Bloom 4.5 and **post-exposure +0.75 EV**, which will blow out highlights in every capture; measure layer
value from the base map, not from the screenshot.

---

## 7. REGRESSION ORACLES

Two, both in `DeNelle.EditorRegression` (`Assets/Editor/Regression/DeNelle.EditorRegression.asmdef`),
both following the contract `public static bool Run(out string reason)` — true + one-line reason on pass,
false + joined failures on fail.

**O1 — `TerrainLayerRegression` (NEW, `Assets/Editor/Regression/TerrainLayerRegression.cs`).** The oracle
that makes the colourblind gate machine-checkable rather than a matter of opinion:
1. Every `.terrainlayer` in `Assets/Generated/Terrain/` has a **non-null `diffuseTexture` AND a non-null
   `normalMapTexture`** — this alone would have caught today's state.
2. Every referenced texture resolves to a path under `Assets/Generated/Terrain/Layers/` (i.e. a **tracked**
   location), never under `Assets/Blink/` or `Assets/polyperfect/`. This is R1 turned into a test.
3. **Rec.709 luminance of each march's primary layer is within ±0.06 of its §3 target, and ΔL between any
   two march layers is ≥ 0.15.** Assert Ashwood ≥ 0.60 explicitly (the §3 inversion, so it cannot regress).
4. `LayerCount` in the builder equals the layer count the runtime repaint writes (R2 turned into a test).

**O2 — `BiomeCatalogTest`** per WO-1101 AC-4: loads `biomes.json`, asserts every `realm-map.json` `biome`
value resolves and every `ArenaBiomeDressing` key folds to a canonical entry.

**Registration pattern — copy the neighbour verbatim.** In `Assets/Editor/Regression/DataRegression.cs`
(`DeNelle.Editor.DataRegression.RunAll`), matching the biome-roads line at `:481`:
```csharp
DeNelle.Core.Diagnostics.Guard.Try("Regression", "terrain-layer suite", () => { if (!DeNelle.Editor.Regression.TerrainLayerRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[terrain-layer] " + r); });
DeNelle.Core.Diagnostics.Guard.Try("Regression", "biome-catalog suite", () => { if (!DeNelle.Editor.Regression.BiomeCatalogTest.Run(out var r)) failures.Add(r); else log.AppendLine("[biome-catalog] " + r); });
```
Match the declared namespace to the registration line — `RealmMapRegression` uses `DeNelle.Editor`,
`BiomeRoadsRegression` uses `DeNelle.Editor.Regression`; both styles exist in the folder.

**Dual-copy note:** `biomes.json` needs **no test-list edit** to be covered. `DataWebRegression.CheckDualCopyDrift`
(`Assets/Editor/Regression/DataWebRegression.cs:236`) auto-enrols every `.json` present in both roots, and
`CoreDataHubRegression` (`:45`) auto-asserts a Resources twin — so a StreamingAssets-only copy fails
immediately. Add it to `CanonicalJsonIntegrityTest.RequiredFiles` (`Assets/Data/Tests/CanonicalJsonIntegrityTest.cs:50`)
only if it should be a must-exist file.

---

## 8. SCREENSHOT VERIFICATION PLAN

For a visual change the screenshot **is** the data (memories `screenshots-are-primary-evidence-for-visual-defects`,
`headless-screenshot-verify-ui-before-build`). Compile-green proves nothing here.

**Step 0 — build the capture tool (file #9), because it does not exist (R3).**
`Assets/Editor/OverworldCaptureTool.cs`, batchmode entry `DeNelle.Editor.OverworldCaptureTool.CaptureOverworld`,
output `Builds/world-capture/`, marker `WORLD_CAPTURE_OK <count>`. Model it on
`VfxProofCapture.FrameCamera` (`:1121-1145`: `cam.transform.position = centre + dir * dist;
cam.transform.LookAt(centre)`) and its `RenderToTexture` pair. It must open
`Main_Castle_Overworld`, spawn a camera, and shoot from a fixed list of world positions.
**Run WITHOUT `-nographics`** — `UICaptureLaunch.cs:501-505` documents that `-nographics` yields blank
frames, and there is already a flat-frame guard that fails the run.

**Required shots** (all at gameplay camera height and pitch, not a top-down map view):

| Shot | Position / aim | Must show |
|---|---|---|
| S1 Goldfields | east quadrant, looking E | ≥ 2 distinct ground layers blending; pale low-contrast field; long raking shadows |
| S2 Stoneback | west quadrant, looking W | faceted mid-value rock with white in the crevices; visible normal-map relief |
| S3 Mirewood | south quadrant, looking S | crushed dark band; the wet/sheened ground reading specular; fog closing distance |
| S4 Ashwood | north quadrant, looking N | **pale ground with dark trunks** — the §3 inversion, proven |
| S5 Seams | from the hub centre, one full 360° or four 45° diagonals | quadrant transitions blend, no hard visible edge, no tiling repeat legible |
| S6 Village2 | Village2 terrain | same 2-layer-in-frame beat (AC-3) |

**GREYSCALE CHECK (the gate, not a nicety).** Desaturate S1-S4 and place them side by side. Each biome
must remain identifiable with zero colour information, matching WO-1044's own greyscale tests: Goldfields
"a near-white page with three or four charcoal silhouettes"; Stoneback "mid-grey faceted shapes with white
cut into the crevices"; Mirewood "near-uniform dark grey with bright ribbons of water"; Ashwood "two values
only, plus two glows". **If two biomes are indistinguishable in greyscale, the change is not done** — that
is the current failure (ΔL 0.074 Goldfields↔Stoneback) and re-shipping it would waste the whole pass.
Pair with O1's numeric ΔL assertion so the judgement is backed by a measurement, and **open the PNGs** —
do not report a capture count as evidence.

**AC-2 device truth.** A **player build** (not editor) screencap of the hub, plus the quoted
`[Flow:World]` line naming which splat authority ran and the per-layer coverage percentages. A bake-only
change is invisible on device (R2/DEF-108), so this shot is what proves the runtime path was handled.

**Gate order:** `COMPILE_GATE_OK` → `REGRESSION_OK <n>/<n> suites` (read the count off the marker, never
restate it) → `WORLD_CAPTURE_OK` with the PNGs actually opened → owner felt-verifies and closes (§13; a
headless run cannot judge feel, and this is a feel feature).

---

## 9. WHAT WO-1044 OVERTOOK IN WO-1101 (1101 predates the rulings)

1. **WO-1101 D1-D5 "Owner decision points" are CLOSED, not open.** D2 (differentiation language) and D3
   (hub quadrant identity) are answered by WO-1044 §1: the four marches keep the Goldfields/Stoneback/
   Mirewood/Ashwood quadrant scheme and are differentiated by value/texture/light. Do not re-ask.
2. **D4 (DEF-108 direction) is the one genuinely live decision** and it is technical, not creative. This
   plan's recommendation: **promote the runtime repaint to the designed system** and have the bake write the
   same result, with `BiomeCatalog` as the single authority both read (R2). Flag to the owner; do not stall on it.
3. **Ashwood's ground value is inverted in the current code** (§3) — WO-1044's "pale powdery ground, ink on
   ash" overrides the shipped dark `Exterior_Dead` layer and the WO-1101 §0.2 description of the runtime
   repaint painting "dead-ash" across the north. Implement canon, not the existing behaviour.
4. **The tunnel display string is "The Rootways"** (R1). `BiomeRoads.cs:98`
   `TunnelDisplayName = "The Hollow Roads"` is the only authored dungeon display-name constant in the tree —
   a one-line change. **The id `dg_hollow_roads` and `ArmRoomIdFor`'s `arm_ashwood` / `arm_goldfields` /
   `arm_mirewood` / `arm_stoneback` (`BiomeRoads.cs:164-178`) are hard contracts — do not rename.**
   Out of scope for this WO; noted so it is not lost.
5. **WO-1101 §0.4's "do not mint a third vocabulary" still stands**, and is now easier: `ZoneManager.cs:45-53`
   already holds the canonical four (`Goldfields`/E/1, `Stoneback`/W/2, `Mirewood`/S/3, `Ashwood`/N/4).
   `biomes.json` maps onto **that**, and folds `realm-map.json`'s `biome` values and `ArenaBiomeDressing`'s
   six arena keys as aliases.
6. **Confirmed at source, per the prior audit:** no `biomes.json` in either canonical location, no
   `BiomeCatalog`, no `BiomeCatalogTest` anywhere under `Assets`; `ArenaBiomeDressing` is referenced only by
   `BattleArena.cs` and a comment in `ArenaPrefabAuditRegression.cs:8`. All three are genuinely new work.

---

## 10. SUGGESTED LANE SPLIT

- **1101-A (hub ground)** — files 1-4 + 9. The critical path. Not a `VillageSceneBuilder` lane, so it runs
  parallel to town work. Bake: `DeNelle.Editor.ExteriorTerrainBuilder.BuildExterior`.
- **1101-B (Village2)** — file 10. Depends on A's curated layer assets; disjoint files. Sequence after A.
- **1101-C (catalog + oracles)** — files 5-8. Disjoint from A/B, parallel-safe.
- **Phase 3 (realm-map regions)** — parked, pinned on WO-827 travel.

**Attended bakes only. Never bake with the Unity editor open** (CLAUDE.md §3).
