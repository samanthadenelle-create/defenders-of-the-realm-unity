# Hovl Studio VFX — Inventory & Hero-Ready Shortlist (WO-VFX-001)

**Status:** LIVING DOC — inventory only. No VFX code written yet. Owner reviews and gives go/no-go before WO-VFX-002+ (pooling / skill-tree / projectiles).
**Root:** `Assets/Hovl Studio` (repo-root-relative)
**Project:** Unity 6 LTS + URP, DeNelle Studios / Defenders of the Realm.
**Compiled:** 2026-07-10.

---

## 0. TL;DR (for the go/no-go)

- **6 Hovl packs are imported and present** (260 prefabs total). No packs are referenced-but-missing.
- **URP status = GREEN.** Every one of the 245 materials references a **Hovl custom URP Shader Graph** (`HS_*.shadergraph`). There are **ZERO** Standard/Built-in/Legacy-particle shaders. **No magenta risk, no Support Package needed** — this is already the URP variant of the packs.
- **Recolorable = YES, broadly.** The dominant shader `HS_Blend_CG` (used by 212/245 mats) exposes an HDR **`Color`** property + a **`Use only color`** toggle. One base fireball becomes fire/ice/lightning/arcane by changing the material HDR color (or, better, the ParticleSystem StartColor at runtime — no material duplication needed).
- **Note vs. Grok's pack guidance:** the packs Grok flagged as ideal to standardize on — **"Toon Projectiles 2"** and **"AAA Stylized"** — are **NOT present** in this folder. Our closest equivalents are **AAA Projectiles Vol 1** (the standardize-on-this workhorse: 27 matched projectile families with cast+projectile+impact) and **RPG VFX Bundle** (buffs/auras/RPG moments). Recommend standardizing on **AAA Projectiles Vol 1** as the projectile system.
- **All shared assets are centralized** in `HSFiles/` (Materials, Textures, Shaders, Scripts, Sounds) — prefabs across all 6 packs point back into it. Moving/pruning a pack means checking HSFiles refs.

---

## 1. Top-Level Pack List (counts)

| Pack | Prefabs | Primary type(s) | Present? |
|---|---|---|---|
| **AAA Projectiles Vol 1** | 163 | Projectile / Cast(Flash) / Impact(Hit) / 2D | ✅ imported |
| **RPG VFX Bundle** | 27 | Aura/Buff / Impact / Explosion / Debuff | ✅ imported |
| **Magic circles** | 26 | Aura(ground circle) / Shield / Cast | ✅ imported |
| **AOE Magic spells Vol.1** | 17 | AOE / Explosion / Cast / Impact | ✅ imported |
| **Map track markers VFX** | 16 | Environment / UI marker (loop) | ✅ imported |
| **3D Lasers Pack** | 11 | Beam | ✅ imported |
| **HSFiles** (shared) | 0 prefabs | 245 mats · 223 tex · 10 shaders · 15 scripts · sounds | ✅ shared backbone |
| ~~Toon Projectiles 2~~ | — | — | ❌ NOT present |
| ~~AAA Stylized~~ | — | — | ❌ NOT present |
| ~~Support Package~~ | — | (not needed — already URP) | ❌ NOT present / not required |

**Shared backbone (`HSFiles/`):** `Materials/` (245), `Textures/` (223), `Shaders/` (10 `.shadergraph`), `Scripts/` (15 `.cs`), `Sounds/`, `Models/`, `Animations/`, `Settings/`.

---

## 2. Shaders & URP / Magenta Status

### 2.1 Shaders present (all URP Shader Graph — `HSFiles/Shaders/`)

| Shader Graph | Material usage | Purpose | Recolorable |
|---|---|---|---|
| `HS_Blend_CG` | **212** | Main additive/blend particle (fireballs, flashes, hits, most trails) | ✅ HDR `Color` + `Use only color` |
| `HS_Blend_TwoSides` | 8 | Two-sided blend (ribbons, ground planes) | ✅ Color |
| `HS_LitFresnel` | 6 | Lit + fresnel rim (meshy/soul effects) | ✅ Color |
| `HS_Trail` | 5 | Motion trails | ✅ Color |
| `HS_DissolveNoise` | 5 | Dissolve/erode edges | ✅ Color |
| `HS_Distortion` | 2 | Heat/refraction distortion (needs Opaque Texture) | partial |
| `HS_Electricity` | 2 | Animated electricity/arc | ✅ Color |
| `HS_BlendDistort` | 2 | Blend + distortion | partial |
| `HS_ChannelCut` | 2 | Channel-masked reveal | ✅ Color |
| `HS_LightGlow` | 1 | Soft glow sprite | ✅ Color |

### 2.2 URP / Magenta verdict — **GREEN, no action required**

- Every `.mat` in `HSFiles/Materials` resolves to one of the 10 `HS_*` Shader Graphs above (verified by `m_Shader` guid mapping across all 245 mats). **No `Standard`, no `Legacy Shaders`, no `Mobile/Particles/*`, no missing-shader refs.**
- Shader Graph shaders compile against the **active SRP**; our project is URP, so they render correctly. No pink.
- **`HS_Distortion` / `HS_BlendDistort` caveat:** these sample the camera **Opaque Texture**. If a given URP Renderer/quality level has **Opaque Texture disabled**, distortion effects show flat/invisible (not magenta). Enable `Opaque Texture` on the URP Asset used in-game if we ship any distortion effect. Low priority (only 4 mats).

### 2.3 Magenta fix steps (ONLY if a pink material ever appears — e.g. after re-import on a fresh machine)

1. **First choice — import Hovl's free "VFX URP/HDRP Support Package".** Hovl ships a free converter on the Asset Store that swaps Standard-pipeline mats to URP/HDRP. Not present today and **not needed** (we already have URP mats), but it's the canonical fix if a re-import ever drops built-in mats.
2. **Fallback — reassign shader per material:** select the pink material → set shader to the matching `HS_*` Shader Graph (`HSFiles/Shaders/HS_Blend_CG` for additive particles) → re-set the main texture + HDR Color. Do NOT swap to URP/Lit — Hovl effects rely on the additive/soft-blend behavior of `HS_Blend_CG`.
3. **Bulk case:** if a whole pack re-imported as built-in, run Unity's `Edit ▸ Rendering ▸ Materials ▸ Convert...` then spot-fix any additive particles back to `HS_Blend_CG` (URP's auto-convert makes them opaque/Lit, which is wrong for glows).

---

## 3. Per-Pack Catalog

### 3.1 AAA Projectiles Vol 1 — 163 prefabs — ⭐ standardize-on-this
`Assets/Hovl Studio/AAA Projectiles Vol 1/Prefabs/`

The workhorse. **27 matched elemental families** (+ a Dragon punch), each authored as a full cast→fly→hit set. Element index is consistent across all sub-folders:

> 1 nature arrow · 2 electro · 3 black fire · 4 yellow arrow · 5 red · 6 blue fire · 7 pink · 8 dagger · 9 water · 10 blue laser · 11 orange arrow · 12 slime · 13 red laser · 14 blue rapid · 15 pink crystal · 16 fire · 17 nova violet · 18 nova orange · 19 circle bomb · 20 pink arrow · 21 red arrow · 22 cute star · 23 cube · 24 green explosion · 25 orange explosion · 26 blue diamond/crystal · 27 heart

| Sub-folder | Count | Type | Notes |
|---|---|---|---|
| `Projectile VFX loop/` | 28 | **Projectile** (pure visual, no logic) | ⭐ POOL THESE. No mover attached → we drive with our own Rigidbody/mover. Recolorable, mobile-safe. |
| `Projectiles with logic/` | 28 | **Projectile** + `HS_ProjectileMover.cs` | Reference for behavior; strip the Hovl mover, keep visuals for our system. |
| `Projectiles(Particle collision)/` | 28 | **Projectile** (particle-collision hit) | Uses `HS_ParticleCollisionInstance` to spawn hit — reference only. |
| `Flash and hits/` (Flash N) | ~28 | **Cast** (muzzle/spawn flash) | ⭐ short one-shot; ideal skill windup/muzzle. |
| `Flash and hits/` (Hit N) | ~28 | **Impact** (on-hit burst) | ⭐ short one-shot; ideal projectile impact / enemy hit. |
| `Projectiles 2D/` | 27 | **Projectile (2D)** | Billboard 2D variant — skip for our 3D combat. |
| `Demo scenes/` | 1+scenes | Demo | `SceneSmoke.prefab`, `Readme.txt`, 3 demo `.unity`. |

**Per-family kit example (element 16 "fire"):**
`.../Projectile VFX loop/Projectile 16 fire.prefab` (fly) · `.../Flash and hits/Flash 16 fire.prefab` (cast) · `.../Flash and hits/Hit 16 fire.prefab` (impact). Same triplet exists for all 27 elements.

**Recolor/perf:** all use `HS_Blend_CG` → recolor by StartColor. Small particle counts, no heavy sim → **mobile-safe + pooling-friendly**. This is the pack to standardize projectiles on.

### 3.2 RPG VFX Bundle — 27 prefabs
`Assets/Hovl Studio/RPG VFX Bundle/Random effect prefabs/`

RPG "moment" effects — buffs, debuffs, explosions, level-up.

| Prefab | Type | Likely use |
|---|---|---|
| `Buff heal.prefab` | Aura (loop) | Heal cast / regen |
| `Buff orange circle.prefab`, `Buff orbital.prefab`, `Buff palladin.prefab`, `Buff white twist.prefab`, `Buff chain.prefab`, `Soft blue buff.prefab`, `Cute buff Yorai.prefab` | Aura/Buff (loop) | ⭐ DoT/aura loops, collector FULL glow, companion ambient |
| `Lvl up.prefab` | Cast/burst | ⭐ **building level-up burst** |
| `Magic Sparks.prefab`, `Gold dot.prefab`, `Snowflake trails.prefab` | Ambient/sparkle | ⭐ collector fill sparkle, town ambient |
| `Chain explosion.prefab`, `Wheat explosion.prefab`, `Electro splash.prefab` | Explosion | ⭐ raid/AOE explosion |
| `Punch Hit.prefab`, `Wheat arrow hit.prefab`, `Chain strike.prefab`, `Yellow Flash.prefab` | Impact | Melee/enemy hit |
| `Dragon punch.prefab` | Cast/impact | Big melee finisher |
| `Debuff 1.prefab`, `Debuff chain.prefab` | Aura (debuff loop) | Poison/curse DoT |
| `Buff orange shot.prefab`, `Blue capture.prefab`, `Water flaw.prefab`, `Mountains shield.prefab`, `Hyperdymension circle.prefab` | Mixed | Special skills |

### 3.3 Magic circles — 26 prefabs
`Assets/Hovl Studio/Magic circles/Prefabs/` (+ `Loop version/`)

Ground-projected summoning circles + shields. Each has a **one-shot** and a **`Loop version/`**.

| Prefab family | Type | Likely use |
|---|---|---|
| `Magic circle fire / fire call / water / electro / blood / dark star / forest archer / sun / sun sparks / octagon / pink` | **Cast** (ground circle, one-shot) | ⭐ summon/cast windup under hero or enemy |
| `Loop version/Magic circle * loop` | **Aura** (looping ground circle) | ⭐ channel/DoT zone, portal telegraph |
| `Magic shield holy / runes / sparks / sakura / yingyang` (+ loop) | **Shield** | ⭐ hero/building shield, block bubble |

`HS_LitFresnel`/`HS_Blend_CG`. Recolorable. Shields loop → set **Prewarm** on when spawned already-active.

### 3.4 AOE Magic spells Vol.1 — 17 prefabs
`Assets/Hovl Studio/AOE Magic spells Vol.1/Prefabs/`

Bigger staged AOE attacks — best for boss/raid & ground-slam moments.

| Prefab | Type | Likely use |
|---|---|---|
| `Meteor.prefab`, `Meteor 2.prefab`, `Meteor shower.prefab`, `Meteor shower 2.prefab` | Projectile+Explosion (AOE) | ⭐ raid ultimate / boss AOE. Higher particle cost — pool + cap on mobile. |
| `Meteor hit.prefab`, `Meteor hit 2.prefab` | Impact/Explosion | Meteor landing |
| `Lightning strike.prefab`, `Lightning hit.prefab` | Cast+Impact | ⭐ Thunderbolt strike-down |
| `Energy explosion.prefab` | Explosion | ⭐ ground slam / raid burst |
| `Front spikes attack.prefab`, `Flower slash.prefab`, `Ise attack.prefab`, `Knives.prefab`, `Knife hit.prefab` | AOE/Cast/Impact | Melee AOE, hero slash |
| `Magic attack.prefab`, `Magic hit.prefab` | Cast+Impact | Generic arcane |
| `Leaves buff.prefab` | Aura | Nature buff |

### 3.5 3D Lasers Pack — 11 prefabs
`Assets/Hovl Studio/3D Lasers Pack/Prefabs/`

Continuous beams (`Laser beam 1 nature` … `10 fire`, + `Laser beam demo interactions`). **Type = Beam.** Driven by `Hovl_Laser.cs` (stretches a mesh between two points). Recolorable. Use for hero beam skill / tower laser / boss channel. Beam requires a start+end target → our own aiming code (WO-VFX-002+).

### 3.6 Map track markers VFX — 16 prefabs
`Assets/Hovl Studio/Map track markers VFX/Prefabs/`

Ground/world markers, each with a `Loop`. **Type = Environment/UI.**
`Marker 1 arrows · 2 Pointer · 3 Zone · 4 Pillar · 5 Circle · 6 Arrows · 7 Danger zone · 8 Safe zone` (+ Loop each). Use for objective pings, raid target ring, safe/danger zone telegraphs, build-placement highlight.

---

## 4. Shared Scripts (`HSFiles/Scripts/`) — reference only (do not ship Hovl movers as-is)

| Script | Role | Our stance |
|---|---|---|
| `HS_ProjectileMover.cs` | Moves projectile forward, spawns hit on collide | Reference — replace with our pooled projectile/Rigidbody system. |
| `HS_ProjectileMover2D.cs` | 2D variant | Skip. |
| `HS_ParticleCollisionInstance.cs` | Spawns hit FX on particle collision | Reference. |
| `Hovl_Laser.cs`, `Hovl_LaserDemo.cs`, `Hovl_DemoLasers.cs` | Beam stretch + demo | Reference for beam skill. |
| `HS_Rotator.cs`, `HS_EffectOnDie.cs`, `HS_EffectSound.cs`, `HS_CallBackParent.cs`, `HS_HittedObject.cs`, `HS_RaycastInstance.cs` | Small helpers | Some (rotator, sound) reusable; most are demo glue. |
| `For demo scenes/HS_*` | Demo shooting/camera | Editor demo only. |

---

## 5. ⭐ Hero-Ready Shortlist (30 prefabs → game uses)

Recolor note: **R = recolorable** by ParticleSystem StartColor / material HDR `Color` (all `HS_Blend_CG`-based, so almost everything is R). Prefer recoloring one base at runtime over duplicating materials.

### Projectiles (skill-tree actives — pair with a Cast + an Impact)
| # | Use | Prefab (path under `Assets/Hovl Studio/`) | R |
|---|---|---|---|
| 1 | **Fireball I/II/III** (fly) | `AAA Projectiles Vol 1/Prefabs/Projectile VFX loop/Projectile 16 fire.prefab` | ✅ recolor → ember/blue-fire |
| 2 | **Thunderbolt** (fly) | `AAA Projectiles Vol 1/Prefabs/Projectile VFX loop/Projectile 2 electro.prefab` | ✅ |
| 3 | **Arcane Blast / Magic Missile** | `AAA Projectiles Vol 1/Prefabs/Projectile VFX loop/Projectile 17 nova violet.prefab` | ✅ → arcane/void |
| 4 | **Ice Shard / Frostbolt** | `AAA Projectiles Vol 1/Prefabs/Projectile VFX loop/Projectile 26 blue diamond.prefab` | ✅ |
| 5 | **Nature/Poison bolt** | `AAA Projectiles Vol 1/Prefabs/Projectile VFX loop/Projectile 1 nature arrow.prefab` | ✅ |
| 6 | **Water/holy bolt** | `AAA Projectiles Vol 1/Prefabs/Projectile VFX loop/Projectile 9 water.prefab` | ✅ |
| 7 | **Ranged archer arrow** | `AAA Projectiles Vol 1/Prefabs/Projectile VFX loop/Projectile 11 orange arrow.prefab` | ✅ |
| 8 | **Beam skill / tower laser** | `3D Lasers Pack/Prefabs/Laser beam 10 fire.prefab` | ✅ (needs beam code) |

### Cast / windup (muzzle at spawn)
| # | Use | Prefab | R |
|---|---|---|---|
| 9 | Fire cast windup | `AAA Projectiles Vol 1/Prefabs/Flash and hits/Flash 16 fire.prefab` | ✅ |
| 10 | Lightning cast | `AAA Projectiles Vol 1/Prefabs/Flash and hits/Flash 2 electro.prefab` | ✅ |
| 11 | Arcane cast | `AAA Projectiles Vol 1/Prefabs/Flash and hits/Flash 17 nova violet.prefab` | ✅ |
| 12 | Big spell summon circle (under caster) | `Magic circles/Prefabs/Magic circle fire call.prefab` | ✅ |
| 13 | Channel/telegraph loop circle | `Magic circles/Prefabs/Loop version/Magic circle electro loop.prefab` | ✅ (loop; Prewarm) |
| 14 | Lightning strike-down cast | `AOE Magic spells Vol.1/Prefabs/Lightning strike.prefab` | ✅ |

### Impacts / hits (hero + enemy)
| # | Use | Prefab | R |
|---|---|---|---|
| 15 | Fire impact | `AAA Projectiles Vol 1/Prefabs/Flash and hits/Hit 16 fire.prefab` | ✅ |
| 16 | Lightning impact | `AAA Projectiles Vol 1/Prefabs/Flash and hits/Hit 2 electro.prefab` | ✅ |
| 17 | Arcane impact | `AAA Projectiles Vol 1/Prefabs/Flash and hits/Hit 17 nova violet.prefab` | ✅ |
| 18 | Generic melee/enemy hit | `RPG VFX Bundle/Random effect prefabs/Punch Hit.prefab` | ✅ |
| 19 | Physical arrow hit | `AAA Projectiles Vol 1/Prefabs/Flash and hits/Hit 11 orange arrow.prefab` | ✅ |
| 20 | Crystal/ice impact | `AAA Projectiles Vol 1/Prefabs/Flash and hits/Hit 26 blue crystal.prefab` | ✅ |

### Knight melee slash / ground slam
| # | Use | Prefab | R |
|---|---|---|---|
| 21 | Sword slash arc | `AOE Magic spells Vol.1/Prefabs/Flower slash.prefab` | ✅ |
| 22 | Ground slam / shockwave | `AOE Magic spells Vol.1/Prefabs/Energy explosion.prefab` | ✅ |
| 23 | Spike/lunge AOE | `AOE Magic spells Vol.1/Prefabs/Front spikes attack.prefab` | ✅ |

### Auras / DoT loops / buffs
| # | Use | Prefab | R |
|---|---|---|---|
| 24 | Heal / regen aura | `RPG VFX Bundle/Random effect prefabs/Buff heal.prefab` | ✅ (loop) |
| 25 | Generic buff aura (companion ambient) | `RPG VFX Bundle/Random effect prefabs/Buff white twist.prefab` | ✅ (loop) |
| 26 | Poison/curse DoT | `RPG VFX Bundle/Random effect prefabs/Debuff 1.prefab` | ✅ (loop) |
| 27 | Hero/building shield bubble | `Magic circles/Prefabs/Loop version/Magic shield holy loop.prefab` | ✅ (loop; Prewarm) |

### Collector / building / raid / ambient (game-economy juice)
| # | Use | Prefab | R |
|---|---|---|---|
| 28 | **Collector fill sparkle** + **FULL glow aura** | `RPG VFX Bundle/Random effect prefabs/Magic Sparks.prefab` (sparkle) + `.../Gold dot.prefab` (FULL gold glow) | ✅ (loop) |
| 29 | **Building level-up burst** | `RPG VFX Bundle/Random effect prefabs/Lvl up.prefab` | ✅ (one-shot) |
| 30 | **Raid explosion** + **objective marker** | `AOE Magic spells Vol.1/Prefabs/Meteor hit.prefab` (blast) + `Map track markers VFX/Prefabs/Marker 7 Danger zone Loop.prefab` (telegraph) | ✅ |

**Integration-point coverage (per coordinator's list):** 16 skill-tree actives → #1–20 (cast+projectile+impact triplets, recolor per element); Knight melee slash / ground slam → #21–23; collector fill sparkle + FULL glow + destruction explosion → #28 + #22/#30; building level-up → #29; NPC/companion ambient → #25/#28; enemy hit + death → #15–20 + #22.

---

## 6. URP + Unity 6 Best-Practice Notes (for WO-VFX-002+ — code comes after owner review)

- **Import order:** materials are already URP — no Support Package step. If a fresh re-import ever goes magenta, import Hovl's free URP/HDRP Support Package FIRST (see §2.3).
- **Recolor via HDR Color, not material dupes:** set `ParticleSystem.main.startColor` (or the renderer material's HDR `Color`) at runtime so one Fireball base serves fire/ice/lightning/arcane. `HS_Blend_CG`'s `Use only color` toggle drives fully by tint when you want a flat recolor.
- **POOL everything that spawns often.** These prefabs are individually cheap but many-unpooled instantiations tank mobile. Projectiles, hits, and flashes especially → object pool keyed by prefab.
- **Use ParticleSystem main module for overrides:** scale (`transform.localScale` / `startSizeMultiplier`), speed (`simulationSpeed`), lifetime — don't edit the source prefab per-skill.
- **Projectiles = Hovl visuals + OUR movement.** Take the `Projectile VFX loop/` prefabs (no logic) and drive them with our Rigidbody/mover; do not ship `HS_ProjectileMover`.
- **Loops (auras/shields/circles):** enable **Prewarm** so they appear already-running when spawned mid-effect.
- **Mobile:** favor low particle counts (AAA Projectiles/Flash/Hit are fine as-is); **cap the heavy AOE** (Meteor shower, Energy explosion) — pool + limit concurrent instances. Prefer Mesh Renderer over Billboard where a mesh variant exists.
- **Distortion effects** (`HS_Distortion`/`HS_BlendDistort`, 4 mats): require **Opaque Texture ON** in the active URP Asset or they render flat — enable before shipping any.

---

## 7. Missing / Not-Present (for completeness)

- **Toon Projectiles 2** — not in folder.
- **AAA Stylized (projectiles)** — not in folder.
- **Magic effects pack / RPG VFX Bundle Vol 2** — not in folder (only "RPG VFX Bundle").
- **Hovl URP/HDRP Support Package** — not present; not required (already URP).

If the owner wants the toon-stylized look Grok referenced, those are separate Asset Store purchases. Our present set already covers all 6 integration points above.
