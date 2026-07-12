# Hovl Studio VFX — Full SME Dossier

**Date:** 2026-07-12 · **SME: overnight session 2026-07-12**
**Scope:** everything under `C:\eoa\Assets\Hovl Studio\` — what we own, how the vendor intends it to be used, how our game actually consumes it, and the concrete gaps that explain the owner's felt verdict: *"we are not making them anything like how the demo shows."*
**Verified from actual files and code, not comments** (CLAUDE.md catalog rule). Companion docs: `docs/vfx/HovlStudio_Inventory.md` (WO-VFX-001 inventory), `docs/vfx/SkillTree_VFX_Mapping.md`.

---

## Table of Contents

1. [Pack inventory — what we own](#1-pack-inventory)
2. [The Hovl scripts — logic, knobs, and what breaks without them](#2-the-hovl-scripts)
3. [Demo-scene wiring — how the vendor composes the look](#3-demo-scene-wiring)
4. [How OUR game consumes them — architecture + concrete gaps](#4-how-our-game-consumes-them)
5. [Web research — publisher, docs, videos, known issues (cited)](#5-web-research)
6. [Corrective recommendations — ranked](#6-corrective-recommendations)
7. [Executive summary (2-minute read)](#7-executive-summary)

---

## 1. Pack inventory

**What is installed is ONE product: the Hovl Studio "RPG VFX Bundle", v6.0.4** (purchased Jul 10 2026; store release Jul 8 2026, 90.4 MB). The bundle *is* the union of five individual Hovl packs plus a shared backbone folder — there are **no version mismatches** because everything came in one import:

| Folder under `Assets/Hovl Studio/` | = Store pack | Prefabs | Demo scene(s) | Docs in pack |
|---|---|---|---|---|
| `AAA Projectiles Vol 1` | AAA Stylized Projectiles Vol.1 | **163** | `Demo projectiles simple spawning.unity`, `Demo projectiles(Full particles).unity`, `Demo projectiles 2D.unity` | `Demo scenes/Readme.txt` — **the big one; full HS_ProjectileMover manual** (updated in v6.0.4) |
| `RPG VFX Bundle` | bundle-exclusive extras | **27** | `Demo scene/Demo random effects.unity` | none |
| `Magic circles` | AAA Magic Circles and Shields | **26** (incl. `Loop version/`) | `Demo magic circles.unity` | `Readme.txt`, `Loop version/How to use.txt` |
| `AOE Magic spells Vol.1` | AOE Magic spells Vol.1 | **17** | `Demo AOE skills.unity` | `Readme.txt` |
| `Map track markers VFX` | Map Track Markers VFX | **16** (each with a `... Loop` twin) | `Demo scene MTM.unity` | `Readme.txt` |
| `3D Lasers Pack` | 3D Lasers Pack | **11** | `Demo lasers.unity`, `Demo lasers collisions.unity` | `Documentation.txt`, `If you want to use lasers in 2D.txt` |
| `HSFiles` (shared backbone) | — | 0 | — | — |

**`HSFiles/` shared backbone:** `Materials/` (**245 .mat**), `Textures/` (223), `Shaders/` (**10 URP Shader Graphs** — see below), `Scripts/` (**15 .cs**, all in namespace `Hovl`), `Sounds/`, `Models/`, `Animations/`, `Settings/` (**`VolumeURP.asset`** — the demo post-processing profile, load-bearing for §3). Every pack's prefabs point back into HSFiles — prune with care.

**Shader folder contents** (`HSFiles/Shaders/`, all `.shadergraph`, all URP-native — this is the v6.0.0 "all shaders updated to Shader Graph" generation):
`HS_Blend_CG` (used by 212/245 mats — the main additive/blend particle shader, HDR `Color` property + `Use only color` toggle), `HS_Blend_TwoSides`, `HS_BlendDistort`, `HS_ChannelCut`, `HS_DissolveNoise`, `HS_Distortion`, `HS_Electricity`, `HS_LightGlow`, `HS_LitFresnel`, `HS_Trail`.

**Changelog facts verified against the files** (owner's Asset Store record → local reality):

- **v6.0.4 "updated documentation"** ✓ — the AAA Projectiles `Readme.txt` now contains a complete, current `HS_ProjectileMover` manual (pooling, Detached objects, lifetime model). Read it; it is quoted throughout §2.
- **v6.0.3 "projectile effects SEPARATED from the scripted prefabs; projectiles loopable"** ✓ — `AAA Projectiles Vol 1/Prefabs/` has **three parallel projectile folders**: `Projectile VFX loop/` (28, **pure visual, zero scripts, looping particle systems, no Light component**), `Projectiles with logic/` (28, carry `HS_ProjectileMover` + Rigidbody + Collider + a real-time Light), and `Projectiles(Particle collision)/` (28, use `HS_ParticleCollisionInstance`). Verified by grep: `Projectile 16 fire.prefab` in the loop folder has **no `m_Script` and no Light**; `Dragon punch projectile.prefab` in the logic folder carries script GUID `605a456c…` = `HS_ProjectileMover`.
- **v6.0.0 / v5.2.0 Shader Graph** ✓ — all 245 materials resolve to the 10 `HS_*` graphs; **zero** legacy/Standard/built-in particle shaders (independently re-verified in WO-VFX-001; the F8-49 magenta class does NOT apply to Hovl — see §4c).
- **v5.3.3 "HS_ParticleCollisionInstance + objects pool"** ✓ — the installed script contains a full static-pool implementation (`[PS_Effect_Pool]` DontDestroyOnLoad root, per-prefab queues).
- **v6.0.3 "laser scale changeable via script"** ✓ — `Hovl_Laser.cs` has the `laserSize` field + `ApplyLaserSize()` (scales transform + LineRenderer width + counter-scales texture tiling).

**Not present** (for completeness): Toon Projectiles 2, AAA Stylized (separate SKUs), the old "Support Package for Hovl Studio assets" (deprecated on the store; **not needed** — v6 is Shader-Graph-native), and the `Tools > RPchanger` menu tool (bundle description mentions it for converting *to* Built-in; not in our import, and we never want Built-in).

---

## 2. The Hovl scripts

All 15 scripts live in `Assets/Hovl Studio/HSFiles/Scripts/` (5 of them under `For demo scenes/`), namespace `Hovl`. Verdict-per-script on **what breaks visually if a prefab is spawned without the script running**:

### 2.1 `HS_ProjectileMover.cs` — the flagship gameplay script

Attached to all 28 `Projectiles with logic/` prefabs. The v6.0.4 Readme documents it verbatim; the code confirms:

- **Movement:** `FixedUpdate` sets `rb.linearVelocity = transform.forward * speed` (default 15). Projectile must face +Z.
- **On `OnCollisionEnter`:** freezes the Rigidbody, disables the Light and Collider, **stops the projectile PS**, positions the child `hit` object at `contact.point + normal*hitOffset`, orients it (`FromToRotation(up, normal)` → optional fire-point rotation / rotationOffset / `LookAt(point+normal)`), plays `hitPS`, then destroys-or-disables after `hitPS.main.duration`.
- **`Detached[]` (trails/smoke):** on impact these children are **un-parented and their emission stopped so in-flight particles finish their lifetime naturally** — the trail fades behind the stopped projectile instead of vanishing. On pooled reuse (`notDestroy=true` + `OnEnable`) they are restored to their cached local pose and replayed.
- **`flash` (muzzle):** detached to world space on Start/OnEnable so the muzzle flash stays at the fire point while the projectile flies.
- **Pooling:** `notDestroy=true` makes every Destroy a `SetActive(false)`; `OnEnable` fully re-arms (velocity zeroed, collider/light re-enabled, PS cleared+played, detached restored). Pool-safe by design since v6.
- **Public knobs:** `speed, hitOffset, UseFirePointRotation, rotationOffset, hit, hitPS, flash, projectilePS, Detached[], rb, col, lightSourse, notDestroy, lifeTime (5s), detachedLifeTime (1s)` (all `[SerializeField] protected`, class is `protected virtual` throughout — designed to be subclassed).
- **Without it:** the scripted prefab never moves, never collides, its Light never turns off, its hit child never plays, and the trail never releases. (The `Projectile VFX loop/` prefabs are the vendor's official script-free alternative — that's the v6.0.3 separation, and it's what we use.)

### 2.2 `HS_ParticleCollisionInstance.cs`

On `Projectiles(Particle collision)/` prefabs. `OnParticleCollision` → spawns `EffectsOnCollision[]` at each collision event (with offset/rotation modes), **through its own static pool** (`[PS_Effect_Pool]` root, per-prefab queues, `MaxSpawnsPerCollisionCall` throttle, `UsePooling=true` default). Requires the ParticleSystem's Collision module with "Send Collision Messages". Without it: particle-collision projectiles pass through the world with no hit effect. **Note:** if we ever pool prefabs that carry this script, we'd have two pooling systems nested (ours + its static pool) — currently we don't reference any `Projectiles(Particle collision)/` prefab, so this is dormant.

### 2.3 `Hovl_Laser.cs` (+ `Hovl_LaserDemo.cs`)

The beam engine for the 3D Lasers Pack. Every `Update`: raycasts forward up to `MaxLength`, stretches the LineRenderer from origin to hit point, drags `HitEffect` to the hit point (oriented to the surface), and **retiles `_MainTex`/`_Noise` texture scale by distance** so the beam texture doesn't stretch. `laserSize` scales transform+width and counter-divides tiling (v6.0.3). `DisablePrepare()` must be called before destroying so the beam doesn't flash at the origin. Pack doc: *"The script won't work if you don't select Hit Effect."* **Without it: a laser prefab is a dead 1-unit line — completely unusable.** `Hovl_LaserDemo` is the same plus demo damage (`HS_HittedObject` via tag "Target").

### 2.4 Small runtime helpers (ride inside effect prefabs)

| Script | Logic | Without it |
|---|---|---|
| `HS_Rotator` | `InvokeRepeating` every 0.0167s adds `(x,y,z)` to localEulerAngles. OnEnable/OnDisable-safe → **pool-safe**. | Rotating rings/circles freeze — magic circles lose their spin. |
| `HS_CallBackParent` | On `OnParticleSystemStopped` (needs PS StopAction=Callback) re-parents itself back or self-destroys. | Detached sub-effects leak in the scene or never return. |
| `HS_EffectOnDie` | Watches each particle's remaining lifetime; when a particle dies, spawns `EffectsOnDie` from a local pool at the particle's death position (e.g. meteor-shower: each meteor particle spawns an impact where it lands). | Staged AOE effects (Meteor shower) lose their landing explosions — half the effect. |
| `HS_EffectSound` | `Start()`-driven: plays AudioSource clip, optional `InvokeRepeating` + random volume. **Not pool-aware** (Start fires once; a pooled re-enable does not replay a non-repeating sound). | AOE spells go silent. |
| `HS_HittedObject` | Demo target health bar (`TakeDamage`). | Demo-only. |
| `HS_ProjectileMover2D` | 2D legacy variant (Instantiate/Destroy style, no pooling). | 2D-only; skip. |

### 2.5 Demo-scene glue (`For demo scenes/`)

- **`HS_DemoShooting`** — the projectile demo driver: mouse-aim raycast rotation of the turret, left-click single / right-click autofire, A/D to switch prefab, **and a full per-prefab object pool** (`Hovl_GlobalProjectilePool` DontDestroyOnLoad root, `maxPoolSizePerPrefab=40`, exposes `IPooledProjectile.OnSpawnedFromPool`). The vendor's demos are pooled, exactly like our manager — pooling is the *endorsed* pattern.
- **`HS_CameraHolder`** — the showcase driver for RPG Bundle / AOE / Magic circles demos: orbit camera + GUI buttons (Previous/Play again/Next) + **the hue slider**: it caches every child ParticleSystem's startColor as HSV, then on slider move recolors each PS with `Color.HSVToRGB(newHue, cachedS, cachedV)` keeping cached alpha — i.e. **the vendor's official recolor shifts HUE ONLY and preserves each sub-system's saturation, brightness and alpha**. This is the "change the color in 1 click" script from the store description, and it is the reference implementation our tinting should match (§4d).
- **`HS_RaycastInstance`** — click-to-spawn at raycast hit (Map markers demo), auto-destroy after PS duration.
- **`HS_DemoShooting2D`**, **`Hovl_DemoLasers`** — 2D and laser demo drivers (laser one instantiates on mouse-down, calls `DisablePrepare` on mouse-up).

---

## 3. Demo-scene wiring

Read as YAML (grep for script GUIDs + settings; nothing edited). Which Hovl script drives each demo:

| Demo scene | Driver script | Composition |
|---|---|---|
| `AAA .../Demo projectiles simple spawning.unity` | `HS_DemoShooting` | Turret + FirePoint aim at mouse; **pooled** spawn of `Projectiles with logic/` prefabs; walls + a backdrop object literally named **"Black"** (dark surround so HDR glow reads); camera has `m_RenderPostProcessing: 1` |
| `AAA .../Demo projectiles(Full particles).unity` | `HS_DemoShooting` | same, with the particle-collision prefab set |
| `RPG VFX Bundle/Demo random effects.unity` | `HS_CameraHolder` | orbit cam + Next/Previous cycling one effect at a time over a single dark Cube floor + one directional light + hue slider |
| `AOE Magic spells Vol.1/Demo AOE skills.unity` | `HS_CameraHolder` | same showcase pattern; dark ambient (flat ambient sky tone ~0.21–0.26 luminance, no skybox brightness) |
| `Magic circles/Demo magic circles.unity` | `HS_CameraHolder` | same |
| `Map track markers/Demo scene MTM.unity` | `HS_RaycastInstance` | click ground → marker at hit point |
| `3D Lasers Pack/Demo lasers*.unity` | `Hovl_DemoLasers` (+ `HS_HittedObject` in the collisions one) | hold-mouse beam, `DisablePrepare` on release |

**The single most load-bearing wiring fact:** **every demo scene (8/8) references `HSFiles/Settings/VolumeURP.asset`** (guid `bd84b2d7…`) via a scene Volume — a URP post-processing profile with:

- **Bloom: ACTIVE, intensity = 5, threshold = 1.1** (scatter 0.7 default)
- ColorAdjustments: active
- Tonemapping / ChromaticAberration / DoF etc.: present but inactive

So the "demo look" = **HDR-emissive Shader-Graph particles (HS_Blend_CG colors are HDR, luminance > 1) + Bloom intensity 5 + a dark, low-albedo surround + one effect on screen at a time + a close orbit camera**. The glow that sells these effects is *mostly bloom*; without it the same prefabs render as thin, flat sprites. The vendor's store copy says this outright: *"promo media uses post-process Bloom from the Volume component."*

Other demo composition details worth copying:

- Projectiles are always fired as the **flash (muzzle, detaches at fire point) → moving projectile with Light + trailing Detached[] → hit effect oriented to the surface normal** triple. Never a bare projectile.
- Ground/impact pairing: hits are rotated by `FromToRotation(Vector3.up, contact.normal)` — ground hits sit flat on the ground, wall hits face out of the wall.
- Mover speed default **15 u/s** (demo turret fires at this), fire rate slider ~0.1s in autofire.
- The demos rotate effects onto a **dark backdrop** ("Black" object / dark Cube / dark ambient) — on a bright sunlit terrain the additive blend has far less contrast to pop against.

---

## 4. How OUR game consumes them

### 4.a Architecture (what exists — and most of it is right)

- **`VFXManager.Hovl.cs`** (`Assets/_Modules/Village/Vfx/VFXManager.Hovl.cs`, partial of `VFXManager`, WO-VFX-002): `VFXManager.PlayKey(key, pos, rot, parent, color, scale, lifetime, follow)` resolves a string key through **`HovlVfxCatalog`** (`Assets/Resources/VFX/HovlVfxCatalog.asset`, 30 rows: Key → serialized prefab ref + PoolSize/DefaultScale/DefaultLifetime/Recolorable/IsLoop). Pooled per key under the shared `_poolRoot`; shared caps `_maxActiveOneshots=40`, `_maxActiveLoops=20` (`VFXManager.cs:139-142`); FlowTrace-instrumented (no-key, no-prefab, cap-hit, no-visual all self-report).
- **Catalog generation:** `Assets/Editor/HovlVfxCatalogGenerator.cs` — script-authored (menu `Defenders/VFX/Generate Hovl VFX Catalog`, marker `HOVL_VFX_CATALOG_OK`), curated Map of 30 keys → exact prefab paths, reflection/SerializedObject so `DeNelle.Editor` never references `DeNelle.Village`. Last regenerated at HEAD (commit `369c4f30`, 30 keys incl. `Poi_NodeAura`/`Poi_Landmark`).
- **Projectile model — deliberate and vendor-endorsed:** the catalog's five `*_Projectile` keys all point at **`Projectile VFX loop/`** prefabs (the v6.0.3 script-free, loopable, light-free variants), and we fly them with our own movement: `RangedAttackVFX.PlayHovlTravel` (`RangedAttackVFX.cs:236-246`) and `HeroAbilities.FlyCosmeticProjectile` (`HeroAbilities.cs:1399+`, hardcoded 26 u/s lerp proxy) attach **`HovlVfxFollower`** (LateUpdate position-copy) to a mover/proxy transform, then `handle.Stop()` on arrival and fire the `*_Impact` key at the landing point. Enemies do the same via `Enemy.cs:1584-1596` (cast key at chest height + impact key on a `ProjectileMover` land callback). This is exactly the pattern Hovl's own v6 separation and the Projectile Factory integration use — **HS_ProjectileMover NOT being driven is not a bug**; the loop prefabs never had it.
- **Callers:** ArcaneTower, DefenseTower, TowerCombat, HealingFountain (loop aura handle), Enemy ranged, HeroAbilities (16 actives via `def.VfxCast/VfxImpact/VfxResidual`), ActionBundlePlayer (`row.vfxKey` at a bone), StructureDamageVisuals (Raid_Explosion, Ember_Burn), WeaponTrailController (Melee_Slash), PoiCalloutSystem.

### 4.b GAP #1 — **Bloom: the demo look runs at intensity 5; our world runs at 0** ⭐ the headline

- Hovl demo profile: **Bloom intensity 5, threshold 1.1** (`HSFiles/Settings/VolumeURP.asset`).
- Our global **`Assets/DefaultVolumeProfile.asset`: Bloom active but intensity = 0 — effectively OFF** (threshold 0.9).
- The only place we turn bloom on is **inside the battle arena**: `BattleArena.cs:163-166` builds a local volume with intensity **1.4**, threshold 1.2, priority 100 (WO-504) — i.e. even our best case is ~3.5× dimmer than the vendor's showcase, and **everywhere outside the arena (overworld casts, tower bolts, fountain aura, POI callouts, structure burns) renders with ZERO bloom.**
- This single setting is the largest contributor to "nothing like the demo": the HS_Blend_CG materials emit HDR luminance specifically so bloom can halo them; at intensity 0 they read as thin translucent sprites.
- Secondary: demos also stage against dark backdrops; our daylight terrain further reduces additive contrast. We can't go full demo-dark, but bloom + threshold tuning recovers most of it.

### 4.c URP / magenta state — **GREEN for Hovl; F8-49 was a different pack**

All 245 Hovl materials resolve to the 10 `HS_*` Shader Graphs (re-verified WO-VFX-001 §2.2); zero legacy particle shaders. The half-upgraded-legacy-shader magenta history (F8-49, memory `spell-vfx-pixelated-repeated-ask`) belongs to the **Lana Studio / Spells Pack** legacy prefabs, which the VFXType path scrubs via `ProofUrpParticleShaders` (`VFXManager.cs:596+`). `CreateHovlInstance` (`VFXManager.Hovl.cs:288-290`) deliberately skips that pass for Hovl — correct. Two caveats: `HS_Distortion`/`HS_BlendDistort` (4 mats) need **Opaque Texture ON** in the URP asset or they render flat; and the deprecated Support Package is irrelevant to v6 (do not chase it).

### 4.d GAP #2 — **our tint flattens the effect; the vendor's recolor is hue-shift-only**

`ApplyStartColor` (`VFXManager.Hovl.cs:344-351`) writes ONE flat color into **every child ParticleSystem's startColor**, replacing gradients/MinMaxGradients and the authored per-sub-system color balance (hot cores near white, halos saturated, sparks varied). The vendor's own recolor (`HS_CameraHolder.Counter/OnGUI`) caches each PS's HSV and **changes hue only, preserving each system's saturation, brightness (value) and alpha**. Result of ours: recolored effects turn into a uniform single-tone mush and lose the bright core that bloom feeds on. Also note our tints come in as LDR colors (e.g. `BlastColor`, `def.UnityColor`) — startColor multiplies the HDR material color, so an LDR startColor can *drag luminance below the bloom threshold*, dimming the effect twice.

### 4.e GAP #3 — **loop projectiles hard-clear on impact; the demo lets trails finish**

On arrival every caller does `handle.Stop()` → `ReturnHovlToPool` → `StopAllParticles` = `Stop(true, StopEmittingAndClear)` + immediate `SetActive(false)` (`VFXManager.Hovl.cs:301-322`, `VFXManager.cs:841-845`). The whole projectile — including its trail stretched out behind it — **vanishes in one frame at the moment of impact**. `HS_ProjectileMover` instead stops emission and lets live trail particles complete their lifetime (`ReleaseDetachedObjects`, Readme: "existing particles finish their lifetime"). The impact burst masks the head, but the mid-air trail pop is a visible quality drop on every ranged shot. Fix = a "soft stop": `StopEmitting` (no clear) + deferred pool return (`ReturnHovlAfterDelay` already exists at `VFXManager.Hovl.cs:325-329` — it's just not what `VFXHandle.Stop()` calls).

### 4.f GAP #4 — **no projectile point-light**

The demo's `Projectiles with logic/` prefabs carry a real-time Light that HS_ProjectileMover enables in flight and kills on impact — projectiles light the ground as they pass. Our `Projectile VFX loop/` variants ship with **no Light** (verified: 0 Light components in `Projectile 16 fire.prefab`), so night/dusk shots don't illuminate anything. Optional polish, per-prefab cost on mobile; the vendor's own Readme warns "light strongly loads the game."

### 4.g Smaller deltas (mostly fine / know-about)

- **Impact orientation:** demo hits orient to the surface normal (`FromToRotation(up, normal)`); our impacts mostly spawn `Quaternion.identity` (`Enemy.cs:1595`, `HeroAbilities.cs:1373`, `TowerCombat.cs:567`). For ground hits identity ≈ correct (they're authored Y-up); wall/steep hits will look pasted-flat. Low priority.
- **Loops + Prewarm:** auras/shields/circles spawned mid-state should have Prewarm on (inventory §6 note) — we replay via `Clear()+Play()` each acquire, so loops ramp in from empty each time.
- **Sound:** AOE prefabs carry AudioSource + `HS_EffectSound` (e.g. `Energy explosion.prefab` has one). `HS_EffectSound` is `Start()`-driven and **not pool-aware** — first play sounds, pooled replays are silent (and a `Repeating` one keeps invoking while parked in the pool unless disabled). We otherwise run audio through `IAudioService`; decide one owner.
- **Scale:** Hovl PS use `scalingMode: 0` (Hierarchy), so our `transform.localScale` override works correctly, and `ReturnHovlToPool` resets it. Fine as-is.
- **`HS_Rotator` / `HS_CallBackParent`** are OnEnable/OnDisable- and callback-driven → they survive our pooling. Fine.
- **Lasers unused:** no catalog key references `3D Lasers Pack`. When we add a beam skill, the prefab is unusable without `Hovl_Laser` (needs `HitEffect` assigned, `DisablePrepare()` before despawn, edit the raycast to target enemies per the pack doc).
- **`Map track markers`** used for `Poi_Landmark` (Marker 4 Pillar Loop, scale 4) — loop-twin choice is correct.

### 4.h Which prefabs are unusable without their scripts (summary)

| Prefab family | Script | Without it |
|---|---|---|
| `3D Lasers Pack/Prefabs/*` | `Hovl_Laser` | **Dead** — no stretch, no hit, no tiling |
| `AAA .../Projectiles with logic/*` | `HS_ProjectileMover` | Never moves / never hits (use the `VFX loop` twins instead — we do) |
| `AAA .../Projectiles(Particle collision)/*` | `HS_ParticleCollisionInstance` | No hit effects on contact |
| `AOE .../Meteor shower*` (staged) | `HS_EffectOnDie` (in-prefab) | Loses per-meteor landing bursts — keep the script alive (it ships inside the prefab; our pooling keeps it) |
| Magic circles / markers / buffs | `HS_Rotator`, `HS_EffectSound`, `HS_CallBackParent` (in-prefab) | Spin freezes / silence / leaked children — all ride inside the prefab and work under our pool |

---

## 5. Web research

(Sourced by the overnight research agent; URLs verified 2026-07-12.)

**Publisher:** Hovl Studio = Vladyslav Horobets. Store publisher page: https://assetstore.unity.com/publishers/28391 · ArtStation portfolio/support channel: https://hovl.artstation.com/ (403s automated fetches; indexed piece: https://hovl.artstation.com/projects/YaZx13 "Lightning explosion VFX") · YouTube: https://www.youtube.com/c/HovlStudio · contact per pack readmes: hovlstudio1@gmail.com.

**The bundle we own:** RPG VFX Bundle — https://assetstore.unity.com/packages/vfx/particles/spells/rpg-vfx-bundle-133704 — $24 (from $48), v6.0.4 (Jul 8 2026, 90.4 MB — exact match to install), Unity 2020.3 → 6000.0.67f1, Built-in/URP/HDRP all supported. Contents = AAA Magic Circles and Shields + AAA Stylized Projectiles Vol.1 + AOE Magic spells Vol.1 + 3D Lasers Pack + Map Track Markers VFX + extras (mirror: https://unityassetcollection.com/rpg-vfx-bundle-free-download/). Store copy highlights: multipurpose custom shaders; "easily re-sized and re-colored"; **"Scripts, one of which allows you to change the color of the effects … in 1 click"** (= `HS_CameraHolder`'s hue slider, §2.5); **Shader Graph required (ships with asset); `Tools > RPchanger` converts to Built-in** (never run in our URP project). Demo video: **https://www.youtube.com/watch?v=hI15nSorz68**.

**Member-pack pages:** 3D Lasers Pack https://assetstore.unity.com/packages/vfx/particles/3d-lasers-pack-131685 ($14) · AAA Stylized Projectiles Vol.1 https://assetstore.unity.com/packages/vfx/particles/aaa-stylized-projectiles-vol-1-130378 (28 projectiles / 27 hits / 25 flashes — matches our folders) · AOE Magic spells Vol.1 https://assetstore.unity.com/packages/vfx/particles/spells/aoe-magic-spells-vol-1-133012 ($20, "easily re-sized, re-timed and re-colored", with sound FX) · AAA Magic Circles and Shields https://assetstore.unity.com/packages/vfx/particles/spells/aaa-magic-circles-and-shields-128906 · Map Track Markers VFX https://assetstore.unity.com/packages/vfx/particles/map-track-markers-vfx-131762 ($5). Other demo videos: lasers https://www.youtube.com/watch?v=3gJeNxUjfzc · AAA projectiles https://www.youtube.com/watch?v=CnVCioEvolY · map markers https://www.youtube.com/watch?v=3OkmjnaJWRk · playable AOE demo https://simmer.io/@ErbGameArt/aoe-magic-spells-vol-1.

**URP / setup facts:**
- The old **"Support package for Hovl Studio assets"** (https://assetstore.unity.com/packages/tools/utilities/support-package-for-hovl-studio-assets-157764) is **deprecated/delisted**; it was the Built-in→URP/HDRP material swapper for Unity ≤2021. **Irrelevant to us** — v6.x ships Shader Graph natively for Unity 2022+/Unity 6 (store copy on the AAA Projectiles page).
- Vendor doc note (cached Support-package docs, https://gfx-station.com/support-package-for-hovl-studio-assets/): *"if materials seem dark, multiply the Emission by a large value"* — the vendor's own acknowledgment that the look depends on HDR emission intensity (feeding bloom).
- Store copy confirms: **"promo media uses post-process Bloom from the Volume component"**; vendor recommends Post Processing be enabled (https://www.gameassetdeals.com/asset/130378/aaa-stylized-projectiles-vol-1, https://www.gameassetdeals.com/asset/133704/rpg-vfx-bundle). Matches §3's found-in-files profile (Bloom 5 / threshold 1.1).
- Magenta/pink: no Hovl-specific reports found; generic mechanism = built-in shaders or missing Shader Graph in a URP project (https://discussions.unity.com/t/why-do-i-only-get-only-pink-materials-from-shader-graph/919731). Our v6 install is clean (§4c).
- Community traffic is thin: RealtimeVFX threads are vendor showcases (https://realtimevfx.com/t/3-vfx-packs-for-asset-store-from-hovl-studio/13191, https://realtimevfx.com/t/aoe-magic-spells-vol-1/6715); no Reddit/Unity-forum problem threads surfaced. The one integration precedent: **Infinity PBR's Projectile Factory explicitly removes `HS_ProjectileMover` and drives Hovl visuals with its own movement** (https://infinitypbr.gitbook.io/infinity-pbr/projectile-factory/3rd-party-particle-integrations-16/hovl-studio-4/aaa-stylized-projectiles-vol.-1) — the same architecture we chose.
- Per-version changelog notes are only on the store's JS-rendered Releases tab; the owner's purchase record (v6.0.4 / 6.0.3 / 6.0.0 / 5.3.3 / 5.2.0 / 5.0.1 notes) was verified against local files in §1 instead.

---

## 6. Corrective recommendations

Ranked by expected felt impact per unit of work. File:line pointers into OUR code.

1. **Turn bloom on outside the arena.** ⭐ Root cause of "flat vs demo". Raise `Assets/DefaultVolumeProfile.asset` Bloom intensity from **0** to a real value (start ~1.5–2.5, threshold ~1.1 to match the vendor profile; the demos run 5, which is showcase-hot), or add a global gameplay Volume the way `BattleArena.cs:907+` (`BuildArenaBloom`, constants at `BattleArena.cs:163-166`) already does locally — that code is the in-repo template. Verify the overworld/main cameras have `m_RenderPostProcessing: 1` and HDR on. This one change moves every Hovl effect in the game toward the demo look at zero per-effect cost. (Owner felt-verifies — bloom strength is a taste dial.)
2. **Fix `ApplyStartColor` to hue-shift, not flat-fill.** `VFXManager.Hovl.cs:344-351`. Port the vendor's own algorithm from `HS_CameraHolder` (`HSFiles/Scripts/For demo scenes/HS_CameraHolder.cs`, `Counter`/`OnGUI`): cache each child PS's startColor HSV on first acquire, then apply only the requested HUE, keeping each system's saturation/value/alpha (and keep HDR luminance ≥ authored so tints don't pull effects under the bloom threshold). Callers keep passing the same `Color`; only the application changes.
3. **Soft-stop projectiles on impact.** Give `VFXHandle.Stop()` (or a `StopSoft()` used by the projectile callers at `RangedAttackVFX.cs:178-181`, `HeroAbilities.cs:1424`, `ArcaneTower.cs:392+`) a graceful path: `Stop(true, StopEmitting)` **without Clear**, then `ReturnHovlAfterDelay(go, key, ~0.6s)` (`VFXManager.Hovl.cs:325-329` already exists). Trails then finish their lifetime as `HS_ProjectileMover.ReleaseDetachedObjects` does, instead of vanishing mid-air.
4. **Orient impacts to the surface.** Pass a normal-derived rotation (`Quaternion.FromToRotation(Vector3.up, normal)`) instead of `Quaternion.identity` at `Enemy.cs:1595`, `HeroAbilities.cs:1373`, `TowerCombat.cs:567` where a hit normal (or "hit a wall vs ground" flag) is known. Ground-only hits can stay identity.
5. **Optional: projectile point-light.** Add a small pooled Light (enabled in flight, off on return) to the follower path (`HovlVfxFollower.cs` or on the `[Hovl_…]` instance) to recover the demo's ground-lighting — gate behind quality tier; vendor warns it's the expensive part.
6. **Prewarm the loop auras.** For `IsLoop` rows spawned into an already-active state (Heal_Aura, Aegis_Shield, Taunt_Aura, Poi_NodeAura), enable Prewarm on the main PS at pool-build time (`CreateHovlInstance`, `VFXManager.Hovl.cs:278+`) so they appear mid-cycle, not ramping from empty.
7. **Decide sound ownership for AOE prefabs.** Either strip/disable `HS_EffectSound`+AudioSource on pooled instances and route through `CoreServices.Audio`, or make the pool call `PlayOneShot` on re-enable. Today: first play sounds, pooled replays are silent (`HS_EffectSound.cs` is Start-only).
8. **If/when we ship a beam skill:** use the lasers pack with `Hovl_Laser` intact (assign `HitEffect`, call `DisablePrepare()` before pool return, swap the raycast for our targeting per `3D Lasers Pack/Demo scene lasers/Documentation.txt`). Unusable without the script.
9. **If we ever ship a distortion effect** (4 mats on `HS_Distortion`/`HS_BlendDistort`): enable **Opaque Texture** on `Assets/Settings/DeNelle-URP.asset` first, or it renders flat.

**Explicitly fine as-is (do not churn):** the string-key catalog + generator pipeline; pooling (vendor's own demo pools the same way); using the `Projectile VFX loop/` script-free prefabs with our movers (vendor-endorsed v6.0.3 pattern, same as Projectile Factory); shader/material state (URP Shader Graph everywhere, no magenta risk, no Support Package needed); `transform.localScale` scaling (PS scalingMode = Hierarchy); skipping `ProofUrpParticleShaders` for Hovl.

---

## 7. Executive summary

**What we own:** one product — the Hovl Studio **RPG VFX Bundle v6.0.4** (Jul 2026), which unpacks as five packs (AAA Projectiles ×163 prefabs, RPG extras ×27, Magic circles ×26, AOE spells ×17, Map markers ×16, Lasers ×11) sharing one backbone folder (`HSFiles`: 245 materials, 10 URP Shader Graph shaders, 15 scripts, the demo post-processing profile). Everything is the current Shader-Graph generation: **no magenta risk, no conversion package needed** — the F8-49 magenta history belongs to the separate Lana/Spells legacy packs, not Hovl.

**Our integration is architecturally sound.** The string-key catalog (30 keys, script-generated), pooling, and driving the script-free "Projectile VFX loop" prefabs with our own movers are all the vendor-endorsed v6 patterns (Hovl's own demo pools; the leading third-party integration also replaces Hovl's mover). Nothing needs re-architecting.

**Why ours doesn't look like the demo — four concrete, fixable deltas, in order of impact:**

1. **Bloom.** Every Hovl demo scene runs a Volume with **Bloom intensity 5**; our global profile ships Bloom at **intensity 0** (off), and only the battle arena turns it on (at 1.4). Outside the arena the effects render with no glow at all. This is the headline fix: one profile change affects every effect in the game.
2. **Tinting.** Our recolor floods every particle system with one flat color, destroying the authored bright-core/halo structure. The vendor's own "1-click color change" shifts **hue only** and preserves each sub-system's brightness and alpha — port that (about 20 lines).
3. **Impact pop.** We hard-clear the projectile loop the frame it lands, so the trail behind it vanishes mid-air. The vendor's mover lets trails finish their lifetime — a "stop emitting, return to pool after ~0.6 s" tweak restores it.
4. **Small polish:** orient hit effects to the surface they strike; optional in-flight point-light; prewarm looping auras; sort out sound on pooled AOE prefabs.

**Unusable without their scripts:** the 3D Lasers prefabs (need `Hovl_Laser` — keep it when we build a beam skill) and the "Projectiles with logic / Particle collision" variants (which we correctly avoid in favor of the script-free loop twins).

Full details, file:line pointers, and cited sources in sections 1–6 above.
