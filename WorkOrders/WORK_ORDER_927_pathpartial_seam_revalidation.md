# WO-927 — PathPartial seam revalidation (the connector justification is dead; measure the instance)

> # ★ ROOT CAUSE FOUND 2026-08-08 — §5's measurement gate is DISCHARGED. Read §0 first.
>
> The owner found it by eye in the editor, in minutes, after four bake-and-correlate rounds found
> nothing. **Every hypothesis so far tested a property of THE STAIR. The defect was never in the stair
> — it is in where the stair is POINTED.** §0 below is now the live section; §1-§4 are kept as the
> record of how the false trail was cleared, and §5's measurements are superseded by the direct
> observation. Do not re-run §5 M1-M7 as written.

## 0. ★ THE ROOT CAUSE — confirmed, with the owner's own observation

**Owner, 2026-08-08, looking at `dg_ember_deep` / `dg_sunken_vault` in the scene view:**
> *"see steps extend through actual floor"* ... *"which is why the plane couldnt make a level"*
> ... *"we need that edge"*

She selected the pair `stair_dn_0` (0, 0, 20) and `stair_up_1` (0, -6, 20) — a clean vertical stack,
exactly `FloorSeparationY` apart — and saw the flight driving up **into solid floor** instead of
arriving at the floor's hole.

### The chain, every link evidenced

| # | Link | Evidence |
|---|---|---|
| 1 | `GraphDungeonComposer.SolveMate` degenerates on a **vertical** socket — both outwards are straight up/down, so the planar solve has nothing to solve — and takes the branch that hardcodes `yaw = 0f` | `GraphDungeonComposer.cs:621-637` |
| 2 | **Every `StairUp` in the game is therefore yaw 0.** The `StairDown` was placed by its *corridor* mate and carries an arbitrary 0/90/180/270 | all five layout JSONs, 13 StairUp rooms, all `yawDeg 0.0` |
| 3 | Only **Delta yaw = 180** puts the flight's top nose inside the mating floor hole | prefab geometry + `AssemblyYaw = 180` (`DefaultStairConnectorRoomsBuilder.cs:885-886`) |
| 4 | At any other Delta the flight climbs into a **solid slab** | the owner SAW this |
| 5 | Buried under a slab there is no `agentHeight` (2.0 m) clearance, so the voxelizer **carves no walkable span at the top** | Unity rasterization rule |
| 6 | No navmesh at the top = nothing to path FROM = `PathPartial` | matches canon's "top seam tracks whole EXACTLY" |

### The predicate matches every measured dungeon, 4 for 4

| Dungeon | Pair Delta yaws | Predicted whole | Canon measured |
|---|---|---|---|
| `dg_bonecrypt` | 90, 180, 270, 180 | 2 / 4 | **2 / 4** |
| `dg_ember_deep` | 0, 180, 180, 270, 180 | 3 / 5 | **3 / 5** |
| `dg_sunken_vault` | 0, 180, 270 | 1 / 3 | **1 / 3** |
| `dg_descent_probe` | 0 | 0 / 1 | **0 / 1** |

It also explains two things canon measured but could not account for:
- **Bottoms are fine (4/4, 4/5, 3/5, 2/3)** — the bottom nose lands flush on the room's own floor in
  OPEN AIR, so it always has clearance. Only the top is buried.
- **The first descent is always the broken one** in ember_deep, sunken_vault and the probe
  (`dn_0 <-> up_1` is Delta 0 in all three), which is why whole dungeons read `PathPartial` even
  where good stairs exist further in — *reachability is gated by the first failure on the path*.

### Why four rounds missed it

`DungeonBakerChecks.TryMate` accepts a mate on `align = dot(a.Outward, -b.Outward)`. For a vertical
pair that is `dot((0,1,0), (0,1,0)) = 1.0` — **at every yaw**. The one check that should have caught
this is structurally blind to the only degree of freedom that matters, so every bake reported
`matesFail=0` and `fallbacks=0` while half the stairs pointed at nothing.

### THE FIX — three parts, not one

1. **Delta yaw = 180 BY CONSTRUCTION.** In `SolveMate`, stop hardcoding `yaw = 0f` for vertical mates;
   set the child to oppose the parent (`parentYaw + 180`). Same "make it impossible by construction"
   shape as the WO-835 action-bar fix.
2. **A FLAT TOP LANDING AT FLOOR HEIGHT — owner ruling 2026-08-08, "we need that edge".** Rotation
   alone is not sufficient. A navmesh is a **shared-edge graph**: two surfaces connect only when their
   polygons were triangulated together. Today `LandingOverlap` (0.35 m) is applied ALONG THE SLOPE, so
   the ramp ends at y **6.24 / z 3.76** against a `TopNose` of y 6.00 / z 3.50 — **0.24 m proud, at
   42.71 degrees**. That is a wedge intersecting a slab, not a landing. The flight must end in a FLAT
   segment at exactly floor height, overlapping the hole lip, so the rasterizer emits one continuous
   span across the seam. Mirror what the bottom already does correctly.
3. **An oracle for the vertical mate**, asserting Delta yaw = 180 and failing when it is not. Without
   it this regresses silently, because `align` will keep reporting 1.0.

### Also fix, same defect wearing a different hat

**Every extract is seated in a STAIR room.** All five of `dg_ember_deep`'s
(`ed-extract-l1..l5` -> `stair_up_1..5`), and `dg_bonecrypt`'s `bc-extract-l1` -> `stair_up_1`. The
exit beacon is 6.4 m tall centred at local y 6.2, so it spans world y -3.0..+3.4 and rises **through
the floor above**, out of the descent hole — which is the green bar the owner first reported, and why
several mirrored `EXIT` labels stack through the floors in one frame. Visual half: **WO-1008**.

---

**Status: SUPERSEDED BY §0 — root cause found. Original status: READY TO IMPLEMENT** (measurement first — §5 is a hard gate on §6)
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
