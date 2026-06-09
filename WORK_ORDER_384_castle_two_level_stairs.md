# WORK_ORDER_384 — Castle two-level access: wide climbable stairs + NavMesh connection

**Status:** READY TO IMPLEMENT
**Lane:** 1 World/Env (castle geometry) — `CastleHubBuilder.cs` is single-writer (§9), one agent.
**Source:** This session (2026-06-09)
**Depends on:** none. **Related:** WO-383 (OuterWorld seam — *horizontal*; this is the *vertical* ground↔battlements link).

---

## Problem (playtest-confirmed via code)
The castle ground level → upper battlements connection is currently an **invisible 36° ramp** (`CastleHubBuilder.BuildNavMeshFloor` → `UpperRamp_Nav`, ~20m run × 8m wide, pos (26,5.75,0), rot (0,0,-36)). The visible stair props (`MainStairs_Poly_ToUpperBattlements`, `QuaterniusStairs_UpperAccess`) are **dressing only — not the nav path**, and don't line up with where the climb happens. Net effect in playtest: **there are no real walkable steps connecting the two levels** — a hidden ramp does it, which reads as "disconnected."

Design intent (owner): wide stairs the player AND enemies can climb so enemies can swarm up to attack siege/defense items on the upper battlements.

## Goal
Real **wide, visible, climbable stairs** as the actual nav path between the courtyard (y≈0) and the upper battlements (y≈11.5), navmesh-connected as ONE walkable sheet, multi-agent capable, enabling a future enemy siege-climb loop.

## Approach — EDITOR-TIME generation + headless bake (NOT runtime bake)

> **Engineering note / deviation from the relayed best-practice:** the relayed reference script bakes NavMesh at runtime (`NavMeshSurface.BuildNavMesh()` in `Start()`). Do NOT do that here — the castle has an **editor-baked, persisted NavMesh** (`Assets/Scenes/MainCastle_Hall/NavMesh-NavMeshSurface.asset`, commit `d31c931`); runtime baking is costly on mobile/WebGL and would double-bake. The geometry-generation logic is correct — run it at **build time** via the existing castle tooling. (Runtime bake is the right pattern only for dynamically-spawned zones.)

1. **Generate wide stairs geometry in `CastleHubBuilder`** (extend `BuildNavMeshFloor` or add a `BuildUpperStairs` step). Two acceptable forms:
   - **(Preferred) Modular prefab stairs** — instantiate Quaternius/polyperfect stair pieces (`Stairs_Medieval_Stone` / `Stairs_Exterior_Straight`) tiled to ~**8–9m wide**, rising ground→y≈11.5, with their **MeshColliders intact** so the NavMeshSurface (Use Geometry = Physics Colliders) bakes them. Place them where the ramp currently is (replace `UpperRamp_Nav` as the *visible* path).
   - **(Fallback) Procedural step cubes** — the relayed `ProceduralWideStairs` cube-per-step logic (width 8–9, height ~11.5, stepCount ~12, stepHeight 0.3–0.5), parented under `CastleHubRoot`, renderers on (visible), colliders on. Same bake.
   - **FIT-TO-BOUNDS (REQUIRED) — tile from measured anchors, never scale a single blob.** Derive the **step count from the measured run** between a courtyard start anchor and the upper-battlement **edge** anchor (and rise from the measured height), then tile fixed-depth steps to fill it exactly. Result: bottom tread at the courtyard, **top tread flush at the battlement edge** — no overshoot past the castle, no gap short of it. This is the project's wall-fit philosophy (tile to pitch at factor 1.0, no distortion — see `OVERNIGHT_STATUS_castle.md`) applied to stairs. The same count-from-bounds approach lets the run shrink/extend to whatever the castle bound is without distorting any piece.
2. **The stairs themselves ARE the nav path** — bake the real stair colliders as the walkable surface, and **remove `UpperRamp_Nav`** (the invisible ramp). No hidden ramp standing in for the stairs, no cosmetic-stairs-over-a-secret-ramp — that mismatch is precisely what this WO eliminates. If the stepped slope bakes marginally, the NavMeshLink in step 3 (a first-class Unity nav feature, not a band-aid) guarantees the connection.
3. **Add a NavMeshLink across the full stair width as the reliability backup** — `CastleHubBuilder` already adds NavMeshLink via reflection (`WireOuterWorldConnection`); reuse that pattern: link bottom-of-stairs ↔ top-of-stairs, `width = stair width (~8m)`, `bidirectional = true`, area 0. This guarantees the level connection even if the slope bake is marginal.
4. **Re-bake headless** via `CastleHubBuilder.BatchAddFloorAndBakeCastle` (Use Geometry = Physics Colliders, persist asset, save scene). Editor must be CLOSED.
5. **NavMesh agent settings:** step height 0.3–0.5, max slope 45° (matches both stairs and enemies; shared agent type).

## Follow-on (scope separately if large — note in Notion)
- **Enemy siege-climb loop:** wire enemy `NavMeshAgent.SetDestination` to upper-battlements siege/defense items. NOTE: the castle is currently the **home hub, not a wave arena** — enemies attacking it is *new gameplay*, not part of this WO's core. This WO delivers the *climbable path*; the attack loop is its own ticket.

## Castle-builder architecture notes (for the implementer)
- **Hardcoded placements are the root of "doesn't end at the edge."** `CastleHubBuilder` positions elements at fixed local coords/scales (corner towers ±42, walls ±44, ramp `(26,5.75,0)`, single stair prefab `(16,5.5,0)`). A fixed-size stair object will overshoot or fall short. Make stair generation **anchor-driven**: derive run+rise from a courtyard start anchor + a battlement-edge end anchor, tile to fit (see FIT-TO-BOUNDS above).
- **Second tier = one large plane (collider destroyed).** The upper battlements is a single ~44×44 platform (`BattlementsPlatform_Wide42m_ForPlayerTowers_LOS_DownToCourtyard`, a Cube scaled `(44,0.8,44)`) whose **MeshCollider is destroyed in the builder** — walkability up there comes ONLY from the invisible `UpperBattlements_Nav` plane (44×44 at y≈11.5). Therefore: the stair **end anchor = the edge of that platform plane** (≈ ±22 from centre, y≈11.5), and the **stair top must OVERLAP `UpperBattlements_Nav`** (NOT the dead cube collider) so the bake fuses courtyard-run → stairs → upper plane into one connected navmesh sheet. Verify the top tread sits on/overlapping the nav plane — not floating above it or short of its edge.

## Acceptance criteria
- [ ] Wide (≥8m) visible stairs connect courtyard → upper battlements; the climb path matches the visible stairs (no hidden-ramp mismatch).
- [ ] NavMesh is ONE connected walkable sheet ground↔upper (verify in the Navigation display + an agent can path up).
- [ ] The hero (NavMeshAgent) climbs to the battlements in playtest.
- [ ] Multiple agents can climb side-by-side (width supports it).
- [ ] NavMeshLink present across the stair width as backup.
- [ ] WO-373 regression gates still PASS; ground-level + spawn unchanged.

## What NOT to touch
- **Do NOT runtime-bake** over the persisted castle NavMesh (no `BuildNavMesh()` in a runtime `Start()`); generate + bake editor-side.
- Do not hand-edit `MainCastle_Hall.unity` — go through `CastleHubBuilder` + headless bake.
- Keep movement world-absolute (WO-368). Don't touch the camera.
- `CastleHubBuilder.cs` is single-writer — one agent only.

## Reference (owner-relayed procedural script — geometry logic is the keeper; run it editor-side, not at runtime)
`ProceduralWideStairs` step-cube generation + `AddWideNavMeshLink(startPos, endPos, width)` + enemy `AttackSiegeTarget(siegeItem) => agent.SetDestination(siegeItem.position)`. Adapt the generation into the builder; if a runtime MonoBehaviour is ever used instead, `DeNelle.Village`'s asmdef must reference `Unity.AI.Navigation` (the project currently touches NavMeshSurface/NavMeshLink via reflection to avoid that).
