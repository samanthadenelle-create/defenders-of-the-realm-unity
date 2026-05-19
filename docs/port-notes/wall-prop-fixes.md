# Wall + prop placement fixes — VillageSceneBuilder.cs (2026-05-19)

Two placement bugs the owner flagged after reviewing the built Avalon village,
both fixed in `Assets/Editor/VillageSceneBuilder.cs` (surgical edits, no rewrite).

## Bug 1 — Curtain wall: gaps / overlaps where pieces meet

### Root cause (confirmed)

`BuildWallRing()` scales each straight `wall_straight` piece to fill its
WallLayout segment length with `s.x *= length / MeasureLocalLength(visual)`.

`MeasureLocalLength()` returned `Renderer.bounds.size.x` — a **world-space**
AABB. By the time it was called the `visual` mesh had been:

1. rotated by `WallStraightYawFix` (90deg) as its own `localRotation`, and
2. parented under `go`, which itself carries the per-segment `WallLayout`
   rotation (`seg.Rotation`, a Y-rotation that aligns the run to its side).

A world-space AABB's `.x` therefore no longer corresponds to the mesh's own
run-length axis — depending on the segment's compass heading it read the
piece's **depth/thickness** (~0.6u) or some diagonal mix, never the true run
length. The scale-to-fit factor `length / baseLen` was consequently wrong for
every straight, so sections came out too long or too short — visible gaps and
overlaps at corners, the south bow, and along straight runs.

A second, related defect: the KayKit `wall_straight` mesh's long axis is its
**local Z**, not local X (that is *why* the 90deg yaw fix exists — to swing the
long axis onto `go`'s local-X run direction). So even a correct measurement fed
into `s.x` would have stretched the wrong axis. The fix had to both measure
*and* scale the correct axis.

### Fix

- Removed `MeasureLocalLength()`. Added `TryMeasureLocalBounds(GameObject, out
  Bounds)`: takes each child `MeshFilter.sharedMesh.bounds`, pushes its 8
  corners through `go.worldToLocalMatrix * meshFilter.transform.localToWorldMatrix`,
  and encapsulates. That round-trip cancels both the yaw fix on `visual` and the
  segment rotation on `go`, yielding extents in the visual's own local space —
  the axes `localScale` actually stretches. (Skinned-mesh fallback included for
  completeness.)
- Added `FitWallVisualToRun(GameObject visual, float runLength)`: measures the
  local bounds, picks whichever **horizontal** axis (local-X vs local-Z) is the
  longer one — that is the run axis — and stretches *that* axis's `localScale`
  component to exactly `runLength`. Auto-detecting the long axis makes the fit
  correct no matter the piece's native orientation or yaw-fix value.
- `BuildWallRing()` straight-piece block now calls `FitWallVisualToRun(visual,
  length)` instead of the old `MeasureLocalLength` + `s.x` math.

WallLayout already insets each run's straight sections off the corner blocks
(`cornerInset = (WallThickness + 0.55) * 0.5` per side) and bakes that into each
segment's `Length`. Corner pieces stay at native scale. So straights scaled to
fill `Length` exactly now tile flush between the native-scale corners — no gap,
no overlap. WallLayout's own segment lengths were sound; the bug was entirely in
the builder's measurement/scale axis.

`BuildPlotFence()` had the identical `MeasureLocalLength` + `s.x` bug for its
fence-side pieces — switched to `FitWallVisualToRun(f, span)` as well.

## Bug 2 — Smaller items: inconsistent scale

### Root cause (confirmed)

KayKit props are authored across several folders/packs (`decoration/props`,
`decoration/nature`) with **no shared unit** — a barrel, a haybale, a weapon
rack, a lumber pile and the tree meshes all have different native mesh sizes.
The dressing passes instantiated them at `localScale = 1` (props) or with flat
multipliers applied to that raw size (`Vector3.one * 1.2f` for orchard trees,
`Lerp(0.9f, 1.3f, ...)` for scattered trees). Because the multipliers acted on
inconsistent native sizes, props read noticeably too big or too small relative
to each other and to the buildings.

### Fix

- Added `NormalizeProp(GameObject go, float targetSize)`: measures the
  instance's true mesh bounds via `TryMeasureLocalBounds`, then applies a
  **uniform** scale so its largest extent (footprint or height, whichever
  dominates) equals `targetSize` world metres. Factor is clamped to [0.05, 40]
  so a freak mesh or placeholder cube can't explode/vanish. Every prop is then
  sized to one common yardstick.
- `PlaceProp()` gained an optional `targetSize` parameter (default 1.0m) and now
  calls `NormalizeProp` for the model (non-placeholder) case.
- Per-prop target sizes applied at the call sites:
  - Workshop yard — lumber 1.3m, stone 1.2m, barrel 1.0m, weapon rack 1.6m.
  - Orchard haybales 1.4m.
  - Orchard fruit trees normalised to ~3.5m (replaced flat `*1.2`).
  - Northern scattered trees normalised to `5m * Lerp(0.9..1.3)` — the natural
    size variation is preserved, but now relative to a known 5m base instead of
    a per-import-variable raw mesh size.
  - Approach-lane foliage — tree 4.5m, boulder 1.3m (were native scale 1).
  - Keep's Avalon banner normalised to ~3.2m (was native scale 1).

The Elarion standing-stone ring was left as-is: all six stones share one mesh
at one hand-tuned scale, so they are already internally consistent — Bug 2 is
about props reading inconsistently *against each other*, which the ring does not.

## What the integrator should check in the rebuilt screenshot

- **Curtain wall**: straight sections butt flush against the corner pieces at
  all six corners (NE, SE, bowE, bowW, SW, NW); no slivers of ground showing
  through and no pieces visibly clipping/overlapping. Check both long E/W runs,
  the short SE/SW bow legs, and the south bow face either side of the S gate.
- **Gates**: the four gate openings still line up centred in their side; the
  flanking straights meet the gate piece cleanly.
- **Plot fences**: the 2x2-hex building fences and the workshop-yard fence form
  closed rectangles with corners meeting (no gaps/overlaps on any side).
- **Props**: barrels, lumber/stone piles, weapon rack, haybales all read at a
  believable, consistent scale relative to each other and to the buildings —
  nothing dwarfing a doorway or shrunk to a pebble. Orchard + scattered + lane
  trees look uniformly sized (with gentle natural variation on the scatter).
- The build log's wall-section / prop counts are unchanged from before — only
  sizes changed, not the number of placed objects.
