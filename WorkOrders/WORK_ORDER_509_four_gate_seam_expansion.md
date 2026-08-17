<!-- era-sweep-2026-08-17 -->
> ### ⛔ ERA SWEEP 2026-08-17 — CLOSED as OBSOLETE (deleted system)
> **Dead thing:** OuterWorld.unity. **Git first-add:** 2026-06-24.
> **Evidence:** `Assets/Scenes/OuterWorld.unity` is absent from disk and from `git ls-files`; scope is extending the castle↔OuterWorld scene seam to all four OuterWorld edges.
> Only the `**Status:**` line was rewritten. The body below is UNTOUCHED — CLAUDE.md §15, *"frozen, never rewrite"*.
> **TO REVIVE:** nothing was deleted and not one line of the body below was changed. If this work is still wanted, re-date the WO (add a `**Minted:** <today>` line), re-point it at the live scene/system, and set `**Status:** READY TO IMPLEMENT`.

# WORK_ORDER_509 — four-gate seam expansion (OuterWorld N/S/E/W, natural styling)

**Status:** CLOSED — OBSOLETE: OuterWorld.unity no longer exists (era sweep 2026-08-17)
**Depends on:** WO-492 / the V2 enemy-seam-navmesh work for the ENEMY-crossing half (see Section 3 flag).
**Origin:** owner spec (Grok-drafted), synthesized against the real RuntimeRegionGate system.

## 1. Goal
Extend the working south seam (castle <-> OuterWorld) to ALL FOUR cardinal edges of the OuterWorld map. Each
gate: bidirectional crossing, and NATURALLY styled so the player intuitively reads "this is a path out/in" —
vs the impassable world boundary (cliffs/water/dense forest) on the non-gate edges.

## 2. Current state (what we reuse — verified this session)
- `RuntimeRegionGate` is the working crossing primitive (memory `region-gate-crossing-primitive`): walkable
  approach -> threshold trigger -> masked transition. Driven by `Assets/Resources/Data/region-gates.json`
  (data-driven; we tuned its `triggerRadius` 44->8 earlier). The live south seam =
  `__RuntimeSeam_castle_to_outerworld` (deck weld + NavMeshLink + trigger + HeroLinkCrossing + the gate beacon
  we just fixed to render). Player crossing WORKS today.
- So a 4-gate expansion is mostly DATA + REUSE: add three more gate entries (N/E/W) to region-gates.json, each
  instantiating a RuntimeRegionGate with the same crossing mechanics, positioned on the OuterWorld edges.

## 3. ★ HONEST FLAG — enemy cross-seam pathing is the UNSOLVED V2 problem ★
The spec says "enemies should path correctly across seams." That is the KNOWN-UNRESOLVED issue
(memory `v2-enemy-seam-navmesh-traversal`): enemies do NOT reliably path across the RegionGate seam today — the
chase stalls at the seam, which is exactly why "castle = safe" / OuterWorld-only spawns is the current design.
So:
- **PLAYER bidirectional crossing on all 4 gates = shippable now** (reuse the south seam's HeroLinkCrossing).
- **ENEMY bidirectional crossing = BLOCKED on WO-492 / the V2 navmesh stitch** (Grok-escalated). Do NOT promise
  enemy cross-seam pathing in this WO until the navmesh-link AI traversal is solved. Build the 4 gates so enemy
  crossing "lights up" once V2 lands (the NavMeshLink + AI link are part of each gate), but acceptance for THIS
  WO is player-crossing + the gates existing + styled.

## 4. Approach (data-driven, reuse-first)
1. **Data:** extend `region-gates.json` to define 4 gates (N/S/E/W) — each with: edge/position + facing, the
   crossing params (triggerRadius, load mode warp/stream), the target region + return pose, and a `style` key
   (cliffpass / riverford / forestarch / <south>). Keep South = existing (refine only).
2. **Spawner:** the RuntimeRegionGate builder reads each entry and instantiates the SAME mechanics as the south
   seam (deck weld, NavMeshLink, trigger volume, HeroLinkCrossing, AI link, beacon). One code path, N instances.
3. **Bidirectional:** each gate's return path mirrors the south seam (cross out -> OuterWorld edge; cross back
   -> the connected region). Confirm the player can exit AND re-enter from each of the 4.
4. **Natural styling (FELT / ART — owner dresses, per the arena pattern):** each gate gets a diegetic look that
   says "path":
   - South: keep/refine existing.
   - North: cliff pass / mountain trail with a carved path.
   - East: river ford / bridge with shallow water + stepping stones.
   - West: forest archway / ancient road through dense trees.
   - Build the gate MECHANISM + a placeholder marker in code; the owner places the real art (KayKit/Quaternius/
     polyperfect rock/tree/bridge meshes) by eye (CLI can't judge "feels natural") — capture her placement into
     a recipe afterward (the arena-prefab pattern).
5. **Boundary distinction:** the NON-gate edges read as impassable (cliffs / deep water / dense forest wall) so
   the 4 styled gates are the obvious exits. (Boundary art = same felt/owner pass.)

## 5. Deliverables
- region-gates.json with 4 gate entries + the RuntimeRegionGate builder handling N instances (verify it already
  loops over entries vs hardcodes the south one — extend if hardcoded).
- The 4 gates instantiated on the OuterWorld edges, player-crossable both ways, navmesh-linked.
- Natural styling per gate (owner art pass) + impassable-boundary styling on non-gate edges.
- NavMesh + player-cross confirmation all 4 directions (headless seam-reachable regression per gate). Enemy
  cross-pathing confirmation is DEFERRED to V2 (WO-492).

## 6. Acceptance
- Player can exit + re-enter OuterWorld from each of the 4 gates (bidirectional), navmesh links bake, no
  soft-lock. Gates are visually distinct paths; non-gate edges read impassable. Gate-clean; mobile-cheap.
- BONES vs FINESSE: the gate mechanism + data + player crossing + navmesh = CLI gate-provable (extend the
  SEAM-REACHABLE regression to all 4); the natural STYLING is owner felt/art. Enemy crossing = WO-492 dependency.

## 7. Do NOT
Promise enemy cross-seam pathing here (V2). Hand-edit `.unity` scenes (§3 — recipe/builder). Greenfield a new
crossing system — reuse/generalize RuntimeRegionGate (memory `region-gate-crossing-primitive`).
