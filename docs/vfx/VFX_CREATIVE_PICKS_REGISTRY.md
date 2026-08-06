# VFX Creative Picks Registry — Elemental Wheel × 6-Beat Kit (for owner ratification)

**Author:** UI seat (consolidating 4 creative-SME determinations) · **Date:** 2026-08-05
**Status:** ✅ RATIFIED by owner 2026-08-05 (see WO-884 §0.2 for the LOCKED contract + constraints). This
registry is DATA for `VfxElementTables` — not a second runtime system. No per-ability particle code.
**Feeds:** WO-884 `VfxElementTables` (the facade's element→VFXType resolution).
**Locked pick-notes:** Ice recipes use COLD motion (slow drift, settle down/out — NOT firefly upward);
portal flame accent stays SECONDARY; dissolve/blink is one-shot (no demo pause-loop).
**Companion:** `docs/vfx/PARTICLE_PACK_UTILIZATION_MAP.md` (the pack inventory + two-family rule).

---

## 1. The model (what the owner defined)

A spell / tower shot / attack is a sequence of **beats**, all reading as ONE coherent idea, with the
**element** as the selector. Beats (ordered lifecycle):

| # | Beat | Family | Facade family | Paired sound? |
|---|------|--------|---------------|---------------|
| 1 | **Aura** — passive elemental presence at rest (also from HP-state / items) | A continuous | `Aura`/`Ambient` | optional loop sfx |
| 2 | **Wind-up** — charge/gather on the caster | A continuous (short) | `Cast` | cast sfx |
| 3 | **Telegraph** — the *warn* beat, read **as CASTING**: a visible charge/gather build-up (you watch it cast → that IS the warning). Shares the wind-up's gathering language. | A→B (gather then pulse) | `Cast`→`Impact` | cast/warn sfx |
| 4 | **Release** — launch flash at origin: **Cast**(hero hand) or **Muzzle**(tower barrel) | B burst | `Cast`/`Muzzle` | release sfx |
| 5 | **Traversal** — projectile path, OR a continuous channel (stream spells) | A continuous | `Projectile`/`Stream` | travel loop sfx |
| 6 | **Hit** — impact at the target | B burst | `Impact` | impact sfx |

**Caster-agnostic:** hero spells AND tower shots pull the same element kit; only beat 4 differs
(hand cast vs barrel muzzle). A tower usually skips beats 1–3.
**Audio:** each beat can call a sound clip. `VFXManager` already auto-fires a paired `SfxId` via its
`VfxToSfx` map (`VFXManager.cs`), so wiring sound = extending that map — no new bus.
**Colourblind law (owner red/green):** every beat reads by **SHAPE + MOTION-direction + timing**, never
hue. Fire = chaotic *upward* flames · Ice = *angular outward* shards · Arcane = *symmetric radial ring* ·
Nature/Poison = *viscous downward* globs · Physical = *grey radial* debris · Holy = *soft rising column*.

---

## 2. THE ELEMENTAL WHEEL (the reusable engine)

All prefab names are Particle Pack recipes (see §7 for the gitignored-pack committing rule). "proc" =
keep the existing procedural `AbilityVfxKit`/`Cast_*` path. "custom" = no pack match, needs authoring.

| Element | 1 Aura | 2 Wind-up | 3 Telegraph | 4 Release (cast / muzzle) | 5 Traversal | 6 Hit | Paired SFX | Coherent identity |
|---|---|---|---|---|---|---|---|---|
| **Fire** ★ | TinyFlames (cling) | TinyFlames/MediumFlames gather | EarthShatter ground-ring *(AoE only)* | hand flame burst / **MuzzleFlash** | **FireBall** (orb+trail) | **SmallExplosion** → BigExplosion (ults) | `FireExplosion` | chaotic upward flame + rising embers; round orb |
| **Ice/Frost** | DustMotes drift ⚠snow-gap | proc / IceLance gather | EarthShatter ring (blue) | proc / MuzzleFlash | **IceLance** | IceLance shard burst | *needs ice sfx* | angular outward crystalline shards |
| **Arcane** | ElectricalSparks *(enemy)* / proc | proc `Cast_MageCharge` | — | EnergyExplosion core / MuzzleFlash | proc `Projectile_ArcaneBolt` / EnergyExplosion orb | **EnergyExplosion** | `ArcaneExplosion` | symmetric radial energy ring |
| **Nature/Poison** | PoisonGas | proc | — | MuzzleFlash | **GoopStreamEffect** | **GoopSpray** + puddle | *needs poison sfx* | viscous downward globs that pool |
| **Physical** | GroundFog / DustMotes | proc `Cast_KnightSlam` / MuzzleFlash (bow) | — | **MuzzleFlash** (ranged) | **RocketTrail** (arrow/spear streak) | **surface-typed** (see §4) / **SmallExplosion** generic | `Shockwave` / impact thud | grey radial debris scatter |
| **Lightning** | ElectricalSparks | ElectricalSparks | — | ElectricalSparks | ElectricalSparksEffect / Legacy LightningStormCloud | ElectricalSparks | *needs sfx* | jagged branching sparks ⚠Legacy-dep |
| **Water** *(new)* | proc | proc | — | Shower splash | *no travel today* | **BigSplash** | *needs sfx* | mapped; no ability uses it yet |
| **Steam** | RisingSteam | — | — | PressurisedSteam (jet) | RisingSteam (channel) | Env_SteamBurst | — | rising vapour column |
| **Wind** *(new)* | **custom** (DustMotes/steam motion approx) | custom | custom | custom | custom | custom | — | ⚠ **NO pack recipe — needs authoring** |
| **Holy** | proc `softhealingaura_Aura` | proc `Cast_Heal` | — | proc | — | proc `Impact_Heal` | `Heal` | ⚠ **NO pack recipe — soft rising column, procedural** |
| **Shadow** | PoisonGas | proc | — | proc | proc | EnergyExplosion (dark) | — | dark roiling miasma |

★ Fire is the only element with a full, native pack kit end-to-end. Mage leans here + Arcane (owner:
"Mage lives heavily in the magic realm").

---

## 3. Ability → element tags (abilities inherit the wheel)

From `abilities.json`. Each ability just carries an element + which beats it uses.

**Mage — Thrain (richest):** fireball=Fire(full) · meteor=Fire(+EarthShatter telegraph, BigExplosion hit) ·
frost-nova=Ice(EarthShatter ring + IceLance shards) · arcane-bolt=Arcane · void-rift=Arcane/Shadow(EarthShatter
tear + PoisonGas hold) · cataclysm=Fire(PlasmaExplosion/BigExplosion) · blink=Dissolve(scripted) · **shell / heal /
manaweave = Holy/Arcane utility → procedural (no pack)**.
**Knight:** q-dash=Dissolve+MetalImpacts · w-charge=StoneImpacts+DustExplosion · r Radiant=Fire/Holy(BigExplosion+ring) ·
thunderbolt=Lightning · emberbrand=Fire(+WildFire ground DoT) · throwing-spear=Physical(RocketTrail+MetalImpacts) ·
**all heals/wards = procedural**.
**Ranger — Sylas:** q=Physical(MuzzleFlash+RocketTrail+FleshImpacts) · snare-trap=Ice/Nature · healing-shot=Nature(GoopStream
drain)+heal · storm-of-arrows=Physical(EarthShatter footprint + FleshImpacts volley) · tumble=DustExplosion.
**Cleric:** not in abilities.json today — if added, its holy kit is **custom/procedural** (pack can't serve it).

---

## 4. ON-HIT (weapon connects — surface-typed; all Burst)

| Hit case | Recipe | SFX |
|---|---|---|
| Physical → flesh (organic foe) | **FleshImpacts** | flesh thud |
| Physical → armour/metal | MetalImpacts | metal clang |
| Physical → stone/wall | StoneImpacts | stone |
| Physical → wood (barrel/crate) | WoodImpacts | wood |
| Physical → dirt/sand | SandImpacts | — |
| Generic physical on-hit *(owner call)* | **SmallExplosion** | `Shockwave` |
| Fire proc | TinyExplosion + TinyFlames cling | `FireExplosion` |
| Ice proc | IceLance shard burst | — |
| Arcane proc | EnergyExplosion | `ArcaneExplosion` |
| Nature/poison proc | GoopSpray + GoopStream puddle | — |
| Ranged release (any) | MuzzleFlash | `TowerShot`/bow |

---

## 5. DEATH ladder (escalation by scale+motion, all Burst + optional lingering loop)

| Death (VFXType) | Recipe | Lingering | SFX |
|---|---|---|---|
| Death_Generic | SmallExplosion | — | `EnemyDeath` |
| Death_Skeleton (Hollow) | SparksEffect (bone-grey) + SmokeEffect wisp ⚠approx | short wisp | `EnemyDeath` |
| Death_Wolf | SparksEffect (crystal) + slow Steam drift ⚠snow-gap | snow drift | `EnemyDeath` |
| Death_Tiefling | SmallExplosion (ember) | brief WildFire lick | `EnemyDeath` |
| Death_Brute (golem) | **DustExplosion** (500-grain) | SmokeEffect settle | `EnemyDeath` |
| Death_EnemyExplosion_Dungeon | EnergyExplosion | — | `EnemyDeath` |
| Elite_Death | EnergyExplosion (full) | SmokeEffect column | `EnemyDeath` |
| **Boss_Death** / Death_Boss | **BigExplosion** (8-layer, whole) | WildFire OR SmokeEffect column | `EnemyDeath` + 0.7 shake |

*(Point BOTH `Death_Boss` legacy alias and `Boss_Death` at BigExplosion so they can't drift.)*

---

## 6. ON-HEAL, HP-STATE, AURAS

### 6a. On-heal (restoration reads by rising shape + heal number, never green)
| Moment | Recipe | Family | SFX |
|---|---|---|---|
| Cast_Heal (heal cast / beacon) | **RisingSteam** warm column | A loop→Stop | `Heal` |
| Impact_Heal (contact) | **FireFlies** upward burst | B | `Heal` |
| Regen-over-time (`RegenTick`) | RisingSteam low loop (swap the per-tick pop) | A loop | `Heal` (soft) |
| Mage Arcane Shell (`shell`, 4s) | HeatDistortion dome + sparse DustMotes shell ⚠subtle on mobile | A loop | ward sfx |
| Mage Manaweave (3s) | DustMotesEffect drawn **inward** | A loop | — |

### 6b. On-HP (HP-state driven — owner "on hp") — CONFIRMED hooks
All fire from `HeroHealth.UpdateInjuredState()` (L1166, called every frame off the single HP source) +
`RegenTick()` (L1107). HP fraction drives **pulse rate / emission** (mirrors `HeartAuraController`'s
colour-free tell). ⚠ **Standing flag:** today's wounded tell is a **red** edge vignette (`HeroInjuredVignette`)
— invisible to a red/green-colourblind owner. These world-space recipes must become the PRIMARY read.
| State (proposed VFXType) | Trigger | Recipe | Family | Read |
|---|---|---|---|---|
| **Aura_LowHealth** (Fraction < 0.30) | injured latch L1171; severity `InverseLerp(0.30,0,Frac)` L1196 | **SmokeEffect** guttering wisps, pulse faster as HP falls | A loop→Stop when healed | fast urgent pulse = danger, by rhythm+guttering shape |
| **Aura_NearDeath** (Fraction < 0.25) | AegisAutoThreshold L76 | **TinyFlames** fast gutter (candle-about-to-die) | A loop (sub-tier) | near-panic cadence + shrinking flame |
| **Aura_HealingInProgress** | `RegenTick` L1107 while amount>0 | **RisingSteam** low, calm **upward** | A loop→Stop | shares heal rising-language; opposite motion to gutter |
| HP-threshold cross (wounded) | `injured != _injured` L1173 | **DustMotesEffect** downward settle | B one-shot | a single settling puff marks the drop |
Ladder: RisingSteam rise (healing) → SmokeEffect slow pulse (wounded) → TinyFlames fast gutter (critical).

### 6c. Item-granted auras (owner "items (healaura)") — CONFIRMED hook
Seam = `GearVisualApplier.Apply(body, loadout)` (L41), called after `HeroBodySwapper` + on every equip change.
Propose a small **`GearAura`** held-loop component here (mirror `ArcaneAura.Ensure/SetAuraKey` + `Pets/AuraController`),
keyed by the item's element/aura + tiered by `GearProgression`. The **Aura beat sourced from an item, not a cast.**
| Item aura (proposed) | Source | Seat | Recipe | Note |
|---|---|---|---|---|
| **Aura_ItemHeal** (HEALAURA) | heal relic/amulet | body/chest | **RisingSteam** low held | the one new item aura — reuses heal rising-language |
| Fire weapon smolder | fire weapon | RightHand socket | **TinyFlames** faint | reuses Aura_Flame on the weapon bone |
| Frost weapon chill | frost weapon | weapon socket | DustMotes drift | reuses Aura_Ice ⚠snow-gap |
| Arcane weapon hum | arcane weapon | weapon socket | ElectricalSparks faint | reuses Aura_EnemyCaster |
Pattern: gear grants a persistent aura by attaching one `GearAura` keyed to the item's element — elemental
weapon auras reuse the §6d recipes at "faint"; only Aura_ItemHeal is genuinely new (and it borrows heal language).

### 6e. Harvest / economy — CONFIRMED
Resource set (reconciled): **Iron, Wood, Food, Crystals, Gold**. All auras Family A (held while harvesting,
`Stop()` on idle/depleted). Differentiated by **motion vector**, not colour (the sparkle trio Iron/Crystal/Gold
splits by motion). Hosts: `NodeFillIndicator` (node collecting/ready states), `CollectorStackView` (the "I am full"
tell — already built + wired at `StructureFactory:767`).
| Resource | Recipe | Element tag | Reads-as (motion) |
|---|---|---|---|
| **Iron** ★ | DustMotesEffect + SparksEffect | Physical (`Aura_Dust`) | heavy dust **settling** + metal spark glint ("aura of dust for iron", literal) |
| **Wood** | DustMotesEffect (flat drift) | Nature | flat **sideways-drifting** chip motes ⚠approx (no leaf/sawdust recipe) |
| **Food** | FireFlies (sparse) | Nature | light motes **rising** slowly (pollen) ⚠approx (no crop recipe) |
| **Crystals** | FireFlies (dense shimmer) | Arcane | **suspended twinkling**, no travel (literal) |
| **Gold** | SparksEffect (bright, short) | Arcane | glint pops that **fall** (coin-shimmer) — motion-split vs Crystal |

**Ready-to-collect beacon** (build ON the existing tell, don't replace): `Collector_Ready` = **FireFlies rising
bob** (`AddAura(Holy)`, low emission) — rising = "come pick me up", colourblind-safe by upward motion + bob.
Fires when `ResourceCollector.IsFull` / node `ready`. Reuses `SfxId.LevelUp` glint. (Alt for distance: RisingSteam.)

### 6f. Structures (Healer + the general pattern) — CONFIRMED
Slots in as **one new `case` in `StructureFactory.AttachBehaviorImpl` (:682)**, cloning `HealingFountain`'s
proven tick-heal + aura-hold body but retargeting Heart→units-in-radius. Wheel element = **Holy** (rising shape).
| Beat | Recipe | Map | Family | SFX |
|---|---|---|---|---|
| 1 · idle heal-field AURA | RisingSteam (low/wide) | `Aura_Healer` (reuse) | A loop | (optional hum) |
| 2 · per-tick CAST pulse (telegraphs-as-casting) | FireFlies upward burst | `Impact_Heal` | B | **`SfxId.Heal`** (auto) |
| 3 · heal CONTACT on unit | FireFlies | `Impact_Heal` | B | `SfxId.Heal` (auto) |

**The general pattern (the payoff):** a new structure = **stats + two tags**. Author a `RepoProps` row with a
`behaviorId`, add one `case` that copies range/fireRate off the repo and runs a radius tick, drop a
`VfxEmitter{ family=Aura, element=X }` for the field, and call `Vfx.On(this).AddImpact(X).At(pos/unit)` on each
tick/contact — the **same three-beat skeleton every time**. Swapping the element tag re-skins the whole structure
with **zero new VFX code**: Healer=Holy · Slow-field=Ice (`Aura_Ice`+`Impact_Ice`) · Damage-aura=Shadow
(`Aura_Necromancer`+`Impact_ExplosionAether`) · Buffer=Arcane (`Aura_EnemyCaster`+`Impact_Aether`). Each already
resolved by `VfxElementTables`, colourblind-safe by the wheel, auto-paired to sound.

### 6d. Persistent auras (all Family A: PlayAura→Stop)
| Aura | Recipe | Note |
|---|---|---|
| Aura_EnemyCaster | ElectricalSparks | crackling conduit |
| Aura_Necromancer | PoisonGas | roiling ground cloud |
| Aura_Healer | RisingSteam (low) | shares heal language |
| Aura_Flame | TinyFlames | body cling |
| Aura_Ice | DustMotesEffect drift | ⚠ **snow-gap** |
| Aura_Dust | GroundFog low | ⚠ fog≠kicked-dust |
| Aura_SmokeReaper | SmokeEffect | best-fit match |
| Aura_HeartPulse | FireFlies | combat/raid Hearts only (hub tree withholds) |
| Aura_EmpowerTower | RisingSteam tinted | scales L1→L3 |
| Aura_PetLevel1/2/3 | DustMotes → FireFlies → FireFlies+Sparks | density escalation |
| Pet_Aura_Fire / Ice | TinyFlames / DustMotes ⚠snow-gap | |
| Boss_Aura_Phase1/2/3 | RisingSteam → MediumFlames → **WildFire** | calm→enraged→seething by scale |

---

### 6g. Building damage-state (owner "burning + smoke on damaged buildings", "know when critical to save before destroyed")
**Already built — do NOT rebuild the observer.** `StructureDamageVisuals.cs` (WO-672) is the ONE presentation
observer: self-installing, reads `HpFraction`/`IsBroken` from `damage-states.json` thresholds, pooled via
`VFXManager.PlayKey`, worst-first burn-loop cap, colourblind-safe. Covers Wall/Building/Tower/Collector
(Gate/Heart opt out). My recommendations (re-skin recipes + close the "critical" gap — approve/veto):
| Damage state | Today | Recommended pack re-skin | Read |
|---|---|---|---|
| Smolder (hp ≤ 0.5) | reduced Ember_Burn Hovl | **SmokeEffect** low (light smoke wisp) | "taking damage" by rising smoke |
| Fire (hp ≤ 0.25) | full Ember_Burn + bar pulse | **MediumFlames + SmokeEffect** | "on fire" by active flame + smoke volume |
| **CRITICAL-save beacon** *(NEW — the gap)* | — (only fire+bar pulse) | **SparksEffect fast-pulse + "!" tag** (mirror the collector-ready beacon, alarm cadence) | **"repair me NOW before it's destroyed"** — urgency by FAST pulse rate, not colour |
| Broken (hp = 0) | Raid_Explosion + persistent ember | **DustExplosion/BigExplosion** one-shot + lingering **WildFire/SmokeEffect** column | destruction + smoking ruin |
Wiring: re-point `StructureDamageVisuals`' recipe keys (data-only, no observer rewrite) + add the critical
beacon at the fire threshold. Loop budget: burn loops already worst-first capped — fold into the §8 nearest-N/cap ruling.

## 7. PORTALS / SPAWN / DESPAWN

| Moment (VFXType) | Recipe | Family | SFX |
|---|---|---|---|
| Env_DungeonPortal (open mouth loop) | **keep procedural vortex** + MediumFlames mouth accent ⚠no-swirl-recipe | A loop | portal hum |
| Portal_Enter | EnergyExplosion (outward) + ParticlesLight | B | — |
| Portal_Exit | EnergyExplosion (inward, mirror) | B | — |
| **Enemy_Spawn** *(NEW enum)* | Respawn via SpawnEffect (cutoff, bottom-up) | scripted | — |
| Elite_Spawn | EnergyExplosion (upward, dark) | B | — |
| Boss_Spawn | BigExplosion + Legacy LightningStormCloud accent | B | shake |
| Summon (necromancer/pet) | Respawn cutoff + Area_generic ground swell | scripted+A | — |
| **Despawn_Dissolve** *(NEW enum)* | Dissolve via SpawnEffect (cutoff reversed) | scripted | — |

---

## 8. UI RECOMMENDATIONS (these are the calls I'm making — veto any, else they stand)

| # | Topic | My recommendation | Why |
|---|---|---|---|
| 1 | Snow gap (Ice) | **Ship the DustMotes/Steam approximation now.** No custom art. | Reads as frost by drift-motion; not worth blocking on a bespoke snow recipe. Revisit only if it looks wrong on screen. |
| 2 | Wind element | **Drop Wind for now.** | No ability/tower uses it. Don't author speculative art; add it the day a Wind ability is designed. |
| 3 | Portal swirl | **Keep the existing procedural vortex + MediumFlames mouth accent.** | The vortex already works; a custom swirl is cost with no clear win. |
| 4 | Holy / heal / shield | **Keep procedural.** | Pack has zero holy recipes; the procedural rising-column already reads right (shape-first). |
| 5 | Blink | **Use the scripted Dissolve/Respawn look.** | It's the pack's one scripted recipe and reads distinctly as a teleport, not a flash. |
| 6 | New enum values | **Approve all** (Enemy_Spawn, Despawn_Dissolve, Aura_LowHealth/NearDeath/HealingInProgress/ItemHeal, WO-884's four, + harvest/structure). | Append-only = zero risk to existing catalog ordinals. |
| 7 | Colourblind low-HP fix | **Make the §6b world-space HP auras the primary read; demote the red vignette.** | The current red-vignette-only tell is invisible to you — this is a real bug, not a nicety. |
| 8 | Lightning | **Keep procedural** (skip the gitignored Legacy dependency). | Only one ability (knight.thunderbolt) needs it; not worth a Legacy-folder dependency. |
| 9 | Loop budget | **Raise `_maxActiveLoops` for dungeon/combat scenes AND add nearest-N gating on enemy/pet auras.** | Both cheap; prevents silent aura drops in a dressed dungeon or a fire-DoT fight. |

**Sequencing recommendation:** ship WO-884's five P1 deliverables first (they're already spec'd), then wire the
ratified registry picks in the order Death → On-hit → Heal/HP → Auras → Harvest/Structures → Portals/Spawn.

---

## 9. Guardrails (apply to every ratified pick)
- **Gitignored pack (§5 of the map):** never point a catalog row at `ParticlePack/**`. Mirror each ratified prefab into committed `Resources/VFX/**` via a `BossFireBreathBuilder`-style `CopyAsset` script; missing-on-clone degrades to procedural.
- **Keep multi-layer prefabs whole** (FlameThrower, BigExplosion, Candles). Quality tiers disable children, never delete.
- **One bus:** everything through `VFXManager` via the `Vfx` facade + its paired `VfxToSfx` audio. No second stack.
- **Append-only `VFXType`.** New values (`Enemy_Spawn`, `Despawn_Dissolve`, and WO-884's `Env_Candle`/`Env_SteamVent`/`Env_SteamBurst`/`Cast_MuzzleFlash`) append at the end.

---
*Source determinations: 4 creative-SME passes (spells+on-hit, on-death, heal+auras, portals+spawn),
2026-08-05, each grounded in abilities.json + the roster code + the verified pack inventory.*
