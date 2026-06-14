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
