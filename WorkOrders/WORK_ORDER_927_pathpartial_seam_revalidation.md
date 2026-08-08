# WO-927 — PathPartial seam revalidation (the connector justification is dead; measure the instance)

**Status: READY TO IMPLEMENT** (measurement first — §5 is a hard gate on §6)
**Date:** 2026-08-08 · **Priority:** High · **Lane:** Dungeons / navmesh
**Authored by:** the owner (2026-08-08). *CLI refinement pass: sharpened §2 row 3, added measurement M7, recorded the alignment-with-anchor note in §8. Content and disposition are the owner's.*
**Related:** `WorkOrders/DESIGN_CONNECTOR_IS_THE_ONLY_CONTRACT.md` (written 2026-08-07 22:25)
**Triggered by:** `CANON_GROUND_TRUTH_2026-08-08.md:59` — hypothesis #1, landing measured at 1.30 m

---

## 1. Executive summary

The design document's core justification for the stitch / connector architecture (§5.5.2) **is no longer
valid.** It claimed `PathPartial` was an *erosion* problem: a top landing of **0.80 m** against the
**1.00 m** minimum walkable slot.

The 08-08 anchor refutes it directly. `TurnRun 4.0 → 3.5` shipped (`4ec16f96`), the landing now measures
**1.30 m**, slope 40.6°, both measured — **and the path outcome did not move.**

The architecture in §5.5 may still be correct. It can no longer rest on the killed height hypothesis.
A fresh, measurement-based justification is required.

---

## 2. Original claim vs. current reality

| Item | Design doc (08-07 22:25) | 08-08 anchor / current state | Status |
|---|---|---|---|
| Top landing height | 0.80 m | **1.30 m** | **KILLED** |
| Minimum required | 1.00 m | 1.00 m | Unchanged |
| Path outcome after the widening | implied: would resolve once eroded width returned | **still `PathPartial`** — the fix landed and changed nothing | **CONFIRMED** |
| Primary problem framing | vertical erosion / undersize | no longer vertical | **INVALID** |
| Connector justification | required to absorb the height deficit | must be re-justified or retired | **OPEN** |

> ⚠ **Row 3 is stated precisely on purpose.** "Path geometry unchanged" is easy to misread as *"the geometry
> is fine."* What is unchanged is the **outcome**: the landing was widened to 1.30 m and the dungeon still
> reports `PathPartial`. That is what kills the hypothesis — the remedy was applied in full and bought
> nothing.

---

## 3. Consolidated analysis (all axes)

**3.1 Vertical continuity.** The landing now sits 0.30 m *above* the old minimum. The original erosion
driver is gone. Residual vertical mismatch, if any, is small and secondary.

**3.2 Lateral (X/Z) seam alignment.** No measured offsets exist. **Highest-risk unquantified axis.** Any
remaining functional or visual break is now more likely lateral than vertical.

**3.3 Connector mesh scaling.** No scale values recorded. The connector may still carry a non-unit scale
tuned to the old 0.80 m geometry. Non-uniform or compensatory scale can hide *or* create both lateral and
vertical problems.

**3.4 Connector mesh bounds.** Local bounds and world-space span unknown. The missing comparison is
**authored size vs. the actual gap being bridged**. Bounds will immediately show whether the mesh is still
shaped for the old height.

**3.5 Attachment point coordinates.** The world-space positions of the two `PathPartial` attachment points
have never been captured. These are the ground-truth delta vector. Until they exist, every continuity claim
stays qualitative.

---

## 4. Risk assessment

| Risk | Severity | Notes |
|---|---|---|
| Stale justification in the design doc | **High** | §5.5.2 still cites the killed 0.80 m claim |
| Lateral misalignment unmeasured | **High** | most likely remaining continuity issue |
| Connector still scaled for old geometry | **Med–High** | may be masking or creating new problems |
| Architecture may be over-specified | **Medium** | connector-as-only-contract may no longer be necessary |
| Future readers inherit a false causal story | **High** | same class of miss as the 923-vs-927 banner-table discrepancy |

---

## 5. Required measurements — BLOCKING

Collect and record all seven before any further design or implementation decision.

| # | Measurement |
|---|---|
| M1 | World-space coordinates of **both** `PathPartial` attachment points (X, Y, Z) |
| M2 | Residual **delta vector** between those points |
| M3 | Connector **local scale** and **lossy scale** |
| M4 | Connector **mesh local bounds** (size + center) |
| M5 | **World-space span** the connector covers, per axis |
| M6 | Visual + NavMesh check with the connector **temporarily disabled** |
| M7 | **`NavMesh.CalculateTriangulation` dump filtered to the failing ramp's bounds** — read *where* the strip actually breaks *(added per `CANON_GROUND_TRUTH_2026-08-08.md:88-92`)* |

⚠ **Probe-radius law carries into this WO** (`CANON_GROUND_TRUTH_2026-08-08.md:104-108`): the radii are
deliberately opposite — **tight 0.35 m** on the ramp so a hit cannot be the floor underneath, **generous
6 m** when finding a room's floor. **Do not unify them.** The instruments here have been wrong twice, and
both times looked confident.

---

## 6. Recommended actions

**Immediate**
- [ ] Capture M1–M7.
- [ ] Update or strike §5.5.2 of `DESIGN_CONNECTOR_IS_THE_ONLY_CONTRACT.md` so it no longer references the
      0.80 m hypothesis.
- [ ] Add a **"Hypothesis killed — 08-08"** banner at the top of that design doc.

**Once measurements exist**
- [ ] Decide whether residual deltas are small enough that simple positional alignment — no connector, or a
      minimal one at unit scale — is sufficient.
- [ ] If a connector remains necessary, rewrite its justification from the *actual* remaining lateral /
      continuity issue, not the old vertical-erosion story.
- [ ] Decide whether "the connector is the only contract" still holds or should be relaxed.

**Process**
- [ ] This WO is the formal record that the height-based justification is dead.
- [ ] Link it from the design doc and from the 08-08 anchor.

---

## 7. Disposition

The stitch / connector approach is **not** automatically invalidated. What is invalidated is **the specific
reason the design document gave for it.** Until the attachment-point coordinates, lateral deltas, scale
values and mesh bounds are measured, the architecture sits on an unproven foundation. Next concrete step is
data collection, then a revised justification — or a simplification of the seam.

**Next gate:** M1–M7 recorded + §5.5.2 updated.

---

## 8. Why this WO is the RIGHT next move, not a fifth correlation round

Worth stating, because it looks superficially like the four rounds that already failed.

The 08-08 anchor's own conclusion is that **four rounds of correlation each cost one bake and each returned
nothing**, because *"a 15-ramp sample bucketed against scalars cannot resolve this"* — the variable is
**per-INSTANCE, not per-shape**.

This WO does not bucket a population against a scalar. It captures **ground truth on one specific failing
seam**: real coordinates, a real delta vector, real bounds, and a triangulation dump of where the strip
breaks. That is precisely the anchor's named next move — *"stop correlating and look."*

Corollary that must survive into the analysis: **reachability is gated by the first failure on the path, not
by the average** (`CANON_GROUND_TRUTH_2026-08-08.md:79-81`). Measure the **first** failing descent out of
the entry, not a convenient one — `dg_bonecrypt` has two ramps that work, sitting unreachable behind a
broken first one.
