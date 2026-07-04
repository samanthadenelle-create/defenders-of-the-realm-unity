# WORK ORDER 607 — Castle-moat seam: 4-side walk-traversability (mirror South → W/N/E)

**Status: READY TO IMPLEMENT (with owner felt-verify gate).** Provisional number — slot into
`CLI_LANES_WO_NUMBERS.md` / the master backlog (numbering authority is the master doc, not the FS max).

**Origin:** owner directive 2026-07-04 "mirror south to other three sides and bake again." The bridges,
links, gate-exit strips, lip, and hedge are ALREADY 4-side-cloned in code. The remaining gap is
**walk-traversability across the moat on W/N/E** (the fleet `CHECK5` oracle is red on W/N/E; South passes).
This WO holds the two root causes (one precise, one deep) proven from captured fleet data + code this session.

## What this session already did (IN TREE, UNCOMMITTED — held pending this WO + felt-verify)
- `Assets/Editor/CastleHubBuilder.cs` — `BuildSeamlessOuterWorldSeam` now bakes **4** cross-moat
  `NavMeshLink_CastleToOuterWorld_{S,W,N,E}` (South endpoints rotated 90/180/270 about origin). Braces 246/246.
- `Assets/_Modules/Village/World/CastleMoatBuilder.cs` — `BuildOuterLip` **notched at the 4 bridge mouths**
  (via `MeasureMouth`, mirroring the hedge/navcarve gap logic) so no deck ever crosses solid lip regardless
  of pose (owner: "you have to do the lip"). Runtime (no bake). Braces 143/143. LIP FELT-VERIFY PENDING (no
  code oracle for deck-clip; needs the human/F8 visual path).
- Baked scenes `MainCastle_Hall.unity` + `OuterWorld.unity` re-baked (castle nav + 4-link seam + OuterWorld nav).
- Gate: COMPILE_GATE_OK. Fleet: CHECK5 still `PathPartial West/North/East` (the links did NOT green it — root below).

## ROOT 1 — South-only `RUNTIME_SEAM_NAV_FAIL` = precise, side-specific bug (type A)
`RuntimeRegionGate.cs:360-368`: the nav-slope width clamp runs **only at yaw=0 (South)** —
`GameObject.Find("RuntimeSeam_Bridge_South")` + `Mathf.Abs(DeltaAngle(_facingYaw,0f))<1f`. Only South
narrows its slope to the stone-bridge deck width (`bb.size.x-1.2f`); after the runtime bake's 0.18 voxel +
agent-radius erosion + `minRegionArea 0.5` prune (`RebakeSourceSurface:755-757`), that narrowed South lane
is the one thin enough to fail the courtyard→threshold weld → the South-only `RUNTIME_SEAM_NAV_FAIL`.
**⚠ The clamp is intentional (flag_14: stop the hero nav-walking off the bridge sides).** So the fix is NOT
a blind "drop the clamp" — that regresses flag_14. Options, felt-verify each:
  - raise the clamp floor / reduce the `-1.2f` margin so the South lane stays wide enough to survive erosion
    while still inside the parapets; OR
  - apply the SAME clamp per-side (de-hardcode `"RuntimeSeam_Bridge_South"` → `"RuntimeSeam_Bridge_"+SideName(_facingYaw)`
    at `:361` AND `:414`) so all four sides are contained identically, then widen the floor once.
  - Runtime bake only → verify with a rebuild + fleet (`RUNTIME_SEAM_NAV_OK [South]`), then F8 walk-off check.

## ROOT 2 — W/N/E `CHECK5 PathPartial` = deep seam-weld problem (type B, WO-453 class — DO NOT guess a clone fix)
There is NO clone/bake line that singles out W/N/E — bridges (CHECK4 parity), carve mouths, gate-exit strips,
per-gate deck nav + AI link are all symmetric. The one real asymmetry: **the OuterWorld landing is a single
south-authored constant** `WorldGeometry.SouthGateSeamLanding`, reused on every side by rotation
(`RuntimeRegionGate.cs:980-1010`, `_landing` `:180`, AI `link.endPoint=ToWorld(_landing)` `:820`). This assumes
OuterWorld's navmesh is 4-fold rotationally symmetric about the castle origin — it is not proven to be. So the
deck→OuterWorld runtime overlap-fusion connects only on the tuned South side; W/N/E get the south landing merely
rotated onto whatever OuterWorld nav happens to be there → severed mid-crossing → `PathPartial`.
**Fix (the real work):** author/sample a REAL per-side OuterWorld landing on the live OuterWorld navmesh for
W/N/E (not a rotated south constant), and/or replace the overlap-fusion with an explicit stitched/linked weld
per side. This is the parked WO-453 / "V2 enemy seam navmesh traversal" class — bounded, but needs the
un-stacked OuterWorld work + per-side felt-verify. NOT a rushed batch job.

## Acceptance
- Fleet `MOAT_COMPLETE` with `CHECK5 South/West/North/East … PathComplete` (all four), `RUNTIME_SEAM_NAV_OK`
  all sides, CHECK4 parity + CHECK6 carve stay green.
- Owner F8 felt-verify: hero crosses on all 4 sides; no walk-off-bridge-sides; lip does not clip any deck.

## Files
`RuntimeRegionGate.cs` (:360-368 clamp, :980-1010 landing, :180/:820 link), `CastleHubBuilder.cs`
(`BuildSeamlessOuterWorldSeam` :2196-2267), `CastleMoatBuilder.cs` (`BuildOuterLip`, `CheckReachability`
:1365-1416, `TryBankPoints` :1469-1492), `region-gates.json` (4 rows), `WorldGeometry.SouthGateSeamLanding`.

## NOT TO TOUCH
The bridge pose/offsets (already wired; the doc's scale 2.969 vs 2.049 pitch inconsistency is a separate
owner confirm). The flag_14 containment intent (fix must preserve no-walk-off-sides).
