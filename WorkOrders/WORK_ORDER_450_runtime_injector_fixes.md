<!-- era-sweep-2026-08-17 -->
> ### ⛔ ERA SWEEP 2026-08-17 — CLOSED as OBSOLETE (deleted system)
> **Dead thing:** OuterWorld.unity. **Git first-add:** 2026-06-22.
> **Evidence:** `Assets/Scenes/OuterWorld.unity` is absent from disk and from `git ls-files`; 450a is the hub floor z-fight against OuterWorld terrain and 450b injects an `OuterWorldBoundaryInjector` on OuterWorld scene load.
> Only the `**Status:**` line was rewritten. The body below is UNTOUCHED — CLAUDE.md §15, *"frozen, never rewrite"*.
> **TO REVIVE:** nothing was deleted and not one line of the body below was changed. If this work is still wanted, re-date the WO (add a `**Minted:** <today>` line), re-point it at the live scene/system, and set `**Status:** READY TO IMPLEMENT`.

# WORK ORDER 450 — Runtime injector fixes (NO bake): hub z-fight + OuterWorld edge boundary

**Status:** CLOSED — OBSOLETE: OuterWorld.unity no longer exists (era sweep 2026-08-17)
pure runtime, low-risk, parallel-safe. These are the two "no-bake" bugs from the overnight tally.

## Why runtime injectors (per RCA + CLAUDE.md §3)
Both fixes follow the established `GroundZFightFixer`/`CampSystem` RuntimeInitialize injector pattern — they
spawn/adjust objects on scene load, so they need **no editor bake** and **no hand-edited scene** (honoring §3's
"avoid MainCastle/Village rebakes" + the single-Unity-gate rule).

## 450a — Hub floor z-fight (the "flashing floor / fighting for ownership")
**Root (RCA):** castle plaza `CourtyardFloor_*` tiles at Y=0.01 are coplanar with OuterWorld terrain at Y=0 →
z-fight. The existing `GroundZFightFixer` would fix it but is **gated to `Village*` scenes** (`InVillageScene()`,
`GroundZFightFixer.cs:132-136`) and only matches a plane **named "Ground"** (`:166-172`) — so it **never runs in
`MainCastle_Hall`** and wouldn't match `CourtyardFloor_*` anyway.
**Fix:** extend `GroundZFightFixer`:
- Broaden the scene gate to also fire for the hub (`MainCastle_Hall` / names starting `Castle`/`MainCastle`).
- Broaden the ground finder to also match `CourtyardFloor_*` (and `qFloorWood`) tiles.
- Nudge the matched tiles to a clearly non-coplanar Y (drop to ~−0.05 to −0.5, or raise to ≥0.05 so they win
  the depth test cleanly). One owner = no fight = no flash.

## 450b — OuterWorld edge boundary (the "no collision boundaries")
**Root (RCA):** there is **no world-boundary collider** at the OuterWorld terrain edge (±150). The hero walks
off into the void. The boundary builder (`BuildBoundaryWalls`/`BuildEdgeCliffRing`) was specced in
`WORK_ORDER_33_map_edge_boundary.md` but **never implemented** (zero `.cs` references). (Terrain ground collider
is fine — no fall-through; fort walls have colliders but only exist at ±95 camps.)
**Fix:** a runtime injector (new `OuterWorldBoundaryInjector`, RuntimeInitialize on OuterWorld load, mirroring
`GroundZFightFixer`) that spawns **4 tall invisible `BoxCollider` slabs at ±142 on X and Z**, walling the play
area just inside the ±150 edge. No renderer (invisible). NavMesh already ends at the edge; this catches the
`NavMeshAgent`/cast mover + off-mesh cases. (WO-33's cliff-ring *visual* is optional later polish.)

## Acceptance
- [ ] In `MainCastle_Hall`/at the OuterWorld landing, the ground no longer z-fights/flashes (one surface wins).
- [ ] The hero cannot walk off the OuterWorld terrain edge — stopped by the boundary at ±142.
- [ ] No bake performed; both are runtime injectors; no `.unity` hand-edited.
- [ ] §12: each injector logs a `[Flow:Fix]` Once line on activation (so we can confirm it ran).
- [ ] Compile gate green; brace + NUL guards.

## What NOT to touch
- Do NOT re-seat the plaza tiles in `CastleHubBuilder.cs:213` (that's the rebake alternative — out of scope here;
  the runtime nudge avoids the bake). Do NOT implement WO-33's visual cliff ring now (collider-only).
- §0: CLI edits `.cs` on the Windows path; UI does not touch code.

*Cross-ref:* RCA 2026-06-17, `GroundZFightFixer.cs`, `ExteriorTerrainBuilder.cs` (terrain extent ±150),
`WORK_ORDER_33_map_edge_boundary.md` (the unimplemented boundary spec), WO-449 (the world this sits in).
