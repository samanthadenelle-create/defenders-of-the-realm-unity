# WORK ORDER 972 — Walls cannot be built beside each other (the 2x2 claim on a 1-cell tile)  — **OWNER CLOSED 2026-08-22** (felt-verified by the owner; PO closes, section 13).

**Status:** CLOSED 2026-08-27 — owner felt-tested PASS on APK 2026.08.27.343739 (village review).

*(Board note 2026-08-24: bucket corrected DONE/IMPLEMENTED → **FIXED**. Nothing about the work changed — §13 reserves DONE/closing for the PO, and this line's own text says the owner's felt-verify is still owed, so the row belongs in the felt-test queue, not the closed pile.)*

> ### VERIFIED AT SOURCE 2026-08-22 (status audit) - the fix is present on BOTH paths
> * `StructureFactory.MeasureClaimFootprintXZ` (`Assets/_Modules/Village/Catalog/StructureFactory.cs:965-979`) returns the **authored** `repo.placement.footprint` on both axes for `CatalogType.Wall`; the mesh is not resized.
> * **PLACEMENT** path: `Assets/_Modules/Village/BuildMode/BuildModeController.cs:1566` - `_grid.FootprintCells(StructureFactory.MeasureClaimFootprintXZ(entry), ArmedYawDegrees)`.
> * **REPLAY / load** path: `Assets/_Modules/Village/BuildMode/BaseLayoutLoader.cs:326` - `Vector2 claimXz = StructureFactory.MeasureClaimFootprintXZ(entry);`.
> * Oracle registered: `Assets/Editor/Regression/DataRegression.cs:900` -> `[wall-adjacency]` (`WallAdjacencyRegression`).
>
> Owner felt-verify still owed - the PO closes, not an agent (CLAUDE.md 13).

> ### GAP THIS WO NEVER TESTED - **GATES ARE NOT COVERED** (found 2026-08-22)
> `MeasureClaimFootprintXZ` special-cases **`CatalogType.Wall` ONLY** (`StructureFactory.cs:968`:
> `if (entry == null || entry.type != CatalogType.Wall) return measured;`).
> `gate_stone` is authored **`"type": "Gate"`** (`Assets/Resources/Data/Canonical/structures-catalog.json:383`,
> with `repo.placement.footprint: 2.8`), so a gate still claims its **measured mesh**, not its authored footprint.
> **Gate-beside-wall may therefore still reject**, and WO-972 never exercised a gate. Do not read this ticket's
> DONE as covering gates - that needs its own ticket and its own oracle case.

**Silo:** Build-mode placement (Village / BuildMode)
**Source:** Owner F8 capture **seq 2327**, scene `Main_Castle_Overworld`, 2026-08-11 02:05:32 UTC
**Capture:** `logs/f8-inbox/capture-20260810-210535-seq2327.md`
**Owner, verbatim:**

> **"cannot build walls beside each other"**

---

## 1. RCA — proven from the capture, not inferred

### PROVEN-BY-CAPTURE — the reject itself

Her `Player.log` carries the reject 60+ times in the seconds before the F8, all identical:

```
[Flow:Build] REJECT Occupied cell=(17,16) fp=(2x2) gate=CellGrid occupantCell=(17,17) occupant='wall_wood'.
[Flow:Build] PlaceLoop LIVE: armed='wall_wood', ghostValid=False (reject=Occupied), input=LeanTouchBuildDriver
```

`fp=(2x2)` is the whole bug. **A wall claims a 2x2 block of grid cells.**

She had a wall run along z=17 and was starting a corner one row south at (17,16). The neighbouring
wall's *phantom* cells owned that square, so the ghost went invalid and the placement silently refused.

> The harvested lines showing `ghostValid=True` are from ~8 s EARLIER, while the `16_17` job was still
> building. They are not the failing moment; the failing frames report `ghostValid=False (reject=Occupied)`.
> So this was never a "valid ghost that would not commit" — it is a genuine occupancy refusal from a
> claim that is wrong.

### PROVEN-BY-CAPTURE — a wall is nowhere near 2x2

```
[Flow:Structure] 'wall_wood' carries Collider 'MeshCollider' bounds size=(3.03, 3.73, 1.42) ...
```

The fitted palisade is **3.03 m across and 1.42 m thick**, on a **3.00 m cell**
(`PlacementGrid.cellSize = 3f`, `PlacementGrid.cs:37`). It is a one-cell tile that overshoots its cell
by **3 centimetres** — 1%.

### READ-AT-SOURCE — how 3.03 m becomes a 2x2 block

Two independent collapses stack:

1. `StructureFactory.MeasureUprightFootprintMetres` (`StructureFactory.cs:693`) reduces the mesh to a
   **single scalar**: `Mathf.Max(b.size.x, b.size.z)` — the 1.42 m depth is thrown away.
2. `PlacementGrid.FootprintCells` (`PlacementGrid.cs:235-239`) then **ceils and squares** it:
   `cells = Ceil(3.03 / 3.00) = 2` → `return new Vector2Int(cells, cells)` → **2x2**.

So a 1 % overshoot on one axis doubles the claim, and squaring re-applies that doubling to the *thin*
axis, which was never over a cell at all. A 3.03 x 1.42 m body claims **36 m² of a 3 m grid**.
Consumed at `BuildModeController.cs:1517` and `BaseLayoutLoader.cs:311-313`.

### PROVEN-BY-CAPTURE — the second symptom, same root

The run she *did* land is on a **six-metre pitch**:

```
[Flow:Grid] Occupy cell=(12,17) footprint=(2x2) id='wall_wood'
[Flow:Grid] Occupy cell=(14,17) footprint=(2x2) id='wall_wood'
[Flow:Grid] Occupy cell=(16,17) footprint=(2x2) id='wall_wood'
```
with collider centres at **x = -7.50 / -1.50 / +4.50** — 6 m apart, for a 3.03 m wall.
**Every "wall run" she can build today has a ~3 m hole in it between each segment.** That is the same
sentence from the other side: she cannot put two walls beside each other, and what she gets instead is
a dashed line. Both halves are one root cause.

### Candidates ELIMINATED by data (named in the brief)

| Candidate | Verdict |
|---|---|
| Under-construction scaffold blocks adjacency | **NO** — the occupant is the completed `14_17`/`16_17` wall; the trace names `gate=CellGrid`, not a build-job gate. |
| Singleton / one-per-town rule | **NO** — `reason=Occupied`, not `Singleton`; three walls already stand. |
| Gate-clearance pathing protection | **NO** — that gate reports `BlocksGate` (`BuildModeController.cs:1630`) and never fired. |
| Placement succeeding, rendering elsewhere | **NO** — `ghostValid=False`, no `Place`/`Occupy` line follows. |
| NavMeshObstacle claiming neighbours | **NO** — the obstacle is 3x3 m (below); it is not consulted by placement at all. |

**The block is NOT intentional pathing protection.** It is an arithmetic defect in the cell claim.
The words-based refusal is still shipped (§3) because the refusal told her nothing but a red tint.

---

## 2. The fix — claim-side only, mesh untouched

### A wall claims off its AUTHORED footprint, not its fitted mesh

New `StructureFactory.MeasureClaimFootprintMetres(entry)`: identical to the measured value for every
row **except `CatalogType.Wall`**, which claims off `repo.placement.footprint` (**2.1 m -> 1x1**).
Both claim sites now call it (`BuildModeController.cs:1517`, `BaseLayoutLoader.cs:311`) so a reloaded
wall claims exactly what placement promised.

### Wall may abut wall

With a one-cell claim, two neighbouring 3.03 m bodies overlap by 3 cm **by design** — that overlap is
what makes a run continuous. The strict `OverlapsXZ` test would read those centimetres as occupied and
merely move the reject from `gate=CellGrid` to `gate=WorldOverlap`, so `OverlapsExistingStructure` now
skips `WallSegment` blockers when the armed entry is a Wall. **Not a hole in the rule:** the cell grid
still refuses two walls on the same square, and the gate lane test (step 4) is untouched.

### Why this respects all three standing constraints

| Constraint | How it is honoured — verified, not assumed |
|---|---|
| **Walls excluded from the height-cadence fit** (narrowing opens pathable gaps + shrinks the obstacle) | **The mesh is never touched.** No `heightMul`, no scale, no prefab edit. Only which cells are marked occupied changes. This fix in fact *removes* the coupling that caused the bug — the wall's grid claim no longer follows its fitted mesh size at all. |
| **NavMesh carve must not shrink** | **Byte-identical.** `BaseLayoutLoader.AddFootprintBlocker` sizes the box `Clamp(rendered * 0.85, cellSize, claim)`. Wall: `rendered*0.85 = 2.58` -> clamped to the `cellSize` floor of **3** at BOTH the old 2x2 claim and the new 1x1. The capture confirms today's value: `kept root footprint box 3x3m (h=4)`. The `NavMeshObstacle` is the same box, unchanged. |
| **WO-948 walls build at L1 only** | Untouched — no palette, tier, or `maxLevel` change. |

### Save-compat — nothing moves

`PlacementGrid.CellToWorld` (`PlacementGrid.cs:118-123`) seats a structure on its **origin cell centre,
independent of footprint**, and `BaseLayoutLoader.cs:271` places at exactly that. So every already-saved
wall replays at the identical world position and merely claims **fewer** cells. A shrinking claim can
never invalidate a saved layout. **No migration, no save-schema bump.**

### Verified against the captured geometry

Origin solves to `(-45, -45)` from the captured centres. Her exact failing placement at cell (17,16)
now claims only (17,16) — free — and its AABB `z=[3,6]` clears the neighbour's body `z=[6.79,8.21]`.
She can also now place at (17,17), directly beside the `16_17` wall: **3 m pitch, bodies touching, a
continuous run.**

---

## 3. Words, never colour alone

The owner is **red/green colourblind** — a red ghost tint carries no information for her, and
`"Too close to another building"` never said *what* was in the way. Both Occupied gates already
captured the occupant id for the trace; that name now reaches the player:

> **"Too close to another building - Wooden Palisade is already on that square"**

Plumbed via `_lastRejectDetail` (cleared at the top of every evaluation, so it can never carry a stale
name) into `ReasonLabelText`, which already feeds **both** the toast and the floating ghost label
("tell me why it's red", 2026-07-24). ASCII only.

---

## 4. Instrumentation (§12) — the datum that was missing

The RCA had to **bound** the measured footprint from a MeshCollider dump plus the blocker clamp,
because `MeasureUprightFootprintMetres` logs its result **nowhere**. That gap is now closed
permanently — `FlowTrace.Once` per wall id, stating the authored claim AND the measured mesh:

```
[Flow:Build] WALL CLAIM 'wall_wood': the grid claim is driven by the AUTHORED
placement.footprint=2.100m, while the fitted MESH measures N.NNNm across. ...
```

This also answers, from her next capture, the one number this RCA could not pin exactly: the
**renderer**-measured width (bounded to (3.00, 3.53] m — the collider read 3.03; the blocker clamp
proves `rendered * 0.85 < 3.0`). No behaviour depends on it — the fix is deliberately built so it
does not need that number — but it will now be on the record.

---

## 5. Files changed

| File | Change |
|---|---|
| `Assets/_Modules/Village/Catalog/StructureFactory.cs` | `+ MeasureClaimFootprintMetres` (wall claim + proving line). `MeasureUprightFootprintMetres` untouched. |
| `Assets/_Modules/Village/BuildMode/BuildModeController.cs` | Claim call swapped; wall-abuts-wall allowance; `_lastRejectDetail` / `OccupantLabel` words. |
| `Assets/_Modules/Village/BuildMode/BaseLayoutLoader.cs` | Claim call swapped so replay matches placement. |
| `Assets/Editor/Regression/WallAdjacencyRegression.cs` | **NEW** — see §6. |

**NOT touched:** any `.unity` scene, any prefab or mesh, `structures-catalog.json`, `heightMul`, the
palette, the gate-clearance rule, `DataRegression.cs`, or the file fence.

Brace + NUL check: **PASS** on all three edited `.cs` (526/526, 84/84, 156/156; no NUL bytes).

## 6. Regression

`WallAdjacencyRegression.Run(out string reason)` — `[wall-adjacency]`, markers
`WALL_ADJACENCY_OK` / `WALL_ADJACENCY_FAIL`. Fails if a wall's claim grows past one cell, if the
square-claim collapse returns, if the wall abut allowance is removed, or if the words-based refusal
is reduced to colour. Registration line is handed to the committer; the suite does **not** self-register
(`DataRegression.cs` is lane-fenced).

## 7. Acceptance

- [ ] Two walls place on adjacent cells; the run reads continuous, no ~3 m holes.
- [ ] A wall places on the cell diagonally/perpendicular to an existing wall (her exact seq-2327 case).
- [ ] Two walls on the SAME cell are still refused.
- [ ] A refused placement says **in words** what is in the way.
- [ ] Existing saved towns reload with every wall in its original position.
- [ ] Enemies still cannot path through a wall run (obstacle is 3x3 m, unchanged).
- [ ] PO felt-verifies and closes (headless cannot judge feel).
