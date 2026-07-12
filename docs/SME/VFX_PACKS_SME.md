# VFX Packs SME Dossier — Mirza Beig / Spells Pack / Lana Studio / VfxParade

**Date:** 2026-07-11 (overnight SME research session)
**Scope:** the four NON-Hovl VFX packs (Hovl Studio has its own dossier). Every claim below is
verified from the working tree at `C:\eoa` (material YAML, shader source, git index, our consuming
code), plus store/publisher web research. Product identities cross-checked against
`docs/SME/ASSET_STORE_LEDGER_2026-07-12.md`.
**Context:** project memory records a "half-upgraded URP pack materials" problem class — legacy
built-in-pipeline particle shaders that URP cannot render, so Unity substitutes
`Hidden/InternalErrorShader` and the effect draws as solid opaque error-shader quads (the
"magenta" class, ticket F8-49). Each pack is audited for that class below.

---

## Table of contents

1. [Mirza Beig — Ultimate VFX v3.5.2](#1-mirza-beig--ultimate-vfx-v352)
2. [Zakhan — Spells Pack v1.3.14](#2-zakhan--spells-pack-v1314)
3. [Lana Studio — Casual RPG VFX v1.2](#3-lana-studio--casual-rpg-vfx-v12)
4. [VfxParade (in-house tool, not a store pack)](#4-vfxparade-in-house-tool-not-a-store-pack)
5. [Executive summary + ranked fix list](#5-executive-summary--ranked-fix-list)

---

## 1. Mirza Beig — Ultimate VFX v3.5.2

### 1.1 Identity + inventory

- **Product:** *Ultimate VFX*, Unity Asset Store id **26701**, publisher **Mirza Beig**
  (publisher id 7271). Installed version **3.5.2 (May 2019)** — a **pre-URP-era pack built for
  Unity 2018/legacy built-in pipeline**. Official docs: http://www.mirzabeig.com/products/ultimate-vfx/
  (live Google Doc since v3.2.0 per the bundled changelog `Assets\Mirza Beig\_DOCS\README - Ultimate VFX.txt`).
- **Git status:** **gitignored** (`.gitignore` lines 185–186; comment cites 3,426 files kept out of
  git-add-all danger). `git ls-files` confirms **0 tracked files** — absent on a fresh clone.
- **Counts (measured):** **564 prefabs, 719 materials, 15 scenes, 56 C# scripts, 24 shader files.**
- **Folder architecture** (`Assets\Mirza Beig\`):
  - `Particle Systems\Ultimate VFX\` — the core: `Prefabs/`, `Materials/`, `Scenes/1 - Ultimate VFX (Demo).unity`,
    `Demos/` (Fireworks + three "Wallpapers": Comet Ocean, Gravity Clock, Journey to the Singularity),
    and five free **Expansions**: `XP - ACTION`, `XP - STORM` (2 demo scenes incl. a terrain demo),
    `XP - SHOCKWAVES`, `XP - TITLES`, `XP - CONSTR. KIT` (the v3.5.0 addition of 180+ construction-kit prefabs).
  - `Particle Systems\_Common\Scripts\` — the runtime helper components every prefab leans on (below).
  - `Scripting\Effects\` — the bundled bonus assets: **Particle Affectors**, **Particle Force Fields**,
    **Particle Flocking**, **Particle Lights**, **Particle Plexus** (v1.1.0), each with demo scenes.
  - `Editor Extensions\Utilities\` — **Multi-Asset Renamer**, **Particle Playback**, **Particle Scaler**
    editor windows (menu: `Window > Mirza Beig`).
  - `Shaders\` — 24 custom shaders: `Particles\` (Additive/Alpha families with Anim Blend, Distance Fade,
    Alpha Cutoff, No Fog, Intersection Highlight, Mask, and the **Distortion** pair), `Standard\`
    (two "Terrain Rain" surface shaders), `Image Effects\` (Sharpen, Screen Rain post effects).
- **Naming convention:** prefabs are `pf_vfx-ult_<area>_psys_<loop|oneshot>_<name>.prefab`; materials
  `mat_vfx-ult_particle_<sprite|spritesheet>_<tex>-<shaderVariant>-[params].mat`. `loop` prefabs are
  ambient/continuous; `oneshot` are impact/burst effects.

**Bundled scripts — implementation and logic (the intended usage model):**

Every Ultimate VFX prefab is a plain Shuriken `ParticleSystem` hierarchy plus zero or more of these
`MirzaBeig.ParticleSystems` / `MirzaBeig.Scripting.Effects` components. There is no manager or
service; you `Instantiate` a prefab and the components self-drive:

| Script (`Particle Systems\_Common\Scripts\`) | Logic |
|---|---|
| `ParticleSystems.cs` | The base wrapper: caches all child `ParticleSystem`s and exposes whole-hierarchy `Play/Pause/Stop/Clear/SetLoop/SetPlaybackSpeed/Simulate/IsAlive/IsPlaying` — the pack's intended API surface for driving an effect as one unit. |
| `DestroyOnParticlesDead.cs` / `DestroyAfterTime.cs` / `DestroyOnTrailsDestroyed.cs` | Lifetime management for one-shots: poll `IsAlive()` (or a timer) and `Destroy(gameObject)` — spawn-and-forget. |
| `RewindParticleSystem*.cs` (3 variants) | Editor-preview/looping helpers that re-`Simulate` a system backwards/forwards (the "fully previewed without hitting play" feature from v2.6.2). |
| `ParticleSystemTimeRemap.cs`, `AnimatedLight.cs`, `Rotator.cs`, `Billboard.cs`, `TransformNoise.cs`, `PerlinNoise.cs`, `CameraShake.cs`, `TrailRenderers.cs`, `RendererSortingOrder.cs` | Per-prefab garnish: light intensity curves synced to effect time, constant rotation, camera-facing, perlin positional jitter, shake on burst, trail width/color control, explicit sort order. |
| `_Demo\*` (`DemoManager`, `LoopingParticleSystemsManager`, `OneshotParticleSystemsManager`, `ParticleManager`, `MouseFollow`, `MouseRotateCamera`, `FPSDisplay`, `FPSTest`) | The demo-scene carousel (prev/next through prefab lists, click-to-spawn oneshot at mouse; hold left-click = continuous spawn). Demo-only; uses **legacy Input** (`Input.*`) — will be inert if the project ever goes Input-System-only. |

| Script (`Scripting\Effects\`) | Logic |
|---|---|
| `ParticleAffector.cs` (abstract) + `AttractionParticleAffector` / `VortexParticleAffector` / `TurbulenceParticleAffector` (+ `Noise.cs`) | Attach to a ParticleSystem; each `LateUpdate` does `GetParticles`, applies a per-particle force computed by the subclass (pull toward center / tangential swirl / noise-field turbulence) scaled by radius falloff, then `SetParticles`. CPU main-thread; cost scales with particle count. |
| `ParticleForceField.cs` (abstract) + Attraction/Vortex/Turbulence variants (+ `Noise2.cs`) | The inverse topology: a **scene-space field** with `radius`, `force`, `center` that affects any registered particle systems in range (not just its own). Newer replacement for the affectors (README: experimental multithreaded versions were removed in v3.2.0). Note: Unity has since shipped a native `ParticleSystemForceField`; the Mirza ones predate it and offer per-particle scaling knobs. |
| `ParticlePlexus.cs` | Per-frame: `GetParticles`, then for each particle finds neighbours within `maxDistance` (capped by `maxConnections`/`maxLineRenderers`) and draws pooled `LineRenderer`s between them, lerping width/colour from the particle (`widthFromParticle`, `colourFromParticle`). The classic "constellation web" effect. O(n²) neighbour scan — keep particle counts low. |
| `ParticleFlocking.cs`, `ParticleLights.cs` | Boids-style cohesion on particles; a pooled point-light-per-particle system (pre-dates Shuriken's native Lights module — redundant today). |
| `CreateLUT.cs`, `IEBase.cs`, `MirzaPostProcessing.cs`, `Sharpen.cs` | Legacy `OnRenderImage` image-effect plumbing — **dead under URP** (URP does not call `OnRenderImage`; post is Volume-based). |

### 1.2 Shader / material audit — 719 materials

Measured by parsing every `.mat`'s `m_Shader` reference and resolving GUIDs to shader source:

| Count | Shader | URP fate |
|---|---|---|
| **658** | `Universal Render Pipeline/Lit` | Renders. **This is the footprint of our own `MagentaMaterialFixer` mass-pass** (it swaps Standard/Legacy/error shaders → URP/Lit in place, `Assets\Editor\MagentaMaterialFixer.cs:5-11`). Caveat: URP/Lit is an **opaque lit surface shader** — converted *additive particle* materials lose their blend mode and glow; they render, but as solid lit sprites, not luminous particles. Functional, not faithful. |
| 21 | `Particles/Anim Additive` (custom Mirza CG, not URP-tagged) | Hand-written vert/frag (non-surface) shaders **compile and render in URP** via the `SRPDefaultUnlit` pass — no fog/soft-particle depth support, but visually intact. Same for No Fog (6), Alpha Cutoff (5), Intersection Highlight (1), Additive Soft+Mult (2). |
| **16** | `Mirza Beig/Particles/Distortion/Alpha Blended` | **BROKEN under URP** — `Distortion-Add.shader` / `Distortion-Alpha.shader` use **`GrabPass`**, which URP does not support (needs the Opaque Texture / Shader Graph Scene Color rework). These 16 `*-alphaDistort-*` materials feed **39 distinct prefabs**, e.g. `pf_vfx-ult_xp-storm_psys_loop_heavyRain[2,3] [+distortion]`, `pf_vfx-ult_demo_psys_loop_portalBlue/portalOrange`, `pf_vfx-ult_demo_psys_oneshot_ultraMissile`, `pf_vfx-ult_xp-action_psys_loop_explosion`, `pf_vfx-ult_demo_psys_loop_ghostPortal2/nucleus/miragePulse/flameheart/blastFurnace/bloodStorm`… The distortion sub-emitters in those prefabs will not draw (or draw wrong). |
| **4** | built-in `Particles/Standard Surface` | **Magenta class** (built-in surface shader, dead under URP). Mats: `mat_vfx-ult_particle_sprite_droplet-standard-[nsf]`, `..._rapids3-blur-x2-standard-[nsf]`, `..._softRing-standard-[nsf]`, `..._spritesheet_dust-standard-[nsf]` — all four used by **`pf_vfx-ult_xp-storm_psys_loop_terrainRain.prefab`**. |
| **1** | built-in `Legacy Particles/~Additive-Multiply` | **Magenta class.** `mat_vfx-ult_particle_spritesheet_snow-alphaMobile.mat` → `pf_vfx-ult_xp-storm_psys_loop_softSnowfall.prefab` + `softSnowfall2.prefab`. |
| 2+2+1 | `Screen Rain` / `Mirza Beig/Standard/Terrain Rain` (+`2`) | The Terrain Rain pair are **`#pragma surface` shaders → magenta under URP** (STORM terrain demo). Screen Rain is an `OnRenderImage` image effect → silently never runs under URP. |

Bottom line: the pack's core sprite/spritesheet library renders (post-fixer), but **anything with
distortion, the storm-terrain set, and the two soft-snowfall prefabs are broken under URP**, and
every fixer-converted additive material has degraded blending.

### 1.3 How WE consume it

Runtime consumption: **none.** `Assets\Editor\VFXCatalogGenerator.cs:19-21` states it explicitly:
*"ONLY git-committed packs are referenced … NOTHING under Assets/Mirza Beig/** (gitignored, absent
on clone)."* No `Assets\_Modules\**` file references the pack. Grep hits are all asset-pipeline
hygiene:

- `Assets\Editor\AssetImportPostprocessor.cs:88,97,310` — import-time texture cap: Mirza Beig
  spritesheets (shipped up to **8192²**) are clamped to **2048** (`VfxSheetCap`).
- `Assets\Editor\TextureBatchOptimizer.cs:79,101-106` — same 2048 cap in the batch pass (WO-408 §B;
  called out as "the scariest single-texture payload").
- `Assets\Editor\MagentaMaterialFixer.cs:20,28` — names the pack as a target of the URP swap pass;
  "re-run it after any pack re-import" (gitignored, so YAML edits don't stick across clones).

So Ultimate VFX is a **local-only browsing library** — 236+ MB of import weight, texture-cap
special-casing, and fixer maintenance for zero shipped effects.

### 1.4 Web research

- Store page: https://assetstore.unity.com/packages/vfx/particles/ultimate-vfx-26701 — official
  compatibility note: **SRPs (URP/HDRP) are NOT supported**; built-in pipeline only.
- Publisher: https://assetstore.unity.com/publishers/7271 (Mirza Beig — well-known particle/VFX
  author; his *newer* products are URP-native, e.g. Lightning VFX (URP), and he publishes free URP
  volumetric fog — but Ultimate VFX was never URP-ported).
- Docs: http://www.mirzabeig.com/products/ultimate-vfx/ (live Google Doc).
- URP conversion guidance: there is no vendor path. The viable conversions are (a) our
  MagentaMaterialFixer swap for lit/opaque cases, (b) `URP/Particles/Unlit` with matching
  blend (`_Surface=Transparent`, `_Blend=Additive/Alpha`) for particle materials — the same recipe
  as `VFXManager.ConfigureUrpParticleBlend` — and (c) rewriting the GrabPass distortion shaders
  against URP's Camera Opaque Texture (real shader work, only worth it per hand-picked effect).

### 1.5 Verdict — **Candidate for the deferred asset purge** (recommend, don't delete now)

Zero runtime references, officially unsupported under URP, the heaviest texture payload in the
project, and a standing maintenance tax (re-import + re-fix cycle). Per canon the purge waits for
polish-end — until then: keep it out of catalogs (already enforced), don't wire any
`[+distortion]`, `terrainRain`, or `softSnowfall` prefab anywhere, and if an individual effect is
ever wanted, copy the prefab out, rebuild its materials on `URP/Particles/Unlit` with the correct
additive/alpha blend, and commit the copy (the pack itself never ships). The
Affector/ForceField/Plexus scripts are pipeline-agnostic and genuinely good — if we ever want a
plexus or vortex moment, lifting those scripts alone is cheap and safe.

---

## 2. Zakhan — Spells Pack v1.3.14

### 2.1 Identity + inventory

- **Product:** *Spells Pack*, Unity Asset Store id **141539**, publisher **Zakhan** (support:
  Zakhanfx@hotmail.com; sibling products: Archer Pack, Spells Pack 2 family). Installed version
  **1.3.14 (2026-05-24)** — current era, **Unity 6.3 support**, Input System demo.
- **Git status:** **gitignored** (`.gitignore` lines 187–188; ~2,037 files) — 0 tracked files,
  absent on fresh clone. (This is why the four gameplay-used prefabs are mirrored into
  `Assets/Resources/VFX/` — see 2.3.)
- **Counts (measured):** **466 prefabs, 148 materials, 1 demo scene, 4 C# scripts, 4 shaders**
  (demo-environment only — spell effects use stock URP shaders, no custom shaders).
- **Folder architecture** (`Assets\Spells Pack\`):
  - `Particles\` — the real content: `Prefabs\` organized as `Spells\`, `Projectiles\{Projectiles,Casting,Explosion}\`,
    `Auras\`, `Buffs\`, `Shields\`, `Tomes\`, `Variations\Spells\{Fire,Ice,Storm,Dark,Light,Nature,Arcane}\`
    — path encodes element + moment (e.g. `Casting_Fire`, `Explosion_Fire_2`, `Projectile_Fire_3`),
    which is exactly what VfxParade's substring filters exploit.
  - `Demo\` — `Demo.unity`, environment art (Ground/Rocks/Vegetation + their 4 custom shaders),
    post-process volume profile, Input System actions asset, UI.
  - `Packages\` — **two sidecar unitypackages**: `URP (6000.3.14f1+).unitypackage` (13.6 MB) and
    `HDRP (6000.3.14f1+).unitypackage` (7.8 MB).
  - `Documentation\Documentation.txt` (+ a zip copy) — the pipeline install guide.

**Bundled scripts — implementation and logic (the intended composition pattern):**

- `Demo\Scripts\Demo.cs` (`ZakhanSpellsPack.Demo`) — the showcase browser: seven category arrays
  (Spell/Projectiles/Aura/Shield/Variations/Buff/Tome), Next/Back activate exactly one prefab at a
  time and print its name to a `Text` title. Input is the **new Input System**
  (`SpellsPackInputActions.inputactions` + generated `SpellsPackInputActions.cs`,
  `inputActions.Keyboard.Next/Back.performed`) — this is the v1.3.x "Input System demo fix" from
  the store changelog.
- `Demo\Scripts\CreateProjectile.cs` — turret-style spawner: `InvokeRepeating("Create", Time, Time)`
  instantiates the assigned projectile `Rigidbody` at its transform, **but only one live instance at
  a time** (`Update` re-arms when the instance dies). Intended as the fire-and-watch demo emitter.
- `Demo\Scripts\Projectile.cs` — **the pack's intended projectile lifecycle**, and the pattern to
  copy when wiring these effects: the projectile prefab root carries a `Rigidbody` + collider and a
  child trail/FX object. `Start` sets `rb.linearVelocity` (Unity 6 API — confirms the 6.x refresh).
  `OnCollisionEnter`: instantiate `ExplosionPrefab` at the hit point (destroy after
  `DestroyExplosion` s), **detach the trail child so it survives the parent** (destroy after
  `DestroyChildren` s — lets the trail fade instead of vanishing), destroy the projectile. Cast →
  projectile → explosion is therefore three separate prefabs composed by this script, matching the
  folder split `Casting\` / `Projectiles\` / `Explosion\`.

### 2.2 Shader / material audit — 148 materials — **KEY FINDING**

| Count | Shader | URP fate |
|---|---|---|
| **88** | `Universal Render Pipeline/Particles/Unlit` (GUID `0406db5a14f94604a8c57ccfbc9f3b46`, verified in `Particles\Materials\Arcane Shield.mat`, `Aura*.mat`, etc.) | Renders correctly. **The base pack's spell content is URP-NATIVE in v1.3.14.** |
| 37 | `Universal Render Pipeline/Lit` | Renders (models/props + some effect meshes). |
| **15** | `Custom/Vegetation` | **Magenta class — demo environment only.** All four demo shaders (`Demo\Environment\Shaders\{Vegetation,Rock,GroundCover,Terrain}.shader`) are **`#pragma surface`** built-in-pipeline surface shaders → error-shader under URP. |
| 4 / 3 / 1 | `Custom/Rock` / `Custom/GroundCover` / `Custom/Terrain` | Same — `Demo\Environment\**` materials (`Ground_Mat`, `CliffBig02_Mat`, `VegetationLarge01_Mat`, `Grass_01_Mat`, …). |

**The coordinator's lead — "was the URP sidecar package ever imported?" — resolves as: NO, and it
doesn't need to be.** Proof chain:

1. The `URP (6000.3.14f1+).unitypackage` contents (626 entries, listed from the tarball) all install
   under **`Assets/Spells Pack/LWRP(URP)/…`** — `Particles_LWRP\Materials\*_LWRP.mat` (122),
   `Particles_LWRP\Prefabs\**` (465 duplicate prefabs), its own `Demo_LWRP` scene + its own
   `UniversalRenderPipelineAsset`/renderer/global-settings assets.
2. That `LWRP(URP)` folder **does not exist** in our tree — never imported.
3. But the **base** `Particles\Materials\*.mat` files already reference the URP shader GUIDs
   directly (item 1 of the table). In the v1.3.x era the pack is authored URP-first; the sidecar
   package is a *parallel duplicate content set* (note the `_LWRP` naming and its own pipeline
   assets — a legacy-workflow holdover), not a required upgrade patch.
4. `Documentation\Documentation.txt` is **stale relative to the shipped files**: it still describes
   `Packages\URP (2020.3.33+)` and a `LWRP(URP)\Demo_LWRP\Settings\UniversalRenderPipelineAsset`
   flow (import package → install URP → assign the pack's pipeline asset in Graphics Settings).
   Following it verbatim would import 465 duplicate prefabs and tempt someone to swap the
   project's render pipeline asset for the pack's — **do not follow it in this project.**

**Verdict on F8-49 causality:** the Spells Pack gameplay content is *not* a magenta source. Its only
magenta-class materials are the 23 demo-environment materials, confined to `Demo\Demo.unity`
(never in a build — not in EditorBuildSettings). The recorded F8-49 root in
`MagentaMaterialFixer.cs:147-155` is Unity's built-in `Default-Particle` material on renderer
slots (found in a **Hovl** prefab) plus legacy pack materials — see the Lana section for the live
legacy-material population.

### 2.3 How WE consume it

Live, via two mechanisms (pack is gitignored, so nothing references it by GUID at runtime):

- **Mirror to Resources** — `Assets\Editor\SpellsPackVfxMirror.cs:18-27`
  (`Defenders > VFX > Mirror Spells Pack To Resources`; batchmode
  `DeNelle.Editor.SpellsPackVfxMirror.CopyToResources`): copies exactly four prefabs into the
  git-committed `Assets/Resources/VFX/Projectiles/` — `Projectile_Fire_3`, `Casting_Fire`,
  `Casting_Fire_2`, `Spell_Fire_6`. Those mirrored copies are what ship.
- **VFX catalog picks** — `Assets\Editor\VFXCatalogGenerator.cs:91-92` (fire impact burst =
  `Spell_Fire_6`, "a fireball reads as a real fireball"), `:109` (fireball projectile =
  `Projectile_Fire_3`), `:127-137` (fire cast wind-up = `Casting_Fire*`; heal-over-time =
  `Buff_Nature`). The generator's comment at `:120-123` records a past incident: picks that pointed
  at *unmirrored* Spells Pack paths broke on fresh clones — hence the Lana-first policy.
- **VfxParade manifest** — `Assets\VfxParade\Editor\VfxParadeManifestBuilder.cs:34` scans
  `Assets/Spells Pack` (BuildAll ≈ 466 prefabs) into a build-baked manifest so the owner can browse
  the whole pack in a standalone build (see §4).
- Texture pipeline: `TextureBatchOptimizer.cs:139` caps the pack's textures at 512.
- The owner's one committed parade pick (`Assets\VfxParade\vfx-picks.json`): `Buff_Nature`, moment
  "hit", note "good spell landing on shield".

### 2.4 Web research

- Store: https://assetstore.unity.com/packages/vfx/particles/spells/spells-pack-141539 (multi-pipeline:
  built-in / URP / HDRP). Publisher's newer family: Spells Pack 2 + element sub-packs
  (Fire/Storm/Dark/Arcane/Light + free version), Archer Pack
  (https://assetstore.unity.com/packages/vfx/particles/spells/archer-pack-194309 — same aesthetic,
  referenced in the bundled docs).
- Vendor URP guidance = the bundled `Documentation.txt` (quoted/marked stale above). Support email
  `Zakhanfx@hotmail.com`.

### 2.5 Verdict — **KEEP AS-IS** (live pack; two small hygiene items)

Gameplay content is URP-native and actively wired (fire projectile/cast/impact + nature buff). Do
NOT import the URP/HDRP sidecar packages. Hygiene, when convenient: (a) the 23 `Demo\Environment`
materials are error-shader magenta — harmless in builds but they pollute editor browsing and any
"magenta scanner" run; either leave the whole `Demo\` folder for the polish-end purge list or let
MagentaMaterialFixer sweep them to URP/Lit; (b) the two sidecar `.unitypackage` files (21 MB) are
purge-list fodder too.

---

## 3. Lana Studio — Casual RPG VFX v1.2

### 3.1 Identity + inventory

- **Product:** *Casual RPG VFX*, Unity Asset Store id **239285**, publisher **Lana Studio**
  (publisher id 55292; support: Glowinghuman@gmail.com; also ships the free *Hyper Casual FX*).
  Installed version **1.2 (2025-09-25)** — the version whose demo scripts were fixed for Unity 6.
  Store facts: 126 particle prefabs, 14 demo scenes, 96 hand-drawn textures (128²–1024²),
  **"all effects created without custom shaders"**, top-down oriented, priced ~€23.
- **Git status:** **TRACKED — 595 files committed.** This is the ONLY committed VFX pack, which is
  exactly why `VFXCatalogGenerator.cs:19-20` makes it the canon source: it survives a fresh clone.
  One exception: `.gitignore` lines 285–286 exclude `Casual RPG VFX/Upgrade for URP/` — the URP
  upgrade sidecar is deliberately kept out of git.
- **Counts (measured):** **128 prefabs, 22 materials, 15 demo scenes, 3 C# scripts, 0 custom shaders.**
- **Folder architecture** (`Assets\Lana Studio\Casual RPG VFX\`): `Prefabs\` in gameplay-moment
  families — `Area_generic`, `Backlight_resources`, `Burst`, `Fire`, `Fog`, `Loot`, `Orbs`,
  `Range_attack`, `Regeneration`, `Shields`, `Slash`, `States`, `Top_down_attack` — plus
  `Materials\`, `Textures\`, `Models\` (9 FBX ribbons/quads), `Demo\{Scenes,Scripts,Models,Materials,Animations}`,
  `Scripts\UVscroll.cs`, `Readme.txt`, and `Upgrade for URP\Upgrade for URP.unitypackage`.

**Bundled scripts — implementation and logic:**

- `Scripts\UVscroll.cs` (used inside effect prefabs): every `Update`, sets
  `renderer.materials[materialId].SetTextureOffset("_MainTex", time * scrollSpeed)` — classic
  scrolling-texture ribbons/auras. Two implementation notes: (1) it calls `.materials[...]`, which
  **instantiates per-renderer material copies** (fine for VFX instances, but they must be destroyed
  with the object — our spawn-and-destroy flows do this); (2) it targets **`_MainTex`** — after any
  swap to `Universal Render Pipeline/Particles/Unlit` (which samples `_BaseMap`), a plain offset on
  `_MainTex` is a no-op. Our runtime heal already handles this: `VFXManager.cs:671` copies/aliases
  the texture so scrolling keeps working (see 3.3).
- `Demo\Scripts\ObjectsSwitcher.cs` (namespace `Sveta`): trivially exclusive-activates one prefab
  from a list (`Switch(±1)` wraps, `SwitchTo` SetActives exactly one, fires a
  `UnityEvent<string>` with the prefab name for the demo label).
- `Demo\Scripts\_InputKeyBoard.cs`: the **Unity 6 demo fix** named in the store changelog — a
  KeyCode→UnityEvent binder that compiles BOTH ways:
  `#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER` it maps `KeyCode` to Input System
  `KeyControl`s (a big explicit switch) and polls `wasPressedThisFrame` etc.; otherwise it uses
  legacy `Input.GetKey*`. Demo scenes bind arrow keys to `ObjectsSwitcher.Switch`.
- Intended demo composition: 15 scenes (`Demo_Area_generic`, `Demo_Burst`, `Demo_Fire`,
  `Demo_Fire_cartoon`, `Demo_Fog`, `Demo_Loot`, `Demo_Orbs`, `Demo_Range_attack01/02`,
  `Demo_Regeneration`, `Demo_Shields`, `Demo_Slash`, `Demo_States`, `Demo_Top_down_shot`,
  `Demo_Backlight_resources`) — one per prefab family, each just a floor + `ObjectsSwitcher` +
  `_InputKeyBoard`, with small Animators (`Prefabs.controller`, `Projectiles01/02.controller`)
  flying projectile prefabs across the scene. The pack has **no runtime lifecycle scripts** — every
  effect is a self-contained looping or one-burst `ParticleSystem`; the consumer owns spawn/destroy.

### 3.2 Shader / material audit — 22 materials — **THE magenta-class population**

| Count | Shader | URP fate |
|---|---|---|
| **10** | built-in **`Legacy Shaders/Particles/Additive`** (`m_Shader: {fileID: 10720, guid: 0000000000000000f000000000000000}`) | **Magenta class — dead under URP.** Mats: `1Add_mat`, `Add_01`–`Add_03`, `Add_offset01`–`Add_offset06`. |
| **9** | built-in **`Legacy Shaders/Particles/~Additive-Multiply`** (`fileID: 10721`) | **Magenta class.** Mats: `1AB_mat`, `AB_01`–`AB_03`, `AB_offset01`–`AB_offset05`. |
| 3 | `Universal Render Pipeline/Lit` | Demo floor/props (already swept). |

**All 128 effect prefabs draw exclusively through those 19 legacy materials** ("no custom shaders"
means the author shipped built-in-pipeline stock particle shaders). As raw assets under URP, every
Lana effect is a cluster of solid error-shader (magenta) quads — this pack, not Spells Pack, is the
live population of the F8-49 "half-upgraded URP pack materials" class.

**Why the game still looks right:** `Assets\_Modules\Village\Vfx\VFXManager.cs:572-693` (WO-602)
runs `ProofUrpParticleShaders` on **every spawned instance**: it walks the instance's
`ParticleSystemRenderer`s, detects broken shaders (`VFXManager.cs:698-707`: null,
`Hidden/InternalErrorShader`, or any `Legacy Shaders/*` prefix), replaces them with a cached
`Universal Render Pipeline/Particles/Unlit` material, re-applies the correct transparent blend via
`ConfigureUrpParticleBlend` (`:737-` — `_Surface=1`; `_Blend` 2=Additive / 0=Alpha, chosen from the
legacy shader name), and re-binds the texture to `_BaseMap` (with the `_MainTex` caveat handled,
`:671`). It even refuses to reuse `MagentaFix_*` URP/Lit materials for ribbons (`:649-652`) because
Lit renders solid grey trails. So: **healed at runtime inside VFXManager flows only.** Any Lana
prefab instantiated outside VFXManager (a builder, a scene drop, a future system) renders broken.

**The vendor fix exists on disk and was never applied:** `Upgrade for URP\Upgrade for URP.unitypackage`
(310 entries: 21 materials + all 128 prefabs + textures/scenes/scripts — an in-place overwrite of the
whole pack with URP-shader materials). `Readme.txt` instructs exactly that: *"If you want to use
this asset in a URP … go to the Casual RPG VFX/Upgrade for URP folder and run 'Upgrade for URP'."*
It also notes demo scenes were authored for **Gamma** color space; we run Linear, so post-upgrade
effects will read slightly punchier/darker than the store video — acceptable, and our catalog picks
were chosen by eye in-game anyway.

### 3.3 How WE consume it — the workhorse pack

- **`Assets\Editor\VFXCatalogGenerator.cs:78` + the pick table at `:89-176`** — Lana supplies ~35 of
  the catalog keys: physical/ice/aether/heal impacts (`Slash_stone_once`, `Hit_frost`, `Hit_magic`,
  `Hit_heart`), shockwave/shards/smoke bursts, arrow + dark-magic projectiles, ALL cast wind-ups
  (`Orbs_electric`, `Orbs_leaves`, `Flash_dubble_circle`, `Area_generic_*_outbreak` — chosen
  Lana-over-Spells at `:120-126` specifically because Lana is git-committed and clone-safe), all
  death poofs, all loop auras (`Fire_medium`, `Fog_frost`, `Regeneration_health_loop`, `Fog_poison`,
  `Fog_speedSlow`), the environment torch flame (`Fire_small`), and the juice/celebration set
  (`Flash_star`, `Burst_rainbow_mist`, `Level_up`, combo rings).
- Runtime spawning goes through `VFXManager` (catalog asset in Resources → instantiate → URP-proof →
  play), plus `AbilityVfxKit.cs` (hero ability moments) which shares the URP-proofing path.
- Texture pipeline: `TextureBatchOptimizer.cs:77,140` caps Lana textures at 512.

### 3.4 Web research

- Store: https://assetstore.unity.com/packages/vfx/particles/casual-rpg-vfx-239285; publisher:
  https://assetstore.unity.com/publishers/55292. Store page confirms: 126 prefabs, 14 demo scenes,
  96 hand-drawn textures, no custom shaders, all-platform.
- URP conversion guidance = the vendor's own bundled upgrade package (above). No known third-party
  issues; the pack's simplicity (stock shaders only) is why both the vendor package and our runtime
  swap work cleanly.

### 3.5 Verdict — **KEEP + FIX** (fix rank #1 of this dossier)

This is the project's primary shipped VFX pack, and its 19 source materials are the genuine,
still-live magenta class — currently masked by a per-instance runtime heal that costs a renderer
walk + material instantiation on every spawn and protects only VFXManager-spawned instances.
Recommended durable fix, in preference order:

1. **Author-side material swap, committed** (preferred): batch-edit the 19 tracked `.mat` assets to
   `Universal Render Pipeline/Particles/Unlit` with correct blends — `Add_*` → `_Blend=2`
   (Additive), `AB_*` → the additive-multiply approximation the runtime heal already picks — i.e.
   run the existing proven recipe (`ConfigureUrpParticleBlend` + `_BaseMap` bind) once in the
   editor against the material assets and commit. Small diff (19 files), no prefab churn, fresh
   clones are permanently clean, and the runtime proof becomes a no-op safety net.
2. Alternative: import the vendor `Upgrade for URP.unitypackage` — equivalent end state but
   overwrites 310 files including all 128 prefabs and the demo scenes; since the pack is
   git-tracked, that's a large diff to review under §0 mount-garble discipline. Only prefer this if
   we want the vendor's exact URP material tuning.
   Either way, keep `ProofUrpParticleShaders` armed (it also guards Hovl and future packs).
   Do NOT run plain `MagentaMaterialFixer.Run()` as the fix here — its asset sweep converts
   `Legacy Shaders/*` to **URP/Lit** (opaque, lit), which would visibly deaden every additive glow;
   the F8-49 particle path / Particles-Unlit recipe is the correct one for these 19.

---

## 4. VfxParade (in-house tool, not a store pack)

### 4.1 Identity + inventory

**Not an Asset Store product** — correctly absent from the purchase ledger. It is our own
DeNelle-built VFX curation tool, **16 git-tracked files** under `Assets\VfxParade\`:

- `Runtime\VfxParadeManifest.cs` — a `ScriptableObject` living in `Resources` holding
  `List<VfxParadeEntry>` with **direct prefab references** (path + name + prefab). The direct refs
  are the whole trick: they force gitignored Spells Pack prefabs (and their materials/textures)
  **into a standalone build**, where `Resources.Load` by path could never reach them
  (`VfxParadeManifestBuilder.cs:9-11` documents the why).
- `Runtime\VfxParadeRuntime.cs` — the in-build parade overlay (detailed below).
- `Editor\VfxParadeManifestBuilder.cs` — scans `Assets/Spells Pack` (`:34`), sorts, loads each
  prefab, writes the manifest asset. Entries: menu `Tools/VFX Parade/Build Runtime Manifest (FULL
  Spells Pack)` or batchmode `DeNelle.Editor.VfxParadeManifestBuilder.BuildAll` (~466 prefabs);
  a category-filtered variant exists. Warns "(Spells Pack is gitignored - is it imported?)" when
  the source folder is empty.
- `Editor\VfxParadeWindow.cs` — the editor-window sibling for in-editor browsing.
- `vfx-picks.json` (repo root of the module) — the owner's committed picks (currently one:
  `Buff_Nature` / moment `hit` / "good spell landing on shield"). The runtime writes to
  `Application.persistentDataPath/vfx-picks.json`; this committed copy is a harvested snapshot.

### 4.2 Implementation and logic (`VfxParadeRuntime.cs`)

Launched by reflection from AdminOverlay (Settings → DevTools → "VFX Parade") so the HUD asmdef
never references this assembly (`:27-29`). Singleton `Launch()` (`:140`); `OnEnable` freezes
`Time.timeScale` (curation pauses the game) and all UI/animation runs on unscaled time. Loads the
manifest from Resources (`:193-211`), then:

- **Spawning** (`:284-320`): each entry is instantiated under a `VfxParadeAnchor` positioned
  `_distance` metres along `Camera.main.forward`, base-rotated to face the camera, and re-spawned
  every 3 s (`LoopSeconds`) so one-shot effects replay.
- **View control** (`:325-491`): drag = orbit (the *effect* rotates in place under the fixed game
  camera — deliberately never fights `Camera.main`), wheel/pinch = zoom (1.5–12 m), preset snaps
  Front/Side/Top/45, sticky auto-spin (40°/s). View resets per effect; UI raycast guard stops drags
  over the panel.
- **Filter** (`:69-260`): a cycle button narrows the ~466 entries by substring token (Fire, Ice,
  Storm, Dark, Light, Nature, Arcane, Casting, Projectile, Explosion, Aura, Buff, Shield) —
  exploiting Spells Pack path naming; an empty result falls back to All with a warning.
- **Curation** (`:749-823`): moment tag cycle (cast/hit/death/buff/projectile/aura/other) + free-text
  note; BOOKMARK does load-append-write on `vfx-picks.json` (re-loads before writing so parallel
  sessions never clobber; `:786-787`). The AI then reads the JSON and wires effects into the
  catalog — the owner curates in a build, no editor, no drag-drop (memory
  `never-dragdrop-or-manual-playtest` honored by design).
- **UI**: entirely code-built uGUI ScreenSpaceOverlay (sort order 32500, above AdminOverlay's
  32000), self-created EventSystem if missing, ASCII-only, per the "UXML does not ship in builds"
  project rule (`:22-25`). Null-safe: broken manifest entries are warned and skipped.

### 4.3 Shader/material audit + consumption

No materials, no shaders, no store assets — nothing to audit. It is itself a consumer: today it
parades **Spells Pack only** (builder root `:34`). Extending the parade to Hovl (or any pack) is a
one-line root change + rebake of the manifest.

### 4.4 Verdict — **KEEP AS-IS**

Healthy, purpose-built, git-tracked, and the reason the owner can curate 466 gitignored effects
from a standalone build. Only wishlist item: parameterize the builder's source root so the same
parade serves Hovl/Lana families.

---

## 5. Executive summary + ranked fix list

We audited the four non-Hovl VFX packs by parsing every material's shader reference on disk,
reading every bundled script, unpacking the sidecar upgrade packages, and tracing our own consuming
code. The headline: the "half-upgraded URP pack" problem is real but lives in exactly one shipped
place, and one suspected cause is cleared.

**Lana Studio Casual RPG VFX is the live magenta-class population and our workhorse pack at the
same time.** All nineteen of its particle materials still sit on built-in legacy particle shaders
(ten on Legacy Particles Additive, nine on the Additive-Multiply variant), which URP cannot compile
— left alone, every one of its 128 effect prefabs draws as flat opaque error-shader quads. Around
thirty-five VFX catalog keys (impacts, cast wind-ups, death poofs, loop auras, torch flames, level-up
celebration) point at this pack, and it only looks correct in game because VFXManager re-materials
every spawned instance at runtime. That heal costs work on every spawn and protects only
VFXManager-spawned instances; the source assets remain broken and the pack is git-tracked, so a
permanent fix is a small committed diff. The vendor's own URP upgrade package sits un-imported on
disk (and gitignored), which is the literal "half-upgraded" state the memory describes.

**The Spells Pack lead is cleared.** Its separate URP unitypackage was indeed never imported — but
inspection of the package contents and the base materials shows it is a legacy-era duplicate
content set, not a required patch: the base pack in v1.3.14 is already authored on URP shaders
(88 materials on URP Particles Unlit, 37 on URP Lit), and the four prefabs we mirror into
Resources render correctly. Its only error-shader materials are 23 demo-environment materials
(vegetation, rocks, ground — all built-in surface shaders) confined to a demo scene that never
ships. Do not follow the pack's bundled install doc; it is stale and would import 465 duplicate
prefabs plus a competing render-pipeline asset.

**Mirza Beig Ultimate VFX is a 2019, built-in-pipeline-only pack with zero runtime references** —
our catalogs explicitly exclude it, and its only project footprint is texture-cap plumbing and
fixer maintenance. Our mass fixer already swapped 658 of its 719 materials to URP Lit (they render,
though converted additive particles lose their glow blend), but its sixteen distortion materials
use GrabPass — unsupported in URP — breaking 39 prefabs, and its storm-terrain and soft-snowfall
prefabs sit on dead built-in shaders. Its bundled scripting gems (Particle Plexus, force fields,
affectors) are pipeline-agnostic and worth lifting individually if ever wanted.

**VfxParade is not a store pack at all** — it is our own in-build curation overlay plus a
manifest baker that smuggles the gitignored Spells Pack into builds via direct prefab references,
with bookmarks written to a picks JSON the AI wires into the catalog. Keep as-is.

### Ranked fix list

1. **Lana material conversion (player-facing, cheap, permanent).** Batch-swap the 19 legacy
   materials in `Assets\Lana Studio\Casual RPG VFX\Materials\` to URP Particles Unlit with the
   correct additive / additive-multiply blends — reuse the proven `ConfigureUrpParticleBlend`
   recipe as an editor pass — and commit. Do NOT use the generic MagentaMaterialFixer sweep for
   these (it targets URP Lit, which deadens additive glows). Keep the runtime proof armed as a
   safety net. Acceptance: a Lana prefab dropped in a scene with VFXManager absent renders
   correctly; `ProofUrpParticleShaders` logs zero swaps on catalog spawns.
2. **Spells Pack hygiene (non-urgent).** Mark the bundled `Documentation.txt` install flow as
   stale/inapplicable in the WO that touches the pack next; add `Demo\Environment` materials + the
   two sidecar unitypackages (21 MB) to the polish-end purge list. No gameplay risk today.
3. **Mirza Beig disposition (polish-end purge candidate).** Zero shipped effects; officially no SRP
   support; heaviest texture payload; distortion/terrain/snow prefabs broken under URP. Recommend:
   list the whole pack for the deferred purge, with a carve-out note that individual effects can be
   copied out and re-materialed on demand, and that the Plexus/ForceField/Affector scripts are
   liftable. Until purge: keep it excluded from catalogs (already enforced) and never wire its
   `[+distortion]`, `terrainRain`, or `softSnowfall*` prefabs.
4. **VfxParade wishlist (optional).** Parameterize `VfxParadeManifestBuilder`'s source root so the
   parade can serve Hovl and Lana families as well as Spells Pack.

### Sources

- [Ultimate VFX — Unity Asset Store](https://assetstore.unity.com/packages/vfx/particles/ultimate-vfx-26701)
- [Mirza Beig — publisher page](https://assetstore.unity.com/publishers/7271)
- [Ultimate VFX — official docs (mirzabeig.com)](http://www.mirzabeig.com/products/ultimate-vfx/)
- [Spells Pack — Unity Asset Store](https://assetstore.unity.com/packages/vfx/particles/spells/spells-pack-141539)
- [Archer Pack (Zakhan) — Unity Asset Store](https://assetstore.unity.com/packages/vfx/particles/spells/archer-pack-194309)
- [Casual RPG VFX — Unity Asset Store](https://assetstore.unity.com/packages/vfx/particles/casual-rpg-vfx-239285)
- [Lana Studio — publisher page](https://assetstore.unity.com/publishers/55292)
