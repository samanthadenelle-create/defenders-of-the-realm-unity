# Elemental Codex — Defenders of the Realm (Unity v2)

**Status:** Designer reference — review-and-approve before binding any VFX assignment.
**Game:** Defenders of the Realm, Unity 6 LTS, URP.
**Owner:** DeNelle Studios.
**Date:** 2026-05-27.
**Author:** Game-design agent (owner to ratify all VFX tint decisions).

**Source docs:** `docs/enemy-codex.md`, `docs/enemy-mob-sets-work-order.md`, `Assets/_Modules/Core/Data/SpecialAbility.cs`, `Assets/_Modules/Village/Hero/HeroAbilities.cs`, `Assets/Mirza Beig/Particle Systems/Ultimate VFX/`.

---

## 0. Purpose

This codex establishes the **elemental vocabulary** of the game — the four magical schools that govern every enemy, hero spell, and tower ability. It maps each element to a **visual aura color** so designers can scan the battlefield instantly and know what kind of threat they face.

The system is grounded in what already exists in the ATB engine (`Defs.cs`, `DamageElement`) and the `SpecialAbility` enum (`SpecialAbility.cs`). This codex does not invent new runtime systems; it documents the design intent so every artist, designer, and engineer works from the same palette.

---

## 1. The Four Elemental Schools

| School | Aura Color | Feeling | Where it lives |
|---|---|---|---|
| **Physical** | Bone-white / charcoal dust | Weight, ruin, inevitability | Melee Hollow Ones; Wildlands bruisers |
| **Aether** | White → pale violet | The Withering's own magic; ghost-light | Hollow Caster, Necromancer, hero Arcane spells |
| **Flame** | Red → ember orange | Rage, corruption, living fire | Tiefling Cultist; hero Meteor Strike; tower FireAura |
| **Ice** | Pale blue → frost cyan | Cold grief, wolf-winter, stillness | Feral Wolf; hero Frost Nova; tower FrostNova |

### Color intent for particle artists

When you open a Mirza Beig prefab and need to tint it to match an element, apply these adjustments to the particle system's **Start Color** and **Color over Lifetime** fields:

| School | Primary Start Color | Secondary / Glow | Emission intensity |
|---|---|---|---|
| Physical | `#B0AFA3` (bone grey) | `#5C4A2A` (dark ochre) | Low — this element reads heavy, not bright |
| Aether | `#E8E0FF` (ghost white) | `#9B6FFF` (violet) | Medium–high — Withering magic glows |
| Flame | `#FF4400` (red-orange) | `#FF9900` (ember gold) | High — fire demands attention |
| Ice | `#80CCFF` (frost blue) | `#FFFFFF` (crystal white) | Medium — cold glints, doesn't blaze |

> **Red / White / Blue shorthand.** When reviewing in the inspector or pointing things out verbally:
> - **Red** = Flame
> - **White** = Aether
> - **Blue** = Ice
> - **Grey / none** = Physical (this element typically has no aura or a very subtle dust loop)

---

## 2. Enemy Elemental Roster

Every enemy has a primary element that drives its ATB combatant type and its visual aura in the village scene.

### 2.1 The Hollow Ones (wave faction)

| Enemy | Element | Aura Color | Aura VFX Prefab (Mirza Beig) | Notes |
|---|---|---|---|---|
| **Hollow Walker** | Physical | None | — | Bare skeleton; no aura needed — readable as "base threat" |
| **Hollow Warrior** | Physical | Subtle dust | `Prefabs/Loop/pf_vfx-ult_demo_psys_loop_dusty.prefab` (dim) | Optional low-opacity loop on feet; reserved for elites |
| **Hollow Rogue** | Physical | None | — | Speed is the tell; no aura keeps it visually fast and uncluttered |
| **Hollow Caster** | **Aether** | **White → violet** | `Prefabs/Loop/pf_vfx-ult_demo_psys_loop_electroCore.prefab` | Tint to pale violet; this reads "magic threat" at a glance |
| **Hollow Reaper** | Physical | Bone-grey wisp | `Prefabs/Loop/pf_vfx-ult_demo_psys_loop_smokeWisps.prefab` | Dark wisps around the scythe — "death" register, not magic |
| **Hollow Brute (Bone-Golem)** | Physical | Dust + rumble | `Prefabs/Loop/pf_vfx-ult_demo_psys_loop_dusty.prefab` | Ground-level dust loop at feet; reinforces heavy mass |
| **Hollow Mender** | **Aether** | **Soft white** | `Prefabs/Loop/pf_vfx-ult_demo_psys_loop_finalRest.prefab` | Tint to warm white; a gentle healing glow — "something still cares here" |
| **Cellar Hollow** | Physical | None | — | Dungeon-only sorrow variant; no aura — it should feel pitiable, not threatening |
| **Necromancer of the Wound** | **Aether** | **Violet ghost-portal** | `Prefabs/Loop/pf_vfx-ult_demo_psys_loop_ghostPortal.prefab` | The canon boss aura; deep violet, high intensity — the Wound's hand |

### 2.2 The Wildlands (living faction)

| Enemy | Element | Aura Color | Aura VFX Prefab (Mirza Beig) | Notes |
|---|---|---|---|---|
| **Orc Raider** | Physical | None | — | Raw martial aggression; no supernatural element |
| **Wildlands Caveman** | Physical | None | — | Primal, not magical |
| **Feral Wolf** | **Ice** | **Frost blue** | `Expansions/XP - STORM/Prefabs/Loop/pf_vfx-ult_xp-storm_psys_loop_lightSnow2.prefab` | Cold-spirit wolf; light snow particles around feet and flanks |
| **Tiefling Cultist** | **Flame** | **Red → ember** | `Expansions/XP - TITLES/Prefabs/Loop/pf_vfx-ult_xp-titles_psys_loop_fire.prefab` | Demon-kin with Wound-Brand; tint fire loop to deep red |

### 2.3 Boss elemental assignments

| Boss | Element | Signature aura | Special VFX moment |
|---|---|---|---|
| **Necromancer of the Wound** | Aether | Ghost portal loop | "Withering Surge" → `explosion2` tinted violet |
| **The Apprentice of the Apothecary** | Aether | Soft white finalRest | "Caustic Spill" → `purplePuff` tinted acid green |
| **The Vault Keeper** | Physical | None | "Vault Slam" → `shards2-burst2` bone shards |
| **The First Wolfwarden** (Phase 1) | Physical | None | "Warden's Call" → wolf howl + `smokeWisps` |
| **The First Wolfwarden** (Phase 2 Wolf) | Ice | Light snow loop | "Savage Lunge" → `hitBall2-burst2` tinted ice blue |
| **The Inn-Keeper** | Aether | Very dim finalRest | "Last Call" → `sparkle3-burst` tinted mournful white |
| **The Mournful Alpha** | Ice | Light snow + shockwave | "Pack Cry" → `distortedShockwave-light` ice blue |
| **The Watcher** | Aether | Ghost portal dim | Phase 2 escalation → `electroCore` tinted white |

---

## 3. Hero Spell Elemental Map

Blaise (the Mage) casts four spells across two elements — Aether and Ice — plus one Flame ultimate. This makes the hero the **counterpart** to the Hollow Ones: Aether answers Aether corruption, Ice answers cold threats, Flame answers the Wound's living servants.

| Slot | Spell | Element | Color | Mirza Beig VFX (Travel) | Mirza Beig VFX (Impact) |
|---|---|---|---|---|---|
| **Q** | Arcane Bolt | Aether | White → violet | `XP-CONSTR.KIT/Prefabs/Oneshot/Hitballs/pf_vfx-ult_xp-ckit_psys_oneshot_hitBall2.prefab` | `XP-CONSTR.KIT/Prefabs/Oneshot/Rings/pf_vfx-ult_xp-ckit_psys_oneshot_hitRing2-solid.prefab` |
| **W** | Frost Nova | Ice | Pale blue | `XP-SHOCKWAVES` burst (no travel — instant AoE) | `XP-STORM/Prefabs/Loop/pf_vfx-ult_xp-storm_psys_loop_lightSnow2.prefab` (brief burst) |
| **E** | Healing Beacon | Aether | Warm white | — (no projectile; instant heal) | `XP-CONSTR.KIT/Prefabs/Oneshot/Sparkles/pf_vfx-ult_xp-ckit_psys_oneshot_sparkle3-burst.prefab` tinted warm white |
| **R** | Meteor Strike | Flame | Red → gold | — (falls from sky; no travel prefab needed) | `XP-ACTION/Prefabs/Oneshot/pf_vfx-ult_xp-action_psys_oneshot_explosion2.prefab` tinted deep red |

### VFX tint notes per spell

**Q Arcane Bolt (Aether/white-violet):** The `hitBall2` travel orb should have its Start Color shifted to `#C8A8FF` (pale violet). The `hitRing2-solid` impact ring should pulse white → violet fade. The ring already reads "arcane"; minimal tinting needed.

**W Frost Nova (Ice/pale-blue):** Frost Nova is an instant AoE with no travel phase. The impact is an outward shockwave from the hero position. Use `lightSnow2` as a 0.4-second burst (Stop Action: Destroy) rather than a loop. Tint particles to `#80CCFF` with white secondary.

**E Healing Beacon (Aether/warm-white):** Sparkle burst centered on the Heart of Elarion (not on the hero — the Beacon reaches the Heart). Start Color `#FFFBE8` (candle-warm white). No violet — this is restoration, not corruption.

**R Meteor Strike (Flame/red-gold):** The `explosion2` prefab is already dramatic. Tint Start Color to `#FF2200` and Secondary to `#FFAA00`. At L3 talent (Meteor Shower), spawn 3 staggered instances.

---

## 4. Tower Elemental Map

The village currently has one combat tower (Arcane Tower). Future towers are planned for the full roster. This table locks the elemental identity of each so VFX artists know the palette before the models are authored.

| Tower | Element | Aura Color | Combat VFX (Projectile Travel) | Combat VFX (Impact) | Max-Level Empowerment Element |
|---|---|---|---|---|---|
| **Arcane Tower** | Aether | White glow at muzzle | `hitBall2.prefab` tinted pale violet | `hitRing2-solid.prefab` | Aether surge (triple-shot burst) |
| **Frost Tower** *(planned)* | Ice | Blue crystal mist | `hitBall2.prefab` tinted `#80CCFF` | `lightSnow2` burst (brief) | Ice Nova (AoE FrostNova on fire) |
| **Flame Tower** *(planned)* | Flame | Ember glow at muzzle | `hitBall2.prefab` tinted `#FF4400` | `explosion2` (small scale) | Inferno (DoT Burn on all in range) |
| **Arrow Tower** *(planned)* | Physical | None | Physical arrow (no Mirza Beig needed) | `smokeWisps` brief on impact | Volley (fires at all enemies in range simultaneously) |

> **Designer note on the Arcane Tower:** The current `TowerCombat.cs` fires a pooled projectile via `ProjectilePool`. The projectile prefab field is in `PooledProjectile` — swap it for `hitBall2.prefab` (tinted violet) to get arcane bolts in one prefab swap. The `hitRing2-solid` impact plays at the projectile's hit position via `Instantiate` in the projectile's `OnTriggerEnter`.

---

## 5. Mirza Beig VFX Master Table — by Element

A consolidated lookup so a designer can ask "give me something Flame" and get the right prefab immediately.

All paths are relative to `Assets/Mirza Beig/Particle Systems/Ultimate VFX/`.

### Physical (bone-grey, dust, smoke)

| Use | Prefab |
|---|---|
| Enemy hit impact (all Physical enemies) | `Expansions/XP - CONSTR. KIT/Prefabs/Oneshot/Hitballs/pf_vfx-ult_xp-ckit_psys_oneshot_hitBall2-burst2.prefab` |
| Skeleton death burst | `Expansions/XP - CONSTR. KIT/Prefabs/Oneshot/pf_vfx-ult_xp-ckit_psys_oneshot_shards2-burst2.prefab` |
| Heavy unit death (Brute/Vault Keeper) | `Expansions/XP - CONSTR. KIT/Prefabs/Oneshot/pf_vfx-ult_xp-ckit_psys_oneshot_shards2-burst2.prefab` + `smokeWisps` |
| Reaper / sorrow ambient | `Prefabs/Loop/pf_vfx-ult_demo_psys_loop_smokeWisps.prefab` |
| Brute / Warrior foot-dust | `Prefabs/Loop/pf_vfx-ult_demo_psys_loop_dusty.prefab` |

### Aether (white → violet)

| Use | Prefab |
|---|---|
| Necromancer boss aura | `Prefabs/Loop/pf_vfx-ult_demo_psys_loop_ghostPortal.prefab` |
| Hollow Caster aura | `Prefabs/Loop/pf_vfx-ult_demo_psys_loop_electroCore.prefab` |
| Hollow Mender / Inn-Keeper aura | `Prefabs/Loop/pf_vfx-ult_demo_psys_loop_finalRest.prefab` |
| Hero Q travel (Arcane Bolt) | `Expansions/XP - CONSTR. KIT/Prefabs/Oneshot/Hitballs/pf_vfx-ult_xp-ckit_psys_oneshot_hitBall2.prefab` |
| Hero Q impact | `Expansions/XP - CONSTR. KIT/Prefabs/Oneshot/Rings/pf_vfx-ult_xp-ckit_psys_oneshot_hitRing2-solid.prefab` |
| Hero E buff (Healing Beacon) | `Expansions/XP - CONSTR. KIT/Prefabs/Oneshot/Sparkles/pf_vfx-ult_xp-ckit_psys_oneshot_sparkle3-burst.prefab` |
| Hero R ultimate (Meteor Strike) | `Prefabs/Oneshot/pf_vfx-ult_demo_psys_oneshot_ultima2.prefab` |
| Arcane Tower projectile | `Expansions/XP - CONSTR. KIT/Prefabs/Oneshot/Hitballs/pf_vfx-ult_xp-ckit_psys_oneshot_hitBall2.prefab` |
| Arcane Tower impact | `Expansions/XP - CONSTR. KIT/Prefabs/Oneshot/pf_vfx-ult_xp-ckit_psys_oneshot_distortedShockwave-light.prefab` |
| Hollow Caster bolt travel | `Expansions/XP - CONSTR. KIT/Prefabs/Oneshot/Blobs/pf_vfx-ult_xp-ckit_psys_oneshot_blob-hollow.prefab` |
| Hollow Caster bolt impact | `Prefabs/Oneshot/pf_vfx-ult_demo_psys_oneshot_purplePuff.prefab` |
| Hollow Mender heal pulse | `Expansions/XP - CONSTR. KIT/Prefabs/Oneshot/Sparkles/pf_vfx-ult_xp-ckit_psys_oneshot_sparkle3-burst.prefab` (tinted green) |
| Boss (Necromancer) death | `Prefabs/Oneshot/pf_vfx-ult_demo_psys_oneshot_explosion6.prefab` + `distortedShockwave2` |

### Flame (red → orange)

| Use | Prefab |
|---|---|
| Tiefling Cultist aura | `Expansions/XP - TITLES/Prefabs/Loop/pf_vfx-ult_xp-titles_psys_loop_fire.prefab` |
| Hero R impact (Meteor Strike) | `Expansions/XP - ACTION/Prefabs/Oneshot/pf_vfx-ult_xp-action_psys_oneshot_explosion2.prefab` |
| Future Flame Tower projectile | `hitBall2.prefab` tinted `#FF4400` |
| Future Flame Tower impact | `explosion2` (small scale) |

### Ice (pale blue → frost white)

| Use | Prefab |
|---|---|
| Feral Wolf aura | `Expansions/XP - STORM/Prefabs/Loop/pf_vfx-ult_xp-storm_psys_loop_lightSnow2.prefab` |
| Hero W impact (Frost Nova) | `XP - STORM/pf_vfx-ult_xp-storm_psys_loop_lightSnow2.prefab` (burst mode) |
| Future Frost Tower projectile | `hitBall2.prefab` tinted `#80CCFF` |
| Future Frost Tower impact | `lightSnow2` (brief burst) |
| Dungeon ground fog (Cold biomes) | `Expansions/XP - STORM/Prefabs/Loop/pf_vfx-ult_xp-storm_psys_loop_groundFog.prefab` |

### Ambient / Environmental

| Use | Prefab |
|---|---|
| Title screen embers | `Expansions/XP - TITLES/Prefabs/Loop/pf_vfx-ult_xp-titles_psys_loop_embers2.prefab` + `streaks` |
| Dungeon portal | `Prefabs/Loop/pf_vfx-ult_demo_psys_loop_hyperspace.prefab` |
| Heart of Elarion pulse | `Prefabs/Loop/pf_vfx-ult_demo_psys_loop_nucleus.prefab` |

---

## 6. Enemy Rotation by Elemental Type — Designer Notes

The following wave compositions deliberately mix elements to give the player clear visual reads at a glance. Use this as a template when authoring new `WaveEnemyGroup` ScriptableObjects.

### Wave archetypes by elemental feel

**"The Grey March" — Pure Physical wave.** Walkers, Warriors, one Brute. No auras. The baseline threat. Reads as relentless, heavy, inevitable. The player's first several waves are this register.

**"Withering Whisper" — Aether-primary.** Hollow Caster (white aura) hangs behind a Warrior screen. Caster bolt (purple puff) demands the player break through the melee wall to stop the ranged threat. Add a Hollow Mender (soft white aura) to introduce the healer priority-kill mechanic.

**"The Cold Pack" — Ice.** Feral Wolves (blue snow aura) in 3–4 packs. Fast, low HP, high dodge. The ice color scheme signals "different rules apply" — the player learns that swarm speed beats individual power. Pair with a Caveman Brute (no aura) to anchor the pack.

**"Wound's Hand" — Aether-boss.** Necromancer (violet ghost-portal) leads a Walker escort. The violet aura is immediately recognizable as "boss-tier Aether" after the player has seen Hollow Casters (pale white aura). Escalation is visual before it is mechanical.

**"Cultist Strike" — Flame.** Tiefling Cultists (red fire aura) deep-dungeon encounter. The red breaks sharply from the bone-grey and white of every prior encounter. Signals: "you have gone somewhere new and the rules are different."

---

## 7. Elemental Counter Table (ATB)

When the player faces an elemental enemy in an ATB breach battle, their spell choice matters. This table summarizes the design intent for elemental counter damage in the ATB engine.

| Attacker element | Strong against | Weak against | Flavor |
|---|---|---|---|
| Physical | Flame (solid beats fire) | Ice (cold stops momentum) | Reliable, no spikes |
| Aether | Physical (magic pierces armor) | Nothing (Aether is neutral on both sides) | Consistent damage vs. all |
| Flame | Ice (fire melts cold) | Physical (solid absorbs heat) | High ceiling, exploitable |
| Ice | Physical (cold slows movement) | Flame (fire overwhelms frost) | Control utility |

> **Note to implementor:** `DamageElement` in `DeNelle.Core.Combat` and `ENEMY_DEFS` in `Defs.cs` carry the element field already. The counter table above is the *design intent* — a future ticket wires the multiplier table into `BattleEngine.CalculateDamage`. Today the element is stored but not yet multiplied.

---

## 8. Ratification checklist

Items flagged for owner review before binding to prefabs:

- [ ] Aura color palette (`#E8E0FF` / `#9B6FFF` for Aether) — approve or adjust
- [ ] Physical enemies having no aura vs. a subtle dust loop — approve the "no aura = threat floor" design intent
- [ ] Hollow Mender aura tinted warm white (not violet) — confirm the healer reads distinct from caster
- [ ] Tiefling Cultist fire loop assigned (deep red tint) — confirm Flame = red in the player's mental model
- [ ] Ice = pale blue (not purple) for Feral Wolf — confirm the wolf doesn't read as Aether
- [ ] Elemental counter multipliers (Physical/Aether/Flame/Ice) — approve the counter table or revise damage math
