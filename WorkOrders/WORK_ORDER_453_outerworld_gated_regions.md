<!-- era-sweep-2026-08-17 -->
> ### ⛔ ERA SWEEP 2026-08-17 — CLOSED as OBSOLETE (deleted system)
> **Dead thing:** OuterWorld.unity. **Git first-add:** 2026-06-22.
> **Evidence:** `Assets/Scenes/OuterWorld.unity` is absent from disk and from `git ls-files`; the vision is "make OuterWorld much larger" by extending that scene's terrain.
> Only the `**Status:**` line was rewritten. The body below is UNTOUCHED — CLAUDE.md §15, *"frozen, never rewrite"*.
> **TO REVIVE:** nothing was deleted and not one line of the body below was changed. If this work is still wanted, re-date the WO (add a `**Minted:** <today>` line), re-point it at the live scene/system, and set `**Status:** READY TO IMPLEMENT`.

> ⚠ **NUMBER COLLISION — this document does not own WO-453; `WORK_ORDER_453_dev_capture_toolkit.md` does.**
> Referred to hereafter as **WO-453-C (OuterWorld gated regions)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> ownership decided by **first-on-disk** (`git log --follow --diff-filter=A`): the winner's file was created first.
> Banner only — nothing was renumbered or deleted.
> ⚠ **Work HAS shipped under this number** — commit messages and/or a `.RESULT.md` cite WO-453 for THIS document. It is deliberately **not renumbered**; a renumber would orphan those references. Use the alias above when you need to name it unambiguously.

# WORK_ORDER_453 — OuterWorld expansion: gated regions, walled edges, navlink path-cuts, leashed monster families

**Status:** CLOSED — OBSOLETE: OuterWorld.unity no longer exists (era sweep 2026-08-17)
**Owner directive 2026-06-21.** Builds on the PROVEN seam (`ISSUE_navlink_seamless_walk.md`) + memories
`world-architecture-gated-regions-playable-connectors`, `region-gate-crossing-primitive`,
`autopilot-chaos-not-one-scripted-path`. Reconciles with WO-467 (RegionGate recipe) + WO-468
(castle→OuterWorld redesign) + `docs/MONSTER_FAMILY_ARCHITECTURE.md`. Owner-led world architecture.

## Vision (owner)
Make OuterWorld **much larger**; the **terrain edges are HIGH WALLS that naturally give each region its
shape** (diegetic containment, not invisible colliders); the ONLY ways out are **cut paths crossed by
navlinks** (the seamless WALK, no warp); each region holds **families of enemies roaming LEASHED large
areas** (leader/follower packs), with a **danger gradient** (tougher toward the outward gate). A proven
path runs **south-side → enemy garrison**.

## The PROVEN base (do NOT reinvent — extend it)
`ISSUE_navlink_seamless_walk.md` §0.5: an input-driven (`NavMeshAgent.Move`, not `SetDestination`) hero
**cannot** auto-cross a NavMeshLink — so the seam is delivered by **manual link traversal**:
- `HeroLocomotion.TryTraverseSeamLink()` — near a seam endpoint + input pointing across → slide the hero
  in-world to the far endpoint at move speed, `_agent.Warp` to re-bind, cooldown. `_isTeleporting=true`
  during the slide so the ±50 off-mesh clamp is skipped. Seamless walk, bidirectional, NO warp/fade.
- Each region = its own baked `NavMeshSurface`; regions sit **side-by-side** (un-stacked); the path-cut
  carries a `NavMeshLink` + the manual-traversal endpoints.
- Coverage: the path corridor must bake a continuous agent-width walkable strip (the cave/garrison
  approach must be ON-mesh — `SEAM-REACHABLE` oracle proves it).

## Acceptance criteria — REGION 1 (prove the convention on ONE region)
1. **Larger walled region:** extend the OuterWorld terrain (via `ExteriorTerrainBuilder` +
   `OuterWorldNavBake`, editor closed) to a measured size (see §Budget). The region's terrain EDGES rise
   into **high walls** (terrain height ramp, not invisible boundary colliders) that visually enclose it
   and leave only the intended path openings. Reuse/extend `OuterWorldBoundaryInjector`.
2. **One path-cut → enemy garrison, crossed by the PROVEN seam:** a single cut in the wall is the exit;
   the hero **walks** across it (manual NavMeshLink traversal, no warp) into the next area / the enemy
   **garrison** (Garrison_* via `GarrisonController`, or Village2). Mirror the south-side seam exactly.
3. **One leashed monster family roaming the region:** a leader/follower pack (`MONSTER_FAMILY_ARCHITECTURE`
   on `RegionMobSpawner`'s threat-scaled leashed roaming) wanders a LARGE leashed area inside the region —
   does not leave the region, does not stack at the gate.
4. **Danger gradient:** enemies scale tougher toward the outward path-cut (`ZoneManager` depth/threat).

## INSTRUMENT-FIRST — prove by DATA, per CLAUDE.md §12 HARD GATE (non-negotiable here)
No "looks right" sign-off — each criterion has a headless oracle (extend `AutoPilotProbes`):
- **SEAM-REACHABLE**: the path-cut + garrison approach are within 2m of baked navmesh (no SEAM-OFF-MESH).
- **WALL-CONTAINMENT**: the region's walkable navmesh is bounded by the walls — no off-region leak.
- **LEASH-CONTAINMENT**: the monster family stays within its leash radius across N seeded runs (chaos
  seeds, fixed oracle — `autopilot-chaos-not-one-scripted-path`).
- **PERF-BUDGET**: a `FlowTrace.Measure` frame/memory probe on the enlarged region stays within budget.
- The manual WALK across the seam is NOT fleet-verifiable (bot warps to the trigger) → **owner felt-verify**
  that one beat only; everything else is data-proven headless first.

## Budget (measured, not guessed — per the gated-regions memory)
Do NOT pick an arbitrary size. Measure this machine's memory/frame headroom on the current OuterWorld,
then size Region 1's expansion to stay within it. Record the measured numbers in the RESULT.

## Files (reconcile, do NOT greenfield)
- `Assets/Editor/ExteriorTerrainBuilder.cs` (terrain size, wall-edge height ramp, path corridor flatten)
- `Assets/Editor/OuterWorldNavBake.cs` (re-bake the region surface; editor closed)
- `Assets/_Modules/Village/Hero/HeroLocomotion.cs` (`TryTraverseSeamLink` — the proven seam; parameterize endpoints per cut)
- `Assets/Editor/CastleHubBuilder.cs` `BuildBridgeNavLink` / a RegionGate builder (WO-467) — the navlink path-cut recipe
- `Assets/_Modules/Village/World/OuterWorldBoundaryInjector.cs` (wall edges)
- `Assets/_Modules/Village/World/Camps/GarrisonController.cs` + `RegionMobSpawner` + `MONSTER_FAMILY` (the garrison + leashed family)
- `Assets/_Modules/DevTools/AutoPilotProbes.cs` (the oracles above)
- `Assets/_Modules/Core/World/ZoneManager.cs` (region + danger tier)

## What NOT to touch / NOT to do
- **No warp** (owner-rejected). The crossing is the manual-traversal WALK.
- **No hand-edited `.unity`** — regenerate via the editor builders, batchmode, **editor closed**.
- Do NOT regen the hand-dialed MainCastle_Hall geometry.
- Do NOT build all regions at once — Region 1 proven (oracles green + owner felt-verify) BEFORE replicating.

## Sequence
1. Measure the perf budget on current OuterWorld.
2. Build Region 1 (larger walled terrain + one path-cut + navlink + manual-traversal endpoints) → re-bake.
3. Add the leashed monster family + danger gradient.
4. Add/extend the oracles; run the headless fleet → all green (SEAM-REACHABLE, WALL/LEASH-CONTAINMENT, PERF).
5. Owner felt-verify the WALK beat. Then write the RegionGate recipe (WO-467) and replicate per region.
