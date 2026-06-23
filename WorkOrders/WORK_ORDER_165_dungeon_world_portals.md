# WORK ORDER 165 — Dungeon World Portals (the dungeon relocation: hidden portals in the zones)

**Status: READY TO IMPLEMENT (phased)**
**Priority:** Medium-High — the dungeon half of "crystals + dungeons move to world nodes." Crystals got
WO-153/154; dungeons need their own portal WO.
**Date:** 2026-05-30
**Lane:** gameplay + world. Code + scene-transition reuse; no `VillageSceneBuilder` rewrite; no bake by UI.
**Source:** owner — dungeons relocate from the village to world nodes; `ZONE_STREAMING_ARCHITECTURE.md`
"hidden dungeons — random portals on the map."

---

## Reconcile — reuse, don't rebuild
| Need | State | Reuse |
|---|---|---|
| Walk-in dungeon scenes + enter/return | **BUILT** — `DungeonController` + dungeon scenes (Healer's Cottage etc.) | the portal *loads* these; the destination already exists |
| Random region-gated spawner | **SPEC'd (WO-154)** — rare crystal spawner | **extend it** with a "dungeon portal" payload — don't write a second spawner |
| Region identity / depth | WO-164 `ZoneManager`/`ThreatLevel` | richer dungeons deeper/deadlier |
| Removed village portal generator | WO-150 stripped it | dungeons live in the world now, not the town |
| Zone state record | WO-164 `ZoneState` | a discovered portal flips a persisted flag |

## Build
A **hidden dungeon portal** spawns at a random valid point **within eligible zones**, marked by a portal
cue (code-built VFX — reuse `PortalVFXController`), discovered by exploring, entered to load the dungeon,
returning the player to the same world spot on exit.

- **Spawn:** extend the WO-154 region-gated random spawner with a `DungeonPortal` payload type (eligible
  regions = designer-set, deeper/deadlier host rarer/richer dungeons via `ThreatLevel`).
- **Persistence (vs the rare-crystal twin):** a portal **persists once discovered** (or until entered),
  not time-out-despawn — discovery sets `ZoneState` dungeon flag (a found dungeon is remembered).
- **Enter/return:** reuse `DungeonController`'s transition — portal → load dungeon scene → on exit return
  to the world spot. Reuse the additive-load pattern (`WorldSceneLoader`).
- **Which dungeon:** map portal → a dungeon scene/def (the existing dungeons + room for new ones); region/
  depth can gate which dungeon tier appears.

## Phases
- **P1 — Portal payload + spawn** (extend WO-154 spawner; region-gated; persistent-on-discover).
- **P2 — Enter/return** (wire portal → `DungeonController` load → return-to-world-spot).
- **P3 — Region/depth gating + rewards** (deeper region = richer dungeon; `ZoneState` remembers).

## Constraints
- Reuse WO-154 spawner, `DungeonController`, `PortalVFXController`, `WorldSceneLoader`, WO-164 ZoneManager.
  **No second spawner, no new transition system.** Code-built cue (no UXML). Village→Core only; `?.`.

## Acceptance criteria
1. Hidden dungeon portals spawn at random valid points in eligible zones (via the WO-154 spawner extended, not a new one).
2. A portal **persists once discovered** (ZoneState flag), unlike the time-limited rare crystal.
3. Entering loads the dungeon (reuse `DungeonController`); exiting returns to the same world spot.
4. Deeper/deadlier regions host rarer/richer dungeons (ThreatLevel-gated).
5. Built on existing dungeon + spawner + portal-VFX + zone systems; brace balance; no bake.

## Open questions for owner
- **Eligible regions:** all four, or deep-only (Mirewood/Ashwood)? (Default: all, richer deep.)
- **New dungeons or reuse existing?** (Default: reuse the built dungeons first; new dungeon content is its own WO.)
- **Discovery aid:** silent, or a faint compass/HUD ping when near an undiscovered portal? (Recommend faint ping.)

## Done checklist (CLAUDE.md §10)
- [ ] Portals spawn region-gated via the extended WO-154 spawner; persist on discovery (ZoneState)
- [ ] Enter→dungeon (DungeonController) → return to world spot
- [ ] Region/depth gates dungeon richness; built on existing systems
- [ ] Brace balance; Village→Core only; no bake/UXML
- [ ] `WORK_ORDER_165_dungeon_world_portals.RESULT.md` when complete
