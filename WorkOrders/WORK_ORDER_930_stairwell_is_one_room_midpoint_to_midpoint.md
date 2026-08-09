# WO-930 — The stairwell is ONE room: midpoint to midpoint, run derived from footprint

**Status: ✅ SHIPPED 2026-08-08** — `3ab1bfb6` (the one-room stairwell; **the first floor-to-floor
`PathComplete` in project history**, old pair-model probe kept as a control) → `e7163c9c` (skinned via
shared `RoomForgeMaterials`, 0 bad surfaces) → `5f0e23aa` (candle lights + a caught RED gate:
`dg_sunken_vault.json` dual-copy drift) → `cb092b7f` (**all 4 content dungeons PathComplete, 12 descents,
0 mate failures, 14/14 dual-copy parity**; `dg_descent_probe`/`dg_stair_rig` deliberately left on the old
model as controls) → `51a89364` (`RoomPrefabMeta` on `StairwellRoom`; oracle rewritten, 8 new cases,
3 legacy quarantined). **Original status: READY TO IMPLEMENT · SHIP-BLOCKING.**
⚠ **No `.RESULT.md` exists for this WO** — recorded as debt in `CANON_GROUND_TRUTH_2026-08-09.md` §7.
**Date:** 2026-08-08 · **Priority:** ~~SHIP-BLOCKING~~ (delivered; every multi-level dungeon depended on it)
**Lane:** Dungeons / RoomForge · **Author:** the owner (design + diagram). CLI wrote it up.
**Replaces:** the `_Up`/`_Down` pair model. **Supersedes as an approach:**
`DESIGN_CONNECTOR_IS_THE_ONLY_CONTRACT.md`, which reached for the same idea the harder way.
**Closes the root cause found in:** WO-927.

---

## 1. The rule

> **A stairwell is ONE room that owns its own subrooms.**
> **It connects the MIDPOINT of the upper floor to the MIDPOINT of the lower floor.**
> **Its run is the room's footprint. Its slope is DERIVED from run and rise, never authored.**
> **The upper level is a PARTIAL floor — a gallery — so the stair rises through OPEN AIR.**
> **Outside, it is an ordinary room with ordinary sockets. Nothing else changes.**

Everything below follows from those five lines.

---

## 2. Why this is the fix and not a refactor

Five separate defect classes stop existing. Not "become less likely" — **cease to have a mechanism.**

| Defect (all observed today) | Why it cannot occur |
|---|---|
| Flight rotated out of alignment with its floor hole (`AssemblyYaw`) | there is no hole |
| Flight climbing through its own solid ceiling (measured: ZERO overlap) | there is no ceiling shaft |
| Pair delta-yaw arbitrary per instance (only 180 worked: 2/4, 3/5, 1/3, 0/1) | there is no pair |
| Placement-order dependence (half the pairs skipped the vertical mate entirely) | there is no vertical mate |
| Slope pinned at 42.7 deg with 2.3 deg of margin | run is the footprint, not one room's interior |

And the sixth, which is likely the one still blocking the navmesh **right now**:

**THE STAIR NO LONGER SQUEEZES UNDER A SLAB.** Today the flight passes beneath the upper floor
with **2.36 m** of headroom against a **2.0 m** `agentHeight` — a 0.36 m margin, at the exact point
the navmesh fails to carve. Under a gallery the stair rises through open volume: clearance becomes
the full room height and the pinch is gone.

⚠ **This is a hypothesis about the carve, NOT a proven cause.** It is consistent with the evidence
(alignment corrected, geometry visibly landing in the hole, ramp still not carving) but it has not
been isolated. Do not present it as settled in the RESULT.

---

## 3. The geometry

**Run is midpoint to midpoint.** Today the flight is anchored EDGE to EDGE inside one room — bottom
2.0 m off the south wall (`EntryPadDepth`), top 1.5 m short of the north wall (`TopLandingDepth`) —
which caps the run at 6.5 m in a 10 m room and pins the slope. Anchoring at midpoints makes the run
the room's own pitch, and lengthening the room lengthens the run instead of driving the top nose into
a wall (the owner watched it land in one).

Slope for `FloorSeparationY = 6`:

| Footprint pitch | Run | Slope | Margin to 45 deg |
|---|---|---|---|
| 10 m (1 cell) | 10.0 | **31.0 deg** | 14 |
| 12 m | 12.0 | **26.6 deg** | 18 |
| 14 m | 14.0 | **23.2 deg** | 22 |
| 20 m (2 cells) | 20.0 | **16.7 deg** | 28 |
| *today, edge-anchored* | *6.5* | *42.7 deg* | *2.3* |

⚠ **45 deg is a CLIFF, not a target.** `NavMeshAreas.asset` agent 0 has `maxSlope = 45`, and this
builder already documents the consequence at `DefaultStairConnectorRoomsBuilder.cs:115-117`:
*"At 3.0 the slope reaches 45.0 deg — exactly the agent maximum, i.e. the ramp stops carving at all."*
Aim for 25-31 deg. **Never derive a slope that lands near 45.**

**The room takes more cells rather than changing the grid.** `RoomForgeCanon.Cell` stays **10** and
stays **even** (the composer's header warns an odd cell puts sockets on halves and `RoundToInt`
quantises a unit of drift per stairwell — the original bonecrypt/ember_deep abort). The stairwell
simply CLAIMS more cells: a 2x1 claim gives a 20 m run at 16.7 deg. No kit rebuild, no re-authored
coordinates, no new grid concept.

---

## 4. Structure — per the owner's section drawing (2026-08-08)

```
+-------------------------------------------------------------------+
|                                                                   |
|   S===============+          [GAP]          +==================S  |   <- UPPER: two partial
|        floor      |            |            |      floor          |      floors, gap between
|                    \           |           /                      |
|                     \      staircase      /                       |
|                      \         |         /                        |
|   S===========================================================S   |   <- LOWER: full length
|                          floor                                    |
+-------------------------------------------------------------------+
        S = socket (any floor edge, either level)
```

- **One volume, no internal barriers.** A/B/C/D are subrooms of a single room.
- **The LOWER floor runs the FULL length** of the room.
- **The UPPER level is TWO PARTIAL FLOORS**, one at each end, with a **GAP between them**. That gap is
  a deliberate structural element, not leftover space — it IS the stairwell void.
- **The staircase descends through the gap**, from an upper floor's inner edge down to the lower floor.
  Because it spans the gap rather than fitting between two walls of one room, the run is long and the
  slope is shallow. **This is what buys the angle.**
- **Sockets sit on every floor EDGE, at BOTH levels** — both ends of the lower floor, and the outer end
  of each upper floor. A connector can therefore be at any level, at either end, and the composer mates
  every one of them identically.
- **Connectors on the perimeter, at whichever level.** A socket already carries its own local
  position INCLUDING Y, and `SolveMate` solves `pos = pPos - rotatedSocket`, so **height resolves for
  free** — that is exactly how today's pair lands `FloorSeparationY` apart. A gallery-level socket
  mates by the ordinary planar path.

**Consequence, and it is the big one: THE COMPOSER NEEDS NO CHANGE.** No level attribute on the
graph, no new mate rule, no ordering constraint. Two prefabs butt together, one socket empties into
the next. All multi-level complexity lives INSIDE a prefab where it is built once and proven once.

---

## 5. What gets DELETED (not deprecated)

- `RoomSocketType.StairUp` / `StairDown` — they only ever existed because a socket could not carry a
  third dimension
- `SolveMate`'s degenerate vertical branch — **the line that caused WO-927**
- `IsVertical`, the `SEALED_VERTICAL` seal branch, the 3D nudge for stair pairs
- `PlaneCuts` floor holes and `DeclareShafts` ceiling shafts, for stairs
- `DungeonBaker`'s `StairConnectorAliases` bake-time rename
- The `_Up` / `_Down` prefab split and its "one owner owns the flight" contract

---

## 6. What must NOT be broken

- **`RoomsOverlap` MUST learn vertical extent.** `DungeonBakerChecks.RoomsOverlap:190` returns false
  whenever two rooms differ by more than half a floor — correct for single-storey rooms, **wrong for a
  room that IS two storeys.** Without this, a stairwell can be placed straight through a neighbour and
  the check will never look. `RoomPrefabMeta` gains an extent; default = one floor, so every existing
  room is byte-unchanged.
- **`[room-shell]` coverage.** The oracle samples the footprint and asserts coverage outside a declared
  shaft. A gallery is a legitimate partial floor and must be DECLARED, not treated as a hole.
- **Keep the `Descend` / `Climb` teleport ports until a bake reports `PathComplete`.** Removing the
  only working traversal before its replacement is proven is how a dungeon becomes unplayable.

---

## 7. Acceptance

- [ ] `dg_descent_probe` bakes **`PathComplete`** (today: `PathPartial`, 0/1 ramps whole).
- [ ] All five dungeons report every ramp whole end-to-end (today: 2/4, 3/5, 1/3, 0/1).
- [ ] A `NavMeshAgent` walks floor-to-floor — **not just the hero.** Zero `NavMeshLink`/`OffMeshLink`
      components exist today, which is why enemies, companions and pets are stranded on their spawn
      floor. If the gallery ramp carves as ordinary navmesh, this falls out; **verify it, do not assume.**
- [ ] Derived slope recorded per stairwell and asserted **below 40 deg**.
- [ ] An oracle proves the stairwell prefab is internally traversable **at prefab-build time**, so it is
      true every time it is placed rather than re-derived per bake.
- [ ] Owner felt-verifies and closes.

---

## 8. Provenance

The design, the diagram and every correction in it are the owner's, 2026-08-08, arrived at while
walking `dg_ember_deep` in the editor. Her corrections, in order — each one moved this:

1. *"see steps extend through actual floor ... which is why the plane couldnt make a level"* — the root
   cause, found by eye after four bake-and-correlate rounds found nothing.
2. *"we need that edge"* — a navmesh is a shared-edge graph; rotation alone was never sufficient.
3. *"lands in a wall"* — killed the idea that a longer run fits inside a 10 m room, which is what forced
   midpoint anchoring.
4. *"connect center of room with center of lower room"* — the anchoring rule.
5. *"it's all one big cube now ... two half floors"* — the gallery, which removes the slab over the stair.
6. *"the socket is completely fine"* — correctly refuted the CLI's claim that the graph needed a level
   field; the socket already carries Y.
