# Wave authoring reference — the 20-wave schedule that `waves.json` used to carry

> **Status:** REFERENCE / DESIGN INTENT. This is **not** live data and is **not** read by any code.
> **Minted:** 2026-07-30 (CLI), preserving work that would otherwise have survived only in git history.

## Why this file exists

`waves.json` carried an authored 20-wave enemy schedule -- families rotating hollow/orc/troll, counts
escalating, gate routing, staggered release intervals, a Necromancer boss cadence every 6 waves, and the
apex dragon as the terminal wave. **None of it ever ran.**

`WaveManager.StartWave` runs the WO-362 smart-composition path first and only falls through to the
authored batches when it spawned nothing; `_smartComposition` is serialized `1` in both live hubs and both
carry spawn points, so the smart path always succeeds. Every `enemies[]` field -- type, count, spawnPoint,
delay, interval -- was discarded on every wave of every session. Only `countdownSeconds`, `boss` and
`apexBoss` ever took effect.

That supersession was deliberate (WO-362, mid-June: *"use new composer instead of flat spawning"*). The
**data** is what went wrong: this schedule was authored 2026-07-11, roughly four weeks AFTER the batches
went inert, against a port that no longer runs. Nothing said so.

**Owner ruling 2026-07-30:** smart composition stays the authority; the inert batches are stripped from
`waves.json` so the file stops lying. The design thinking is kept here.

## Where this should go

The architectural end-state is that the two authorities **compose** rather than compete:
`WaveCompositionBuilder.Build` SEEDS from an authored roster (families/counts as the tier input) and
applies tactical positioning, rotating gates, anti-repeat and elite cadence on top. When that work happens,
this table is the seed data. Until then it is history, not a spec.

Note two authored intents the generated path currently contradicts, worth honouring in any seeding work:

- **Wave 3 "The Deep Ones"** authors troll + ogre, but the generated brute pool cannot produce either
  before wave 6 -- the set-piece is unreachable today.
- **Wave 20** authors an EMPTY roster (*"the dragon IS the wave"*) yet generation still fields ~21 ground
  enemies -- the authored intent is inverted, not merely ignored.

## The authored schedule (as it stood at strip time)

Batch fields: `type` = enemies.json id - `count` - `spawnPoint` (spawn-0 N / -1 E / -2 S / -3 W)
- `delay` s after wave start - `interval` s between spawns.

### Wave 1 - First Light

- countdown 45s  *(these three STILL take effect - they were not stripped)*

> WO-316 FAMILY COMPOSITION: each wave is authored as ONE Hollow squad with a ROLE MIX (tank + healer + a few DPS), not a clone-swarm. WaveManager.SpawnComposedFamilyGroups buckets these batches by enemies.json 'family', stamps each member's EnemyRole from its 'role' (brute->Tank, caster->Healer, skirmisher->Ranged, grunt->DPS), and releases the whole squad as ONE coordinated group (formation spread + 'hold then charge together'). Keep all batches in a wave on the SAME family + SAME spawnPoint so they compose into a single cohesive squad. Counts scale up per wave.

| type | count | spawnPoint | delay | interval |
|---|---|---|---|---|
| `hollow-walker` | 2 | spawn-0 | 0 | 1.4 |
| `hollow-warrior` | 1 | spawn-0 | 0 | 1.4 |
| `hollow-acolyte` | 1 | spawn-0 | 0 | 1.4 |

### Wave 2 - The Warband Comes

- countdown 300s  *(these three STILL take effect - they were not stripped)*

> WAVE 2 = ORC WARBAND (enemy-variety 2026-06-13). A cohesive greenskin squad — berserker brutes + a shaman caster + a necromancer elite — so wave 2 is a DISTINCT faction, not another Hollow clone-swarm. All 'orc' family → composes as one orc squad.

| type | count | spawnPoint | delay | interval |
|---|---|---|---|---|
| `orc-berserker` | 3 | spawn-0 | 0 | 1.2 |
| `orc-shaman` | 1 | spawn-0 | 0.6 | 1.2 |
| `orc-necromancer` | 1 | spawn-0 | 1.2 | 1.2 |

### Wave 3 - The Deep Ones

- countdown 300s  *(these three STILL take effect - they were not stripped)*

> WAVE 3 = BRUTE BAND (enemy-variety 2026-06-13). Cave Trolls + an Ogre (the 'troll' brute family — big slow silhouettes) with a pair of orc-berserkers for front-line pressure. The heavy payoff wave of the opening act (the apex dragon now closes the schedule at wave 20, F8-44).

| type | count | spawnPoint | delay | interval |
|---|---|---|---|---|
| `troll` | 2 | spawn-0 | 0 | 1.6 |
| `ogre` | 1 | spawn-0 | 1.5 | 1.6 |
| `orc-berserker` | 2 | spawn-0 | 0.4 | 1.2 |

### Wave 4 - The Green Tide

- countdown 300s  *(these three STILL take effect - they were not stripped)*

> WAVES 4-19 (F8-44, owner 2026-07-11): the escalating mid-schedule. Families rotate orc → troll → hollow so factions alternate; spawn gates rotate spawn-1 → spawn-2 → spawn-3 → spawn-0 so the siege shifts around the compass; counts grow gradually (~wave-3 weight at 4, ~2.5-3x by 19 — WaveScalingCurve handles stat escalation by wave number). Necromancer of the Wound boss cadence at 6/12/18.

| type | count | spawnPoint | delay | interval |
|---|---|---|---|---|
| `orc-berserker` | 3 | spawn-1 | 0 | 1.2 |
| `orc-shaman` | 2 | spawn-1 | 0.8 | 1.2 |

### Wave 5 - Stonebreakers

- countdown 300s | **apexBoss:** `boss-dragon-syndrath` hp 4200  *(these three STILL take effect - they were not stripped)*

> TEST ONLY (2026-07-24) — apex dragon 'Syndrath' bolted onto wave 5 so the owner reaches the flying boss fast for testing. REVERT before ship: delete the 'apexBoss' block below (the real apex wave is 20). Enemies left intact so the wave still plays; the dragon spawns alongside them via WaveManager.SpawnApexBoss.

| type | count | spawnPoint | delay | interval |
|---|---|---|---|---|
| `troll` | 2 | spawn-2 | 0 | 1.6 |
| `ogre` | 2 | spawn-2 | 1.2 | 1.6 |

### Wave 6 - The Wound Speaks

- countdown 300s | **boss:** `necromancer`  *(these three STILL take effect - they were not stripped)*

> BOSS WAVE (cadence 6): the Necromancer of the Wound walks with the Hollowed.

| type | count | spawnPoint | delay | interval |
|---|---|---|---|---|
| `hollow-walker` | 3 | spawn-3 | 0 | 1.4 |
| `hollow-warrior` | 2 | spawn-3 | 0.6 | 1.4 |
| `hollow-acolyte` | 1 | spawn-3 | 1.2 | 1.4 |

### Wave 7 - The Deathspeaker's Levy

- countdown 300s  *(these three STILL take effect - they were not stripped)*

| type | count | spawnPoint | delay | interval |
|---|---|---|---|---|
| `orc-berserker` | 4 | spawn-0 | 0 | 1.2 |
| `orc-shaman` | 2 | spawn-0 | 0.8 | 1.2 |
| `orc-necromancer` | 1 | spawn-0 | 1.6 | 1.2 |

### Wave 8 - The Warrens Empty

- countdown 300s  *(these three STILL take effect - they were not stripped)*

| type | count | spawnPoint | delay | interval |
|---|---|---|---|---|
| `troll` | 3 | spawn-1 | 0 | 1.6 |
| `ogre` | 2 | spawn-1 | 1.2 | 1.6 |

### Wave 9 - March of the Forgotten

- countdown 300s  *(these three STILL take effect - they were not stripped)*

| type | count | spawnPoint | delay | interval |
|---|---|---|---|---|
| `hollow-walker` | 4 | spawn-2 | 0 | 1.4 |
| `hollow-warrior` | 2 | spawn-2 | 0.6 | 1.4 |
| `hollow-rogue` | 2 | spawn-2 | 1.0 | 1.0 |
| `hollow-acolyte` | 1 | spawn-2 | 1.4 | 1.4 |

### Wave 10 - Warband Ascendant

- countdown 300s  *(these three STILL take effect - they were not stripped)*

| type | count | spawnPoint | delay | interval |
|---|---|---|---|---|
| `orc-berserker` | 5 | spawn-3 | 0 | 1.2 |
| `orc-shaman` | 2 | spawn-3 | 0.8 | 1.2 |
| `orc-necromancer` | 1 | spawn-3 | 1.6 | 1.2 |

### Wave 11 - Oak and Stone

- countdown 300s  *(these three STILL take effect - they were not stripped)*

| type | count | spawnPoint | delay | interval |
|---|---|---|---|---|
| `troll` | 3 | spawn-0 | 0 | 1.6 |
| `ogre` | 3 | spawn-0 | 1.2 | 1.6 |

### Wave 12 - The Second Dirge

- countdown 300s | **boss:** `necromancer`  *(these three STILL take effect - they were not stripped)*

> BOSS WAVE (cadence 12): the Necromancer returns at the head of a full Hollow column.

| type | count | spawnPoint | delay | interval |
|---|---|---|---|---|
| `hollow-walker` | 4 | spawn-1 | 0 | 1.4 |
| `hollow-warrior` | 3 | spawn-1 | 0.6 | 1.4 |
| `hollow-rogue` | 2 | spawn-1 | 1.0 | 1.0 |
| `hollow-acolyte` | 2 | spawn-1 | 1.4 | 1.4 |

### Wave 13 - The Red Banner

- countdown 300s  *(these three STILL take effect - they were not stripped)*

| type | count | spawnPoint | delay | interval |
|---|---|---|---|---|
| `orc-berserker` | 6 | spawn-2 | 0 | 1.2 |
| `orc-shaman` | 2 | spawn-2 | 0.8 | 1.2 |
| `orc-necromancer` | 1 | spawn-2 | 1.6 | 1.2 |

### Wave 14 - Giants at the Gate

- countdown 300s  *(these three STILL take effect - they were not stripped)*

| type | count | spawnPoint | delay | interval |
|---|---|---|---|---|
| `troll` | 4 | spawn-3 | 0 | 1.6 |
| `ogre` | 3 | spawn-3 | 1.2 | 1.6 |

### Wave 15 - The Hollowed Hundred

- countdown 300s  *(these three STILL take effect - they were not stripped)*

| type | count | spawnPoint | delay | interval |
|---|---|---|---|---|
| `hollow-walker` | 5 | spawn-0 | 0 | 1.4 |
| `hollow-warrior` | 3 | spawn-0 | 0.6 | 1.4 |
| `hollow-rogue` | 2 | spawn-0 | 1.0 | 1.0 |
| `hollow-acolyte` | 2 | spawn-0 | 1.4 | 1.4 |

### Wave 16 - The Last Levy

- countdown 300s  *(these three STILL take effect - they were not stripped)*

| type | count | spawnPoint | delay | interval |
|---|---|---|---|---|
| `orc-berserker` | 6 | spawn-1 | 0 | 1.2 |
| `orc-shaman` | 3 | spawn-1 | 0.8 | 1.2 |
| `orc-necromancer` | 2 | spawn-1 | 1.6 | 1.2 |

### Wave 17 - The Mountain Moves

- countdown 300s  *(these three STILL take effect - they were not stripped)*

| type | count | spawnPoint | delay | interval |
|---|---|---|---|---|
| `troll` | 4 | spawn-2 | 0 | 1.6 |
| `ogre` | 4 | spawn-2 | 1.2 | 1.6 |

### Wave 18 - The Third Dirge

- countdown 300s | **boss:** `necromancer`  *(these three STILL take effect - they were not stripped)*

> BOSS WAVE (cadence 18): the Necromancer's final walk — the heaviest Hollow column before the sky itself turns.

| type | count | spawnPoint | delay | interval |
|---|---|---|---|---|
| `hollow-walker` | 5 | spawn-3 | 0 | 1.4 |
| `hollow-warrior` | 4 | spawn-3 | 0.6 | 1.4 |
| `hollow-rogue` | 3 | spawn-3 | 1.0 | 1.0 |
| `hollow-acolyte` | 2 | spawn-3 | 1.4 | 1.4 |

### Wave 19 - Eve of the Wing

- countdown 300s  *(these three STILL take effect - they were not stripped)*

| type | count | spawnPoint | delay | interval |
|---|---|---|---|---|
| `orc-berserker` | 7 | spawn-0 | 0 | 1.2 |
| `orc-shaman` | 3 | spawn-0 | 0.8 | 1.2 |
| `orc-necromancer` | 2 | spawn-0 | 1.6 | 1.2 |

### Wave 20 - The Last Wing

- countdown 300s | **apexBoss:** `boss-dragon-syndrath` hp 4200  *(these three STILL take effect - they were not stripped)*

> APEX WAVE — the Black Dragon, 'Syndrath the Devourer'. The terminal wave of the schedule and the realm's register breaking under the Withering (dragon-boss.md §2): a rare set-piece sky-boss ABOVE the canon Necromancer of the Wound. No ground enemy batches — the dragon IS the wave; it circles the Heart on its own kinematic flight and dives to strike. 'enemies' is left empty intentionally; WaveManager spawns the Boss_Dragon prefab from the 'apexBoss' block and calls DragonBoss.Configure(id, Heart, hp). hp 4200 mirrors the DragonBoss inspector default (well above the Necromancer's 1700). countdownSeconds 300 = the React LATER_PREPARE_SECONDS build window.

*No authored batches.*

---

**Totals preserved:** 19 waves declaring batches - 55 batch entries - 148 authored enemies.

Source: `Assets/Resources/Data/Canonical/waves.json` @ commit `167083da`, before the WO-783 D1 strip.
