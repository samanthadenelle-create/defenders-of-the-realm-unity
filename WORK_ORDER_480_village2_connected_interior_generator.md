# WORK ORDER 480 — Village2 (EnemyStrongholdBuilder): connected, walkable interior

**Status: IN PROGRESS — BLOCKED on undiagnosed fragmenter (2026-06-21)**

## Progress log (2026-06-21)
Generator improvements landed (carve outer ring only; walkable ramps replace keep/boss
NavMeshLinks; rubble non-NavigationStatic; no ReturnToOuterWorld_Seam; gate-prop colliders
stripped; inner gate widened). THREE regen+measure passes: islands **38 → 35 → 36** — barely
moved. `Spawn_Keep` still OFF-MESH; `arrival → Chokepoint/Keep` still **PathPartial**.

**NEW DIAGNOSTIC CLUE (the real fragmenter — undiagnosed):** the measure shows TWO islands in
nearly the SAME XZ space — `island[0] size456 x[-50..49] z[-33..33]` and
`island[2] size113 x[-41..43] z[-36..33]`, both y≈0, DISCONNECTED. That is two OVERLAPPING
navmesh sheets in the courtyard, not a wall/gate/gap problem. Stop patching colliders/gaps —
the next step must INSTRUMENT *why two coplanar sheets bake disconnected* (candidates: the
enlarged Floor_Stronghold overlapping the old ground plane / Platform foundation / multiple
NavMeshSurface-collected colliders stacking; check NavMeshSurface collectObjects + layerMask +
voxel/agent settings; consider baking nav from ONE floor source only). Use
`Defenders/Village2/Measure NavMesh (existing bake)` (Village2NavMeshMeasure) to verify — it
reads the REAL NavMeshSurface bake (NOT the legacy baker the old island-map used).

Generator source committed as WIP; the SHIPPED Village2.unity was reverted to the prior
working-gate-crossing scene (commit 8b205129) so the playable state keeps a proven crossing.
Do NOT run `Build Village2 Enemy Stronghold` to ship until this is solved (it regenerates the
still-fragmented interior).

---
**Original spec below (2026-06-21)**
**Owner decision (2026-06-21):** Option 3 — *fix the generator + rebuild*, NOT band-aid
crossings or a fragile hand-laid floor. "One continuous (or near-continuous) walkable
interior surface is the gold standard for reliable AI pathfinding. 38 islands are a
nightmare… Every new outpost generated later will be correct by default."

## Problem (data-cited)
Village2 (built by `Assets/Editor/EnemyStrongholdBuilder.cs`) bakes its interior into
**~38 disconnected navmesh islands**, so the hero ports in but cannot walk arrival →
courtyard → chokepoint → keep door.
- Headless (autopilot, runtime NavMeshSurface): `landing -> 'Spawn_Keep': status=PathPartial`;
  `hero inside wall geometry: InnerWall_Front (non-trigger collider)` ×5.
- Island map (legacy bake): 38 islands; HeroStart→island4, Chokepoint→island2, Keep→island23, Rear→island35.

## Root causes
1. **Concentric carve rings** (`BuildCarveRing`/`AddCarveWall`, ~286-315) seal BOTH the outer
   courtyard ring AND the inner chokepoint ring → the courtyard and chokepoint bake as
   separate patches; gate gaps don't reliably reconnect them.
2. **Raised keep on a platform reached only by a `NavMeshLink`** (`BuildRaisedKeep`/`BuildNavLink`,
   ~344/424). The hero is an **input-driven `NavMeshAgent` (Move(), not SetDestination)** → it
   **cannot auto-cross a NavMeshLink**. So the keep is ALWAYS a separate island for the player.
3. **Destruction rubble** (`WallDamageChance` default 0.3, ~982): broken wall slots become
   `Rubble_Stone` with **non-convex MeshColliders** that fragment the bake + trap (named
   `InnerWall_Front`, the oracle break).
4. (Carried over) `BuildReturnSeam` bakes a `ReturnToOuterWorld_Seam` — Village2 is ONE-WAY; do not build it.

## Required changes (EnemyStrongholdBuilder.cs)
A. **Carve OUTER ring only.** Skip the inner chokepoint `BuildCarveRing` so courtyard+chokepoint
   are one connected surface. Inner walls stay as visual props (no carve). Keep gate gaps.
B. **One continuous interior floor.** Ensure `Floor_Stronghold` (BuildGroundFloor) covers the FULL
   interior the hero traverses — from the arrival point through the keep base — and is
   NavigationStatic so the surface bakes one connected patch.
C. **Keep reachable by the input-hero.** Replace the keep `NavMeshLink` with a **walkable RAMP**
   (NavigationStatic sloped geometry the NavMeshSurface bakes a slope on) so the hero walks up —
   OR (fallback) a paired `HeroLinkCrossing` at the keep stairs. NavMeshLink alone is invalid here.
   Same for the boss chamber if enabled.
D. **Destruction must not fragment nav.** For the stronghold bake, either set rubble colliders to
   convex/Box, OR do not mark rubble NavigationStatic, OR set `WallDamageChance` 0 for village2.
   Rubble stays visual; it must not carve the floor or trap the agent.
E. **Do NOT build `ReturnToOuterWorld_Seam`.** (One-way outpost.)
F. **Arrival on-mesh.** The OuterWorld CavePortal warps to (20.6, 0.1, -38.3). Ensure the baked
   navmesh covers that point (extend floor / entry), OR re-point the CavePortal to the builder's
   on-mesh entry. Verify post-bake.

## Acceptance (verify by DATA, §12)
- Island count on the **NavMeshSurface** bake drops to **1–3** for the interior.
- `arrival (20.6,-38.3) → Spawn_Chokepoint → Spawn_Keep` all `PathComplete` (not PathPartial).
- No `hero inside wall geometry` oracle break; no NRE.
- The committed gate-crossing (`village2_gate`) + arrival still land on-mesh (re-run
  `Village2PlaceGateCrossings` for any residual boundary; re-measure).
- Owner felt-test: walk arrival → keep door.

## NOT to touch / preserve
- Owner's hand-edits were workarounds for the fragmentation — obsoleted by this fix; OK to lose
  on regen. Preserve: arrival intent (20.6,-38.3), the one-way design, battle HUD/enemy-owned,
  garrison roster.

## Measurement note
`Village2IslandMap` re-bakes with the LEGACY baker (`NavMeshBuilder.BuildNavMesh`), which is NOT
the runtime navmesh. Add a measure tool that samples the EXISTING (NavMeshSurface-baked) navmesh
WITHOUT re-baking, for true acceptance.
