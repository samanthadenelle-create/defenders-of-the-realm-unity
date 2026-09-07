# Echoes of Elarion - Raid Balance Audit (2026-09-06)

Prepared for external readers: designers, testers and partners new to the project. Every number
below was read from the game's data files or the owner's own device logs on 2026-09-06; nothing is
estimated unless it says so. Ticket references are kept to the appendix.

A raid: the player attacks an enemy camp with a hero plus a small army of trained troops, on a
180-second clock. One to three stars are earned for how much of the camp is destroyed, how fast, and
how many troops survive; the stars decide the loot.

---

## 1. Verdict: do not rebalance yet

The army is not too small. On the same day, on the easiest camp, with the identical ten troops, the
owner lost with zero stars and then cleared it twice with three stars. The loss was decided by
defects in placement, targeting and readability, not by the slot count, so tuning done before those
defects land would be tuned against the wrong game.

| Time | Result | What actually happened |
|---|---|---|
| 12:59 | 0 stars, ended at 45 s | The hero took the first hit 155 milliseconds after appearing in the camp, took 8 hits, and died at 50 s. All 10 troops finished the raid untouched: they never fought. |
| 14:37 | 3 stars, cleared in 74 s | 10 of 10 troops survived. |
| later | 3 stars, cleared in 62 s | 9 of 10 survived, 1 wounded. |

---

## 2. Why the loss happened

Five defects, in the order the player meets them.

1. **The hero appears inside the defenders' reach with the clock already running.** The first hit
   landed 155 milliseconds after placement, so a player who stops to deploy takes eight hits and
   dies before the army is on the field. *State: ruled on 2026-09-06 and queued* - a staging area
   outside every defender's range, with the clock starting on the first engagement.
2. **The camp's own defenders spent the raid destroying their own spire.** Structures carried no
   faction, so a defender treated the objective it guards as a target. One capture logged more than
   ten thousand defender targeting lines reading "attack the spire", so the player's win was partly
   the garrison's own doing. *State: fixed in code, not yet on device.*
3. **The player's troops chewed walls while enemies stood in reach.** Target selection preferred the
   nearest structure over a reachable unit, so the armies often never met: one raid ended
   "deployed 10, survivors 10, wounded 0" with zero stars.
   *State: fixed in code, not yet on device.*
4. **Rewards vanish on the way to the bank.** A repeat-clear penalty and a full storage bank turned
   1,800 promised wood into 25 banked, unexplained. The player learns that winning pays nothing.
   *State: ruled on 2026-09-06 and queued* (see section 6).
5. **The deploy screen cannot be read under pressure.** The town shows through it, the army count
   overlaps the hero row, troop tiles are unreadable, and the ability row sits under the deploy bar
   in the fight. Deploying takes several taps while the hero is dying.
   *State: backdrop, rally flag and troop tray fixed in code; a redesign is ruled and queued.*

None is a cap problem, and all five are cheaper than a balance pass.

---

## 3. What to fix first, in order

1. **Staging area.** The player must be able to open the deploy screen without being hit; nothing
   else can be judged until the fight starts on the player's terms.
2. **Targeting and faction.** Defenders stop attacking their own camp; attackers prefer reachable
   enemies over walls. Until both hold, no outcome measures the army.
3. **Reward settlement.** The number on the card and the number in the bank must reconcile, or be
   explained before the raid.
4. **Deploy UX.** The redesign, once the first three have removed the time pressure.

---

## 4. Easy-camp acceptance target

The gate for the easiest camp, on the fixed build:

- [ ] First hit cannot occur before the player engages.
- [ ] The 10-slot starter army can reasonably clear.
- [ ] No friendly-targeting defects.
- [ ] Troop AI attacks reachable enemies before walls.
- [ ] A fresh clear produces the displayed reward.
- [ ] Hero death does not instantly invalidate the surviving army.
- [ ] Median new-player clear roughly 90-140 seconds rather than most of the 180-second ceiling.

---

## 5. Progression recommendation

**What "10 troops" means.** The army has 10 slots. Basic troops (Footman, Archer, Spearman) take one
slot each; heavier ones two, three or four. So "10 troops" is really "10 slots": the owner fielded
7 Footmen and 3 Archers, the two day-one units.

**Now: Option A.** No cap change. Ship the fixes, then retest against section 4.

**Long-term: Option C.** The cap is driven by the Barracks: base 10, plus 5 per Barracks tier. That
lines up with the camps the player will meet, since the Hard camp expects 15 slots and the Extreme
camps carry 19 defenders.

**The one-time +5 perk is repurposed.** Under Option C a flat "Expanded Capacity: +5 slots for
6,400 gold" duplicates the tier ladder, so it becomes something else on the same tier. Candidates:
"Command Logistics: +1 deployment preset", or "Reinforcements train 10% faster".

**The camp bridge.** Camps teach composition as the cap grows: Camp I clears with 10 slots of basic
troops; Camp II at 15 introduces tank and healer; Camp III at 20, magic and siege; Camp IV at 25 or
more is the late composition puzzle.

| Option | What changes | Upside | Downside |
|---|---|---|---|
| **A. Retest after the fixes (recommended now)** | Nothing. Ship the staging area, the two AI fixes and the deploy screen, then replay the easy camp. | The appendix says 10 wins the easy camp when the fight happens. Avoids tuning against defects. | One build cycle of waiting. |
| B. Raise the base cap to 15 now (**rejected**) | One constant, 10 to 15. | Cheapest change; opens the Hard camp at once. | Papers over the spawn-in-range death, and the Hard camp's four Trolls still eat Footmen and Archers, so "I cannot win" moves one camp over. Removes the meaning of the 6,400-gold perk. |
| **C. Cap grows with Barracks level (recommended long-term)** | Base 10, +5 per Barracks tier from tier 2 (15/20/25/30/35); the +5 perk repurposed, not stacked. | The army grows because you built something. Lines up with the camps (Hard at 15, Extreme at 19). | The army screen must say the cap and how to raise it, camp cards must say the slots needed, and the training economy needs a look. |

---

## 6. Rulings adopted 2026-09-06

- **Hero death no longer ends the raid.** Surviving troops fight on for the remaining clock, and the
  result is capped at 2 stars.
- **Raid loot is never destroyed at a full bank.** Overflow goes to a Raid Cache with a modest cap,
  claimable after upgrading storage or spending down. Example message: "1,775 Wood held in Raid
  Cache - storage full".
- **The repeat-clear penalty is softened.** 100% on the first clear, 60% during the same cooldown
  cycle, 100% again once the cooldown expires.
- **Enemy level scaling is the next measurement.** The hero took 15 damage per hit against a listed
  base of 10, so scaling is real and unmeasured.
- **Verify the Field Cleric's 205 gold.** It reads like a typo beside 850 / 1,150 / 1,500. It is the
  authored value and is not changed until confirmed.

---

## 7. Questions for external feedback

1. Does a 180-second raid with a 10-slot army feel right for a first raid, or should the first camp
   be smaller (fewer defenders, shorter clock) so a new player wins first try?
2. Is a 2-star cap on hero death the right cost, or should it be 1 star?
3. Should the army cap be a Barracks perk (buy +5 once), a Barracks level (grows every tier), or
   both?
4. When storage is full, should raid loot wait as pending until the player builds storage, or is
   losing it acceptable if the card warns first?
5. Is a healer and a tank at Barracks tier 3 too late for the Hard camp's Trolls, or is the intended
   answer "bring 15 Archers"?
6. Should difficulty labels be words only (Regular / Hard / Extreme), or also show a suggested army
   (for example "bring 15 slots, 2 tanks")?

---

## 8. Appendix

### A.1 Troops (player side)

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

The catapult does double damage to structures and about half to units. DPS is damage divided by the
hit interval, ignoring travel time and targeting.

### A.2 Army size and the Barracks

- Base army: **10 slots**.
- Barracks tier 3 perk "Expanded Capacity": **+5 slots** for 6,400 gold. Nothing else raises the cap
  today (repurposed under section 5).
- Barracks tiers also buff every deployed troop: tier 2 health +8%; tier 3 damage +12% / health
  +10%; tier 4 +18% / +18%; tier 5 +26% / +26%; tier 6 +38% / +38%. Tier costs climb from
  1,490 wood + 1,240 gold (tier 1) to 28,350 wood + 23,030 gold (tier 6).
- The deploy screen's "Power" is the sum of each deployed troop's damage times its damage
  multiplier; the owner's 7 Footmen + 3 Archers read Power 196.

### A.3 The enemy camps

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

The central spire has 1,200 health.

### A.4 Scoring (stars)

Rules as read in code on 2026-09-06.

- Destruction is a weighted score: the central spire is 50%, walls and towers are 30%, and the
  remaining 20% was not read tonight (by elimination it is the garrison).
- 1 star: destroy at least 50% of the camp.
- 2 stars: 1 star plus EITHER finish under the 180 s clock OR keep at least 70% of deployed troops alive.
- 3 stars: 1 star plus BOTH.
- The hero dying ends the raid immediately with whatever destruction stands. (Superseded by section
  6; not yet implemented.)
- Deploying no troops cannot earn the survival star.

### A.5 Rewards: from the number on the card to the number in the bank

Base loot for the easy camp is 1,800 wood, 1,100 iron, 2,200 gold, 60 food, 20 crystals. The star
ladder pays 18% of base on a fail, 50% at one star, 75% at two, 100% at three, 110% for a perfect
clear. Wood and iron scale with the camp's loot multiplier (1.0 / 1.5 / 2.2); gold and crystals do
not.

The owner's 14:37 three-star clear, under the rules live on 2026-09-06 (repeat penalty and full-bank
loss both superseded by section 6, not yet implemented):

| Step | Wood | Iron | Gold |
|---|---|---|---|
| Card promised ("Spoils: ~1800 wood, ~1100 iron, ~2200 gold") | 1,800 | 1,100 | 2,200 |
| Three stars, 100% of base | 1,800 | 1,100 | 2,200 |
| Repeat clear of an already-claimed camp: x0.25 | 450 | 275 | 550 |
| Town storage was full (wood 18,000 / 18,000): banked | **25** | **49** | (not read) |
| Lost at the bank | 425 | 226 | |

So a perfect raid delivered 25 wood of the 1,800 on the card. Each rule is reasonable alone;
together, unexplained, they read as broken.

### A.6 Force comparison

Method: sum damage-per-second and health per side from the base stats above, ignoring range,
targeting, healing, towers, walls and level scaling. A napkin comparison, not a simulation.

| Side | Units | Total DPS | Total health | Seconds to kill the other side's health at full focus |
|---|---|---|---|---|
| Owner's army (easy camp) | 7 Footmen + 3 Archers | 157 | 880 | kills 1,575 of defender health in ~10 s |
| Easy camp garrison | 7 Berserkers + 2 Shamans + boss | 76 | 1,575 | kills 880 of attacker health in ~12 s |

The easy camp tilts to the attacker: the garrison has twice the health but half the damage, and the
attacker also brings a hero, which matches the two three-star clears. The Hard camp is where the
curve first bites: 15 defenders including four 320-health Trolls, iron walls, an arcane spire, and a
15-slot requirement the current cap cannot meet.

### A.7 Sources read on 2026-09-06

- Device logs: the three raid outcome lines (raid scored / stars settled / raid-end reconcile),
  the hero placement and first-damage timestamps (12:58:57.394 placed, 12:58:57.549 first hit,
  12:59:47.750 death), the loot settlement and repeat-clear lines, the bank-full lines.
- Canonical data: troops (stats, slots, cost, train time, unlock tier); building tiers (Barracks
  track and the Expanded Capacity perk); scene configs (the four camps, garrisons, difficulty,
  loot multipliers); enemies (defender stats); the raid loot tunables (star ladder, bases).
- Code read for rules only: raid scoring (star thresholds, 180 s clock, destruction weights,
  hero-death rule), army storage (base cap 10 plus perk bonus), the deploy view model (Power).

### A.8 Not measured tonight (open)

- The enemy level-scaling formula (hero took 15 per hit against a listed 10).
- The remaining 20% of the destruction score.
- Ogre stats (Hard camp).
- Gold actually banked on the 14:37 clear (the log line was cut).
- The Field Cleric's 205 gold cost: authored value, confirm.

### A.9 Ticket map

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
| Hero death continues the raid, result capped at 2 stars | minted 2026-09-06 from the owner's review |
| Raid Cache for overflow loot at a full bank | minted 2026-09-06 from the owner's review |
| Repeat-clear penalty softened to 100 / 60 / 100 | minted 2026-09-06 from the owner's review |
| Measure the enemy level-scaling formula | minted 2026-09-06 from the owner's review |
| Verify the Field Cleric's 205 gold cost | minted 2026-09-06 from the owner's review |
