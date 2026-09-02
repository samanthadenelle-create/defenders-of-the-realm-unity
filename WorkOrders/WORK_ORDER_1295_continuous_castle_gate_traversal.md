# WORK ORDER 1295 — Continuous Castle Gate Traversal

**Status:** HERO HALF FELT-VERIFIED BY OWNER 2026-09-02 — enemy-pathing half still unproven. NOT closed (PO closes, CLAUDE.md §13).

- **Owner felt-test 2026-09-02, verbatim: _"i can now go through gates normally"_ / _"thats a solid win"_.** This is the
  evidence the headed proof structurally could NOT produce: `GateTraversalProof` drives the hero with
  `agent.Move(outward * 0.55f)` — a direct clamped displacement, not the player input path — so it could only ever
  demonstrate that the navmesh SURFACE is continuous. Whether the opening feels smooth under a thumb is a felt
  judgement and only the owner can make it. She has. The retirement of the `GateWarp` + runtime `NavMeshLink`
  (`GateTraversalInjector` reduced to a compatibility no-op) is confirmed correct for the HERO.
- ⚠ **STILL OPEN — the enemy/AI half.** The deleted `NavMeshLink` existed specifically to give *pathfinding agents* a
  real graph EDGE through the wall thickness (old span r=37 -> r=41). Walking across a surface and `SetDestination`
  across it are different guarantees: if the re-bake produced two navmesh surfaces that merely ABUT, `Move` slides
  over the join while `CalculatePath` still returns `PathPartial`, and enemies would quietly stop pathing into town.
  A `NavMesh.CalculatePath == PathComplete` assertion (4 gates x in + out = 8 routes) has been added to
  `GateTraversalProof` for exactly this and has NOT yet been run. Do not close this WO on the felt-test alone.
- Also unresolved and carried: `FeatureFlags.GateTraversal` (`Assets/_Modules/Core/FeatureFlags.cs:424-434`) is now an
  ORPHANED flag with zero consumers, and two comments still describe the deleted warp as live
  (`CastleWallNavObstacleInstaller.cs:39`, `SyntyCastlePerimeterBuilder.cs:371`). Canon rides with the change (§15).

*(Was: **Status:** IN PROGRESS — 2026-09-01)*

## Player report

The hero and enemies catch at the four castle entrances. The merged overworld is one scene, but runtime code adds `NavMeshLink` objects and teleports the input-driven hero between two gate anchors. This makes a visually open doorway behave like a seam.

## Root cause

`GateTraversalInjector` treats the four openings as disconnected navigation islands. It creates a `NavMeshLink` for AI and a `GateWarp` for the hero even though `Main_Castle_Overworld` is continuous. The warp masks collider/navmesh geometry errors and introduces a sticky trigger boundary. Gate jamb colliders also require proof that their bottoms reach the terrain and leave the authored opening clear.

## Patch

- Retire runtime gate links and hero warps in the merged overworld.
- Keep gate art non-blocking; walls/jambs own collision and terminate at the opening.
- Add a headed proof mode and capture runner for all four exits.
- Prove zero `GateWarp` and zero gate `NavMeshLink` instances while walking.

## Acceptance

- North, south, east, and west exits are crossed by ordinary locomotion, without a warp or nav link.
- A screenshot exists before movement and after every movement pulse until the hero is fully outside.
- Evidence records elapsed time, position/radial progress, consecutive-frame image deltas, and start/final comparison for each entrance.
- No gate-owned collider floats above terrain or spans the opening.

