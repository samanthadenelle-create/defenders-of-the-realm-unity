# 🔒 SEAM / BRIDGE / MOAT OFFSETS — LOCKED (2026-07-04)

**Status: LOCKED CANON.** These are the load-bearing geometry constants for the castle
moat crossing (the "south bridge seam"). They are fleet-proven (6/6 bots crossed both
directions, masked warp fired mid-span). **Do not change any value by eye.** The deck
height was *measured* from the FBX mesh (`BridgeDeckMeasure.Run`), not guessed — if the
bridge model or plinth height changes, RE-MEASURE, don't nudge.

Source of truth = `Assets/_Modules/Village/World/CastleMoatBuilder.cs` named constants.
This doc mirrors them so they are never re-derived or guessed (§12 / §15 canon discipline).

## Locked values

| Quantity | Value | Constant / source |
|---|---|---|
| **Bridge span** | **22.2 m** (10.85 local × 2.049 scale) | measured, RESUME 2026-07-03 verified |
| **Castle-end (inner) radius** | **r = 44** | `RampInnerRadius` = `CastleHubBuilder.PlinthHalf` (plinth face) |
| **Moat outer radius** | **62** | `MoatOuterRadius` (owner ruling: ~18 m band) |
| **Moat band** | **44 → 62** (18 m wide), deck spans 44→~66 | `MoatInnerRadius..MoatOuterRadius` |
| **Moat centreline** | **53** | `MoatCentreRadius` |
| **liftY (plinth top / castle-end height)** | **3.0** | `PlayerPrefs "castle.liftY"`, default 3 |
| **Raw seat height (pre-pitch)** | **0.05** | `BridgeY` (offsets.json overrides) |
| **Deck surface local Y** | **2.6** | `DeckSurfaceLocalY` |
| **Deck half-width** | **2.3** | `deckHalf` |
| **Fixed water level** | **1.5** | `FixedWaterY` |
| **Basin lip crest / floor** | crest 2.0 / floor 0.0, width 3 | `LipTopY` / `LipFloorY` / `LipWidth` |
| **Wet-shore strip width** | 1.5 | `MoatShoreStripWidth` |

## 🔒 OWNER-TUNED SOUTH BRIDGE POSE (LOCKED 2026-07-04, "these look perfect")
The owner felt-tuned the south bridge transform in-scene and ratified it. This POSE is the
locked seat for the South crossing (the West/North/East clones yaw-rotate it about origin
per below). **This owner-ratified pose is the authority for the south seat**; the analytic
constants (r=44, band 44→62) still govern the moat ring.

| Field | X | Y | Z |
|---|---|---|---|
| **Position** | -4.5 | -0.64 | -58.8 |
| **Rotation (Euler)** | 0 | 90 | -7.684 |
| **Scale** | 2.969011 | 1 | 1 |

⚠ **CORRECTION (SME investigation 2026-07-04) — these are POST-SEAT capture values, ALREADY WIRED.**
The builder applies the `offsets.json` `bridge_south` entry (currently pos(-4.5,-0.5,-53.4) rot(0,90,0)
scaleXyz(2.049,1,1)) and THEN the analytic descent seat (`CastleMoatBuilder.cs:562-628`) which slides Z
and pitches by `-atan2(liftY=3, span)`. `atan2(3, 22.2m)=7.70° ≈ the -7.684` above — i.e. the rot.z/y/z
here are what the owner read off the Inspector AFTER the seat ran. **Do NOT write these raw into
offsets.json — that double-applies the seat.** The pose is already achieved by the existing entry + seat.
- The `scale 2.969` is INCONSISTENT with the `-7.684` pitch (that pitch implies span 22.2m = scale 2.049,
  the stored value). Likely a mis-transcription of a lossyScale. **OWNER CONFIRM before any pose change:**
  is 2.049 (stored, self-consistent) correct, or did you intend a genuinely different scale?
- `scaleXyz` (Vector3) IS supported by the offset loader (`OffsetTable.cs`) — the earlier "single-float
  schema doesn't fit" note was STALE; a non-uniform scale CAN be stored if ever wanted.

## Seat derivation (locked, `CastleMoatBuilder.cs:604-625`)
1. Slide the bridge FBX along its span so the +Z (castle) face lands at `z = -44` (plinth face).
2. Pitch about the OUTER end by `pitchDeg = -atan2(liftY, span)` so the castle end rises
   exactly `liftY = 3` over the measured span; outer end seats on OuterWorld ground.

## Four crossings = clones of South (locked, `:376 / :472`)
The `label=="South"` special-case and the N/W/E funnel-ramp path are **RETIRED**. Every
crossing is a CLONE of the South frame, yaw-rotated about the world origin:
**South 0° · West 90° · North 180° · East 270°.** CHECK4 parity oracle asserts the clones
match the South baseline (transforms/colliders/renderers).

## ⚠ Tree of Life anchor — y = 0 (owner-flagged critical, 2026-07-04)
The **Heart of Elarion / Tree of Life is anchored at world (0, 0, 0)** — canon centre
(CLAUDE.md §7). The castle plinth top is raised to **liftY = 3**. These two facts must be
reconciled deliberately: a tree authored at y=0 while the plinth surface sits at y=3 will
read as sunk 3 m below the courtyard. **LOCKED FACT, NOT YET A RESOLVED FIX** — if the tree
appears low, the correct anchor is a design/RCA call (instrument from the exe per §12,
do not eyeball-nudge the tree Y).

## 🔒 BASIN LIP IS REQUIRED (owner 2026-07-04, "you have to do the lip otherwise it cuts through the bridge")
The outer basin lip (`BuildOuterLip`, rim ring r=62..65) is **mandatory** — without it the
basin edge reads open / cuts through at the bridge mouth. BUT its crest (`LipTopY`, currently
2.0) MUST sit **below the deck bottom where the deck passes over r=62**. The owner-tuned pose
lowers the bridge (y=-0.64), dropping the deck onto the 2.0 crest → the lip pokes through the
deck. **Paired change:** whenever the bridge pose changes, re-derive `LipTopY` from the MEASURED
deck-bottom height at r=62 under the ratified pose (`BridgeDeckMeasure` / a build capture), keep
crest a hair below it AND above `FixedWaterY=1.5`. Do NOT eyeball the crest (§12). Deck outer-end
top = `walkOuterY + DeckSurfaceLocalY*|scaleY| + 0.05` (`CastleMoatBuilder.cs:649-652`).

## Regression guard
`VerifyMoatComplete` oracle + CHECK4 clone-parity assert the crossing span > band + bedding,
×4, in the fleet asserts. Keep those green whenever any value above is touched.
