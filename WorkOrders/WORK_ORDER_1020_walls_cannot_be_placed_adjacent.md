# WORK ORDER 1020 — Walls cannot be built beside each other (adjacency placement blocked)

**Status:** SUPERSEDED by `WorkOrders/WORK_ORDER_972_walls_cannot_be_built_beside_each_other.md`
(shipped `66b5628c`). Body frozen below as minted — banner only, per CLAUDE.md §15.
**Minted:** 2026-08-10 (UI seat) — provenance stack bumped 1020 → 1021 in the same edit
**Lane:** Build placement VALIDATION (logic). ⚠ **Deliberately NOT in WO-1010**, which is build-UI
layout only and forbids placement-logic changes. Same screen, different layer.
**Provenance:** owner F8 **seq=2327**, 2026-08-10 21:05, `Main_Castle_Overworld`, verbatim:
**"cannot build walls beside each other"**. Capture: `logs/f8-inbox/capture-20260810-210535-seq2327.md`.

---

## ⚠ SUPERSEDED 2026-08-14 — DUPLICATE MINT. Surviving ticket: `WorkOrders/WORK_ORDER_972_walls_cannot_be_built_beside_each_other.md`

**Why:** this is the SAME defect, from the SAME owner capture (F8 **seq 2327**, `Main_Castle_Overworld`,
verbatim *"cannot build walls beside each other"*), as WO-972 — which the CLI seat had already shipped at
`66b5628c`. The UI seat minted 1020 in parallel without knowing. No work is lost; nothing here is a
separate defect.

*(Reference the surviving ticket by FULL FILENAME, never a bare number — three WO numbers in this repo
already have two claimants each, per `docs/reference/WO_TRUE_STATUS_2026-08-08.md` §2, so "WO-972" alone
is not a reference.)*

**What the surviving ticket delivered:** a wall now claims **ONE cell** instead of a 2x2 block. The root
was `MeasureUprightFootprintMetres` collapsing the mesh to `Max(x, z)`, which `FootprintCells` then
**ceilinged AND squared** — so a 1% overshoot (3.03 m measured against a 3.00 m cell) rounded to 2 and
squared to a 4-cell claim, making adjacency impossible. Covered by regression
`WallAdjacencyRegression [wall-adjacency]`.

**Everything below is the original 1020 mint, frozen as written (CLAUDE.md §15) — read it as history, not
as an open instruction.**

---

## 1. What the capture shows

```
[Flow:Build]        PlaceLoop LIVE: armed='wall_wood', ghostValid=True,
                    input=LeanTouchBuildDriver, Mouse.current=True — PlaceConfirm poll runs this frame
[Flow:BuildTimerUI] 'wall_wood@16_17' remaining=8s
[Flow:Build]        PlaceScreen: Rotate Right pressed (90 deg).
[Flow:Build]        ghost rotate -> 90°
```

Facts, and only facts:
- One wall **is already placed and still BUILDING** at grid cell `16_17` (`remaining=8s`).
- The armed ghost is `wall_wood` and reports **`ghostValid=True`** in the logged frames.
- The player was **rotating** the ghost (90°) around the time of the flag.

**⚠ The rejection itself is NOT in this harvest.** `ghostValid=True` is the ghost's state in the frames
captured; we do not have the line where the adjacent placement was refused. **Do not theorise past this
— instrument the refusal first** (§12).

## 2. Prime hypotheses to TEST (cheapest first — do not assume)

1. **An IN-PROGRESS build job reserves more than its own cell.** The neighbour at `16_17` is mid-build
   (8s left). If the job's reservation/occupancy is applied with padding, or claims a footprint larger
   than the finished piece, the adjacent cell reads occupied. **Test:** wait for `16_17` to COMPLETE,
   then try the same adjacent placement. If it succeeds, the defect is in the in-progress reservation,
   not the wall footprint.
2. **Rotated footprint over-claims.** The ghost was rotated 90°; a footprint whose rotation is applied
   to a non-square or off-centre bounds could overlap the neighbour. **Test:** unrotated adjacency.
3. **A deliberate spacing rule.** Some placement validators enforce a gap between structures. If walls
   are (wrongly) subject to a generic building-spacing rule, adjacency is impossible **by design** —
   which is wrong for walls specifically and is a data/exemption fix, not a geometry fix.
4. Collider-vs-grid mismatch: the wall's physical collider extends past its grid cell, so the physics
   overlap check fails even though the grid says free.

## 3. Why this is HIGH

**A wall's entire purpose is to form a continuous RUN.** If segments cannot sit adjacent, the Walls /
"Castle Structures" category is functionally non-functional — the player cannot enclose anything, which
undermines the defense pillar and the WO-1010 build redesign that is being tested right now. It also
makes the small-piece placement flow (the thing the lean rail + nudge D-pad exist to serve) pointless.

## 4. Acceptance

- [ ] Two `wall_wood` segments can be placed in directly adjacent cells, in every rotation.
- [ ] Adjacency works while a neighbouring segment is STILL BUILDING (not only after completion).
- [ ] A run of 5+ contiguous segments can be laid without a refusal.
- [ ] Stone wall + gate adjacency verified too (same category, same rule).
- [ ] Genuinely-invalid placements still refuse, and **say why in words** (colourblind law) — this fix
      must not turn the validator permissive.
- [ ] `[Flow:Build]` logs the REFUSAL REASON on every rejected confirm (the gap that made this capture
      inconclusive) — permanent instrumentation, §12.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites`; add a regression placing an adjacent pair.

## 5. What NOT to touch

- WO-1010's build UI/layout work (this is the validation layer beneath it).
- Costs, catalog data, build timers.
- Do not "fix" this by disabling overlap checks globally — the refusal must stay correct everywhere else.
