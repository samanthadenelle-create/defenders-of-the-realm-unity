# Talent Icon Atlas — Naming & Ability Spec (FINAL DESIGN)

**Date:** 2026-06-27 · **Status:** DRAFT spec against the owner's FINAL canonical talent design.
**DOC-ONLY** (no `.cs` / `.json` touched). Supersedes the prior 6-node-per-hero draft of this file.

---

## 1. Summary

The owner delivered the final talent design. Each hero now has a **20-node tree** —
**Tier 1 / 2 / 3 = 5 nodes each + Tier 4 "Capstones" = 5** — plus **8 Shared Universal nodes**
any class may take. The art sheets map ~1:1: ~20 cells per hero sheet → that hero's 20 nodes; the
8 Common-Shred emblems → the 8 Shared nodes.

| Atlas | File | Grid | Maps to |
|---|---|---|---|
| **Knight** | `Knight Sprite Sheet.jpg` (1168×784) | 6×4, gaps at (2,4)(4,4)(4,5) — 21 cells | Bulwark of Elarion (20 nodes) |
| **Wizard** | `Wizard Sprite Sheet.jpg` (1168×784) | 6×4, center 2×2 = hero portrait — 21 node cells | Aetherweaver (20 nodes) |
| **Ranger** | `Ranger Sprite Sheet.jpg` (1168×784) | 6×4 full — 24 cells | Windstrider (20 nodes + portrait + 3 reserve) |
| **Shared** | `Common Shred.jpg` (784×1168) | 8 framed emblems | 8 Shared Universal nodes |

**Effect-type vocabulary** (per node): `stat[…]`, `unlockAbility`, `modifyAbility`, `aura` (ally-radius buff),
`onEvent` (reflect/revive/laststand/dot/onKill), `proc` (chance-based), `summon`, `stealth`, `invuln`.
**Build-status:** **EXISTS** = current `HeroTalentModifiers` already handles it (only `damageBonus`,
`cdReduction`, and heal-amount modify); **NEW** = needs a new effect handler.

**V1 priority:** V1 ships the **solo Knight (Garran)**; the Knight tree is authoritative. Ranger/Wizard are V2.
Cells are "(row R, col C)" from top-left = (1,1). Numbers in "spec" are proposals in the existing JSON voice
(e.g. "28 dmg at 16m (1.2s cd)") — not committed balance.

---

## 2. KNIGHT — "Bulwark of Elarion" (Tank / Protector)

| # | Tier | Name (owner) | Owner effect | Numeric spec (proposal) | Effect-type | Build | Cell |
|---|---|---|---|---|---|---|---|
| 1 | T1 | Iron Resolve | +18% damage reduction | Passive −18% incoming dmg | stat[damageReduction] | **NEW** | (1,2) Wardplate |
| 2 | T1 | Spear Thrust | unlocks Throwing Spear | Equip ranged poke: 28 dmg @ 16m (1.2s cd) | unlockAbility | **NEW** | (2,5) Spear of the Vigil |
| 3 | T1 | Guardian Stance | +25% block chance | Passive 25% chance to block a hit | stat[blockChance] | **NEW** | (1,1) Aegis of Elarion |
| 4 | T1 | Mending Oath | Mending Salve heals +30% | Self-heal 42 → 55 (5m ring, 16s cd) | modifyAbility (heal) | **EXISTS** | (4,1) Mending Cross |
| 5 | T1 | Battle Call | Taunt affects 3 enemies | Defender's Call taunts up to 3 foes in 6m (12s cd) | modifyAbility + taunt | **NEW** | (1,5) Garran's Panoply |
| 6 | T2 | Aegis Reinforcement | +30% shield strength | Guard/shield absorb value +30% | stat[shieldStrength] | **NEW** | (1,6) Lightward Shield |
| 7 | T2 | Charge Impact | Charge stuns on hit | Heroic Leap/Charge adds 1.0s stun on impact (6s cd) | modifyAbility + stun | **NEW** | (3,1) Wingblade |
| 8 | T2 | Honored Warden | Taunt grants allies 20% DR | Taunt also buffs allies in 6m: −20% dmg for 4s | aura (ally) | **NEW** | (4,2) Oathmark |
| 9 | T2 | Emberbrand Strike | melee attacks burn | Basic hits apply 8 dps burn for 3s | proc + onEvent[dot] | **NEW** | (3,4) Honed Steel |
| 10 | T2 | Shield Wall | nearby allies gain block chance | Allies in 6m gain +15% block | aura (ally) | **NEW** | (3,6) Wardens' Round |
| 11 | T3 | Suppressing Bastion | Volley now taunts | Suppressing Volley taunts every foe hit (36 dmg arc, 20s cd) | modifyAbility + taunt | **NEW** | (3,2) Dawnreaver |
| 12 | T3 | Oathweld Armor | damage taken heals allies | 25% of dmg you take heals allies in 6m | onEvent + aura | **NEW** | (2,6) Spiral Cuirass |
| 13 | T3 | Legendary Vanguard | +35% defense when stationary | +35% defense after 1.5s without moving | stat[defense] (conditional) | **NEW** | (1,4) Twinstone Harness |
| 14 | T3 | Retaliation Surge | reflect 30% damage | Reflect 30% of melee dmg back to attacker | onEvent[reflect] | **NEW** | (3,5) Binding Star |
| 15 | T3 | Bulwark Command | allies near you gain +20% defense | Allies in 6m: +20% defense (aura) | aura (ally) | **NEW** | (2,3) Bulwark Sigil |
| 16 | T4 | Eternal Aegis | 8s full invulnerability | Active: 8s invuln (90s cd) | invuln | **NEW** | (4,6) Sentinel Medallion |
| 17 | T4 | Knight Eternal | passive +45% defense, allies take 25% less dmg | +45% own defense; allies in 8m −25% dmg | stat[defense] + aura | **NEW** | (1,3) Heartstone Cuirass |
| 18 | T4 | Last Stand | low HP → massive DR + reflect | Below 20% HP: −60% dmg + reflect 50% for 5s (120s cd) | onEvent[laststand] + reflect | **NEW** | (2,2) Runed Pauldron |
| 19 | T4 | Holy Retribution | Taunt causes enemies DoT | Taunted foes burn 12 dps for 4s | modifyAbility + onEvent[dot] | **NEW** | (4,3) Sanctified Seal |
| 20 | T4 | Elarion's Champion | all abilities empower nearby allies | Any ability cast grants allies in 8m +15% dmg for 4s | aura (ally) | **NEW** | (3,3) Chevron Blade |

Reserve/non-node: **(2,1) Crossguard Mail** → gear reserve.

---

## 3. RANGER — "Windstrider" (Mobility / Precision) — V2

| # | Tier | Name (owner) | Owner effect | Numeric spec (proposal) | Effect-type | Build | Cell |
|---|---|---|---|---|---|---|---|
| 1 | T1 | Quick Draw | +25% attack speed | Passive +25% attack speed | stat[attackSpeed] | **NEW** | (1,1) Greenwood Bow |
| 2 | T1 | Hunter's Mark | marked enemies take +20% dmg | Mark a foe: it takes +20% dmg for 6s (10s cd) | unlockAbility + mark | **NEW** | (3,6) Talonhead |
| 3 | T1 | Tumble Step | dash + brief dodge window | Dash 6m; 0.4s dodge i-frames (8s cd) | unlockAbility | **NEW** | (4,2) Threefold Loose |
| 4 | T1 | Nature's Gift | +20% health regen in combat | +20% HP regen while in combat | stat[healthRegen] | **NEW** | (1,4) Greenmark Leaf |
| 5 | T1 | Arrow Storm Prep | unlocks Multishot | Equip Multishot: 3 arrows, 18 dmg each (9s cd) | unlockAbility | **NEW** | (2,2) Triple Nock |
| 6 | T2 | Windstrider Boots | +30% move speed | Passive +30% move speed | stat[moveSpeed] | **NEW** | (2,6) Roundbow Crest |
| 7 | T2 | Poison Tip | arrows apply bleed | Arrows apply 6 dps bleed for 4s | proc + onEvent[dot] | **NEW** | (1,3) Dewfletch |
| 8 | T2 | Eagle Vision | +25% range + crit | +25% range; +15% crit chance | stat[range, critChance] | **NEW** | (3,3) Loosed Cord |
| 9 | T2 | Trap Mastery | snares last 50% longer | Snare/root duration +50% | modifyAbility | **NEW** | (3,4) Iron Jaws |
| 10 | T2 | Shadow Veil | temp stealth after dash | Tumble Step grants 2s stealth | stealth | **NEW** | (2,3) Bladeleaf |
| 11 | T3 | Volley Rain | Suppressing Volley larger area | Volley radius +40% (72 dmg over 6m, 42s cd) | modifyAbility | **NEW** | (4,6) Endless Quiver |
| 12 | T3 | Heartseeker | crits auto-apply mark | Critical hits apply Hunter's Mark | proc + mark | **NEW** | (3,1) Scout's Shortbow |
| 13 | T3 | Leafcloak | +35% dodge after movement ability | +35% dodge for 3s after a dash/move skill | stat[dodge] (onEvent) | **NEW** | (4,4) Sela's Wreath |
| 14 | T3 | Beast Companion | summon temporary wolf | Summon wolf (120 HP, 15 dmg) for 20s (60s cd) | summon | **NEW** | (3,5) Beastcaller's Lure |
| 15 | T3 | Precision Strike | high-dmg single-target shot | Aimed shot: 90 dmg @ 18m (12s cd) | unlockAbility | **NEW** | (2,1) Drawn Longbow |
| 16 | T4 | Storm of Arrows | massive arrow-rain ult | Ult: 120 dmg over 8m rain (60s cd) | unlockAbility | **NEW** | (4,3) Twinbow |
| 17 | T4 | Windstrider Legend | permanent +45% speed + dodge | +45% move speed, +30% dodge | stat[moveSpeed, dodge] | **NEW** | (4,5) Leaffletch Bow |
| 18 | T4 | Phantom Hunter | attacks from stealth +50% dmg | First shot from stealth +50% dmg | stealth + proc | **NEW** | (2,5) Snare Ring |
| 19 | T4 | Nature's Fury | arrows apply nature DoT | Arrows apply 14 dps nature DoT for 5s | onEvent[dot] | **NEW** | (1,5) Wildgrowth Sprig |
| 20 | T4 | Elarion's Arrow | all arrows pierce + chain | Arrows pierce + chain to 1 extra foe at 50% | modifyAbility (pierce/chain) | **NEW** | (1,6) Quarry's End |

Reserve/non-node: **(4,1) Wren Thornquiver** → panel hero portrait; **(3,2) Full Quiver**, **(2,4) Goldfeather**,
**(1,2) Forager's Pack** → gear reserve.

---

## 4. WIZARD — "Aetherweaver" (Spell Power / Control) — V2

| # | Tier | Name (owner) | Owner effect | Numeric spec (proposal) | Effect-type | Build | Cell |
|---|---|---|---|---|---|---|---|
| 1 | T1 | Arcane Focus | +20% spell damage | Passive +20% spell dmg | stat[damageBonus] | **EXISTS** | (1,5) Catalyst Core |
| 2 | T1 | Mana Flow | +25% mana regen | Passive +25% mana regen | stat[manaRegen] | **NEW** | (2,1) Lorescript |
| 3 | T1 | Frost Touch | Frost Nova slows more + longer | Nova slow +20%, duration +1.5s | modifyAbility | **NEW** | (3,2) Frostflame Phoenix |
| 4 | T1 | Spellweaver | −15% cooldowns | Passive −15% cd | stat[cdReduction] | **EXISTS** | (1,2) Grimoire of Embers |
| 5 | T1 | Rune Binding | Arcane Bolt chains once | Q chains to 1 extra foe at 40% | modifyAbility (chain) | **NEW** | (3,5) Ward Circle |
| 6 | T2 | Aether Surge | mana restore on kill | +3 mana per kill | onEvent[onKill] | **NEW** | (2,6) Maelstrom Rune |
| 7 | T2 | Meteor Caller | Meteor pulls enemies in | Meteor pulls foes to center before impact | modifyAbility (pull) | **NEW** | (2,5) Fallstar |
| 8 | T2 | Arcane Shield | stronger + longer absorb | Shield absorb +40%, duration +2s | modifyAbility / stat[shieldStrength] | **NEW** | (1,3) Sealed Codex |
| 9 | T2 | Flame Mastery | Fireball bigger explosion | Fireball radius +35% | modifyAbility | **NEW** | (1,4) Wisplight |
| 10 | T2 | Blink Mastery | longer + safer teleport | Blink range +50%, +0.5s i-frames | modifyAbility | **NEW** | (4,2) Stormhood |
| 11 | T3 | Cataclysm Prep | Meteor Strike massive radius | Meteor radius +60% | modifyAbility | **NEW** | (4,6) Cometfall |
| 12 | T3 | Spell Echo | 25% chance to duplicate spells | 25% chance a cast fires twice | proc (duplicate) | **NEW** | (2,2) Soulflame |
| 13 | T3 | Aether Form | −30% mana cost | Passive −30% mana cost | stat[manaCost] | **NEW** | (3,1) Veiled Conjurer |
| 14 | T3 | Runic Overload | temp huge spell power | Active: +60% spell dmg for 6s (45s cd) | onEvent (temp buff) | **NEW** | (4,3) Orbital Sanctum |
| 15 | T3 | Void Rift | AoE stun + damage | Active: 40 dmg + 1.5s stun in 5m (18s cd) | unlockAbility + stun | **NEW** | (4,4) Lantern Pillar |
| 16 | T4 | Cataclysm | devastating area nuke | Ult: 600 dmg over 9m (50s cd) | unlockAbility | **NEW** | (3,6) The Black Wizard |
| 17 | T4 | Aetherweaver Ascension | all spells empowered | All spells +25% dmg & effect | stat[damageBonus] + buff | **NEW** | (1,6) Twinstaff Adept |
| 18 | T4 | Eternal Arcana | permanent +40% spell power + mana regen | +40% spell dmg, +40% mana regen | stat[damageBonus, manaRegen] | **NEW** | (4,5) Warmage |
| 19 | T4 | Reality Rift | temp damage zone | Active: 30 dps zone in 6m for 6s (40s cd) | onEvent[dot] / summon-zone | **NEW** | (4,1) Shadowcowl |
| 20 | T4 | Elarion's Legacy | spells chance to cast twice | 20% chance any spell auto-recasts | proc (duplicate) | **NEW** | (1,1) Apprentice of Alduin |

Reserve/non-node: **(2,3) Alduin the Patient** (center 2×2) → panel hero portrait.

---

## 5. SHARED UNIVERSAL (8 nodes, any class) → `Common Shred.jpg`

| # | Name (owner) | Owner effect | Numeric spec | Effect-type | Build | Emblem |
|---|---|---|---|---|---|---|
| 1 | Vitality | +25% max HP | Passive +25% max HP | stat[maxHpPct] | **NEW** | (1,1) Verdant Crest (green leaf) |
| 2 | Resilience | +20% damage reduction | Passive −20% incoming dmg | stat[damageReduction] | **NEW** | (1,2) Obsidian Ward (purple shield) |
| 3 | Wisdom Surge | +1 Wisdom per level | +1 Wisdom point per hero level | stat (progression) | **NEW** | (1,3) Wayfinder's Star (compass) |
| 4 | Battle Instinct | +15% crit | Passive +15% crit chance | stat[critChance] | **NEW** | (2,3) Sapphire Sigil (blue gem) |
| 5 | Aether Bond | +20% mana/energy regen | Passive +20% mana/energy regen | stat[manaRegen] | **NEW** | (3,1) Arcane Lodestar (purple star) |
| 6 | Legendary Resolve | revive once per run at 40% HP | Auto-revive once/run at 40% HP | onEvent[revive] | **NEW** | (2,2) Hallowed Mark (gold cross) |
| 7 | Swift Recovery | faster out-of-combat regen | +50% HP regen when out of combat | stat[healthRegen] | **NEW** | (2,1) Emberheart (fire) |
| 8 | Elarion's Blessing | all stats +10% | +10% to all stats | stat[allStatsPct] | **NEW** | (3,2) All-Seeing Eye (teal eye) |

---

## 6. BUILD CLASSIFICATION ROLLUP

This drives the build work order — almost the entire final design needs new effect handlers; the current
`HeroTalentModifiers` only covers offensive `damageBonus`, `cdReduction`, and heal-amount modify.

**EXISTS vs NEW per tree:**

| Tree | EXISTS | NEW | Total |
|---|---|---|---|
| Knight (Bulwark of Elarion) | 1 (Mending Oath) | 19 | 20 |
| Ranger (Windstrider) | 0 | 20 | 20 |
| Wizard (Aetherweaver) | 2 (Arcane Focus, Spellweaver) | 18 | 20 |
| Shared Universal | 0 | 8 | 8 |
| **TOTAL** | **3** | **65** | **68** |

**Distinct NEW effect handlers required** (build once, reuse across heroes):

*Stat handlers (extend `HeroTalentModifiers`):*
- `damageReduction` (Iron Resolve, Resilience; conditional in Last Stand)
- `blockChance` (Guardian Stance; ally variants via aura)
- `defense` incl. conditional (Legendary Vanguard "while stationary", Knight Eternal)
- `shieldStrength` (Aegis Reinforcement, Arcane Shield)
- `moveSpeed` (Windstrider Boots, Windstrider Legend)
- `attackSpeed` (Quick Draw)
- `critChance` (Eagle Vision, Battle Instinct)
- `range` (Eagle Vision)
- `dodge`/evasion (Leafcloak, Windstrider Legend)
- `maxHpPct` (Vitality)
- `manaRegen` (Mana Flow, Aether Bond, Eternal Arcana)
- `manaCostReduction` (Aether Form)
- `healthRegen` (Nature's Gift, Swift Recovery)
- `wisdomPerLevel` (Wisdom Surge — progression hook)
- `allStatsPct` (Elarion's Blessing — aggregate multiplier)

*Behavioral handlers (new effect shapes):*
- `unlockAbility` — equip a skill into a slot (Spear Thrust, Multishot, Precision Strike, Storm of Arrows, Void Rift, Cataclysm, Hunter's Mark, Tumble Step)
- `modifyAbility` — parameterized buffs to an existing ability (Frost Touch, Rune Binding chain, Meteor Caller pull, Flame/Blink/Cataclysm-Prep/Trap/Volley masteries, Battle Call, Suppressing Bastion, Elarion's Arrow pierce/chain)
- `taunt` (Battle Call, Honored Warden, Suppressing Bastion, Holy Retribution)
- `aura-ally-buff` (Honored Warden, Shield Wall, Bulwark Command, Knight Eternal, Elarion's Champion, Oathweld)
- `reflect` (Retaliation Surge, Last Stand)
- `laststand` (Last Stand — low-HP trigger)
- `revive` (Legendary Resolve)
- `stealth` (Shadow Veil, Phantom Hunter)
- `summon` (Beast Companion; Reality Rift as summon-zone variant)
- `invuln` (Eternal Aegis)
- `proc-chance` (Emberbrand burn, Poison Tip bleed, Heartseeker crit→mark)
- `proc-duplicate` (Spell Echo, Elarion's Legacy double-cast)
- `dot` (Emberbrand, Holy Retribution, Nature's Fury, Reality Rift, Poison Tip)
- `onKill-resource` (Aether Surge)
- `mark-system` (Hunter's Mark, Heartseeker — shared marked-target state)
- `stun` (Charge Impact, Void Rift)
- `pull`/displacement (Meteor Caller, Charge)
- `temp-empower-buff` (Runic Overload, Aetherweaver Ascension, Elarion's Champion)
- `pierce/chain projectile` (Elarion's Arrow, Rune Binding)

**Consolidated NEW-handler list (de-duped, for the WO):**
`damageReduction, blockChance, defense(conditional), shieldStrength, moveSpeed, attackSpeed, critChance,
range, dodge, maxHpPct, manaRegen, manaCostReduction, healthRegen, wisdomPerLevel, allStatsPct,
unlockAbility, modifyAbility, taunt, aura-ally-buff, reflect, laststand, revive, stealth, summon, invuln,
proc-chance, proc-duplicate, dot, onKill-resource, mark-system, stun, pull, temp-empower-buff,
pierce/chain.`

---

## 7. Open Questions for the Owner

1. **Wisdom economy:** old tree cost 1/2/3 Wisdom for 6 nodes. With 20 nodes/hero + 8 shared, what is the new
   cost curve per tier, and is the 8-node Shared pool a shared budget or separate?
2. **Tier-4 capstones:** are all 5 takeable, or pick-1 (mutually exclusive "ultimate" choice)?
3. **Shared pool gating:** any class takes all 8 shared nodes, or capped (pick N)?
4. **Ability vs passive split:** capstone actives (Eternal Aegis, Storm of Arrows, Cataclysm, Void Rift) — do
   these occupy W/E/R loadout slots, or separate capstone-actives?
5. **`mark-system`** is reused by Ranger (Hunter's Mark, Heartseeker) — confirm it's one shared debuff state.
6. **Knight V1 scope:** the Knight tree is 19/20 NEW handlers. Build the full 20 for V1, or a subset (T1–T2)
   first with capstones in a follow-up?
7. **Art reassignments:** node↔cell picks are best-fit — flag any to move (e.g. Battle Call currently sits on the
   Knight portrait cell (1,5)).
