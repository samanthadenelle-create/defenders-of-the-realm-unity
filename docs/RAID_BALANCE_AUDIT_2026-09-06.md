# Echoes of Elarion - Raid Balance Audit (2026-09-06)

Prepared for external readers: designers, testers and partners who have not seen the project.
Every number below was read from the game's data files or from the owner's own device logs on
2026-09-06. Nothing is estimated unless it says so. Internal ticket references are kept out of the
body; the owner's ticket map is in the appendix.

---

## 1. One-page summary

**What a raid is.** The player leaves their town, picks an enemy camp, and attacks it with a hero
plus a small army of trained troops. The raid lasts up to 180 seconds. The player earns one to three
stars for how much of the camp they destroy, how fast, and how many troops survive, and the stars
decide the loot.

**What "10 troops" means.** The army has 10 slots by default. Basic troops (Footman, Archer,
Spearman) take one slot each; heavier ones take two, three or four. So "10 troops" is really
"10 slots": the owner fielded 7 Footmen and 3 Archers, the two day-one units. A Barracks upgrade
perk adds 5 slots for 6,400 gold, so 15 is reachable in the current design.

**Why the owner's easy raids were lost.** The owner played three raids on the easiest camp today
(The Forsaken Camp, difficulty "Regular"), all with the same 10 troops:

| Time | Result | What actually happened |
|---|---|---|
| 12:59 | 0 stars, ended at 45 s | The hero took the first hit 155 milliseconds after appearing in the camp, took 8 hits, and died at 50 s. All 10 troops finished the raid untouched: they never fought. |
| 14:37 | 3 stars, cleared in 74 s | 10 of 10 troops survived. |
| later | 3 stars, cleared in 62 s | 9 of 10 survived, 1 wounded. |

The loss was not a numbers loss. The hero is placed inside the defenders' reach with the clock
already running, so a player who pauses to deploy troops is dead before the first troop lands. On
the same day, with the same army, the camp was cleared twice with three stars.

**Honest verdict on "is 10 enough".** For the easiest camp, yes, today, when the fight actually
happens: two three-star clears prove it. The things that made it feel impossible are four defects,
not the slot count: the hero spawns in range, the clock starts on entry, the camp's defenders were
attacking their own objective, and the player's troops preferred walls over reachable enemies. Two
of those are fixed in the code as of tonight (not yet on the owner's phone); two were ruled on
tonight and are queued. The right move is to retest on the fixed build before touching the cap.
The harder camps are a different question: the second camp already requires 15 slots and the
fourth has 19 defenders, so the cap must grow with progression regardless.

---

## 2. The numbers

### 2.1 Troops (player side)

Stats are the authored base values before Barracks bonuses. Range is in meters; time in seconds.

| Troop | Slots | Health | Damage / hit | Hit every | DPS | Range | Speed | Cost (gold) | Train time | Unlocks at Barracks |
|---|---|---|---|---|---|---|---|---|---|---|
| Footman | 1 | 100 | 12 | 1.0 s | 12.0 | 2.5 | 4.0 | 550 | 45 s | tier 1 |
| Archer | 1 | 60 | 29 | 1.2 s | 24.2 | 14 | 4.0 | 550 | 60 s | tier 1 |
| Spearman | 1 | 90 | 16 | 1.1 s | 14.5 | 3.5 | 4.0 | 850 | 120 s | tier 2 |
| Field Cleric (healer) | 2 | 75 | 18 | 2.4 s | 7.5 | 9 | 3.8 | 205 | 240 s | tier 3 |
| Shieldguard (tank) | 2 | 180 | 10 | 1.3 s | 7.7 | 2.2 | 3.2 | 1,150 | 180 s | tier 3 |
| Outrider (fast melee) | 2 | 95 | 18 | 0.9 s | 20.0 | 2.5 | 5.5 | 1,500 | 270 s | tier 4 |
| Siege Catapult (one owned) | 4 | 50 | 48 | 2.5 s | 19.2 | 26 | 2.0 | 3,400 | 600 s | tier 4 |
| Battlemage | 2 | 55 | 42 | 1.8 s | 23.3 | 16 | 3.5 | 1,450 | 360 s | tier 5 |
| Echo Legionnaire | 3 | 160 | 28 | 1.0 s | 28.0 | 2.8 | 4.2 | 2,400 | 600 s | tier 6 |

The catapult does double damage to structures and about half to units. DPS is damage divided by
the hit interval; it ignores travel time and targeting.

### 2.2 Army size and the Barracks

- Base army: **10 slots**.
- Barracks tier 3 perk "Expanded Capacity": **+5 slots** for 6,400 gold. Nothing else raises the cap.
- Barracks tiers also buff every deployed troop: tier 2 health +8%; tier 3 damage +12% / health
  +10%; tier 4 +18% / +18%; tier 5 +26% / +26%; tier 6 +38% / +38%. Tier costs climb from
  1,490 wood + 1,240 gold (tier 1) to 28,350 wood + 23,030 gold (tier 6).
- The deploy screen's "Power" number is the sum of each deployed troop's damage times its damage
  multiplier. The owner's 7 Footmen + 3 Archers read Power 196.

### 2.3 The enemy camps

| Camp | Difficulty | Walls | Garrison (defenders) | Boss | Enemy level | Difficulty multiplier | Loot multiplier | Slots needed |
|---|---|---|---|---|---|---|---|---|
| The Forsaken Camp | Regular (the "easy" one) | Wood, 2 gates, 2 catapult towers, siege tower centre | 7 Orc Berserkers + 2 Orc Shamans = 9 | Orc Necromancer | 3 | 1.0 | 1.0 | 10 |
| The Broken Garrison | Hard | Iron, 3 catapults + 1 arcane spire | 4 Trolls + 2 Ogres + 6 Berserkers + 3 Shamans = 15 | Orc Necromancer | 5 (+2) | 1.25 | 1.5 | 15 (locked until the army has 15) |
| The Veiled Enclave | Extreme | Reinforced steel, 2 arcane spires | 7 Hollow Acolytes + 5 Shamans + 7 Hollow Warriors = 19 | Necromancer | 6 (+3) | 1.3 | 2.2 | locked by progression |
| The Iron Bastion | Extreme | Reinforced steel, 2 arcane spires | same composition as the Enclave, 19 | Necromancer | 6 (+3) | 1.3 | 2.2 | locked by progression |

Defender base stats for the easy camp (before level scaling):

| Enemy | Health | Damage / hit | Hit every | DPS |
|---|---|---|---|---|
| Orc Berserker (x7) | 117 | 10 | 1.2 s | 8.3 |
| Orc Shaman (x2, healer) | 78 | 3 | 1.5 s | 2.0 |
| Orc Necromancer (boss) | 600 | 18 | 1.3 s | 13.8 |

The camp's central spire has 1,200 health. Level scaling on top of these base stats was not
measured tonight; the hero took 15 damage per hit, above any listed base value, so scaling is
real and is flagged as an open measurement below.

### 2.4 Scoring (stars)

- Destruction is a weighted score: the central spire is 50%, walls and towers are 30%, and the
  remaining 20% was not read tonight (by elimination it is the garrison).
- 1 star: destroy at least 50% of the camp.
- 2 stars: 1 star plus EITHER finish under the 180 s clock OR keep at least 70% of deployed troops alive.
- 3 stars: 1 star plus BOTH.
- The hero dying ends the raid immediately with whatever destruction stands.
- Deploying no troops cannot earn the survival star.

### 2.5 Rewards: from the number on the card to the number in the bank

Base loot for the easy camp is 1,800 wood, 1,100 iron, 2,200 gold, 60 food, 20 crystals. The star
ladder pays 18% of base on a fail, 50% at one star, 75% at two, 100% at three, 110% for a perfect
clear. Wood and iron scale with the camp's loot multiplier (1.0 / 1.5 / 2.2); gold and crystals do
not.

The owner's 14:37 three-star clear, step by step:

| Step | Wood | Iron | Gold |
|---|---|---|---|
| Card promised ("Spoils: ~1800 wood, ~1100 iron, ~2200 gold") | 1,800 | 1,100 | 2,200 |
| Three stars, 100% of base | 1,800 | 1,100 | 2,200 |
| Repeat clear of an already-claimed camp: x0.25 | 450 | 275 | 550 |
| Town storage was full (wood 18,000 / 18,000): banked | **25** | **49** | (not read) |
| Lost at the bank | 425 | 226 | |

So a perfect raid delivered 25 wood of the 1,800 on the card. Two separate rules (repeat-clear
penalty, storage cap) are each reasonable; together, unexplained, they read as broken.

---

## 3. Force comparison

Method: add up damage-per-second and health for each side using the base stats above, ignoring
range, targeting, healing, towers, walls and level scaling. This is a napkin comparison, not a
simulation.

| Side | Units | Total DPS | Total health | Seconds to kill the other side's health at full focus |
|---|---|---|---|---|
| Owner's army (easy camp) | 7 Footmen + 3 Archers | 157 | 880 | kills 1,575 of defender health in ~10 s |
| Easy camp garrison | 7 Berserkers + 2 Shamans + boss | 76 | 1,575 | kills 880 of attacker health in ~12 s |

On paper the easy camp is roughly even at base stats and tilts to the attacker: the garrison has
twice the health but half the damage, and the attacker also brings a hero. That matches the two
three-star clears. Two things break the curve:

1. **Level scaling.** The camp runs its enemies at level 3; the hero was taking 15 per hit instead
   of 10. Whatever the scaling formula is, it was not measured tonight and belongs in the next
   pass.
2. **The Hard camp doubles the wall.** The Broken Garrison brings 15 defenders including four
   320-health Trolls, a 1.25 difficulty multiplier, iron walls, and an arcane spire, and demands 15
   slots. At the current cap the player cannot enter it. The jump from 9 defenders to 15 with the
   same two day-one troop types is where the curve first bites, and that is a progression gate
   (Barracks tier 3 for the +5 slots and the Shieldguard/Cleric unlocks), not a raid-side number.

---

## 4. The five defects that decide outcomes today

1. **The hero appears inside the defenders' reach with the clock already running.** In the 12:59
   raid the first hit landed 155 milliseconds after the hero was placed. A player who stops to
   deploy troops takes eight hits and dies before the army is on the field. Ruled on tonight: a
   staging area outside every defender's range, with the clock starting on the first engagement.
2. **The camp's own defenders spent the raid destroying their own spire.** Structures carried no
   faction, so a defender treated the objective it guards as a target. In one capture, more than
   ten thousand of the defenders' targeting lines were "attack the spire". The player's "win" was
   partly the garrison's own doing, and the player's loss was hidden inside that noise. Fixed in
   code tonight.
3. **The player's troops chewed walls while enemies stood in reach.** Target selection preferred
   the nearest structure over a reachable unit, so the two armies often never met (one raid ended
   "deployed 10, survivors 10, wounded 0" with zero stars). Fixed in code tonight.
4. **Rewards vanish on the way to the bank.** The repeat-clear penalty and the storage cap turned
   1,800 promised wood into 25 banked with no explanation on the card. The player learns that
   winning pays nothing.
5. **The deploy screen cannot be read under pressure.** The town shows through it, the army count
   overlaps the hero row, troop tiles are unreadable, and the ability row sits under the deploy
   bar in the fight itself. Deploying troops takes several taps at the exact moment the hero is
   dying. Fixed in code tonight (backdrop, flag, tray) and a redesign is queued.

None of these is a cap problem. All five are cheaper than a balance pass, and a balance pass done
before they land would be tuned against the wrong game.

---

## 5. Three balance options

| Option | What changes | Upside | Downside |
|---|---|---|---|
| **A. Retest after the fixes (recommended)** | Nothing tonight. Ship the staging area, the two AI fixes and the deploy screen; the owner plays the easy camp again on that build. | Every number above says 10 wins the easy camp when the fight happens. Avoids tuning against defects. | The owner waits one build cycle to feel the difference. |
| B. Raise the base cap to 15 now | One constant (10 to 15), the Barracks perk still adds +5. | Cheapest possible change; opens the Hard camp immediately. | Papers over the spawn-in-range death; the Hard camp's four Trolls will still eat Footmen and Archers, so "I cannot win" moves one camp over. Removes the meaning of the 6,400-gold perk. |
| C. Cap grows with Barracks level | Base 10, +5 per Barracks tier from tier 2 (15/20/25/30/35), the Expanded Capacity perk on top. | The Clash-of-Clans shape players expect: the army grows because you built something. Lines up with the camps (Hard at 15, Extreme at 19+). | Larger change: the army screen must say the cap and how to raise it, the camp cards must say the slots needed, and the economy for training that many troops needs a look. |

Recommendation: A now, then C as the progression design, with B rejected. The evidence for A is
the two three-star clears with the same ten troops. C is the durable answer to "I cannot win"
once the player reaches the Hard camp, and it turns the cap into a visible goal rather than a
wall.

---

## 6. Questions for external feedback

1. Does a 180-second raid with a 10-slot army feel like the right size for a first raid, or should
   the first camp be smaller (fewer defenders, shorter clock) so a new player wins on the first try?
2. When the hero dies the raid ends instantly. Should it instead continue with the troops alone for
   the remaining clock, with the death costing a star?
3. Should the army cap be a Barracks perk (buy +5 once), a Barracks level (grows every tier), or
   both?
4. The repeat-clear penalty pays 25% of loot for a camp already claimed. Is that the right shape,
   or should repeat clears pay full loot on a cooldown?
5. When storage is full, should raid loot wait as "pending" until the player builds storage, or is
   losing it acceptable if the card says so before the raid?
6. Is a healer and a tank at Barracks tier 3 too late for the Hard camp's Trolls, or is the
   intended answer "bring 15 Archers"?
7. Should difficulty labels be words only (Regular / Hard / Extreme) or should the card also show a
   suggested army (for example "bring 15 slots, 2 tanks")?

---

## 7. Appendix

### Sources read on 2026-09-06

- Device logs: the three raid outcome lines (raid scored / stars settled / raid-end reconcile),
  the hero placement and first-damage timestamps (12:58:57.394 placed, 12:58:57.549 first hit,
  12:59:47.750 death), the loot settlement and repeat-clear lines, the bank-full lines.
- Canonical data: troops (stats, slots, cost, train time, unlock tier); building tiers (Barracks
  track and the Expanded Capacity perk); scene configs (the four camps, garrisons, difficulty,
  loot multipliers); enemies (defender stats); the raid loot tunables (star ladder, bases).
- Code read for rules only: raid scoring (star thresholds, 180 s clock, destruction weights,
  hero-death rule), army storage (base cap 10 plus perk bonus), the deploy view model (Power).

### Not measured tonight (open)

- The enemy level-scaling formula (hero took 15 per hit against a listed 10).
- The remaining 20% of the destruction score.
- Ogre stats (Hard camp).
- Gold actually banked on the 14:37 clear (the log line was cut).

### Ticket map (for the owner)

| Finding | Ticket |
|---|---|
| Staging area, clock starts on first engagement | minted 2026-09-06 (owner ruling 20:26) |
| Defenders attack their own spire | WO-1439 (landed, on device build 358574) |
| Attackers prefer walls over reachable units | WO-1438 (landed in code tonight) |
| Raid walls and spire still admitted as hostile | WO-1458 (in a lane) |
| Loot promised vs banked | WO-1461 |
| Deploy screen backdrop / rally flag / troop tray | WO-1462, WO-1463, WO-1464 (landed in code tonight) |
| Deploy screen redesign | WO-1519 |
| Train/Army screen must say full and upgradeable | WO-1517 |
| Raid HUD posture | WO-1436 (closed, owner PASS) |
| Raid softlock | WO-1437 (closed, owner PASS) |
