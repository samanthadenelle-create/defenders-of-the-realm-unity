# Raid Pillar — Vision (owner brainstorm 2026-06-14)

The Warcraft half of the North Star: attack heavily-defended enemy strongholds. Owner riffed the
shape this session; captured here so we build to ONE coherent target. **Keep the core loop simple and
UNBOUNDED — the depth comes from map complexity + stakes, not rules.**

## The fantasy
"They lure you in. They pincer you. They stay out of range and use catapults." Raids are **layered
defense puzzles** — you don't just walk in and swing; you breach, survive a crossfire, push to a keep.

## Insanely-hard flagship raids (complex maps)
1. **The Iron Bastion** *(concentric fortress)* — double wall ring: outer Iron palisade → a **killing
   courtyard** raked by functional catapult/arcane turrets → inner ReinforcedSteel **keep** with the
   necromancer boss + elite guard. **Offset gates** (inner gate opposite the outer) force you to run
   the gauntlet under fire instead of beelining.
2. **The Bloodmaw Warren** *(brute pit)* — open approach **funnels into a walled choke**; trolls +
   ogres **pincer from side alcoves**, catapults rain from elevated clusters. Melee-punishing.
3. **The Hollow Spire** *(mage enclave)* — **arcane AoE towers** ring a shielded core; acolytes +
   orc-shamans kite, the necromancer raises your fallen. Ranged kill-zone; punishes the rush.

## What makes a map "complex" (generator capabilities to build)
Data-driven via a `layout` field on scene-configs so the owner can author more without code:
- **Concentric rings** (outer + inner keep) — extend `PerimeterWallGenerator` to N rings at decreasing radius.
- **Offset gates** — inner ring's gate on a different side than the outer (force traversal).
- **Functional turret lines/clusters** — EnemyOwned `DefenseTower`s (shipped 2026-06-14) along walls + corners for crossfire.
- **Tiered garrison** — outer fodder → courtyard mid → inner elite + boss in the keep.
- Layouts: `concentric` · `gauntlet` (choke + alcove ambush) · `enclave` (ranged ring + shielded core).

## Lifecycle (owner constraint)
**Raids are loaded ONLY when needed and DESTROYED on leave** — additive scene streaming, never always
in memory. The generator bakes/streams a raid scene on entry; exit unloads it. (Aligns with
`SceneOwnership` enemy-gating + the perf discipline — no idle raid scenes resident.)

## Progression hooks
- **Quest-gated rewards:** specific raids gate quests/unlocks — "complete The Iron Bastion → unlock
  <item>." Wires the raid result into `QuestService` (WO-290) / keystones; raids become content gates,
  not just sandbox fights.
- **Eventually: SKR wager on raids** — stake SKR on entry, clear the base = win the pot, wipe =
  forfeit. Mirrors the Arena SKR-wager stub (client-stub, WebGL-safe). **Additive layer on top of the
  simple raid loop — not a rework.** The simpler the core stays, the cleaner this drops in.

## ✅ Raid data contract — ONE system (architect decision 2026-06-14)
`NPCBaseConfig` = `RaidTemplate` = **the existing `scene-configs.json`** — the same blueprint named three
ways across the brainstorm. Do NOT fork parallel systems. **Extend the one contract** (already consumed by
`RaidBaseGenerator` + the player-level scaler). Fields to ADD to `scene-configs.json`: `recommendedClearTime`
(→ 1★/2★/3★ thresholds, fed by the reused countdown timer), `rewardMultiplier` (resources + Echo-shard rate),
`entranceCount` (1–3: main gate + side breaches), `interiorWallLayers` (0–2: kill-zones), `towerPlacementStyle`
(Circular / Cardinal / OverlappingFire), `eliteCount`, `roleDistribution` (archer/warrior/mage/healer %),
`specialModifiers` (e.g. fog, reinforcements-after-N-min). Note: owner calls the builder "NPCBaseBuilder
(WO-452)" — that's `RaidBaseGenerator` (shipped); WO-452 on the board is the build-palette bug (number
collision — troops/raids use their own WO numbers).

## The 3 flagship raid levels (map 1:1 onto existing configs — enrich, don't create)
| Level | Existing config | Theme | 3★ time | Walls / gates | Towers | AI troops |
|---|---|---|---|---|---|---|
| **1 · Raider Outpost** (Regular/tutorial) | `raider_camp_small` | bandit camp | < 4:30 (270s) | Wood, 8–10/side, **2 gaps** | 4 archer (corners) + 1 weak mage | 12–16 (Footmen+Archers, 1–2 Mage) |
| **2 · Fortified Garrison** (Hard) | `fortified_garrison` | military outpost | < 5:30 (330s) | Wood+Stone, **1 gate** | 6 archer (T2–3) + 2 mage **crossfire** | 22–28 (balanced; archers on walls, warriors at gate, mages center) |
| **3 · Mage Enclave** (Extreme) | `mage_enclave` | arcane fortress | < 7:00 (420s) | Stone/Obsidian, tight, **interior kill-zones** | 8 archer + 3 mage (elevated, overlapping) | 35–45 (heavy mage + elite warriors, synergy) — top shard rate |
Later variants (cheap, same contract): Dragon Cult Ruins · Undead Necropolis · Troll Bridge choke. **Mind the
mobile perf ceiling** (Extreme 45 AI + your cap-30 army + towers — cap live combatants, see WO-453).
Sequencing: these are the **deploy targets** for the troop slice — build after the troop deploy verbs (or in
parallel, it's data + generator work, disjoint from the felt verbs).

## Foundations already shipped (this builds on them)
- Functional EnemyOwned turrets that shoot the player (`DefenseTower.TowerAllegiance` + `GarrisonController.ArmGarrisonTurrets`).
- Enemy family variety (`WaveCompositionBuilder` orc/troll/ogre by wave band).
- Tier walls (Wood/Iron/ReinforcedSteel via `WallTierData`) + `PerimeterWallGenerator` (corner towers + natural gate).
- `SceneOwnership` (enemy bases gate player build/upgrade; death retreats to hub, raid ends).
- `scene-configs.json` data contract (ownership, wallTier, garrison composition + boss, player-level scaling).

## Open / needs owner calls (deferred, not blocking the core)
- ATB combat for raids: ATB was built **single-hero** originally — party/companions need wiring into the
  battle stage (the "no party" bug). Decide ATB-vs-real-time for raid bosses.
- ATB defeat routing (Bug 4) + background (Bug 3) — see ATB RCA.
- Exact difficulty bands + reward tables per flagship raid.

**Next build step:** extend `PerimeterWallGenerator` → a raid-base generator that reads `layout`/`rings`
from an enriched scene-config and bakes concentric rings + offset gates + turret lines + a boss keep,
streamed on demand. Then author the 3 flagship configs.
