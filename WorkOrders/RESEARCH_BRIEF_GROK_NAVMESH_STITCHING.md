# Research brief for Grok — modular dungeon navmesh: does per-room baking actually stitch?

**Paste this whole file to Grok.** It is written to be self-contained — no repo access needed.
**Date:** 2026-08-07 · **Asked by:** CLI seat, in parallel with an internal architect review.

---

## Context you need (and only this)

Unity **6000.4.8f1**, URP, Android/ARM64 mobile target (Solana Seeker). Using the **AI Navigation package**
(`NavMeshSurface` / `NavMeshData` / `NavMeshLink`), not the legacy baked-into-scene navmesh.

We build modular dungeons from room prefabs on a **10 m grid**. Rooms snap together at "connectors" (doorways).
Floors between levels are **6 m** apart. The exact agent settings, read from `NavMeshAreas.asset`:

```
agent id 0 "Humanoid"
  radius 0.5   height 2.0   climb 0.75   maxSlope 45°
  cellSize 0.1667   minRegionArea 2.0
```

Today one `NavMeshSurface` bakes the **whole assembled dungeon** at once
(`collectObjects = All`, `useGeometry = PhysicsColliders`).

We are considering switching to: **bake a navmesh into each room prefab once, then stitch rooms at runtime /
compose time.** We want to know whether that is sound before building it.

---

## THE QUESTION THAT DECIDES EVERYTHING

### Q1 — Do two separate `NavMeshData` instances connect to each other?

If room A and room B each carry their own baked `NavMeshData`, and both are added via
`NavMesh.AddNavMeshData(...)` such that their walkable surfaces **overlap or are exactly coplanar and touching
at the doorway** — can an agent path from A into B **without** a `NavMeshLink`?

**Our current hypothesis (please confirm or destroy):** No. A navmesh is a polygon mesh, and pathfinding walks a
**shared-edge** graph. Shared edges only exist if the polygons were **triangulated together**. Two separately
baked meshes were never cut against each other, so no shared edge exists — and *overlapping is not adjacency*.
Therefore `NavMeshLink` is mandatory, not optional.

**If that hypothesis is wrong** — if Unity connects surfaces by proximity/overlap within some tolerance — then
half our proposed design is unnecessary complexity, so please say so bluntly.

**What a good answer looks like:** a citation to Unity docs, the NavMeshComponents repo, or a Unity engineer's
statement. Not a forum guess. If the behaviour changed between the legacy NavMesh and the AI Navigation
package, say which applies to Unity 6.

### Q2 — Does `AddNavMeshData` respect the instance's rotation?

Modular rooms get placed at yaw **0 / 90 / 180 / 270**. If a prefab's `NavMeshData` is baked in local space,
does adding it with a position **and rotation** correctly transform the navmesh? Or is baked data effectively
world-locked, so rotated instances need a re-bake per orientation?

A design that only works at yaw 0 is not a design, so this is load-bearing.

---

## Secondary questions, in priority order

### Q3 — How does an agent traverse a short `NavMeshLink`?

A 22-room dungeon would carry ~21 links, one per doorway, each spanning roughly a **2 m** gap.

- Does the agent **walk** the link, or does it move discretely / snap across it?
- Is the motion controllable (`NavMeshAgent` autoTraverseOffMeshLink, speed, animation) or fixed?
- Does a **chasing** agent handle links well, or do pursuit paths stutter at every doorway?
- Any per-link runtime cost that matters at ~21 links on a mid-range mobile GPU/CPU?

**Why we care:** we are replacing a teleport with real stairs specifically so the movement stops feeling
discrete. Trading a teleport for 21 small snaps would defeat the purpose.

### Q4 — Voxel rasterization and a small vertical offset

If two walkable colliders overlap horizontally and one sits **0.01 m** above the other, with `cellSize 0.1667`:

- Does rasterization merge them into a single walkable span, or can it emit a 1-voxel step / seam?
- Is there a documented threshold at which two near-coplanar surfaces are treated as one?
- Does `agentClimb 0.75` matter here, or is it resolved before climb is considered?

### Q5 — Erosion at a doorway

Unity erodes walkable area by `agentRadius`. We hit this concretely: a stair landing **0.80 m** wide vanished
because 2 × 0.5 = **1.00 m** minimum.

- Is "walkable width ≥ 2 × agentRadius" the correct rule of thumb, or is there a subtler formula?
- Does `minRegionArea = 2.0` interact — can a thin-but-large-enough region survive erosion?
- **Is there a standard "clearance" rule modular-kit designers use** (e.g. minimum floor depth on each side of a
  doorway) so doorways cannot be severed by erosion?

### Q6 — What do shipped modular/procedural dungeon games actually do?

For runtime-assembled dungeons in Unity specifically:

- Per-room baked `NavMeshData` + links, or a whole-dungeon `NavMeshSurface.BuildNavMesh()` at load?
- If whole-dungeon at runtime: what is the realistic **cost** for ~20 rooms of ~10 m each on mobile? Is it a
  hitch, a loading-screen job, or async-able (`UpdateNavMeshDataAsync`)?
- Any well-known pitfalls with rotated prefab instances and pre-baked navmesh?

### Q7 — Is a final whole-scene bake ever unavoidable?

If navmesh is per-room, what **else** would still force a whole-scene bake?

- occlusion culling
- static batching
- **lightmaps** (we are fully realtime today — `shadows = None`, ~80 realtime point lights per dungeon — but if
  we ever bake lighting, does that reintroduce a mandatory scene step?)

---

## What we are trying to decide

Whether to keep **one whole-dungeon bake** (simple, monolithic, slow, editor-only) or move to **per-room baked
navmesh + links** (faster, modular, and the only version that permits **runtime-generated** dungeons).

**Please do not give us a balanced overview.** Give a recommendation, say what would change your mind, and flag
anything we have assumed that is not true. If the honest answer to Q1 is *"it depends on X"*, tell us what X is
and how to test it in the smallest possible scene.

---

## Ground rules for the answer

- **Cite sources** — Unity docs URL, package version, or repo. Distinguish documented behaviour from community
  folklore, and say which is which.
- **Say when you do not know.** An honest "not documented, here is the two-room test that settles it" is worth
  more to us than a confident guess. We have been burned tonight by exactly one confident recollection.
- **Note Unity version drift** — much of the NavMeshComponents material predates Unity 6 and the AI Navigation
  package rename. Flag anything that may be stale.
