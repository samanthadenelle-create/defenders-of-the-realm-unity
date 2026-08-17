> ⚠ **NUMBER COLLISION — this document does not own WO-467; `WORK_ORDER_467_region_gate_system.md` does.**
> Referred to hereafter as **WO-467-B (moat bridges / seam geometry)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> ownership decided by **first-on-disk** (`git log --follow --diff-filter=A`): the winner's file was created first.
> Banner only — nothing was renumbered or deleted.
> ⚠ **Work HAS shipped under this number** — commit messages and/or a `.RESULT.md` cite WO-467 for THIS document. It is deliberately **not renumbered**; a renumber would orphan those references. Use the alias above when you need to name it unambiguously.

# WORK_ORDER_467 (extension) — MOATED CASTLE + 4 CARDINAL PATHS (seam geometry)

**Status:** SPEC / DESIGN-READY · World/Environment lane (serial, editor-closed bakes) · extends WO-467 RegionGate.
**Scope:** SOUTH bridge = the V1 crossing (already shipped via RuntimeRegionGate); **N/E/W = polish.** The full
4-path moat + the outposts/camps it connects = **V2 world-building** (uses existing systems, not greenfield).

## Owner design (2026-06-23)
Make the seam read as DESIGNED, not a tech workaround: a **natural barrier (moat / lake / stream / terrain)** seals
the castle perimeter, with a **walkable path/bridge at each of the 4 cardinal gates (N/S/E/W)** as the only crossings.
"In essence 4 natural paths." The principle (owner's own insight): **boundaries guide the player** — the barrier is
NON-WALKABLE navmesh, so the hero is penned to the courtyard and can only leave via the 4 paths. No fat trigger needed.

**The paths are dual-purpose (owner):**
- **Player crossings** — castle ↔ OuterWorld.
- **Enemy ROAMING routes** — the 4 paths carry AI navmesh too, so reps/mobs/troops roam castle↔OuterWorld through
  the gates → the world feels ALIVE, not static.
- **Natural CHOKEPOINTS** — the gate openings are tactical pinch-points (defense, ambush, the WO-479 role-mix encounter).
- **Lead to OUTPOSTS + enemy CAMPS** — "can go to three outposts… enemy could have camps over there. lots of ways to go."
  The 4 paths fan out to the existing `EnemyOutpost`/`CampSystem`/`RaidOutpostSystem` content (currently gated off
  under the light-world flag) → the moated castle gives that content its hub + approach routes.

## DANGER-GRADIENT "earn it" (owner 2026-06-23) — the 4 paths are difficulty tiers
Place **naturally higher-level enemies on different cardinal sides**, so each of the 4 paths is an "earn it to go
this way" tier — soft-gating by DIFFICULTY, not walls. The player flows where they can survive and must get stronger
to push the harder direction; a too-tough mob you can't outrun makes a wrong turn *mean* something.
**Already canon + half-built — REUSE, don't greenfield:** `ZoneManager.ThreatLevel`/`DangerTier` (origin-centered
gradient), the per-region `RegionSpawnTable` rosters (Goldfields/Stoneback/Mirewood/Ashwood = distinct difficulties),
`GarrisonStatBlocks.ApplyLevelScale` (level the rosters per band), the red-skull tell `ThreatSkullPlate`. Memories:
`world-architecture-gated-regions-playable-connectors`, `overworld-encounter-isolated-battle` (the +5% chase / wide-
leash danger-gradient stake). Map: N/S/E/W bridge → a roster region of escalating threat; the outposts/camps on each
side scale to that band. This is what gives the moated-castle hub its progression spine.

## The trick (why it works, not just looks good)
The water/terrain ring is a **non-walkable navmesh hole** (blocks hero AND AI). The 4 bridge decks are the only baked
walkable connections. So geometry funnels everyone to the gates — the engine-level version of "level design guides."

## Implementation — REUSE, minimal new code (cite file:line)
- **Terrain moat trench:** `ExteriorTerrainBuilder.CreateTerrainData()` height loop (~352-378) — add a `MoatWeight()`
  blend (mirror `SeamWeight()` ~391 / `CorridorWeight()` ~526) carving a ring trench (~y -6.5) at radius ~55-70u, soft falloff.
- **Castle-side non-walkable ring:** `CastleHubBuilder.BuildNavMeshFloor()` (~667) — new `BuildMoatRing()` (NavMeshObstacle
  carving donut) sealing the perimeter, leaving the 4 cardinal gaps for the bridge decks.
- **4 bridge crossings:** `region-gates.json` → 4 rows `castle_to_outerworld_{south,north,east,west}` (type "bridge").
  `RuntimeRegionGate.TryBuildForScene` already loops rows — no code change; coords from `ReadSouthGatePos()` +
  `Quaternion.Euler(0,yaw,0)` rotation (the proven `BuildGateExitStrips`/`BuildInnerWallRing` pattern), landing from
  `WorldGeometry.SouthGateSeamLanding` (WO-483) else fallback. **Never hardcode coords.**
- **AI roaming:** RuntimeRegionGate already builds the AI `NavMeshLink` (~524-548) once OuterWorld is additive — the 4
  rows just drive it; enemies path the same decks as the hero.
- **Visible bridge mesh:** polyperfect `Bridge_*` (skip-safe LogWarning + plane fallback).
- **Bake:** ExteriorTerrainBuilder → CastleHubBuilder → OuterWorld NavMesh re-bake (editor closed).

## Scope / sequence
- **V1 (DONE):** south crossing live (RuntimeRegionGate: deck-centre trigger + 44m radius + beacon). The moat trench +
  non-walkable ring (south arc) make it *feel designed* — do these only if the beacon/wide-trigger felt-test isn't enough.
- **V2 polish:** complete the ring + N/E/W bridges + the outposts/camps the paths connect (re-enable EnemyOutpost/Camp
  behind the world flag) + chokepoint encounters (WO-479 role-mix). This is the living-overworld build.

## Risks
Moat radius vs re-centered terrain (tune the constant, screenshot-verify) · bridge deck welds (reuse the 18m-overlap +
Y-snap + deck-centre lesson) · don't break existing GateExit infra · keep the barrier non-walkable for AI too.
