# WORK ORDER 167 — Gatehouse pillar/spire clips through the ceiling (all 4 gates)

**Status: READY TO IMPLEMENT**
**Priority:** Medium-High — visible on every gate; the castle reads broken. Cosmetic/placement, not gameplay.
**Date:** 2026-05-30
**Lane:** Architect / World — `VillageSceneBuilder.cs` (gate/gatehouse placement) + rebake. CLI; UI spec.
**Source:** owner playtest screenshot — *"all four gates same problem: the bottom of the pillar needs to stop above the ceiling of the gathouse."*

---

## Symptom
A **tall pillar/spire element on the gatehouse** (the dark vertical column rising above each gate)
extends **downward THROUGH the gatehouse and below its ceiling** — the pillar's base sits at/near ground
level and pierces the gatehouse interior instead of resting **on top of** the gatehouse roof. Identical on
all four gates → a single placement/offset formula is wrong (a Y-origin issue applied to every gate).

## The fix
Raise the pillar element so its **bottom sits at or above the gatehouse ceiling/roofline**, not at ground:
- Find where the gate pillar/spire (the tall column atop the gatehouse) is placed/scaled in
  `VillageSceneBuilder` (the gate-build path: `BuildGates` / `BuildWallPerimeter`, ~`:709`/`:2829`).
- The pillar's **base Y should = the gatehouse roof height** (its local-position Y offset, or its mesh
  pivot, currently resolves to ~0/ground). Set the pillar's base to seat on the gatehouse top so it reads
  as a finial/spire *on* the roof, not a beam *through* the building.
- If the pillar is part of a prefab whose pivot is centered/bottom-low, offset its `localPosition.y` up by
  the gatehouse height so the visible base clears the ceiling.
- Apply once in the shared gate code so all 4 gates fix together.

> CLI's call on the exact mechanism (localPosition Y offset vs. swapping to a roof-seated placement) —
> the requirement is: **pillar bottom ≥ gatehouse ceiling**, no clip-through, on all four gates.

## Acceptance criteria
1. On all 4 gates, the pillar/spire's **bottom rests at/above the gatehouse ceiling** — no portion pierces
   the gatehouse interior or hangs below its roof.
2. The pillar still reads correctly as a gatehouse spire/finial (right height/scale, sits on the roof).
3. Fix is in the **shared gate build code** (one formula → all four gates), not per-gate hand-tweaks.
4. No regression to gate passability, the gate gap, drawbridge alignment, or spawn→Heart path.
5. Brace balance; rebuild via builder (editor closed).

## Notes
- This is purely the **pillar Y-origin / seating** — do NOT change gate width, the gap, or collision.
- Reconcile with WO-166 (gates render/passable) — if WO-166 reworked gate placement, fold this pillar-seat
  fix into the same gate pass rather than a separate edit.

## Done checklist (CLAUDE.md §10)
- [ ] All 4 gate pillars seat at/above the gatehouse ceiling (no clip-through)
- [ ] Pillar reads correct (height/scale/on-roof); shared-code fix (not per-gate)
- [ ] No regression to gate gap / passability / drawbridge / path
- [ ] Brace balance; editor-closed rebake
- [ ] `WORK_ORDER_167_gatehouse_pillar_clipping.RESULT.md` when complete
