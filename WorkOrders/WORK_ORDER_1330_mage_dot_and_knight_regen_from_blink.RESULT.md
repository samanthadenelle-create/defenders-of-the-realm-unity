# WORK ORDER 1330 - RESULT

**Status:** FIXED (edit-only lane; NOT gated, NOT committed - the lead gates and commits).
**Date:** 2026-09-02
**Silo:** Abilities / combat over-time effects / VFX matching

---

## 0. THE THREE THINGS TO READ IF YOU READ NOTHING ELSE

1. **The premise about `Assets/Blink` is wrong, and it is worth knowing.** Blink holds **777 prefabs
   of characters, armour, weapons and UI - and ZERO particle/VFX prefabs.** Two of its four
   sub-bundles (`StylizedArmorBundle2/`, `UltimateBundle/`) contain **only a README.txt of Asset
   Store claim links** - the packs were never downloaded. Not one Blink prefab appears in
   `VfxManualPicks.json`. The game's real VFX warehouse is elsewhere (~11,600 prefabs across
   Mirza Beig, Spells Pack, Hovl Studio, Lana Studio, and the project-owned `Assets/Resources/VFX/`).
   **The shortlists in section 6 come from those packs.** Nothing was picked; they are candidates.
2. **The over-time mechanic DID exist on the live path - as FOUR unrelated ad-hoc tick loops**, none
   of them tunable, and with **no mage ability able to reach one.** Section 1 proves it. The owner's
   correction was right about the thing that mattered and the CLI's first read was wrong about the
   thing that mattered; the truth is in between and is what made the design obvious.
3. **`combat.drainReturnPct` now defaults to 60, NOT 100, and that is deliberate.** Owner ruling,
   verbatim: *"keep drain at 60% for now"*. It is the one balance knob whose default is a **ruled
   value** rather than the previously-shipped behaviour. Do not "correct" it back. Section 5.

---

## 1. FIRST DELIVERABLE - WHICH COMBAT PATH IS LIVE, PROVEN FROM SOURCE

### The live path is the real-time `BattleArena`, driven by `HeroAbilities`. `DeNelle.BattleATB` is dead code.

| claim | proof at source |
|---|---|
| `ff.dungeonrealtime` defaults **ON** | `Assets/_Modules/Core/FeatureFlags.cs:292` - `DungeonRealtimeBattle => Get("dungeonrealtime", defaultOn: true)` |
| ON routes to the real-time arena | `Dungeons/EncounterTrigger.cs:376` -> `BattleArena.Instance.BeginEncounter` at `:395`; `Dungeons/DungeonStubEncounter.cs:198` -> `:216` |
| the ATB route is the OFF branch | `EncounterTrigger.cs:437` and `DungeonStubEncounter.cs:227` both call `SceneRouter.GoBattle` **only** in the flag's OFF branch |
| `ATBCombatManager` is never instantiated | `BattleATB/ATBCombatManager.cs:49-68` self-bootstraps **only** when `scene.name` contains `"ATBBattle"`. GUID `16d3662c6e006d949af639a7c5c30869` appears in **zero** scenes or prefabs |
| `BattleController` exists in exactly one scene | GUID `1d4a7d41f469e9d4885e78e1cc71eaaa` occurs once, in `Assets/Scenes/ATBBattle.unity` - which is `enabled: 1` in `ProjectSettings/EditorBuildSettings.asset:23-25` and therefore **shipped but never loaded** |
| the only other ATB entrances are also off | `Waves/WaveManager.cs:3037`, `:3814` gated on `FeatureFlags.WaveBreachToAtb`, default **false** (`FeatureFlags.cs:279`); `HUD/OwnerDevToolsOverlay.cs:262` gated on `ff.devresourcetool`, default **false** (`FeatureFlags.cs:340`) |

**So `DeNelle.BattleATB` is orphaned-but-compiled.** Its `StatusKind.Burn/Poison/Bleed` machinery
(`BattleATB/Engine/Types.cs:114-116`, `Engine/Combat.cs:135`, `Engine/Actions.cs:91-104`) is a
separate, turn-based, unreachable system. **Nothing was built on it.**

### `CombatStatusTracker` IS live - and it is NOT an effect engine. That is the finding.

The WO asked me to say so if it turned out to be off the live path. It is **on** it, but the answer
is more useful than that:

- Two live owners: `Village/Enemies/EnemyDamageable.cs:71` and `Village/Hero/HeroCombatStatus.cs:20`.
- One live consumer, **presentation only**: `Village/HUD/HudModelProducers.cs:1119` and `:1140`,
  polling at 0.20 s.
- **It stores nothing but expiry times.** `Apply` / `ApplyNamed` / `IsBurning` / `CollectActive` -
  no magnitude, no tick, no sink. **No code anywhere reads `IsBurning` to deal damage.** Expired
  entries are pruned lazily inside `CollectActive`; nothing ticks it.

**It is the right home for the HUD ROW and was never a candidate for the TICK.** This change uses
both together, unchanged: the tracker owns the row, the new engine owns the pulse.

> **Load-bearing gap found in passing:** `HeroCombatStatus.ApplyStatus(StatusEffect, float)`
> (`HeroCombatStatus.cs:23`) - the hero's slow/freeze/**burn** entry point - has **ZERO callers
> repo-wide**. Nothing can currently put a burn, slow or freeze on the *hero*. Not this ticket's
> business; reported.

### The `dot`/`burn`/`poison`/`bleed` tokens - I grepped for the CONSUMER of each, as instructed

The single effect-string dispatch is `HeroAbilities.ResolveEffect`, `HeroAbilities.cs:1181-1199`.

| token | consumer? | verdict |
|---|---|---|
| `dot` | **YES** - `case "dot":` `HeroAbilities.cs:1187` -> `ResolveDot` `:2055` | live; 2 abilities author it |
| `healOverTime` | **YES** - `:1188` -> `ResolveHealOverTime` `:2180` | live; 1 ability |
| `overTime` | **NO CONSUMER** as a standalone token - repo-wide it matches only unrelated identifiers | the compound `healOverTime` is the real token |
| `burn` | **not an `effect` value at all** - zero abilities author it. It is an *ammo rider* (`:1825`), a *talent stat discriminator* (`HeroTalentModifiers.cs:535`), and a *HUD id* | the trap the WO warned about |
| `poison` | **not an `effect` value** - `mage.poison` is an **id**; its effect is `"dot"`. Consumers are the Venombrand rider (`:1407` -> `:1475`) and the ammo rider (`:1832`) | the trap again |
| `bleed` | **NO CONSUMER ON THE LIVE PATH.** Only in the dead ATB engine. `DeNelle.Core.Combat.StatusEffect` (`IDamageable.cs:145-153`) carries only `Slow`/`Freeze`/`Burn` - **there is no `Bleed` in the live enum** | authored prose only |

### So what was ACTUALLY missing - and it is not what either reading assumed

**FOUR separate over-time tick loops existed, none tunable:**

1. `HeroAbilities.BurnDoT` - a coroutine, `const float tick = 1f`
2. `HeroAbilities.PoisonDoT` - a **second** coroutine, byte-for-byte the same loop
3. `HeroAbilities._hpOverTime` - a per-**frame** continuous drip in `Update`
4. `Enemy.DamageOverTimeRoutine` (`Enemy.cs:2655`) - a **third** copy, in another file

**And the mage could not reach any of them.** `mage.poison` is the mage's stock **R** - pressable
with no talent at all, so by the exact argument WO-1306 used against granting `mage.drain`, it gave
the player **nothing new to press**. The `mage-skills` pool contained **no DoT at all**.

**The knight's regen is the mirror image:** `knight.oathmend` exists - but at **tier 3, cost 3, five
points down the chain** `knight.t1n2 -> knight.t1n4 -> knight.t3n2`. What the knight lacked was not a
regen; it was an **early** one. This is called out loudly in the data and section 4 gives the owner a
one-word alternative.

---

## 2. THE SHARED MECHANISM - one engine, both signs

**New: `Assets/_Modules/Core/Combat/OverTimeEffects.cs`** (`DeNelle.Core`).

- `OverTimeEngine<TTarget>` - **pure**: no MonoBehaviour, no coroutine, **no `Time.time`**. The clock
  is a parameter (`Advance(now)`) and the sink is a delegate. Copied deliberately from the precedent
  already in this file's neighbour - `HeroAbilities.TickManaOverTime` was extracted "so the drip is
  unit-testable with an explicit clock (EditMode never runs Update)". **An over-time effect built the
  obvious way is one no gate can ever observe**, and §12 forbids shipping on that basis.
- **Generic over the target so ONE body serves both signs** - `OverTimeEngine<IDamageable>` damages,
  `OverTimeEngine<HeroHealth>` heals. Two closed types, one implementation.
- **Magnitude is always POSITIVE; direction lives in `OverTimeKind`.** No call site can heal by
  passing negative damage - the classic sign bug, made unrepresentable.
- **It owns timing and arithmetic ONLY.** Damage still lands via `IDamageable.TakeDamage` (so through
  `Enemy.TakeDamageFrom`, keeping mitigation, damage numbers, attribution and the death check);
  healing still lands via `HeroHealth.RegenTick`, the same sink Oathmend uses. Nothing is bypassed.

### What now runs on it

| was | now |
|---|---|
| `BurnDoT` coroutine, hardcoded 1s | `ApplyBurnDoT` - one line onto the engine |
| `PoisonDoT` coroutine, hardcoded 1s | `ApplyPoisonDoT` - same engine, different id (poison keeps **None** element; it must never wear the fire tell). The `_venomStacks` **cap ledger is untouched** - the engine owns the tick, the ledger owns the policy |
| *(new)* `mage.wither` | `effect: "dot"` - the **shipped** `ResolveDot`, whose tick is now the engine |
| *(new)* `knight.ironblood` | `effect: "regen"` - the engine, sign flipped |

Driven from one place: `TickOverTimeEffects(Time.time)` in `HeroAbilities.Update`.
**Three loops became one. I did not write a second tick loop.**

### What I deliberately did NOT fold in, and why

- **`_hpOverTime` (loop 3) is untouched.** It is a **continuous per-frame** drip; Oathmend and
  Warden's Grace are felt-verified against that smoothness. Converting it to pulses would be a silent
  feel change to two shipped abilities inside a ticket meant to add two. **Converging it is a
  follow-up.**
- **`Enemy.DamageOverTimeRoutine` (loop 4)** is in another file and another lane. Reported.

### Behaviour is preserved tick-for-tick, and the arithmetic is a reproduction, not a redesign

Three properties of the old coroutine are load-bearing and are copied exactly:

1. **The first pulse lands one full interval late**, never on the cast frame - otherwise every DoT
   silently gains a free tick alongside its impact damage.
2. **Pulse count is CEIL, not floor.** The old loop tested *before* it incremented, so 4.5 s at a 1 s
   tick delivered **five** pulses and over-delivered. Rounding that "cleanly" to 4 would be a stealth
   nerf to `knight.emberbrand-throw` and `mage.poison`.
3. **Magnitude per pulse is `perSecond * interval`**, so **total delivery is invariant under the
   cadence knob**. That is what makes the tick dial safe to hand to the owner as a *feel* lever.

---

## 3. TUNABLES REGISTERED

All four sources moved in the same change (Registry, `TUNABLE_KEYS`, the doc, `ExpectedDefaults`),
plus the two WO-1328 sources that did not exist when the WO was written (see section 8).

| key | kind | default | = today? | what it moves |
|---|---|---|---|---|
| `combat.overTimeTickMs` | int | **1000** | **YES - exactly the `const float tick = 1f` both coroutines hardcoded** | milliseconds between pulses, both signs. **Cadence only** - totals are invariant |
| `combat.overTimeMagnitudePct` | int | **100** | **YES - identity** | percent scale on each pulse, both signs |
| `combat.overTimeDurationPct` | int | **100** | **YES - identity** | percent scale on duration, both signs. Adds pulses, so it *does* move totals |

**Three shared knobs, not six per-ability ones**, per the WO's explicit instruction. Resolved at
**apply time**, so a flip reaches a running client on the ordinary ~40 s path and takes effect on the
next cast - while an effect *already in flight* keeps the cadence it was cast with, so nothing changes
shape underneath the player.

**Clamps - AUTHORED FRESH, flagging them** (no precedent existed): tick `50..60000` ms, both percents
`0..1000`. Each guards a value that would *break* the engine rather than merely mis-balance it - a
tick of 0 is a divide-by-zero and an unbounded pulse loop in one frame; a tick above the ceiling never
fires, which is indistinguishable from a broken ability; a negative percent would invert a DoT into a
heal. Every clamp **traces**, never silently swallows.

### The fail-to-default invariant, driven rather than asserted

| row | tick | magnitude | 8 dps / 4 s burn delivers |
|---|---|---|---|
| *(none)* - offline / 404 / empty table / malformed JSON | 1000 ms | 100 | **4 pulses of 8.0 = 32 - today, exactly** |
| `combat.overTimeTickMs = 250` | 250 ms | 100 | 16 pulses of 2.0 = **32 - unchanged total** |
| `combat.overTimeMagnitudePct = 50` | 1000 ms | 50 | 4 pulses of 4.0 = 16 |
| `combat.overTimeDurationPct = 200` | 1000 ms | 100 | 8 pulses of 8.0 = 64 |
| `combat.overTimeTickMs = 0` (hostile) | clamped to 50 ms | - | never divides by zero |
| `-Clear` | 1000 ms | 100 | **back to 32** |

---

## 4. THE TWO ABILITIES

Both are **re-points of existing nodes, not new nodes** - exactly the shape WO-1306 used one node
over. Adding a node changes tree SHAPE and must answer `TalentTreeShapeRegression` rules 2/3/4/6;
re-pointing changes only what a node *does*. **id, tier, slot, cost, iconPath, x, y and prerequisites
are UNCHANGED on both**, so no row width, no base row, no prereq edge and no save key is disturbed,
and rule 7 `[first-point]` still passes on `mage.t1n3` as before.

**Verified at source that the stat riders survive the `kind` flip:** `HeroTalentModifiers.StatSum`
(`HeroTalentModifiers.cs:423-433`) matches on `Effect.Type` only and never reads `kind`. So the
**-15% cooldown** and the **+25% block chance** are kept, not traded away. Each node gains a castable
and loses nothing.

### `mage.wither` - "Wither" (granted by `mage.t1n4`, cost 1, reachable on the **2nd** point)

`effect: "dot"` - the **shipped** `ResolveDot`. No new mechanism.
Mage path to bar-equippable abilities is now **1 point -> Syphon Essence, 2 points -> Wither.**

| value | provenance |
|---|---|
| damage 12, range 14, cooldown 10, dotDamage 8, dotSeconds 4, castSeconds 0.4 | **copied VERBATIM from `knight.emberbrand-throw`**, the game's one authored non-ultimate DoT - introduces no new balance point |
| manaCost 7 | **copied VERBATIM from `mage.drain`** - mana economy is class-specific and the knight's 3 is not a mage number |
| `color` | **INHERITED** from emberbrand-throw. Not chosen - the CLI does not pick colour |
| name "Wither", description prose | **AUTHORED FRESH - flagging it** |

### `knight.ironblood` - "Ironblood" (granted by `knight.t1n3`, cost 1, reachable on the **2nd** point)

`effect: "regen"` - the shared engine, sign flipped.

> **READ THIS BEFORE RETUNING: `knight.oathmend` already exists and is also a heal-over-time.**
> The difference is deliberate and is the whole design. Oathmend is **tier 3, five points deep**, a
> **BURST** mend (10 HP/s for 5 s). Ironblood is **early, low-rate, long-window** sustain (4 HP/s for
> 12 s) - press it and keep fighting, rather than press it and be topped up.
>
> **⭐ THE ALTERNATIVE IS ONE WORD FROM YOU:** if you would rather simply re-cost Oathmend cheaper
> than carry two mends, drop Ironblood and this node re-point. That call is yours.

| value | provenance |
|---|---|
| cooldown 20, manaCost 4, range 5, castSeconds 0.4 | **copied VERBATIM from `knight.oathmend`** |
| `color` | **INHERITED** from oathmend |
| damage 4 (HP/s), seconds 12 | **AUTHORED FRESH - flagging it.** Derived so the total, **48 HP**, sits within two points of Oathmend's 50 - the same budget spread over a longer window, not a new one |
| name "Ironblood", description prose | **AUTHORED FRESH - flagging it** |

---

## 5. THE THREE OWNER RULINGS THAT LANDED MID-IMPLEMENTATION

### Ruling 1 - drain return = **60**, not 100

Carried through **all six** sources in one change: `RemoteTunables.Registry`, `TUNABLE_KEYS`,
`docs/PROD022_TUNABLE_FLAGS.md`, `ExpectedDefaults` in the C# oracle, plus WO-1328's
`tunable-manifest.js` prose and its regenerated spine. The `[tunable-defaults]` oracle and the 23-case
node oracle both pass; the doc-parity parse was re-simulated across **all 14 rows** and every one
matches the registry.

> **⛔ THIS IS A DELIBERATE DEPARTURE FROM THE RAIL'S "default == today's behaviour" RULE, AND IT IS
> WRITTEN DOWN IN FOUR PLACES SO THE NEXT READER DOES NOT "CORRECT" IT BACK TO 100.**
> It is a **ruled balance value**, not a bug fix and not a drift. The convention exists to stop a
> default changing *silently*; a value the owner stated out loud is the opposite of that. The
> invariant that still binds unchanged: **no row, no network, no parse => exactly what this build
> hardcodes.** The old WO-861 identity (heal == damage dealt) is now reachable by setting the row to
> 100. Same shape as the two `vfx.*` knobs' exception, already recorded in the file header.

**Consequence caught and fixed:** both Syphon descriptions said *"heals you for the damage dealt"* -
which became **false** at 60%. Changed to *"heals you for part of the damage dealt"* in both copies.

### Ruling 2 - *"drain should help stave off not run the show"* - checked against real numbers, not vibes

I read the incoming-damage side at source rather than guessing. **Hero base HP = 100**
(`HeroHealth.cs:39`). **Contact damage ticks every 1.0 s and SUMS every adjacent enemy**
(`HeroHealth.cs:46` `DamageInterval = 1.0f`; the `OverlapSphere` sum at `:352-391`). Per-enemy contact
from `enemies.json`: hollow-acolyte 4, hollow-rogue 5, hollow-mage 6, **hollow-walker 8**,
hollow-warrior 10, hollow-reaper 14, hollow-brute 24. So **realistic incoming is 8-26 DPS** in early
melee.

| sustain source | raw | averaged over its cooldown | vs one hollow-walker (8 DPS) |
|---|---|---|---|
| Syphon Essence @ 60% | 16.8 HP per cast, 9 s cd | **1.9 HP/s** | ~23% offset |
| Ironblood | 4 HP/s for 12 s, 20 s cd (60% uptime) | **2.4 HP/s** | ~30% offset |

**Both sit near 2 HP/s against 8-26 DPS incoming - roughly 10-25% mitigation.** A player using them
well survives a fight they would have lost; **neither can stand still and win by attrition**, because
even Ironblood's *peak* 4 HP/s only ties the single weakest non-cellar enemy, and its averaged rate is
2.4. The two abilities are deliberately in the same band, so the kit has one coherent sustain budget.

**I did not invent a nerf on top of this.** The data says the numbers already satisfy the ruling, and
guessing a lower number would have been exactly the inference-fix §12 forbids. **Headroom you should
know about:** Ironblood is multiplied *twice* - by `HealAmountMultiplier` (heal talents) and again by
`HealthRegenBonus` inside `RegenTick` (Swift Recovery). A fully-invested knight could reach ~6.2 HP/s
peak / ~3.7 averaged - still under one hollow-walker. If that reads too strong in a felt-test,
`combat.overTimeMagnitudePct` moves it in seconds with no rebuild.

### Ruling 3 - **"Syphon Essence"** (a Y, not an I)

Applied to **both** display strings in **all four** canonical files - the node (was "Siphon Ward") and
the pool spell (was "Siphon"). They are one thing to the player, so they carry one name.

**`mage.siphon` was NOT renamed** - it is a **live save key** (a learned-talent ledger and an
equipped-bar slot both persist that string), so renaming it would orphan every save carrying the node.
Same reasoning left the `iconPath` and the effect string alone. The ruling is recorded in both
records' `_comment`/`_note` so the next reader knows the id/name split was deliberate.

**Zero player-facing "Siphon" remains in canonical data** (verified by grep on `name`/`description`).

**Left alone and reported:** `docs/NARRATIVE/WORLDBUILDING_V1.md` and `STORY_BIBLE_POLISH.md` use
lowercase **"siphon" / "siphoner"** as a *worldbuilding noun* (the aether siphon, the orc siphoner).
Different word, different lane - **not renamed.** `docs/design/WO910_*` is a dated frozen ledger
(§15) and was not rewritten.

---

## 6. THE ART - SHORTLISTS ONLY. NOTHING WAS PICKED.

**Both new abilities hold all four VFX fields EMPTY**, exactly as `mage.drain` and `mage.siphon` do.
`PlayResidualLoop` no-ops on an empty key, and `OwnerPickedVfxKeys` (`HeroAbilities.cs:2552`) is the
owner-tag whitelist a key must join. **Oracle rule 8 `[owner-tag]` FAILS IF A KEY APPEARS** - it is
the only kind of assertion that catches a well-meaning seat "finishing" the ability.

Described by **shape, motion, rhythm, density and body position. No hue words.** `LOOPS` is stated per
candidate because a DoT and a regen both need a *sustained* effect - a functional requirement.

### A - sustained DAMAGE-OVER-TIME, on a struck enemy (for `mage.wither`)

| # | prefab | loops? | what it looks like |
|---|---|---|---|
| 1 | `Assets/Lana Studio/Casual RPG VFX/Prefabs/Fire/Fire_cartoon_poison.prefab` | **LOOPS** (6/6 sub-systems `looping:1`, len 4-5 s) | A squat licking silhouette hugging the ground plane, a soft round glow blob behind it, and a thin spray of pinpoint motes. Continuous upward roll with sideways flicker; motes rise and fade. **Irregular, no beat - reads as steady attrition.** Medium density. **At the feet / lower shins.** |
| 2 | `.../Fire/Fire_cartoon_electric.prefab` | **LOOPS** (6/6) | Same four-part chassis, but the mote layer is faster and jitterier: crackling upward with a high-frequency stutter on the small elements over a slow-rolling body. **Distinguishable from #1 by CADENCE, not colour.** Knee-high envelope, **at the feet.** |
| 3 | `.../Fire/Fire_cartoon_frost.prefab` | **LOOPS** (6/6, len 4-5 s) | Same chassis, slower lifetimes. Lazy upward creep, wider spread; motes drift rather than spark. **Slow, near-static rhythm.** Reads as *withering / chilled* rather than burning. **Feet to lower torso.** |
| 4 | `.../Fog/Fog_poison.prefab` | **LOOPS** (4 looping + 2 one-shot entry puffs) | A low flat sheet of billboard puffs churning and swelling in place, slow rotation, almost no vertical travel. Heavy at ground level, thinning fast above the ankle. **Slow breathing swell.** **A disc on the ground under the enemy** - good for a ground-anchored DoT, weaker if the enemy runs out of it. |
| 5 | `Assets/Spells Pack/Particles/Prefabs/Auras/Aura_Dark.prefab` | **LOOPS** (6/6, len 5 s, `scalingMode: Hierarchy` so it fits big and small enemies) | A ring of upward-streaming ribbons rising from a base disc and dissipating at head height, with a slow orbital swirl. **Continuous ascending sweep, ~1 s per rise - steady, no heartbeat.** Moderate, silhouette-preserving. **Enveloping: floor ring up to overhead.** |
| 6 | `Assets/Mirza Beig/.../Prefabs/Loop/pf_vfx-ult_demo_psys_loop_fire.prefab` | **LOOPS** (3 looping + a 0.1 s ignition burst) | A tall narrow tongue with turbulent noise plus rising embers. Fast flicker on the body, slow lift on the embers. Dense core, sparse halo. **Torso-height column** - larger than the Lana ones; will over-cover a small enemy, needs scaling down. **Already the proven `BurningStructure_Aura` pick, so it is known-good under this VFXManager.** |

*Runner-up if you want the DoT to read as an affliction rather than a burn:*
`Assets/Lana Studio/.../States/Aura_slowdown.prefab` (loops + intro burst) - a **downward-pressing**
ring at the feet with descending motes.

### B - sustained HEAL / REGEN, on the knight (for `knight.ironblood`)

| # | prefab | loops? | what it looks like |
|---|---|---|---|
| 1 | `Assets/Lana Studio/Casual RPG VFX/Prefabs/Regeneration/Regeneration_health_loop.prefab` | **LOOPS** (6/6, len 4 s) | **The strongest fit** - the pack author shipped an explicit `_loop` sibling of a one-shot. Motes lift from a floor disc and converge upward past the torso while a flat ground ring pulses. **Gentle 4 s cycle, clearly periodic - it READS as "ticking",** which is exactly what a pulsed regen is. Sparse and airy; does not hide the knight. **Feet-to-overhead rising column on a ground disc.** |
| 2 | `.../Regeneration/Regeneration_health_area_loop.prefab` | **LOOPS** (6/6, len 4 s) | Same motion vocabulary as #1 with a much wider footprint. **A broad disc on the floor around the knight,** motes rising from the whole radius. Use if the regen should read to bystanders; use #1 if it must stay glued to one body. |
| 3 | `.../Regeneration/Regeneration_mana_loop.prefab` | **LOOPS** (6/6, len 1 s + 4 s) | Identical chassis to #1 but the fast 1 s layer makes it visibly **more urgent**. Worth reserving for a second resource bar so the two regens are told apart **by tempo, not hue.** |
| 4 | `Assets/Spells Pack/Particles/Prefabs/Buffs/Buff_Nature.prefab` | **LOOPS** (9/9, len 7 s, scales with parent) | A wave expanding outward from the feet on a 7 s beat plus a soft standing shell around the torso. **Strongly pulsed - one clear throb per cycle, ideal if each pulse should read as a tick.** Heavier than the Lana regens; the shell slightly veils the silhouette. **Enveloping torso shell + expanding floor wave.** |
| 5 | `.../Buffs/Buff_Light.prefab` | **LOOPS** (6/6, len 7 s) | Lighter than #4. Upward-streaming shafts converging above the head with a slow rotating base ring. **Continuous, no hard pulse - "sustained blessing" rather than "ticking".** Thin. **Floor ring plus an overhead convergence point.** |
| 6 | `Assets/Hovl Studio/RPG VFX Bundle/Random effect prefabs/Buff heal.prefab` | **LOOPS** (8/8, len 2 s) | A tight fast-rising twist of streaks around the body on a 2 s cycle. **Brisk and obviously repeating - the most legible "something is repeatedly happening to me" of the six.** Wraps close to the body, **torso spiralling up past the head.** Already the generator's `Heal_Aura` pick, so battle-tested here. |

*Lowest-friction option of all:* `Assets/Resources/VFX/Aura/Aura_HealingInProgress.prefab` and
`Aura_ItemHeal.prefab` - both `looping:1`, project-owned, already under `Resources/`, **and currently
unclaimed by any key.**

### ⛔ Do NOT use `Regeneration_health.prefab` (no `_loop` suffix)
It is **0/6 looping - a ONE-SHOT.** It would fire once and stop while the regen kept ticking silently.

### Owner-tag debt folded in, as the WO asked

- **`mage.siphon` (now Syphon Essence)** has **no `concept-icons.json` row**, so it renders the
  crossed-swords default in the bar. `mage.drain` is unauthored there too. **Both await your tag.**
- **`mage.wither` and `knight.ironblood`** likewise have no icon row - consistent with the above, so
  one pass clears all four.
- Two pre-existing **catalog disagreements** found while surveying, reported not touched:
  `Heal_Aura` and `Aura_HeartPulse` have **different `isLoop` values** in `VfxManualPicks.json` vs
  `HovlVfxCatalogGenerator.cs`.

---

## 7. THE ORACLE - PROVEN RED FIRST

**New: `Assets/Editor/Regression/OverTimeEffectRegression.cs`** `[over-time]`, registered in
`DataRegression.RunAll`. Markers `OVER_TIME_OK` / `OVER_TIME_FAIL`.

Nine rules. It is **not a data lint** - it drives the engine with a fake clock and **counts pulses**.

I cannot run a Unity gate in this lane, so the engine's arithmetic and the oracle's cases were
**replicated exactly** and driven against mutated implementations. **GREEN on HEAD, RED on every
mutation, each naming the right rule:**

| mutation | result |
|---|---|
| *(none)* - HEAD | **GREEN** |
| **MUT 1** - first pulse lands on the CAST FRAME (`NextAt = now`) | **RED** - `[ticks] 1 pulse(s) landed at t=0.99s, before the first full 1s interval` (+3 more, and `[death]` too) |
| **MUT 2** - pulse count FLOORS instead of CEILs | **RED** - `[ceil] 4.5s/1s resolves to 4 pulses, not 5` |
| **MUT 3** - liveness check dropped (the DoT ticks a corpse) | **RED** - `[death] delivered 3 pulses; must stop at 2` + `still in flight (1)` |
| **MUT 4** - per-pulse = `perSecond` (interval factor dropped) | **RED** - `[invariant] quadrupling the pulse rate changed TOTAL from 32.0 to 128.0` |
| **MUT 5** - engine ignores the tunable rail | **RED** - `[invariant]` + `[tunable]` x2 |
| **MUT 6** - the `regen` dispatch case removed | **RED** - `[wiring] 'knight.ironblood' authors effect 'regen' but there is NO dispatch case` |
| **MUT 7** - an owner-untagged VFX key filled in | **RED** - `[owner-tag] 'knight.ironblood' has a NON-EMPTY vfxResidual` |
| **MUT 8** - the granting node un-pointed | **RED** - `[wiring] no talent node grants 'knight.ironblood'` |
| **MUT 9** - the shared engine re-hardcoded as a coroutine | **RED** - all three `[one-loop]` assertions |

**Honest note on MUT 4:** it does **not** red `[ticks]`, because at a 1 s interval `magnitude * 1`
equals `magnitude`. Only `[invariant]` catches it - which is precisely why that rule drives the
engine at **two different cadences** rather than one.

**Also extended: `RemoteTunablesDefaultsRegression`** - `ExpectedKnobCount` 14, the three new
`ExpectedDefaults`, and a full `[consumers]` block for `OverTimeTuning`: defaults with no table, both
clamp ends, **the success path** (250 ms / 50% / 200% must actually resolve to 0.25 / 0.5 / 2.0 - a
refusal-only proof certifies nothing), return-to-default on clear, and a source lint that the engine
still reads all three keys and has not re-grown a `const float tick`.

**Node oracles run and GREEN:** `test/tunables-manifest.test.js` **23/23**,
`test/command-center.test.js` **56/56**, `TUNABLE_MANIFEST_GEN_OK knobs=14`.

---

## 8. COORDINATION - and one thing the WO could not have known

**`WO-1328` landed a Command Center balance editor while I was working**, which added **two more
sources of truth for a knob** that did not exist when this WO was written:
`api/_lib/tunable-manifest.js` (hand-authored owner-facing prose + safe range) and
`api/_lib/tunable-manifest.generated.json` (a spine **derived** from `RemoteTunables.cs`). **All three
of my knobs were carried through both**, and the manifest regenerated. A seat following only the WO's
four-source instruction would have shipped a knob the owner's own console cannot see.

- **WO-1306:** RESULT read first. Its `mage.siphon` row and `mage.t1n3` node were edited **only** for
  ruling 3's rename; the drain default moved per ruling 1. Its rule 7 `[first-point]` still passes.
- **WO-1327:** its two `vfx.*` knobs were already in `RemoteTunables.cs`; additive, no collision.
- **WO-1329 (mage casting registry):** not touched. No edit to `MarqueeSpellVfx`, `motion-castings`
  or `RegistryTarget`.
- **WO-1310 (`HeroSkillTreePanelMvvm` layout solver):** **NOT TOUCHED.** No node was added, moved or
  re-priced - only `kind` / `abilityId` / `effect` / `description` changed, none of which is geometry.
- **WO-1294 (hot-swap bar internals):** **NOT TOUCHED.** Both new spells reach the bar through the
  existing `HeroLoadoutVM` path, which needed no change.
- **WO-1331 / WO-1332 (Aldwin spelling, remote catalog seam):** stayed out. The lowercase narrative
  "siphon"/"siphoner" was left alone rather than edited into their files.

> **⚠ ONE THING FOR THE LEAD.** `Assets/Editor/Regression/RemoteTunablesDefaultsRegression.cs` was
> **modified by another seat mid-lane** - it already carried `ExpectedKnobCount = 14` and my three
> `ExpectedDefaults` rows before I added the `[consumers]` coverage. The values matched mine exactly,
> so I **merged rather than reverted** (§11). Worth a glance during reconciliation.

Also fixed en route: `test/tunables-manifest.test.js` carried a **fifth hand-typed copy** of the drain
default (`ships with 100`), which went red on correct code. It now **derives** the number from the
generated spine - the same duplicated-state disease the manifest exists to cure.

---

## 9. FILES CHANGED

| file | change |
|---|---|
| `Assets/_Modules/Core/Combat/OverTimeEffects.cs` | **NEW** - `OverTimeEngine<T>`, `OverTimeKind`, `OverTimePulse<T>`, `OverTimeTuning` |
| `Assets/Editor/Regression/OverTimeEffectRegression.cs` | **NEW** - the `[over-time]` oracle, 9 rules |
| `Assets/_Modules/Village/Hero/HeroAbilities.cs` | two engines + `TickOverTimeEffects`; `BurnDoT`/`PoisonDoT` coroutines collapsed onto it; new `regen` shape + `ResolveRegen`; anim keyword row |
| `Assets/_Modules/Core/Ops/RemoteTunables.cs` | 3 new knobs + specs; `DrainReturnPctDefault` 100 -> **60** with the departure documented |
| `Assets/Editor/Regression/RemoteTunablesDefaultsRegression.cs` | 14 knobs, drain 60, full over-time consumer coverage + source lint |
| `Assets/Editor/Regression/DataRegression.cs` | registered `[over-time]` |
| `Assets/Resources/Data/Canonical/abilities.json` + StreamingAssets twin | `mage.wither`, `knight.ironblood`; Syphon Essence rename + the "part of the damage" correction |
| `Assets/Resources/Data/Canonical/hero-talents.json` + StreamingAssets twin | `mage.t1n4` and `knight.t1n3` re-pointed; Syphon Essence rename |
| `api/_lib/tunables.js` | 3 allowlist rows |
| `api/_lib/tunable-manifest.js` | 3 presentation cards + the drain copy rewritten to her ruling |
| `api/_lib/tunable-manifest.generated.json` | regenerated (14 knobs) |
| `docs/PROD022_TUNABLE_FLAGS.md` | rows 12-14; row 9 rewritten for the 60 ruling |
| `test/tunables-manifest.test.js` | derives the shipped default instead of retyping it; const-resolution expectation 100 -> 60 |

### Canonical twins - byte-equal
```
abilities.json     fc98a89ce3ae1172d12be44ba766234d   (Resources == StreamingAssets)
hero-talents.json  1f5d21643f962cd3942c3998348b60e0   (Resources == StreamingAssets)
```
All four re-parsed with a strict parser: **PARSE OK, no NUL.**

### Brace / NUL check - every `.cs` touched (CLAUDE.md §1)
```
Assets/_Modules/Core/Combat/OverTimeEffects.cs                    BALANCED  clean
Assets/_Modules/Core/Ops/RemoteTunables.cs                        BALANCED  clean
Assets/_Modules/Village/Hero/HeroAbilities.cs                     BALANCED  clean
Assets/Editor/Regression/RemoteTunablesDefaultsRegression.cs      BALANCED  clean
Assets/Editor/Regression/OverTimeEffectRegression.cs              BALANCED  clean
Assets/Editor/Regression/DataRegression.cs                        BALANCED  clean
```

---

## 10. OPEN FOR THE OWNER

1. **Tag the VFX** - one key each for `mage.wither` (`vfxResidual`, and optionally `vfxCast` /
   `vfxProjectile` / `vfxImpact`) and `knight.ironblood` (`vfxResidual`). Shortlists in section 6,
   described in words, loop status stated. **This is the ONE open slot; everything mechanical is done.**
2. **Tag the icons** - `mage.siphon` / `mage.drain` / `mage.wither` / `knight.ironblood` all render
   the crossed-swords default. One pass clears four.
3. **Confirm the authored numbers** - Ironblood's `4 HP/s for 12s` and the names "Wither" and
   "Ironblood". Everything else was copied verbatim from a shipped ability.
4. **The Ironblood-vs-Oathmend call** - keep both (early sustain + late burst), or drop Ironblood and
   simply re-cost Oathmend cheaper. **One word either way.**
5. **Follow-ups reported, not done:** the continuous `_hpOverTime` drip and `Enemy.DamageOverTimeRoutine`
   are the two over-time paths still off the shared engine; `HeroCombatStatus.ApplyStatus` has zero
   callers so nothing can burn/slow/freeze the *hero*; and two VFX catalog `isLoop` disagreements.
6. **Not gated and not committed** - hand to the lead for `COMPILE_GATE_OK` + `REGRESSION_OK`.
