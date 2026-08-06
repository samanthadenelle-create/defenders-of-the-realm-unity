# Particle Pack → EoA Utilization Map (UI determination)

> **Superseded in depth by** [`VFX_PREFAB_HANDBOOK.md`](./VFX_PREFAB_HANDBOOK.md)  
> (canonical prefab + pipeline doc: family measure, Resources builder steps, full pack tables,  
> element×facade matrix, anti-patterns). Prefer the handbook for implementation.  
> This file remains a short system map for quick scanning.

**Author:** UI seat · **Date:** 2026-08-05 · **Status:** quick map (see handbook for full canon)
**Companion:** `VFX_PREFAB_HANDBOOK.md`, WO-884, WO-759/757.
**Pack root:** `Assets/UnityTechnologies/ParticlePack/EffectExamples/` (gitignored — see handbook §3–§4).

This maps Particle Pack recipes to EoA game moments, FAMILY, and facade one-liners.

---

## 1. The two families (the decision that picks the API)

| Family | Signal in prefab | Facade family | VFXManager path | Lifecycle |
|--------|------------------|---------------|-----------------|-----------|
| **A — Continuous** | `rateOverTime > 0`, looping | `Stream` / `Aura` / `Ambient` / `Projectile` | `PlayAura`/`PlayEnvironment`/`PlayProjectile` → returns `VFXHandle` | `Play()` at start → hold → `handle.Stop()` |
| **B — Burst** | `rateOverTime = 0`, bursts at t=0 | `Impact` / `Muzzle` / `Cast` | `Play(type, pos)` | fire once → pool auto-reclaims |

Everything below is one of these two. The only scripted multi-phase recipe is Dissolve/Respawn
(`SpawnEffect.cs`, a shader-cutoff curve) — used only for materialize/despawn (§4.7).

---

## 2. Game-system map (where + how)

### 2.1 Boss — Syndrath / dragon  ·  driver `DragonBoss.cs`
| Pack recipe | Moment | Family | Facade / status |
|---|---|---|---|
| **FlameThrower** ★ | Breath body (chin socket, aimed at Heart, ~1.4s) | A Stream | `AddStream(Fire).OnBone("VFX_BreathSocket")…` — **BUILT (verify)** |
| Small/Energy/Boss_AttackImpact | Heart connect at ~0.35s into breath | B Impact | `AddImpact(Fire).At(heart)` — BUILT path |
| **FireBall** | Optional spit projectile (flying orb, not a cone) | A Projectile | `AddProjectile(Fire).Follow(orb)` — NEW (backlog P3) |
| **WildFire** | Residual ground burn after breath lands (hemisphere) | A Ambient | `VfxEmitter Steam→Fire ground zone` — NEW (backlog P4) |
| **BigExplosion** | Boss death set-piece | B Impact | reuse `Boss_Death`; upgrade to BigExplosion — NEW (P6) |
| EarthShatter | Wing-slam / ground telegraph shockwave | B Impact | `AddImpact(Physical)` — candidate |

### 2.2 Turrets / towers  ·  driver `TowerCombat.cs` (muzzle L359-383, impact L573)
| Pack recipe | Moment | Family | Facade |
|---|---|---|---|
| **MuzzleFlash** | Barrel flash on fire | B Muzzle | `AddMuzzle(element).OnBone(_firePoint)` — WO-884 §5.2 |
| **FireBall** | Fire-tower projectile | A Projectile | `AddProjectile(Fire).Follow(proj)` → `Projectile_TowerFire` |
| **IceLance** | Frost-tower projectile | A Projectile | `AddProjectile(Ice)` → `Projectile_TowerIce` |
| EnergyExplosion | Arcane/mage-tower projectile or impact | B/A | arcane tower upgrade — candidate |
| **Tiny/SmallExplosion** | Shot impact on the enemy | B Impact | `AddImpact(element).At(hit)` |
| EarthShatter | Catapult/siege (WO-906) ground impact | B Impact | `AddImpact(Physical)` — candidate |

### 2.3 Hero spells + weapon-skill elements  ·  driver `HeroAbilities.cs` (cast L2002, impact L2038, proj L2107)
This is the **Mage magic showcase** (WO-909: "Mage lives heavily in that realm").
| Pack recipe | Moment | Family | Facade |
|---|---|---|---|
| Casting_* / EarthShatter / orbs | Charge/wind-up at the hands | B Cast | `AddCast(element).OnBone("RightHand")` — WO-884 §5.3 |
| **IceLance** | Frost bolt / frost weapon-skill | A Projectile + B Impact | `AddProjectile(Ice)` + `AddImpact(Ice)` |
| **FireBall / Spell_Fire** | Fireball / fire weapon-skill | A Projectile + B Impact | `AddProjectile(Fire)` + `AddImpact(Fire)` |
| **EarthShatter** (8-layer) | Meteor / earth ult impact | B Impact | `AddImpact(Physical/Fire).At(target)` |
| **EnergyExplosion** | Arcane nuke impact | B Impact | `AddImpact(Arcane)` |
| **GoopSpray / GoopStreamEffect** | Poison/acid spit + ground puddle | A Stream + Ambient | `AddStream(Nature)` + `VfxEmitter` puddle — candidate |
| **PoisonGas** | Necromancer/poison AoE cloud | A Ambient/Aura | `AddAura(Shadow)` |

### 2.4 Enemy hits + deaths  ·  drivers `EliteVFXController.cs`, `VfxPool.SpawnDeathBurst`, `Destructible.cs`
| Pack recipe | Moment | Family | Facade |
|---|---|---|---|
| **FleshImpacts** | On-hit blood/flesh response | B Impact | `AddImpact(Physical).At(hit)` — candidate |
| TinyExplosion | Small enemy death pop | B Impact | `Death_*` upgrade |
| SmallExplosion | Standard enemy death | B Impact | `Death_Generic` |
| BigExplosion / Explosion_Dark | Elite/boss death | B Impact | `Elite_Death`/`Boss_Death` |
| **DustExplosion** (500 sand) | Golem/brute crumble, structure destroy | B Impact | `Death_Brute` / `Env_DestructionDust` |

### 2.5 Dungeon ambient + mood  ·  builder `DungeonSceneBuilder.DressRoom` (L664); component `VfxEmitter`
| Pack recipe | Moment | Family | Facade |
|---|---|---|---|
| **Candles / TinyFlames** | Flickering candles on sconces/props | A Ambient + flicker | `VfxEmitter Fire, flicker=true` → `Env_Candle` — WO-884 §5.4 |
| **RisingSteam** | Steam vents / geothermal wisps | A Ambient | `VfxEmitter Steam` → `Env_SteamVent` — WO-884 §5.5 |
| **PressurisedSteam** | Triggered steam-jet trap / pipe burst | B Impact | `AddImpact(Steam)` → `Env_SteamBurst` |
| **MediumFlames / LargeFlames** | Braziers / hearth fire / arena pyre | A Ambient | `VfxEmitter Fire` (hearth L706-713) |
| **GroundFog** | Cold-biome dungeon floor fog | A Ambient (detached) | `Env_GroundFog` (already wired) |
| **PoisonGas** | Hazard room / trap gas | A Ambient | `AddAura(Shadow)` |
| **FireFlies / DustMotesEffect** | Air motes / mood particles in lit rooms | A Ambient | `VfxEmitter` new `Env_Motes` — candidate |
| **HeatDistortion** | Shimmer above braziers/lava | A Ambient | overlay on brazier — candidate |
| SparksEffect / ElectricalSparks | Broken machinery / arcane conduit | A/B | `Env_DestructionSparks` |

### 2.6 Environment / destructibles / surfaces  ·  `EnvironmentVFX.cs`, `Destructible.cs`
| Pack recipe | Moment | Family | Facade |
|---|---|---|---|
| **WoodImpacts** | Barrel/crate break | B Impact | `Env_DestructionDust`/sparks on `TriggerDestruction` |
| **StoneImpacts** | Wall/pillar hit or break | B Impact | wall-damage burst |
| **SandImpacts / DustExplosion** | Dirt/ground destruction | B Impact | ground hit |
| **BigSplash / Shower / WaterLeak** | Fountains, wet caves, leaking pipes, water hits | A Ambient / B Impact | `VfxEmitter` water props — candidate |

### 2.7 Materialize / spawn / portals  ·  `SpawnEffect.cs` (the only scripted recipe), `Portal_Enter/Exit`
| Pack recipe | Moment | Family | Facade |
|---|---|---|---|
| **Dissolve / Respawn** | Enemy/summon materialize + despawn; teleport | Scripted (shader cutoff ~2s) | dedicated — use only where a dissolve is wanted |
| EnergyExplosion / orbs | Portal enter/exit burst | B Impact | `Portal_Enter`/`Portal_Exit` (already exist) |

### 2.8 Weather / big set-pieces  ·  Legacy folder + `WeatherManager.cs`
| Pack recipe | Moment | Family | Facade |
|---|---|---|---|
| RainEffect | Rain weather state | A Ambient | `WeatherManager` — candidate |
| LightningStormCloud | Storm set-piece / storm-tower theme | A/B | candidate |
| WaterFall | Environment water feature (hub/dungeon) | A Ambient | scene prop — candidate |
| PlasmaExplosion | Big magic set-piece / ultimate | B Impact | candidate |

---

## 3. Priority (what earns wiring first vs. "benched but mapped")

| Tier | Items | Why |
|---|---|---|
| **P1 — ship now (WO-884)** | Boss breath (verify), turret muzzle+fire/ice projectile+impact, hero fire/ice cast+impact, dungeon candles, rising steam | The owner's five named asks; highest player-felt traffic |
| **P2 — near** | Enemy death upgrades (Small/Big/Dust), FleshImpacts on-hit, brazier/hearth LargeFlames, PressurisedSteam trap | Cheap oneshots/loops over existing hooks |
| **P3 — content-driven** | FireBall spit, WildFire ground burn, GoopSpray poison, PoisonGas hazard, EarthShatter meteor, water props | Attach to systems as they mature |
| **P4 — set-piece / later** | BigExplosion boss death, Dissolve/Respawn, Legacy weather (Rain/Storm/Waterfall/Plasma) | Scripted or scene-scale; not on the critical path |

---

## 4. Rules that apply to all of them (from WO-759/884)
- Keep multi-layer prefabs WHOLE (never flatten). Quality tiers DISABLE children, never delete.
- Aim = socket rotation (`AimAt`), never rewrite the particle Shape angle (angle = spray width).
- One bus only: everything routes through `VFXManager` via the `Vfx` facade. No `Instantiate` outside the pool.
- Continuous → hold a `VFXHandle`, `Stop()` on end/death. Burst → fire and let the pool reclaim.

---

## 5. Shippability note (gitignored pack)
The pack lives under `Assets/UnityTechnologies/**` which is **gitignored**. Per WO-785, do NOT
point catalog rows at pack paths. Bring each needed recipe into committed `Resources/VFX/**` via a
`BossFireBreathBuilder`-style `CopyAsset` editor script (whole-tree verified, idempotent), then
wire the catalog row to the committed copy. Missing-on-clone degrades to procedural (safe).

---

## 6. Loop-budget caution
`VFXManager._maxActiveLoops = 20`. Ambient loops (candles, steam, braziers, fog, motes) share it.
A dressed dungeon can blow the budget → silent drops. Raise the cap for dungeon scenes or emit only
for nearest-N / on-screen fixtures (WO-884 §6). Never place 30 candle loops against a 20 budget.
