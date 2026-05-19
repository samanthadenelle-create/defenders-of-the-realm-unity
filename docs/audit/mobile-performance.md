# Mobile-Performance & Readiness Audit — Defenders of the Realm (v2 Unity Port)

**Auditor:** Unity 6 mobile-performance engineering pass (read-only static analysis)
**Date:** 2026-05-19
**Target hardware:** Solana Seeker (Android phone, 120 Hz display)
**Scope:** Mobile-first project configuration, static performance risk against the
Week-8 acceptance budget (60 FPS held, frame-time spikes ≤ 33 ms, memory ≤ 400 MB,
runs on Seeker emulator, build outputs an Android `.apk`), and a profiling plan to
verify those gates once a playable build exists.
**Method:** Read-only inspection of `ProjectSettings/` (ProjectSettings.asset,
QualitySettings.asset, GraphicsSettings.asset), `Assets/Settings/DeNelle-URP.asset`
+ `DeNelle-UniversalRenderer.asset`, `Packages/manifest.json`, the Editor scene
builders (`VillageSceneBuilder.cs`, `DungeonSceneBuilder.cs`,
`ExteriorTerrainBuilder.cs`, `AssetImportPostprocessor.cs`), build logs, and the
prior audit docs. No project file was modified.

---

## Executive verdict

**Mobile-readiness: NOT READY — the project is configured desktop-first, not
Seeker-first, and will not meet the Week-8 perf gates without a configuration pass
and a batching strategy.**

The good news: the package set is correct and mobile-appropriate (URP 17.4,
Cinemachine 3, Input System, Addressables, Adaptive Performance module present),
the URP renderer is **Forward** (right for mobile), the **SRP Batcher is ON**, the
KayKit `AssetImportPostprocessor` correctly applies **ASTC 6×6** + Read/Write-off +
Medium mesh compression for Android/iOS, Android architecture is correctly locked
to **ARM64-only**, and the exterior Terrain uses `drawInstanced`. The team clearly
knows the mobile patterns — they are just not all switched on.

The bad news: the three settings the spec explicitly *locks* are wrong or default
— **color space is Gamma (spec mandates Linear), the scripting backend is unset
(defaults to Mono — spec mandates IL2CPP for release), and the three named quality
tiers (Seeker_Low / Seeker_High / Desktop) do not exist** (the project still
carries Unity's six stock tiers). On top of that, the village scene instantiates
**~2,930 individual GameObjects (2,607 of them ground tiles)** with **only
`NavigationStatic` flagged and no `BatchingStatic`** — so the single largest mobile
cost in the project, draw-call submission, has no batching strategy at all. HDR is
left on. No code reads `SystemInfo.deviceModel` to auto-select a Seeker tier (spec
Part 2), and there is no `Application.targetFrameRate` discipline.

### P0 issue count: **6**

| # | P0 finding | Section |
|---|-----------|---------|
| P0-1 | Color space is **Gamma**, spec mandates **Linear** (URP requirement) | §1.1 |
| P0-2 | Scripting backend unset → **Mono** for release; spec mandates **IL2CPP + ARM64** | §1.2 |
| P0-3 | The three named quality tiers (Seeker_Low / Seeker_High / Desktop) **do not exist** | §1.4 |
| P0-4 | **2,607 ground tiles + ~2,930 instances, no `BatchingStatic`, no GPU instancing** — draw-call blowout | §2.1 |
| P0-5 | No playable build / integration pass exists — the Week-8 perf gates **cannot be measured at all** | §2.0, §3 |
| P0-6 | No Seeker auto-detect, no `targetFrameRate`, no frame-pacing discipline — 60/90 FPS target is unenforced | §1.5 |

### Top performance risk

**Draw-call submission from the un-batched village scene (P0-4).** ~2,930 separate
GameObjects, each its own `Renderer`, marked `NavigationStatic` only. The SRP
Batcher reduces *per-draw CPU setup* but does **not** reduce draw-call *count*;
2,600+ ground tiles will issue 2,600+ draws every frame. On a Seeker-class mobile
GPU the realistic ceiling for a smooth 60 FPS scene is roughly 150-300 draw calls.
Without static batching, GPU instancing, or — best — a baked combined ground mesh,
the village scene alone blows the entire CPU render-thread budget before a single
enemy, ability VFX, or HUD element is drawn. This is the one risk most likely to
sink the Week-8 "60 FPS held" gate.

---

# 1. MOBILE-FIRST SETUP

Is the project configured Android/Seeker-first? **Partially.** Platform target,
ARM64, texture compression and the render pipeline choice are correct; color
space, scripting backend, the quality tiers, HDR and frame-pacing are not.

## 1.1 Color space — P0

**Finding (P0-1):** `ProjectSettings.asset` → `m_ActiveColorSpace: 0` = **Gamma**.
`GraphicsSettings.asset` → `m_LightsUseLinearIntensity: 0` corroborates it. Spec
Part 2 "Project settings to lock" explicitly states **"Color space: Linear (URP
requirement)."** URP is designed for linear lighting; running it in Gamma produces
incorrect lighting/emissive math (the Heart's violet emissive, the gate shimmer,
the withering vignette will all read wrong) and is a non-standard, unsupported
configuration.
**Recommendation:** Set Player Settings → Other Settings → **Color Space = Linear**.
Note this triggers a full asset re-import and every authored material/light must be
re-checked — do it *now*, before more content is built, because the cost only
grows. Verify Linear is the active space on the Android build target specifically.

## 1.2 Scripting backend, IL2CPP, ARM64 — P0

**Finding (P0-2):** `ProjectSettings.asset` → `scriptingBackend: {}` is **empty** —
no per-platform override is set, so Android falls back to the editor default
(**Mono**). Spec Part 2: *"Scripting backend: IL2CPP (release builds). Mono is fine
for editor + development builds."* Mono is acceptable for fast iteration but
**cannot ship** — the Solana Mobile dApp Store and Google Play both require IL2CPP
+ a 64-bit binary for release, and IL2CPP gives materially better mobile runtime
performance and a smaller, AOT-compiled binary. `il2cppCompilerConfiguration` and
`il2cppCodeGeneration` are likewise unset.
**Correct setting:** Android → Scripting Backend = **IL2CPP**, IL2CPP Code
Generation = **Faster (smaller) builds** for the foundation milestone, C++ Compiler
Configuration = **Release** for the acceptance build.
**Related — verified GOOD:** `AndroidTargetArchitectures: 2` = **ARM64 only**
(`AndroidAllowedArchitectures: -1`). This is correct — ARM64-only is the right call
for Seeker (no ARMv7 bloat). `apiCompatibilityLevel: 6` = **.NET Standard 2.1** —
correct, matches the Solana SDK minimum. `stripEngineCode: 1` — good. Note: once
IL2CPP is on, set a **Managed Stripping Level** (Low or Medium) — `managedStrippingLevel`
is currently unset.
**Related — P1:** `AndroidMinSdkVersion: 25` (Android 7.1). Spec Part 2 wants the
current Solana dApp Store minimum, "as of 2026-05 = Android 13 / API 33." Raise
**Min SDK to 33** and confirm Target SDK is current (`AndroidTargetSdkVersion: 0` =
"use installed highest" — pin it explicitly for a reproducible build).
**Related — P2:** `applicationIdentifier: {}` is empty — the spec's package name
`studios.denelle.defendersoftherealm` is not set; `companyName: DefaultCompany`,
`productName: defenders-unity`. Cosmetic for perf but a Week-8 deliverable.

## 1.3 Orientation — P1

**Finding (P1):** `defaultScreenOrientation: 4` = **AutoRotation**, and all four
orientations are enabled (`allowedAutorotateToPortrait/...UpsideDown/...LandscapeRight/
...LandscapeLeft` all `1`; `androidResizeableActivity: 1`). The spec does not name
a locked orientation, but a tower-defense village + dungeon-crawl with a Cinemachine
free-look rig and an isometric dungeon cam is a **landscape** game. Leaving all four
orientations on means the camera/HUD must handle portrait *and* a resize on every
rotation — extra layout cost and a real UX hazard on a phone.
**Recommendation:** Confirm the intended orientation with the owner; if landscape
(the likely answer), set **Default Orientation = Landscape Left**, disable the
portrait options, and set `androidResizeableActivity` off unless multi-window is a
requirement. Locking orientation also removes a class of HUD/camera bugs from the
acceptance run.

## 1.4 Quality tiers — P0

**Finding (P0-3):** `QualitySettings.asset` still carries Unity's **six stock
tiers** — Very Low, Low, Medium, High, Very High, Ultra. The spec Part 2 mandates
**exactly three named tiers**: `Seeker_Low`, `Seeker_High`, `Desktop`, each with
specific shadow/MSAA/render-scale/target-FPS values. None of the three exists. The
Android per-platform default is index **2 ("Medium")** — a tier with
`vSyncCount: 1`, real-time shadows on, that does not correspond to anything in the
spec. iPhone/tvOS also default to index 2.
**Correct setting — rebuild QualitySettings to the spec's three tiers:**

| Tier | Shadows | MSAA | Render scale | Target FPS | Notes |
|------|---------|------|--------------|-----------|-------|
| **Seeker_Low** | Soft only, no real-time shadows on dynamic objects | Off | 0.85 | 30 | Seeker fallback; anisotropic off |
| **Seeker_High** | Soft shadows on | 2× | 1.0 | 60 (stretch 90) | Default Seeker target |
| **Desktop** | Full shadows | 4× | 1.0 | 60 | Vercel parity / desktop EXE |

Each tier needs the matching URP asset variant (MSAA and render scale live on the
URP asset, §1.6, not on the QualitySettings tier in URP) — so this is a paired
change: three QualitySettings tiers, each pointing at its own URP asset, with
shadow distance/cascades tuned per tier. Set Android default = `Seeker_High`.

## 1.5 Seeker auto-detect + frame pacing — P0

**Finding (P0-6):** Spec Part 2: *"Seeker auto-detects `SystemInfo.deviceModel` and
defaults to `Seeker_High`."* No code does this — a grep of the modules finds no
`SystemInfo.deviceModel` reference and no quality-tier selection logic (corroborated
by `missing-components.md` §2.1: there is no settings system at all). Equally,
nothing sets `Application.targetFrameRate`. The acceptance gate is "60 FPS held …
stretch to 90 FPS on Seeker's 120 Hz display" — but with `vSyncCount` left at the
stock per-tier value and no explicit `targetFrameRate`, the build will run uncapped
or vsync-locked to whatever the display reports (120 Hz), which both wastes battery
and makes the 60-FPS gate unmeasurable.
**Recommendation:** Add a Core bootstrap (`[RuntimeInitializeOnLoadMethod]` or the
missing settings system) that (a) reads `SystemInfo.deviceModel`, picks
`Seeker_High` on a Seeker and `Seeker_Low` on weaker hardware, and (b) sets
`Application.targetFrameRate` to the tier's target (30/60) with `QualitySettings.vSyncCount = 0`
so `targetFrameRate` is authoritative. The **Adaptive Performance** module is
already in the manifest and `m_UseAdaptivePerformance: 1` on the URP asset — wire
the Adaptive Performance provider so the device can thermally down-clock the tier
under sustained load (this is the right tool for the Seeker's thermal envelope).

## 1.6 URP settings tuned for mobile

`DeNelle-URP.asset` + `DeNelle-UniversalRenderer.asset`, audited line by line:

| Setting | Current | Mobile-correct? | Recommendation |
|---------|---------|-----------------|----------------|
| Renderer type | **Forward** (`m_RenderingMode: 0`) | YES | Keep. Forward is right for mobile; Deferred is not. |
| SRP Batcher | **ON** (`m_UseSRPBatcher: 1`) | YES | Keep — cuts per-draw CPU setup. (Does NOT cut draw count — see §2.1.) |
| **HDR** | **ON** (`m_SupportsHDR: 1`) | **NO** | **P1** — disable HDR for the Seeker tiers. A cozy low-poly KayKit game does not need an HDR buffer; on mobile it costs bandwidth/memory and forces a wider color buffer. Keep HDR on the Desktop tier only if bloom needs it. |
| MSAA | **Off** (`m_MSAA: 1`) | Partly | **P2** — spec wants MSAA **2×** on `Seeker_High`. Forward + MSAA is cheap on mobile (tile-based GPUs do it nearly free) and low-poly art aliases badly without it. Set 2× on Seeker_High, off on Seeker_Low, 4× on Desktop. |
| Render scale | **1.0** (`m_RenderScale: 1`) | Partly | Correct for Seeker_High/Desktop. Seeker_Low tier needs its own URP asset at **0.85**. |
| Main-light shadow res | **2048** | **NO** | **P1** — 2048 is a desktop shadow atlas. Drop to **1024** for Seeker_High, **512** for Seeker_Low. |
| Soft shadows | **Off** (`m_SoftShadowsSupported: 0`) | Partly | **P2** — spec wants soft shadows ON for Seeker_High/Desktop. Enable per-tier; soft-shadow quality is set to 2 (High) — drop to Low/Medium on mobile. |
| Shadow distance | **50** | **NO** | **P1** — 50 m of shadow coverage over a ~300 m terrain is wasteful. Cut to ~**25-30 m** on Seeker tiers; the camera framing rarely needs more. Cascade count is 1 — fine for mobile. |
| Additional lights | **Per-Pixel** (`m_AdditionalLightsRenderingMode: 1`), 4 per object | Risky | **P1** — per-pixel additional lights are expensive on mobile, and the dungeon's lantern PointLight + checkpoint/oil-stone lights stack up. Set additional lights to **Per-Vertex** or **Disabled** for Seeker_Low; keep Per-Pixel only on Seeker_High and budget the dungeon light count (§2.4). Additional-light shadows are already off (`m_AdditionalLightShadowsSupported: 0`) — good. |
| Depth/Opaque texture | Both **off** (`m_RequireDepthTexture: 0`, `m_RequireOpaqueTexture: 0`) | YES | Keep off unless a shader needs them — each is a full-screen pass on mobile. |
| Intermediate texture | **Always** (`m_IntermediateTextureMode: 1`) | **NO** | **P1** — "Always" forces an off-screen render target + a blit to the backbuffer every frame, defeating the bandwidth win of direct-to-backbuffer rendering on a tile GPU. Set to **Auto** so URP only allocates the intermediate target when a renderer feature actually needs it. With zero renderer features (`m_RendererFeatures: []`) this is currently pure waste. |
| Renderer features | **None** | YES (for now) | No custom renderer features = no extra full-screen passes. Good. When the withering vignette / post FX land, budget each as a mobile pass and prefer the Volume post-processing stack over bespoke features. |
| Post-processing | No Volume profile on the URP asset (`m_VolumeProfile: {fileID: 0}`); `Assets/DefaultVolumeProfile.asset` exists | Partly | **P2** — keep post-processing minimal on mobile (no SSAO, no motion blur, no DoF; bloom and color-grading only, and bloom at low quality). Verify the default volume profile is mobile-light before the acceptance build. |
| Color grading | **LDR** (`m_ColorGradingMode: 0`), LUT 32 | YES | Keep LDR + 32 LUT — the mobile-correct choice. |
| GPU Resident Drawer | **Off** (`m_GPUResidentDrawerMode: 0`) | Acceptable | Could help the 2,600-tile problem (§2.1) but GPU Resident Drawer/BRG support on mobile/GLES is limited — prefer static batching or a combined mesh instead. |

## 1.7 Texture compression (ASTC) — verified GOOD

`AssetImportPostprocessor.cs` correctly applies, for every asset under
`Assets/Models/KayKit/`, a per-platform Android + iOS override of **ASTC 6×6**,
`Compressed`, quality 50, Max Size **1024 for atlases / 256 for small props**,
Read/Write **off**, Medium mesh compression, `generateSecondaryUV` for static
meshes, mipmaps on for 3D textures, sRGB on for albedo / off for normal+mask. This
matches spec Part 2 + Part 7 exactly. **No action — this is a model of how the rest
of the config should look.**

**Two caveats (P2):**
- The postprocessor's scope guard is `Assets/Models/KayKit/` **only**. Brand art
  (`heart-wing.jpg`), portraits, intro images, UI sprites and any non-KayKit
  texture get the **project default** compression, not ASTC. Set the project-wide
  **Default Texture Compression Format = ASTC** for Android (Player Settings) so
  nothing slips through uncompressed, and confirm UI sprites have mipmaps **off**
  (spec Part 2).
- ASTC 6×6 quality 50 ("Normal") is a sensible default; if memory is tight at the
  400 MB gate, ASTC 8×8 on ground/prop atlases saves more with little visible loss
  on low-poly art.

## 1.8 Other Player Settings notes

- `gpuSkinning: 0` — **P2**, enable **GPU Skinning** for the animated KayKit
  characters/enemies/pets; it moves skinning off the CPU, which matters in a wave
  with many skinned enemies on screen.
- `m_MobileRenderingPath: 1` (Forward) — correct.
- `mobileMTRendering` on for Android/iPhone — good (multithreaded rendering).
- `androidUseSwappy: 1` — good, the Android frame-pacing library is on.
- `m_ShowUnitySplashScreen: 1` — Unity splash still shows; cosmetic, owner call.

---

# 2. PERFORMANCE RISKS

Static analysis against the 60 FPS / 400 MB budget. Ranked, biggest first.

## 2.0 Context: nothing is measurable yet

Per `architecture-review.md` (ARC-001) and `missing-components.md` (P0-13), the
Weeks 4-7 gameplay systems compile but are **not scene-integrated** — no NavMesh-fed
wave loop has ever run end-to-end, no breach→ATB round-trip, no HUD on screen.
**(P0-5)** This means every number below is a *static estimate*; the Week-8 perf
gates cannot be confirmed or denied until the integration pass produces a playable
build. The integration pass is therefore on the critical path for the perf audit
too, not just the gameplay audit.

## RISK 1 (P0) — Draw-call blowout: ~2,930 un-batched village instances

**`VillageSceneBuilder` build log:** *"2607 ground tiles, 42 wall sections/corners,
4 cardinal gates, 187 plaza/road tiles, 5 gameplay buildings, 14 dressing
buildings, 69 props/fences"* ≈ **2,928 GameObjects**, each a separate KayKit FBX
instance with its own `MeshRenderer`.

**Why this is the headline mobile risk:**
- `BakeVillageNavMesh` marks Ground/Roads/Approaches/Walls/Gates/Buildings with
  **`StaticEditorFlags.NavigationStatic` only** (`VillageSceneBuilder.cs` line
  ~2376). It does **not** add `BatchingStatic` (or `ContributeGI`, `OccludeeStatic`,
  etc.). So **static batching is off** for all 2,600+ ground tiles.
- The SRP Batcher (`m_UseSRPBatcher: 1`) is on, but the SRP Batcher reduces *CPU
  cost per draw call* — it does **not** merge draws. 2,607 ground tiles still issue
  ~2,607 draw calls per frame.
- A Seeker-class mobile GPU sustains roughly **150-300 draw calls** for a
  comfortable 60 FPS. ~2,900 draws is an order of magnitude over budget — the CPU
  render thread alone will miss frame after frame, before any enemy/VFX/HUD cost.
- No GPU instancing path: the KayKit materials are URP/Lit (instancing-capable),
  but instancing only kicks in for identical material+mesh draws *not already*
  static-batched, and `MaterialPropertyBlock`/`ApplyColor` per-tile recoloring (the
  builder tints tiles via `ApplyColor`) can break instancing batches.

**Recommendation (in order of preference):**
1. **Best — bake the ground into a combined mesh.** 2,607 flat hex tiles is a
   single static floor; combine them at build time into a handful of large meshes
   (`StaticBatchingUtility.Combine` or a mesh-merge editor pass) split into ~16-tile
   chunks for culling. This collapses 2,607 draws to a few dozen. Do the same for
   plaza/road tiles.
2. **Mark `BatchingStatic`.** At minimum, OR in `StaticEditorFlags.BatchingStatic`
   alongside `NavigationStatic` in `BakeVillageNavMesh` so Unity static-batches
   identical tiles. This trades draw count for memory (static batching duplicates
   vertex data) — acceptable for flat tiles, watch the 400 MB gate.
3. **GPU instancing for repeated props.** For props/fences/trees that share
   mesh+material, enable **GPU Instancing** on the material and avoid per-instance
   `ApplyColor` (use an instanced property or a small set of pre-tinted materials).
4. **Cull aggressively.** The village is static — bake **Occlusion Culling**, and
   confirm the camera far plane / LOD bias are tight. Consider an LOD/impostor for
   distant tiles.

This single fix is the difference between meeting and missing the "60 FPS held"
gate. It should be the first perf work item after the integration pass.

## RISK 2 (P0/P1) — Memory: ~1.5 GB of imported KayKit models vs the 400 MB gate

The spec and import notes describe ~1.5 GB of imported KayKit packs (Medieval,
Dungeon Remastered's 211 GLTFs, plus Furniture Bits / Space Base Bits seen in the
village build log — *neither of which the village or dungeon needs*).

**Why this is a risk, and why it is partly already mitigated:**
- 1.5 GB on disk is **not** 1.5 GB in RAM — only *loaded* assets count toward the
  400 MB runtime gate. Addressables (in the manifest, configured per spec Part 2)
  is the correct tool: load only the meshes a scene uses, unload on scene exit.
- **But:** the village build log shows the importer touching
  `KayKit Furniture Bits` and `KayKit Space Base Bits` — packs with **no role in
  this game**. If those are in `Assets/` they bloat the Library, slow every import,
  and risk being pulled into a build. (Project memory note "commit: exclude
  unneeded assets" already flags keeping imported packs lean.)
- The Dungeon Remastered pack is 211 GLTFs; spec Part 7 says import **only the
  Cottage meshes** into the `dungeons-healers-cottage` Addressables group. Confirm
  the other ~190 meshes are not in a build-included Addressables group.
- ASTC 6×6 + Read/Write-off (§1.7) already keeps texture/mesh memory lean — good.

**Recommendation (P1):** (a) Delete or `.gitignore`-exclude the Space Base / Furniture
Bits packs and anything else not used by Village or the Healer's Cottage. (b) Audit
the Addressables groups: every group flagged "include in build" must contain only
assets a shipped scene loads. (c) On the acceptance build, watch the Profiler Memory
module — texture + mesh + animation memory is the bulk of a low-poly game's
footprint; 400 MB is comfortable *if* unused packs are excluded and Addressables
unloads dungeon assets when returning to the village.

## RISK 3 (P1) — Unity Terrain cost (exterior wilderness)

`ExteriorTerrainBuilder` creates a single **300×300 m Unity Terrain**: heightmap
res **513**, splatmap res **512**, detail res **512/16**, **320 tree instances**
via the Terrain tree system, `heightmapPixelError: 4`, `drawInstanced: true`,
`treeDistance: 280`, 5 splat layers.

**Assessment — mostly reasonable, with caveats:**
- `drawInstanced = true` is the **right call** — GPU-instanced terrain detail/trees.
- Heightmap 513 and splatmap 512 are modest — fine for mobile.
- **P1 — 5 splat layers.** URP Terrain blends splat layers in the terrain shader;
  on mobile, **4+ layers spill into a second texture-sampling pass**. Keep the
  Seeker tiers to **≤ 4 splat layers** (the 5th costs a whole extra pass), or use
  the URP terrain-lit shader's mobile path.
- **P1 — 320 tree instances + 5,000 m `terrainTreeDistance`** (stock QualitySettings
  value, unmodified). 320 KayKit tree meshes drawn out to 5 km is a lot of geometry;
  the per-tier `terrainTreeDistance` should be cut hard (e.g. 80-120 m) on Seeker
  tiers, and the trees need a billboard LOD. `treeMaximumFullLODCount` is set to 320
  — i.e. *all* trees render at full mesh LOD with no billboard fallback. Lower it.
- **P1 — `heightmapPixelError: 4`** is fine; the stock `terrainPixelError: 1` in
  QualitySettings is tighter — confirm the per-tier terrain pixel error is relaxed
  on Seeker_Low (higher pixel error = fewer terrain triangles).
- **P2** — `missing-components.md` §2.9 records the exterior as visually broken
  (black/unlit terrain, no skybox). A broken-but-cheap terrain is not a perf risk;
  budget it correctly when it is fixed.
- Terrain physics: a 300×300 terrain collider is cheap (heightfield) — not a concern.

**Recommendation:** The Terrain itself is acceptably configured. The risk is the
**tree layer** — cut `terrainTreeDistance` and `treeMaximumFullLODCount` per tier,
add a billboard LOD, and trim to ≤ 4 splat layers on Seeker tiers.

## RISK 4 (P1) — Per-frame GameObject allocation (GC spikes vs the ≤ 33 ms gate)

Carried from `architecture-review.md` CODE-005: `HeroAbilities.SpawnVfx` creates a
fresh `GameObject` + `ParticleSystem` **per ability cast** and `Destroy`s it;
`WaveManager.BuildPlaceholderEnemy` and `PetDeployer.SpawnPet` `CreatePrimitive`
**per spawn**. None is pooled. Per-cast/per-spawn instantiation + `Destroy` =
managed allocations + collider/transform churn → **GC spikes**, which directly
threaten the **frame-time-spike ≤ 33 ms** acceptance gate (a GC collection mid-wave
is exactly a >33 ms hitch).

The team already does the non-alloc pattern correctly for sweep buffers
(`OverlapSphereNonAlloc`, pre-allocated `_overlap` arrays) — it just is not applied
to spawned objects.

**Recommendation (P1):** Pool ability VFX (a small ring buffer of reusable
`ParticleSystem`s) and pool enemies/pets once the KayKit prefabs replace the
placeholder primitives. Verify with the Profiler's GC Alloc column during a wave
(target: near-zero per-frame managed allocation in steady state).

## RISK 5 (P1) — NavMesh & enemy AI cost

`BakeVillageNavMesh` uses the legacy `UnityEditor.AI.NavMeshBuilder` synchronous
bake (the manifest carries `com.unity.modules.ai`, not the high-level
`com.unity.ai.navigation` package). NavMesh **pathfinding** itself is cheap; the
risks are:
- **P1** — every enemy is a `NavMeshAgent`. A wave of agents repathing every frame
  (or on every obstacle change — gates collapsing, buildings dying) costs CPU.
  Budget the wave size, throttle repath frequency, and avoid full `CalculatePath`
  per agent per frame.
- **P2** — NavMesh agents doing local avoidance against each other in a tight breach
  funnel is O(n²)-ish; keep wave counts modest for the foundation (Wave 1 is 8
  enemies — fine).
- The NavMesh data is baked into the scene, not loaded at runtime — no memory or
  load-time concern.

**Recommendation:** Not a foundation blocker at 8 enemies/wave. Profile the AI/agent
cost in the wave once integrated; if it shows, throttle repathing and cap concurrent
agents.

## RISK 6 (P1) — Overdraw & transparency

Low-poly opaque KayKit art has little overdraw *by itself*, but watch:
- **P1** — the **force-field gate shader**, the **Heart crystal emissive**, the
  **withering vignette**, ability VFX (Frost Nova, meteor), lantern glow — all
  transparent/additive. Transparent overdraw is the classic mobile-GPU killer on
  tile-based renderers. Keep transparent particle counts low, particle textures
  small, and avoid large full-screen transparent quads.
- **P2** — the ground is 2,607 tiles; if any tile material is accidentally on the
  transparent queue, overdraw multiplies. Confirm ground/road materials are
  **Opaque** queue.
- UI Toolkit + UGUI world-space canvases: world-space canvases re-batch and can
  overdraw; keep nameplate/speech-bubble canvases small and few.

**Recommendation:** Use the Profiler / Frame Debugger overdraw view on the
acceptance build; budget transparent VFX explicitly.

## RISK 7 (P2) — URP renderer config residue

`m_IntermediateTextureMode: 1` (Always) — covered in §1.6, forces an extra blit
every frame for no benefit (zero renderer features). Set to **Auto**. `HDR ON`
(§1.6) — extra buffer bandwidth/memory. Both are quick wins.

### Risk ranking summary

| Rank | Risk | Severity | Gate threatened |
|------|------|----------|-----------------|
| 1 | Draw-call blowout — 2,930 un-batched village instances | **P0** | 60 FPS |
| 2 | Memory — ~1.5 GB imported models, unused packs in project | **P0/P1** | 400 MB |
| 3 | Unity Terrain — tree distance/LOD count, 5 splat layers | P1 | 60 FPS |
| 4 | Per-frame GameObject alloc — VFX/enemy GC spikes | P1 | ≤ 33 ms spike |
| 5 | NavMesh / enemy agent cost | P1 | 60 FPS (waves) |
| 6 | Overdraw — transparent VFX / shaders | P1 | 60 FPS |
| 7 | URP residue — Intermediate texture "Always", HDR on | P2 | 60 FPS / 400 MB |

---

# 3. PROFILING PLAN — verifying the Week-8 perf gates

**Precondition:** This plan can only run once the integration pass (ARC-001 / P0-13)
produces a playable build — the village wave loop and dungeon walk must actually
run. Until then, profiling has nothing to measure (P0-5).

## 3.1 What to measure (the four Week-8 gates)

| Gate | Metric | Tool | Pass criterion |
|------|--------|------|----------------|
| 60 FPS held | CPU & GPU frame time | Profiler (CPU + Rendering modules), on-device | ≤ 16.7 ms main thread + render thread, sustained, on Seeker_High |
| Frame-spike ≤ 33 ms | Worst-frame time over the run | Profiler frame chart; record the whole acceptance run | No frame > 33 ms during the 5-min playthrough |
| Memory ≤ 400 MB | Total + texture/mesh/managed | Memory Profiler package (snapshot) | Total reserved ≤ 400 MB during village wave AND dungeon |
| Runs on Seeker emulator → `.apk` | Build success + launch | Android build, install on emulator/device | APK builds (IL2CPP/ARM64), launches, plays 5 min, no crash |

## 3.2 On-device workflow (the only workflow that counts)

The editor Game-view FPS is **not** representative — editor overhead, desktop CPU,
no thermal throttling. Profile on a **real Seeker** (or the Seeker emulator if no
device, accepting that an emulator hides GPU/thermal truth).

1. **Build:** Development Build + Autoconnect Profiler + Script Debugging, IL2CPP,
   ARM64, `Seeker_High` quality. Install the `.apk`.
2. **Connect:** Unity Profiler over USB (adb) or Wi-Fi to the running build. Confirm
   it is the **device** session, not the editor.
3. **Capture the acceptance playthrough end-to-end:** studio bumper → title →
   village → place a tower → Wave 1 → fight/breach → ATB → return → walk to dungeon
   → Healer's Cottage → Bryn → lore-stone → encounter → return. Record the full
   Profiler timeline (use `Profiler.logFile` / deep-profile selectively — deep
   profile distorts timings, so use it only to chase a specific spike).
4. **Inspect the frame graph** for every frame > 16.7 ms and especially > 33 ms.
5. **Memory snapshots** with the Memory Profiler package at three points: idle
   village, mid-wave (peak enemy/VFX count), inside the dungeon. Compare against
   400 MB; diff snapshots to catch leaks across the breach→ATB→return round-trip
   (Addressables should *release* dungeon assets on village re-entry).

## 3.3 What to look for, by Profiler module

- **CPU Usage / Timeline:** Is the spike on the **main thread** (gameplay, GC,
  `Instantiate`/`Destroy` — see Risk 4) or the **render thread** (draw-call
  submission — Risk 1)? `Gfx.WaitForPresent` dominating = GPU-bound; a long
  `Camera.Render` on the render thread = draw-call-bound (the expected village
  symptom). Watch the **GC Alloc** column — any per-frame KB is a future spike.
- **Rendering module / Frame Debugger:** **Batches / SetPass calls / Triangles**.
  This is where Risk 1 shows — expect a 4-figure batch count in the village before
  the §2.1 fix. Use the **Frame Debugger** to confirm whether ground tiles batch
  after marking `BatchingStatic` / combining meshes. Use the **overdraw view** for
  Risk 6.
- **Memory Profiler (package):** Texture, Mesh, Animation, and managed heap are the
  big buckets. Confirm unused KayKit packs (Space Base / Furniture Bits) are **not**
  resident. Confirm dungeon assets unload on return to village.
- **Adaptive Performance:** with the provider wired (§1.5), watch thermal state and
  whether the tier auto-throttles under sustained wave load — a Seeker that thermal-
  throttles after 5 minutes still has to hold the gate.

## 3.4 Suggested gating sequence

1. **Build gate first** — get an IL2CPP/ARM64 `.apk` that installs and launches on
   the emulator. Until this exists nothing else is testable (this is also a Week-8
   deliverable).
2. **Static fixes before measuring** — apply §1 (Linear, IL2CPP, the three tiers,
   HDR off, Intermediate=Auto, shadow res) and the §2.1 batching fix *before* the
   first profiling run, so the baseline reflects an honest mobile config.
3. **Village wave profile** — the hardest scene (Risk 1 + 4 + 5 + 6 converge here).
   Iterate batching until batch count is in the low hundreds and frame time ≤ 16.7 ms.
4. **Dungeon walk profile** — lighter geometry (~580 instances) but watch additional
   lights (lantern + checkpoints, Risk in §1.6) and the dungeon Addressables load.
5. **Memory pass** — three snapshots, confirm ≤ 400 MB and no round-trip leak.
6. **Sustained run** — the full 5-minute acceptance playthrough recorded once,
   inspect for any >33 ms frame and for thermal throttling.

## 3.5 Add an in-build perf overlay

There is no settings/HUD perf surface today. For the acceptance run, add a tiny
dev-only overlay (a `[Conditional]` debug HUD) showing FPS / frame-time / draw
calls / total memory — so a regression is visible during play without re-attaching
the Profiler each time. Strip it from the release `.apk`.

---

## Closing summary

The project has a **correct mobile skeleton** — Forward URP, SRP Batcher, ASTC,
ARM64, Addressables, Adaptive Performance — but is **configured desktop-first** in
the three places the spec explicitly locks (Linear color space, IL2CPP backend, the
three named Seeker quality tiers), leaves HDR and the intermediate-texture blit on,
and ships the village scene with **~2,930 un-batched instances** that have no
draw-call strategy. None of the Week-8 perf gates can be *measured* yet because no
integrated playable build exists. Fix the §1 configuration and the §2.1 batching
strategy first; then the §3 on-device profiling plan can honestly verify the
60 FPS / ≤ 33 ms / 400 MB / `.apk` acceptance gates.

_Tend the Heart. Hold the dark. Hold the frame budget._
