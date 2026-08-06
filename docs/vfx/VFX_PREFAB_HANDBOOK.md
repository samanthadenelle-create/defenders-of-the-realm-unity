# VFX Prefab Handbook — EoA (canonical)

**Status:** CANONICAL REFERENCE for any agent or human implementing VFX.  
**Project:** `D:\EoA` · Unity 6000.4.8f1 URP  
**Date:** 2026-08-05  
**Author:** Architecture pass (Grok) consolidating Flames sandbox measurement + WO-759/760/884 + pack inventory.  
**UI refinement pass:** 2026-08-05 — added enum-append single-owner lock (§3.3), building-damage + telegraph-as-cast wiring (§7), registry alignment. (Grok suggests → UI refines → CLI implements.)  

**This doc wins** when it conflicts with older scattered notes on *how prefabs enter the game*.  
Creative beat tables still live in `VFX_CREATIVE_PICKS_REGISTRY.md`; this handbook is the **prefab + pipeline** truth.

| Companion | Role |
|-----------|------|
| `docs/vfx/VFX_CREATIVE_PICKS_REGISTRY.md` | What element × beat *should* look like (owner picks) |
| `docs/vfx/PARTICLE_PACK_UTILIZATION_MAP.md` | Earlier short map (superseded in depth by this doc) |
| `WorkOrders/WORK_ORDER_884_…md` | Facade code + first 5 deliverables |
| `WorkOrders/WORK_ORDER_759_…md` | Pack mental model + breath spatial rules |
| `docs/MAGIC_VFX_LIBRARY.md` | Spells Pack menu (other pack) |
| `docs/LANA_RPG_VFX_NOTES.md` / `MIRZABEIG_VFX_NOTES.md` / `HovlStudio_Inventory.md` | Other packs |

---

## 0. One-paragraph rule (paste into agents)

```
EoA VFX prefabs: never Instantiate pack art from gameplay.
Pipeline = (1) pick a RECIPE from this handbook, (2) classify Family A continuous vs B burst
from emission (rateOverTime>0 → loop handle; rate=0+bursts → oneshot), (3) CopyAsset whole
multi-layer tree into Assets/Resources/VFX/** via an editor builder (pack is gitignored),
(4) VFXType: REFERENCE landed values only — Grok owns enum append (WO-884 single-owner;
registry batch landed 2026-08-05 after Boss_FireBreath), (5) VFXCatalogGenerator Map row
(IsLoop correct), (6) play only through Vfx facade → VFXManager. Never flatten layers.
Aim = socket rotation, not Shape angle.
```

---

## 1. Mental model (the only kitchen)

All Particle Pack effects (and most other particle packs) are the **same toolkit**:

| Block | Meaning |
|-------|---------|
| 1+ **ParticleSystem** children | Layers of the recipe |
| **Emission** | Continuous rate *or* t=0 bursts |
| **Shape** | Cone / sphere / hemisphere / circle — *where* particles spawn |
| Modules + flipbook + materials | Look |
| **Prefab root** | One drag-and-drop unit |

They are **not** different engines per element. FireThrower and RisingSteam differ only in **recipe**.

### 1.1 Prefab vs Inspector vs code vs facade

| Layer | Owns | Does not own |
|-------|------|--------------|
| **Pack / Resources prefab** | Layers, materials, default rates, shapes | When it plays |
| **Inspector** | Scale, density, shape **width** | Aim direction, damage |
| **Socket / bone** | Attach point, offset, forward | Emission math |
| **`Vfx` facade** | Family × element → `VFXType`, bone resolve | Pooling |
| **`VFXManager`** | Pool, quality, Play/Stop, URP proof | Game design |

### 1.2 Multi-layer law

Example (measured): **FlameThrower**

```
FlameThrower          ← jet body (billboard + flipbook), rate ~30, narrow Cone
  ├─ FireEmbers (3)   ← stretch sparks, rate ~100
  └─ Smoke            ← volume, rate ~20
```

**Never flatten.** Quality may **disable** children on the instance; never delete from the recipe.

### 1.3 Aim law

| Control | Set by |
|---------|--------|
| Direction of jet | Socket **rotation** (`LookRotation`) |
| Width of spray | Shape **angle** (art; usually fixed) |
| Spawn outside mesh | Socket **local offset** |

---

## 2. Family A vs Family B (how to classify any prefab)

Open the prefab (or measure emission). Decision:

| If… | Family | Catalog `IsLoop` | Manager path | Facade family |
|-----|--------|------------------|--------------|---------------|
| Any main system has **rateOverTime > 0** | **A Continuous** | **true** | `PlayAura` / `PlayEnvironment` / `PlayProjectile` | Stream / Aura / Ambient / Projectile |
| **rateOverTime = 0** and **bursts at t≈0** | **B Burst** | **false** | `Play` / oneshot | Impact / Muzzle / Cast |
| Shader cutoff script (`SpawnEffect`) | **Scripted** | special | one-shot play, no demo loop | Dissolve / Respawn only |

**Hybrids** (e.g. EarthShatter: some rates + bursts): treat as **A** if the hero layer keeps emitting while held; treat as **B** if the intended use is a single hit. When unsure: **oneshot Impact** for combat hits, **loop Ambient** for ground hazards that linger.

**Bug landmine:** marking a B burst as `IsLoop=true` and fire-and-forget leaks loop slots forever (see `docs/design/VFX_DIRECTION_2026-08-05.md`). Cap is shared (`_maxActiveLoops`, default 20).

### 2.1 Facade API (common class)

```csharp
// Continuous (A) — keep handle
VFXHandle h = Vfx.On(root).AddStream(VfxElement.Fire).OnBone("jaw").AimAt(target).Play();
h?.Stop();

// Projectile travel (A)
VFXHandle trail = Vfx.On(projTf).AddProjectile(VfxElement.Fire).Follow(projTf).Play();

// Burst (B) — no handle
Vfx.On(root).AddImpact(VfxElement.Fire).At(hitPoint).Play();
Vfx.On(tower).AddMuzzle(VfxElement.Fire).OnBone(_firePoint).Play();
Vfx.On(hero).AddCast(VfxElement.Ice).OnBone("hand.r").Play();

// Ambient (A) — component
// VfxEmitter { Family=Ambient, Element=Fire, flicker=true } on a candle prop
```

**Resolution chain:**

```
Vfx.Add{Family}(element)
  → VfxElementTables.Resolve(family, element) → VFXType
  → VFXCatalog prefab (Resources/…)
  → VFXManager pool Play*
```

Adding a pack prefab to “common” **never** means calling `Instantiate`. It means completing that chain for a new or existing `VFXType`.

---

## 3. How to add any pack prefab into common (checklist)

Do these in order. Skip nothing.

### Step 1 — Choose recipe from §5 tables

Name, pack path, family, intended game moment.

### Step 2 — Measure family (don’t guess)

| Signal | Family |
|--------|--------|
| `rateOverTime` max > 0 | A → `IsLoop = true` |
| only bursts | B → `IsLoop = false` |

Count ParticleSystems on root+children; record layer names.

### Step 3 — Append `VFXType` only if needed

- Prefer **repointing** an existing type (`Impact_Flame`, `Death_Generic`, `Aura_Ice`) when the moment already has an enum.  
- **Append** at end of enum only for new moments (`Env_Candle`, `Env_SteamVent`, `Despawn_Dissolve`, …).  
- **Never insert** mid-enum (catalog ordinals break).
- **⚠ SINGLE OWNER of the append edit (coordination lock, WO-884 §0.2):** the enum is ordinal-serialized, so two authors appending in parallel collide/reorder ordinals. **Grok owns the one enum-append edit**, sourced from `VFX_CREATIVE_PICKS_REGISTRY.md` §6/§8, in a single edit; everyone else (CLI, builders) **references the landed values, never mints its own.**  
  **LANDED 2026-08-05** (after `Boss_FireBreath`): `Env_Candle`, `Env_SteamVent`, `Env_SteamBurst`, `Cast_MuzzleFlash`, `Enemy_Spawn`, `Despawn_Dissolve`, `Aura_LowHealth`, `Aura_NearDeath`, `Aura_HealingInProgress`, `Aura_ItemHeal`, `Harvest_Iron`, `Harvest_Wood`, `Harvest_Food`, `Harvest_Crystal`, `Harvest_Gold`, `Collector_Ready`. Reuse `Aura_Healer` for healer structure field — no extra value. Further appends only via Grok + registry, not CLI.

### Step 4 — Commit the art (gitignored pack rule)

Particle Pack lives under `Assets/UnityTechnologies/**` → **gitignored**.

```
Source (gitignored):  Assets/UnityTechnologies/ParticlePack/EffectExamples/.../Recipe.prefab
Dest (committed):     Assets/Resources/VFX/<Category>/<GameName>.prefab
```

Use a **BossFireBreathBuilder-style** editor script:

1. `AssetDatabase.CopyAsset` whole tree (verify descendant + PS counts match).  
2. Clear `playOnAwake` on all systems (combat/env driven by manager).  
3. Optional scale for gameplay.  
4. Idempotent: don’t stomp owner-tuned scale.  
5. Menu + batch marker `*_BUILD_OK`.

Mirror of Spells: `SpellsPackVfxMirror.cs`. Breath: `BossFireBreathBuilder.cs`.

**Do not** point `VFXCatalog` only at gitignored paths for shipped P1 (WO-785 / WO-884).

### Step 5 — Catalog row

`VFXCatalogGenerator.Map`:

```csharp
{ "Your_VFXType", new Pick(
    "Assets/Resources/VFX/…/Your.prefab",
    isLoop: /* true iff Family A */,
    minQuality: 0|1|2,
    poolSize: /* 2 boss loops, 4–8 impacts */ ) },
```

Run: `Defenders/VFX/Generate VFX Catalog` → `VFX_CATALOG_OK`.

### Step 6 — Tables + call site

| If | Edit |
|----|------|
| Element kit | `VfxElementTables` family×element switch |
| Specific moment | Call `Vfx.On(...).Add…` or assign `VfxEmitter` |
| Hovl string key legacy | Prefer migrating to `VFXType`; or keep `Vfx.Key` wrapper |

### Step 7 — Budget

| Family A ambient | Shared loop cap — use nearest-N for many candles/auras |
| Family B | Oneshot cap; correct `IsLoop=false` |

### Step 8 — Soft particles

`Assets/Settings/DeNelle-URP.asset` → `m_RequireDepthTexture: 1` (pack fire soft edges).

---

## 4. Directory layout (committed game art)

```
Assets/Resources/VFX/
  Boss/           Boss_FireBreath.prefab          ← FlameThrower copy (exists)
  Projectiles/    Spells mirrors + flight bodies  ← exists
  Env/            Env_Candle, Env_SteamVent, …    ← builders create
  Impact/         SmallExplosion, FleshImpacts, …
  Death/          DustExplosion, BigExplosion, …
  Aura/           optional tuned loops
  Magic/          IceLance body, EarthShatter, …
```

Pack **source** (local only, not ship path for catalog):

```
Assets/UnityTechnologies/ParticlePack/EffectExamples/
  Fire & Explosion Effects/Prefabs/
  Weapon Effects/Prefabs/
  Magic Effects/Prefabs/
  Misc Effects/Prefabs/
  Smoke & Steam Effects/Prefabs/
  Goop Effects/Prefabs/
  Water Effects/Prefabs/
  Legacy Particles/Prefabs/     ← avoid unless necessary
```

---

## 5. Particle Pack catalog (measured)

**Root:** `Assets/UnityTechnologies/ParticlePack/EffectExamples/`  
**Layers** ≈ ParticleSystem count. **Family** from emission measurement 2026-08-05.

### 5.1 Fire & Explosion

| Recipe | Layers | Family | Pack path (under EffectExamples/) | Common use | Suggested VFXType / table slot | Priority |
|--------|--------|--------|-----------------------------------|------------|--------------------------------|----------|
| **FlameThrower** | 3 | A Stream | `Fire & Explosion Effects/Prefabs/FlameThrower` | Dragon breath cone | `Boss_FireBreath` (built) | P1 ✓ |
| **FlameStream** | 2 | A Stream | `…/FlameStream` | Simpler jet (no smoke) | Stream alt / Medium quality | P3 |
| **FireBall** | 2 | A Projectile | `…/FireBall` | Flying fire orb / tower fire body | `Projectile_TowerFire` / FlameArrow | P1–P2 |
| **WildFire** | 3 | A Ambient | `…/WildFire` | Ground residual burn | Ambient Fire / zone | P3 |
| **LargeFlames** | 2 | A Ambient | `…/LargeFlames` | Pyre / big brazier | Env / hearth | P2 |
| **MediumFlames** | 1 | A Ambient | `…/MediumFlames` | Brazier | Env | P2 |
| **TinyFlames** | 1 | A Ambient | `…/TinyFlames` | Candle cling, low-HP gutter, weapon smolder | `Aura_Flame` / NearDeath / candle | P1–P2 |
| **TinyExplosion** | 3 | B Impact | `…/TinyExplosion` | Light hit / small death | Impact / Death small | P2 |
| **SmallExplosion** | 4 | B Impact | `…/SmallExplosion` | Standard fire impact / death | `Impact_ExplosionFire` / Death_Generic | P1–P2 |
| **BigExplosion** | 8 | B Impact | `…/BigExplosion` | Boss death set piece | `Boss_Death` / Death_Boss | P2–P4 |
| **EnergyExplosion** | 4 | B Impact | `…/EnergyExplosion` | Arcane/magic hit | `Impact_ExplosionAether` / Portal | P2 |
| **DustExplosion** | 5 | B Impact | `…/DustExplosion` | Brute/golem / structure | `Death_Brute` / destruction | P2 |
| ParticlesLight | 0 PS | helper | `…/ParticlesLight` | Light only — not a full recipe | skip | — |

### 5.2 Weapons

| Recipe | Layers | Family | Path | Common use | VFXType / slot | Priority |
|--------|--------|--------|------|------------|----------------|----------|
| **MuzzleFlash** | 2 | B Muzzle | `Weapon Effects/Prefabs/MuzzleFlash` | Tower/gun flash | `Cast_MuzzleFlash` (append) / Muzzle table | P1 |
| **FleshImpacts** | ~4 | B* hybrid | `…/FleshImpacts` | Organic on-hit | surface Impact Physical | P2 |
| **MetalImpacts** | ~4 | B | `…/MetalImpacts` | Armour hit | surface map | P2 |
| **StoneImpacts** | ~4 | B | `…/StoneImpacts` | Wall / rock | surface map | P2 |
| **WoodImpacts** | ~4 | B | `…/WoodImpacts` | Barrel/crate | destruction | P2 |
| **SandImpacts** | ~4 | B | `…/SandImpacts` | Dirt | surface map | P3 |

\*FleshImpacts has residual rate on a child; **use as oneshot Impact** in combat (IsLoop=false) unless you intentionally hold blood spray.

### 5.3 Magic

| Recipe | Layers | Family | Path | Common use | Slot | Priority |
|--------|--------|--------|------|------------|------|----------|
| **IceLance** | ~4–5 | A Projectile (+ mist) | `Magic Effects/Prefabs/IceLance` | Frost bolt body + shards | `Projectile_TowerIce` / FrostBolt | P1 |
| **EarthShatter** | 8 | hybrid → B Impact | `…/EarthShatter` | Meteor / slam / telegraph ground | Impact Physical / Meteor | P2–P3 |

### 5.4 Smoke & Steam

| Recipe | Layers | Family | Path | Common use | Slot | Priority |
|--------|--------|--------|------|------------|------|----------|
| **RisingSteam** | 1 | A Ambient | `Smoke & Steam Effects/Prefabs/RisingSteam` | Vents, heal column language, structure heal field | `Env_SteamVent` / heal rising | P1 |
| **PressurisedSteam** | 2 | A Stream-ish | `…/PressurisedSteam` | Jet trap / pipe | `Env_SteamBurst` or Stream Steam | P2 |
| **Steam** | 1 | A Ambient | `…/Steam` | Soft steam | Ambient | P3 |
| **SmokeEffect** | 1 | A Aura | `…/SmokeEffect` | Low HP gutter wisps, death linger | `Aura_LowHealth` / death linger | P2 |
| **GroundFog** | 1 | A Ambient | `…/GroundFog` | Cold floor fog | `Env_GroundFog` | wired/partial |
| **PoisonGas** | 3 | A Aura | `…/PoisonGas` | Hazard / necromancer | `Aura_Necromancer` / hazard | P3 |
| **RocketTrail** | 6 | B* | `…/RocketTrail` | Arrow/spear streak (often oneshot trail burst) | Projectile Physical | P2 |
| **HeatDistortion** | 1 | A Ambient | `…/` or Misc | Above braziers | overlay | P3 |
| **DustStorm** | 3 | A Ambient | `…/DustStorm` | Sand set piece | later | P4 |

### 5.5 Misc (mood + scripted)

| Recipe | Layers | Family | Path | Common use | Slot | Priority |
|--------|--------|--------|------|------------|------|----------|
| **Candles** | multi flame | A Ambient | `Misc Effects/Prefabs/Candles` | Dungeon sconces | `Env_Candle` | P1 |
| **TinyFlames** | (see fire) | A | fire folder | single flame | candle alt | P1 |
| **DustMotesEffect** | 1 | A Ambient/Aura | `…/DustMotesEffect` | Ice approx, wood harvest, manaweave | `Aura_Ice` approx, harvest Iron/Wood | P2 |
| **FireFlies** | 2 | A Ambient | `…/FireFlies` | Heal contact, crystal/food harvest, ready beacon | Impact_Heal language, harvest | P2 |
| **ElectricalSparks** | 1 | A/B | `…/ElectricalSparks` | Arcane enemy aura | `Aura_EnemyCaster` | P2 |
| **SparksEffect** | multi | B/A | Misc or Legacy | Gold glint / death bones | harvest Gold, Death_Skeleton approx | P2 |
| **SandSwirlsEffect** | 2 | A | `…/SandSwirlsEffect` | desert mood | later | P4 |
| **Dissolve** | multi + script | Scripted | `…/Dissolve` | Blink/despawn | `Despawn_Dissolve` | P3 |
| **Respawn** | multi + script | Scripted | `…/Respawn` | Spawn/summon | `Enemy_Spawn` | P3 |
| EllenDissolve/Respawn | character | Scripted | — | demo only | skip | — |

### 5.6 Goop

| Recipe | Layers | Family | Path | Common use | Slot | Priority |
|--------|--------|--------|------|------------|------|----------|
| **GoopStreamEffect** | 4 | A Stream | `Goop Effects/Prefabs/GoopStreamEffect` | Poison channel + pool | Stream Nature | P3 |
| **GoopSpray** / SprayEffect | 2 | B/A | `…/` | Poison hit / spit | Impact Nature | P3 |

### 5.7 Water

| Recipe | Layers | Family | Path | Common use | Slot | Priority |
|--------|--------|--------|------|------------|------|----------|
| **BigSplash** | 4 | B Impact | `Water Effects/Prefabs/BigSplash` | Water hit | Impact Water (if added) | P3 |
| **Shower** | 3 | A Ambient | `…/Shower` | wet room | Ambient | P3 |
| **WaterLeak** | 4 | A Ambient | `…/WaterLeak` | pipe prop | Ambient | P3 |

### 5.8 Legacy (avoid unless forced)

| Recipe | Note |
|--------|------|
| ElectricalSparksEffect, LightningStormCloud, PlasmaExplosion, Rain, WaterFall, SparksEffect | Legacy folder / extra deps. Prefer modern Misc/Smoke. Lightning → **procedural** per registry. |

### 5.9 Explicit non-goals

| Skip | Why |
|------|-----|
| **Wind** element pack set | No ability; no recipe | 
| Custom holy pack | Use procedural rising column |
| Flattening any multi-layer | Review fail |
| Catalog → gitignored path only for P1 | Clone black hole |

---

## 6. Element × facade family → preferred recipe

Maps **common** calls to pack recipes. Implementation: `VfxElementTables` + catalog prefabs.

| VfxElement | Impact (B) | Muzzle (B) | Cast (B) | Projectile (A) | Stream (A) | Aura (A) | Ambient (A) |
|------------|------------|------------|----------|----------------|------------|----------|-------------|
| **Fire** | SmallExplosion | MuzzleFlash | pack/Lana fire charge | FireBall | **FlameThrower** | TinyFlames | Candles / MediumFlames |
| **Ice** | IceLance shards / SmallExplosion blue | MuzzleFlash | proc/Lana frost | **IceLance** | (rare) Ice mist | DustMotes drift | GroundFog |
| **Arcane** | EnergyExplosion | MuzzleFlash | Lana orbs | Energy orb / existing bolt | — | ElectricalSparks | Lantern |
| **Physical** | surface Impacts / SmallExplosion | MuzzleFlash | Knight flash | RocketTrail / arrow | — | GroundFog/Dust | — |
| **Nature** | GoopSpray | MuzzleFlash | Lana leaves | Goop stream body | GoopStream | PoisonGas light | — |
| **Shadow** | EnergyExplosion dark | — | necro swell | enemy bolt | — | PoisonGas | fog |
| **Steam** | PressurisedSteam pop | — | — | — | PressurisedSteam | RisingSteam | RisingSteam |
| **Holy** | **proc** FireFlies-ish / Impact_Heal | — | **proc** Cast_Heal | — | — | RisingSteam soft | — |
| **Lightning** | **proc** | — | **proc** | — | — | **proc** | — |

**Wind:** not in table until an ability exists.

---

## 7. Game-moment wiring map (where code calls)

| Domain | Driver | Facade pattern | Recipes |
|--------|--------|----------------|---------|
| Boss breath | `DragonBoss.FireBreath` | `AddStream(Fire).OnBone(socket).AimAt(heart).ForSeconds(1.4)` | FlameThrower |
| Tower shot | `TowerCombat.FireAt` | `AddMuzzle` + `AddProjectile` + `AddImpact` at hit | MuzzleFlash, FireBall/IceLance, explosions |
| Hero spell | `HeroAbilities` / `SpellVfxFactory` | `AddCast` on hand, `AddProjectile` on bolt, `AddImpact` | table §6 |
| Enemy death | death VFX path | `AddImpact` / Play(Death_*) | Small/Big/DustExplosion |
| On-hit surface | melee/projectile land | `AddImpact(Physical)` + surface row | Flesh/Metal/Stone/Wood |
| Low HP | `HeroHealth` | `AddAura` LowHealth/NearDeath loops | SmokeEffect, TinyFlames |
| Heal | heal cast/tick | Cast_Heal + RisingSteam / FireFlies | § registry 6a |
| Dungeon dress | `DungeonSceneBuilder` | `VfxEmitter` Ambient | Candles, RisingSteam, MediumFlames |
| Harvest | node/collector | `AddAura` by resource motion | DustMotes, FireFlies, Sparks |
| Structure healer | structure tick | Aura field + Impact_Heal pulses | RisingSteam + FireFlies |
| Blink / spawn | abilities / spawner | Scripted dissolve/respawn once | Dissolve, Respawn |
| Portal | portal controller | keep vortex + optional MediumFlames accent | EnergyExplosion enter/exit |
| Building damage | `StructureDamageVisuals` (WO-672, exists — re-skin, don't rebuild) | data thresholds → smolder/fire/critical/broken tells | SmokeEffect → MediumFlames + **critical-save beacon** (SparksEffect fast-pulse + "!") → DustExplosion + WildFire linger |

**Telegraph = casting:** the warn beat (enemy/structure/boss about-to-act, per-tick heal) reads as a visible
**charge/gather** (Cast language), not a flat flash — you watch it "cast," and that build-up IS the warning.
See registry §1 beat 3 + §6f healer per-tick cast.

---

## 8. Already committed vs still pack-only

### In `Resources/VFX` today (game-safe)

| Path | Role |
|------|------|
| `Boss/Boss_FireBreath.prefab` | FlameThrower recipe (built) |
| `Projectiles/*` | Spells mirrors + custom bodies |

### Must builder-copy next (P1 pack)

| Dest suggestion | Source recipe |
|-----------------|---------------|
| `Resources/VFX/Weapon/MuzzleFlash.prefab` | Weapon/MuzzleFlash |
| `Resources/VFX/Magic/IceLance.prefab` | Magic/IceLance |
| `Resources/VFX/Projectile/FireBall.prefab` | Fire/FireBall |
| `Resources/VFX/Env/Env_Candle.prefab` | Misc/Candles or TinyFlames |
| `Resources/VFX/Env/Env_SteamVent.prefab` | RisingSteam |
| `Resources/VFX/Impact/SmallExplosion.prefab` | SmallExplosion |

Pattern: one generalized **`ParticlePackVfxBuilder`** table of `{ source, dest, vfxTypeName, isLoop, scale }` beats N one-off builders long-term; keep BossFireBreathBuilder as the proven template.

---

## 9. Quality tiers (when using multi-layer)

| Quality | Continuous multi-layer | Burst |
|---------|------------------------|-------|
| Low | Skip stream/aura or root-only | Tiny / skip secondary |
| Medium | Root only (disable embers/smoke) | Mid prefab |
| High | Full tree | Full tree |

Implement by disabling named children **on instance** after pool acquire — not by forking pack YAML.

---

## 10. Loop budget (ops)

| Risk | Mitigation |
|------|------------|
| 30 candle `IsLoop` | nearest-N or room-local only; raise cap in dungeon |
| Tower muzzle marked loop | **IsLoop=false** for MuzzleFlash |
| Enemy auras × pack | nearest-N to camera/player |
| Silent drop | FlowTrace when cap hits (already partial) |

Default `_maxActiveLoops = 20` is **too low** for dressed dungeons + combat auras. Recommend scene-tier raise + nearest-N before mass Ambient wiring.

---

## 11. Other packs (pointers only)

This handbook is Particle-Pack-first for the **common facade**. Other libraries:

| Pack | Gitignored? | Doc | Into common via |
|------|-------------|-----|-----------------|
| Spells Pack | yes | `MAGIC_VFX_LIBRARY.md` | `SpellsPackVfxMirror` → Resources + catalog |
| Lana Casual RPG | often committed | `LANA_RPG_VFX_NOTES.md` | `VFXCatalogGenerator` paths (many rows already) |
| Hovl Studio | often ignored | `HovlStudio_Inventory.md` | `PlayKey` / Hovl catalog — migrate slowly to VFXType |
| Mirza Ultimate | yes | `MIRZABEIG_VFX_NOTES.md` | rare; prefer Spells/Particle/Lana |

**Common facade does not care which pack authored the prefab** — only that a committed Resources prefab + correct IsLoop sit behind a `VFXType`.

---

## 12. Anti-patterns (fail review)

| Don’t | Do |
|-------|-----|
| `Instantiate(packPrefab)` in gameplay | `Vfx` → manager pool |
| Catalog → `UnityTechnologies/...` only | CopyAsset → Resources |
| Flatten FlameThrower / BigExplosion | Keep children |
| `IsLoop=true` on MuzzleFlash / SmallExplosion | Family B → false |
| Shape.angle rewritten every frame for aim | Rotate socket |
| New pool / second VFX manager | `VFXManager` only |
| Insert enum values mid-list | Append only |
| Block ship on custom ice/holy/wind art | Approximations + procedural |
| 40 ambient loops, cap 20 | nearest-N + cap raise |

---

## 13. Agent workflow (end-to-end)

```
1. Read §5 row for the recipe.
2. Confirm Family A/B (§2).
3. Append VFXType if new moment (§3.3).
4. Add builder row: source → Resources/VFX/... whole tree.
5. Map in VFXCatalogGenerator (IsLoop, pool, MinQuality).
6. Point VfxElementTables (or call VFXType directly for non-element moments).
7. One call site: Vfx.On(...).AddX(...).Play() or VfxEmitter.
8. COMPILE_GATE_OK + VFX_CATALOG_OK + builder *_OK markers.
9. Felt-check in Play Mode / VFX Caster (ParticlePack pack).
```

---

## 14. Priority backlog (implementation order)

| Order | Work | Recipes |
|------|------|---------|
| 0 | Facade + tables if missing (WO-884) | — |
| 1 | P1 builders + catalog | MuzzleFlash, FireBall, IceLance, Candles, RisingSteam; breath verify |
| 2 | Death ladder repoints | Small/Big/DustExplosion |
| 3 | On-hit surface | Flesh/Metal/Stone/Wood |
| 4 | Heal + HP auras | RisingSteam, SmokeEffect, TinyFlames, FireFlies, DustMotes |
| 5 | Combat auras + nearest-N | ElectricalSparks, PoisonGas, TinyFlames |
| 6 | Harvest / structures | DustMotes, FireFlies, Sparks, RisingSteam |
| 7 | Portals / Dissolve / Respawn | EnergyExplosion, Dissolve, Respawn |
| 8 | Content extras | WildFire, Goop, Water, EarthShatter |

---

## 15. Doc map (what to open when)

| Need | Open |
|------|------|
| **This prefab → how to ship it** | **This handbook** |
| Element beat fantasy language | `VFX_CREATIVE_PICKS_REGISTRY.md` |
| Facade fluent API code | WO-884 |
| Breath socket / chin aim | WO-759 / WO-757 |
| Loop leak diagnosis | `design/VFX_DIRECTION_2026-08-05.md` |
| Spells pack menu | `MAGIC_VFX_LIBRARY.md` |
| Hovl keys | `HovlStudio_Inventory.md` + `VfxManualPicks.json` |

---

**One-line canon:**  
**A VFX prefab enters common by becoming a committed multi-layer Resources recipe behind a VFXType with correct IsLoop, resolved by the Vfx facade — never by ad-hoc Instantiate or gitignored catalog paths.**
