# VFX Creative Picks Registry — Elemental Wheel × 6-Beat Kit (for owner ratification)

**Author:** UI seat (consolidating 4 creative-SME determinations) · **Date:** 2026-08-05
**Status:** ✅ RATIFIED by owner 2026-08-05 (see WO-884 §0.2 for the LOCKED contract + constraints). This
registry is DATA for `VfxElementTables` — not a second runtime system. No per-ability particle code.
**Feeds:** WO-884 `VfxElementTables` (the facade's element→VFXType resolution).
**Locked pick-notes:** Ice recipes use COLD motion (slow drift, settle down/out — NOT firefly upward);
portal flame accent stays SECONDARY; dissolve/blink is one-shot (no demo pause-loop).
**Companion:** `docs/vfx/PARTICLE_PACK_UTILIZATION_MAP.md` (the pack inventory + two-family rule).
**⚠ READ §10 FIRST (added 2026-08-06):** a large slice of this registry SHIPPED on 2026-08-05, and
several picks were **REFUSED with measurements** rather than built. §10 also carries the **loop-cap
P0** that the §8 item-9 recommendation was written on top of — the cap was not too small, it was
being LEAKED. Shipped/refused status is annotated inline in §4, §5, §6b, §7, §8 and §9.

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

⚠ **THE FIVE SURFACE ROWS ARE REFUSED (2026-08-06, `4ef2d532`, WO-887)** — on three independent
grounds, each sufficient alone. Marked in the table below; do not re-spec them without an owner
surface-taxonomy ruling. **And the surface SIGNAL DOES NOT EXIST — verified, not assumed:** no
`SurfaceType` field, no physic-material read, no per-material tag; wood palisades, stone walls and
steel gates all share ONE **Structure** layer, and both footstep implementations play a single clip
with no surface query. The nearest real signal is `WallTier` on player walls — a progression index,
not a material. **Defining a surface taxonomy is DESIGN work and belongs to the owner.**

| Hit case | Recipe | SFX |
|---|---|---|
| Physical → flesh (organic foe) | ~~**FleshImpacts**~~ **REFUSED** [B] | flesh thud |
| Physical → armour/metal | ~~MetalImpacts~~ **REFUSED** [B] | metal clang |
| Physical → stone/wall | ~~StoneImpacts~~ **REFUSED** [B] | stone |
| Physical → wood (barrel/crate) | ~~WoodImpacts~~ **REFUSED** [B] | wood |
| Physical → dirt/sand | ~~SandImpacts~~ **REFUSED** [B] | — |
| Generic physical on-hit *(owner call)* | **SmallExplosion** | `Shockwave` |
| Fire proc | TinyExplosion + TinyFlames cling | `FireExplosion` |
| Ice proc | IceLance shard burst | — |
| Arcane proc | EnergyExplosion | `ArcaneExplosion` |
| Nature/poison proc | ~~GoopSpray + GoopStream puddle~~ **UNREACHABLE** — `DamageElement` is `{None, Aether, Flame, Ice}`; **this game has no nature element**, so `GoopSpray` can NEVER be selected at all *(verified 2026-08-06, `4ef2d532`)* | — |
| Ranged release (any) | MuzzleFlash | `TowerShot`/bow |

[B] **The three refusal grounds** (`4ef2d532`): **(1) DEMO GEOMETRY** — all five carry, on the
prefab ROOT, a MeshFilter with a built-in primitive, a MeshRenderer with a pack material and a
**SPHERE COLLIDER**; copying one would render a lit primitive and **ADD A PHYSICS COLLIDER at
every hit**. (`MuzzleFlash` has none of the three, which is exactly why it WAS safe to take.)
**(2) CONTINUOUS AT THE AUTHORITY** — all five emit 5/sec on loop, so each is a loop-cap leak
waiting to happen. **(3) NO ENUM HOME** — there is no `Impact_Flesh` / `_Metal` / `_Stone` /
`_Wood` / `_Dirt`.
**Also refused in the same pass:** re-pointing `Impact_Flame` / `_Ice` / `_ExplosionAether` /
`_Physical` at pack recipes — they already point at **deliberate tracked picks**, including the
Lana slash arc the owner ruled for on 2026-08-02.

---

## 5. DEATH ladder (escalation by scale+motion, all Burst + optional lingering loop)

**SHIPPED 2026-08-05 (`29f9ac2b`, WO-886)** with three exceptions and three bugs the work order
could not have known about — see the notes under the table.

| Death (VFXType) | Recipe | Lingering | SFX |
|---|---|---|---|
| Death_Generic | SmallExplosion | — | `EnemyDeath` |
| Death_Skeleton (Hollow) | ~~SparksEffect (bone-grey) + SmokeEffect wisp ⚠approx~~ **REFUSED, NOT FAKED** [A] | short wisp | `EnemyDeath` |
| Death_Wolf | ~~SparksEffect (crystal) + slow Steam drift ⚠snow-gap~~ **REFUSED, NOT FAKED** [A] | snow drift | `EnemyDeath` |
| Death_Tiefling | SmallExplosion (ember) | brief WildFire lick | `EnemyDeath` |
| Death_Brute (golem) | **DustExplosion** (500-grain) | SmokeEffect settle | `EnemyDeath` |
| Death_EnemyExplosion_Dungeon | EnergyExplosion | — | `EnemyDeath` |
| Elite_Death | EnergyExplosion (full) | SmokeEffect column | `EnemyDeath` |
| **Boss_Death** / Death_Boss | **BigExplosion** (8-layer, whole) | WildFire OR SmokeEffect column | `EnemyDeath` + ~~0.7 shake~~ [D] |

*(Point BOTH `Death_Boss` legacy alias and `Boss_Death` at BigExplosion so they can't drift.)*
**Done, and the drift was already live** (`29f9ac2b`): `Death_Boss` sat on the **3f** fallback
case while `Boss_Death` sat on **4f** — now merged onto ONE case sharing ONE prefab. `Boss_Death`
also previously pointed into the **gitignored Spells Pack** and rendered nothing on a clone.
**`Elite_Death` had no catalog row at all**, so elites were dying as plain Hollow trash — the
species check tested FAMILY before ROLE.

[A] **`Death_Skeleton` and `Death_Wolf` are REFUSED, not faked** (2026-08-06): their ratified
recipe `SparksEffect` **MEASURES CONTINUOUS** (80/sec on loop at the ROOT; its only burst is a
0.2 s child, which is not the derivation authority). Cataloguing it would either hand a
rate-emitting loop to a fire-and-forget death — **the loop-cap P0 straight back** — or force a
burst flag onto a live emitter. **They keep their tracked Lana rows.**

[D] **THE 0.7 BOSS DEATH SHAKE IN THIS DOC'S OWN ACCEPTANCE CRITERIA HAS NEVER FIRED.**
`EliteVFXController` is attached to NOTHING — its GUID appears in **zero prefabs and zero
scenes** — so its `GetComponent` always returned null and `OnEliteDeath` has **never run in the
shipped game**. Every kill, boss included, got the flat **0.18**. The tier rule is now lifted
into **statics** that both the Enemy death path and the controller call, rather than
auto-attaching the component (which would also switch on an aura light pulse and a dramatic
spawn routine — **three unrequested felt changes under a death-VFX ticket**).

⚠ **BOSS DEATHS WERE DETONATING TWICE.** Every explosion in this pack ships `looping:1` +
`prewarm:1` with its whole payload in a **burst at t=0**, while the pool reclaims at
duration + max lifetime (**~4.3 s on BigExplosion**) — so the burst **re-fired at t=2**.

**SFX pairing note (`a186c282`):** `playSound` is **false** at all four newly-connected sites.
The most load-bearing is the HERO's own death, because `VfxToSfx` maps every `Death_*` to
`EnemyDeath` — a defaulted `playSound` would fire the **ENEMY death sting when the player
dies**. And **`Death_Wolf` and `Death_Tiefling` are deliberately NOT mapped**: the roster has
exactly three families (hollow, orc, troll), so routing them at an orc or a troll is a **creative
pick and the owner's call**.

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
colour-free tell). ~~⚠ **Standing flag:** today's wounded tell is a **red** edge vignette
(`HeroInjuredVignette`) — invisible to a red/green-colourblind owner. These world-space recipes must
become the PRIMARY read.~~ → **SHIPPED 2026-08-05 (`1534dffb`, WO-888) — the flag is CLOSED.
See "DELTA 2026-08-06" immediately below** *(corrected 2026-08-06: this was a standing flag, it is
now implemented; the vignette was kept, not deleted)*.

#### DELTA 2026-08-06 — the low-health tell is no longer COLOUR-ONLY (`1534dffb`, WO-888)

- Severity drives **three greyscale-legible channels AT ONCE**: **PULSE RATE 0.85 Hz rising to
  3.2 Hz**; **GUTTERING DEPTH** — the trough falls to a **tenth** of authored density, so near
  death the effect nearly goes OUT between beats and snaps back; and **simulation speed**, so
  recovery reads as a SNAP rather than a drift.
- **Below a quarter health the RECIPE SWAPS**, smoke wisps to a candle gutter: **a SHAPE change,
  not a hue change.** Healing is the opposite vocabulary — a calm steady rise.
- **The red vignette STAYS as a REDUNDANT cue rather than being deleted.** Redundancy is good
  accessibility; **colour-ONLY was the bug.** (This supersedes item 7 in §8, which said "demote
  the red vignette".)
- **Mutual exclusion is STRUCTURAL, not behavioural:** exactly ONE handle field, so two HP auras
  at once is **unrepresentable** rather than merely unlikely. Priority **near-death >
  low-health > healing**, so a danger read is never masked by a comfort read.
- **Every loop has a proven stop on every exit** — state change, healed above the cutoff, death
  (including an explicit stop on the lethal-hit line so it dies on the SAME FRAME as the death
  burst), `OnDisable`, `OnDestroy`, scene unload — **plus a WATCHDOG**, because a held loop whose
  driver is disabled would strand a slot forever. A refused start does not latch, so it
  self-heals when a slot frees. **Worst case this adds 3 of the 20 loop slots** (see §8 item 9
  and the loop-cap P0 in the §9 DELTA).
- **AN UNGUARDED HOLE FOUND AND CLOSED:** driving a pulse means mutating a **POOLED instance**,
  and the pool resets only what it changed itself — so the next effect to use that slot would
  silently inherit the modulation forever, with no way to trace it back. A modulator now keeps
  the pristine baseline **ON the instance** and restores from **BOTH ends**: the handle's stop
  AND the pool's return (which also covers the timed return and the destroyed-host sweep).
- Low-health and near-death auras are authored **MinQuality 0** against the ambient default of 1,
  **deliberately**: a survival read that vanishes on a low-end device reintroduces the very bug
  it exists to fix.
- **REFUSED, with measurements:** `Cast_Heal` and `Impact_Heal` are fire-and-forget one-shots
  whose ratified §6a recipes **measure CONTINUOUS** (3/sec and 5/sec on loop), so repointing them
  would **leak a loop slot per cast**; the **arcane gear aura is rate-0 with a single burst**, so
  held as a loop it pops once then occupies a slot showing nothing. **Fire and frost gear auras
  derive continuous and ARE served.**
- **TWO OPEN ITEMS FOR THE OWNER.** (1) `Cast_Heal`'s committed row is a **green glow**, so the
  heal CAST beat still reads partly by HUE even though the HP STATE no longer does — a second
  accessibility pass. (2) **The §6c item heal aura is INERT until she tags an accessory** —
  picking which relic glows is a creative call and the standing rule is to map an owner-tagged
  key VERBATIM, never to pick one. **Only the flameblade carries element data today**, so the
  fire smoulder is the one gear aura with live data.
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
| **Enemy_Spawn** *(NEW enum)* | Respawn via SpawnEffect (cutoff, bottom-up) — **DEFERRED, NOT FAKED** [C] | scripted | — |
| Elite_Spawn | EnergyExplosion (upward, dark) | B | — |
| Boss_Spawn | BigExplosion + Legacy LightningStormCloud accent | B | shake |
| Summon (necromancer/pet) | Respawn cutoff + Area_generic ground swell | scripted+A | — |
| **Despawn_Dissolve** *(NEW enum)* | Dissolve via SpawnEffect (cutoff reversed) — **DEFERRED, NOT FAKED** [C] | scripted | — |

[C] **DEFERRED (2026-08-06, `a12c6d22`)** — these two are **SCRIPTED recipes** carrying a pack
MonoBehaviour plus a demo mesh to dissolve. They need a runtime component driving the **TARGET's**
material cutoff. That is **authoring work, not a copy**, so they were left unbuilt rather than
shipped as a lookalike. 14 of the 16 appended enum values WERE built (marker
`PARTICLE_PACK_VFX_BUILD_OK`); these are the two that were not.
**Related pick note:** `Env_Candle` uses **`TinyFlames`, not the pack's `Candles`** — `Candles`
carries candle GEOMETRY (three mesh renderers).

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
| 7 | Colourblind low-HP fix | ~~Make the §6b world-space HP auras the primary read; demote the red vignette.~~ **SHIPPED 2026-08-05 (`1534dffb`, WO-888)** — auras are now the primary read via pulse rate / guttering depth / sim speed + a sub-quarter RECIPE SWAP. **Amendment: the vignette was KEPT as a redundant cue, not demoted/deleted** — redundancy is good accessibility; colour-ONLY was the bug. *(corrected 2026-08-06)* | The current red-vignette-only tell is invisible to you — this is a real bug, not a nicety. |
| 8 | Lightning | **Keep procedural** (skip the gitignored Legacy dependency). | Only one ability (knight.thunderbolt) needs it; not worth a Legacy-folder dependency. |
| 9 | Loop budget | **Raise `_maxActiveLoops` for dungeon/combat scenes AND add nearest-N gating on enemy/pet auras.** | Both cheap; prevents silent aura drops in a dressed dungeon or a fire-DoT fight. |

**Sequencing recommendation:** ship WO-884's five P1 deliverables first (they're already spec'd), then wire the
ratified registry picks in the order Death → On-hit → Heal/HP → Auras → Harvest/Structures → Portals/Spawn.

---

## 9. Guardrails (apply to every ratified pick)
- **Gitignored pack (§5 of the map):** never point a catalog row at `ParticlePack/**`. Mirror each ratified prefab into committed `Resources/VFX/**`; missing-on-clone degrades to procedural.
  > ⚠ **CORRECTED 2026-08-06 (`948080f5`) — a `CopyAsset` script is NOT sufficient, and this guardrail as originally written shipped the bug.** **`CopyAsset` duplicates the PREFAB ONLY** — never its materials, textures, shaders, meshes or animations. So every prefab mirrored this way was a **tracked file pointing straight back into gitignored art**: measured at **27 of 28 prefabs, 183 references, 73 distinct assets**, all rendering missing on any machine without the packs. Exposure reached a mesh, a nested pack prefab pulled in through the ParticleSystem **LIGHTS** module, two `.anim`, a `.controller` and two C# MonoBehaviours (**the two scripts could not be mirrored and were STRIPPED — felt-visible: `Casting_Fire` no longer spawns a projectile**). Now **0**, with **~23.85 MB deduped into `Resources/VFX/_Shared/`** and a standing regression (**`VFX_ART_MIRROR_OK`**) that fails on any dependency in a gitignored root. **A mirror must also RE-SEED from what it already mirrored** (`29f9ac2b`) — seeding only from prefabs meant later runs skipped the already-mirrored material and never found the pack texture *it* referenced: **six prefabs read as self-contained while their art was one hop away. A fixed point has to be fixed ACROSS runs.** Also: **Lana Studio is NOT gitignored** (only its URP upgrade subfolder is) — that assumption was wrong.
- **Keep multi-layer prefabs whole** (FlameThrower, BigExplosion, Candles). Quality tiers disable children, never delete.
- **One bus:** everything through `VFXManager` via the `Vfx` facade + its paired `VfxToSfx` audio. No second stack.
- **Append-only `VFXType`.** New values (`Enemy_Spawn`, `Despawn_Dissolve`, and WO-884's `Env_Candle`/`Env_SteamVent`/`Env_SteamBurst`/`Cast_MuzzleFlash`) append at the end. **Now proven, not just asserted (`0011b8ba`, 2026-08-06): the catalog serialises `VFXType` by ORDINAL, not by name**, so an insert anywhere above would silently re-point every row below it at the wrong art. Verified after the 16-value append: `Boss_FireBreath` still reads `Type: 79` in `VFXCatalog.asset`. **Never insert, reorder or delete — append only.**
- **(added 2026-08-06, `a12c6d22`) A row written by a BUILDER alone is silently dropped.** `VFXCatalogGenerator.Build()` does `entries.arraySize = rows.Count`, so map entries MUST land in `VFXCatalogGenerator` alongside the rows — otherwise the next regenerate erases them and the effect falls back to something that still looks like it works.
- **(added 2026-08-06) Measure emission off the REAL ASSET, never off this document.** Every builder in the 08-05 wave proves the family from the prefab and hard-fails on a mismatch; `IsLoop` resolves through the ONE shared `VfxLoopFlagRegression` resolver, never a second local derivation.

---

## 10. DELTA 2026-08-06 — the loop-cap P0 this registry was written on top of

*Sourced from `bd532d5b` (+ `3db877d2`). This is the single most load-bearing correction to §8
item 9 ("raise `_maxActiveLoops`"): the cap was not too small, it was being **LEAKED**.*

**THE P0.** `IsLoop` in the VFX catalog was a **sticky manual UI checkbox** that
`VfxCasterWindow` **FORCE-SET true** for any row tagged Projectile or Aura. Nothing ever read
the prefab's actual emission. **95 of 135 Hovl rows carried `IsLoop:1`**, including every
`PP_*Impacts` and `PP_MuzzleFlash` — all **single bursts at t=0**.

**Why that is a leak and not a cosmetic flag.** **A loop row never returns its slot.** The
oneshot branch registers a deadline and gets swept; the loop branch does a bare `++` and hands
back a handle, and the only loop reclaim frees **DESTROYED hosts** — and pooled objects are
never destroyed. **The cap is 20.** So a loop played fire-and-forget costs **one of the 20 global
slots for the rest of the session**. The archer and ballista fire `PP_MuzzleFlash` and **discard
the handle**, so after roughly **twenty shots a tower renders NO projectile at all** — and
simultaneously **starves the Tree of Life aura and every POI marker**.

**THE PROVING LINES (§12 — captured data, not inference).** `ArcherTower_Projectile`,
`ARcaneTower_Projectile`, `ArcaneTower-Baselevel_Projectile`, `Poi_NodeAura` and `Poi_Landmark`
all appear in `break-log` as **"SKIPPED - active loops 20/20"** across **six F8 sessions on two
dates**. All five have now flipped from loop to burst — **they were filling the cap that then
starved them.** `Poi_NodeAura` / `Poi_Landmark` point at files literally named
"...loop.prefab" that emit ONE burst and stop, which is why the mistake looked reasonable.

**THE FIX — derive, don't tick a box.** Both catalog generators now **DERIVE** `IsLoop` from the
art, and the rule is stated **once, in one place**: `main.loop` AND a positive rate over time or
distance, with emission enabled. **The authority is the ROOT system UNLESS the root cannot
emit**, in which case it falls through to the first system that can — Lana's `Fire_medium.prefab`
is a root with its emission module **DISABLED** over a child emitting 15/sec, and strict
root-reading would have called the burning-structure, torch and fog auras one-shots and cut them
off mid-burn. **53 of 122 picks were wrong.** `VfxCasterWindow`'s checkbox is now **read-only and
derived**; the role-based force-set is deleted. `VFXManager` gained a guard: **a loop with a
declared finite lifetime is a timed effect and routes through the leak-proof oneshot path** (no
row declares a lifetime today — it exists so the next fire-and-forget loop cannot quietly
re-open this). New marker **`VFX_LOOPFLAG_OK`**.

**STANDING OWNER RULINGS OUTRANK THE DERIVATION.** Deriving promoted some genuinely continuous
prefabs TO loops — one of them, the **upgrade fireworks**, is played fire-and-forget (a truthful
flag, the same leak). The owner had already reported **"perma-fireworks"** and ruled it
one-shot. So: **the prefab is the authority on what the art DOES, not on what the game SHOULD
DO.** Standing owner rulings are **PINNED in a table with their reason**, and **every consumer
resolves through ONE method**, so a pin cannot be honoured in one place and forgotten in another.

**STILL OPEN — two items, deliberately not claimed closed.**
1. **NOT YET PROVEN: the ABSENCE of the cap message across a full wave.** Six of six captures
   show it firing; **a fleet run is still owed** before anyone claims the before/after.
2. **A SECOND, SEPARATE SIGNATURE, deliberately NOT bundled:** the **oneshot pool saturates at
   40/40** in three other captures. **Different pool, different reclaim path. The loop fix must
   NOT be assumed to close it.**

**And the framing this whole registry should be read against (`3db877d2`): it was a CONNECTION
problem, not an ART problem.** **26 of 79 enum values are wired to real art with ZERO gameplay
callers** — the PERFECT-hit flash, four per-species death bursts, the enemy caster's bolt. **Six
whole tracked Lana categories sit at 0% usage.** A GUID sweep of **8,795 prefabs and 156 scenes
found ZERO VFX scripts attached anywhere**, which is what makes `EliteVFXController` dead three
separate ways. `a186c282` connected four of them; `Die()` had routed override -> typeSet ->
generic and **never consulted the species**, so the pool/factory spawn path could never reach the
four authored death bursts — species now sits AFTER the authored per-prefab set and before the
generic.

**Tower flavour, fixed in the same wave (`4ef2d532`, WO-887):**
`TowerCombat.OnProjectileImpact` **computed the projectile's element EIGHT LINES BELOW the impact
pick and never used it**, so **every empowered tower burst as `Impact_ExplosionAether`** — a fire
tower's bolt detonating in violet arcane light with the arcane bang over it. **Element now
decides FLAVOUR, tier decides SIZE**; non-empowered towers keep their exact existing ladder, and
routing by element also routes the paired `SfxId`, fixing the sound as a side effect. Separately,
**`FireAt` was playing `Projectile_TowerArcane` — a PROJECTILE-BODY row with `IsLoop` TRUE — as a
muzzle flash**: another fire-and-forget loop on the busiest call in the game. Replaced with the
tracked burst.

---
*Source determinations: 4 creative-SME passes (spells+on-hit, on-death, heal+auras, portals+spawn),
2026-08-05, each grounded in abilities.json + the roster code + the verified pack inventory.*
