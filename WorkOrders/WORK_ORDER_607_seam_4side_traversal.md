<!-- era-sweep-2026-08-17 -->
> ### ⛔ ERA SWEEP 2026-08-17 — SUPERSEDED
> **Superseded by:** WO-608 (world merge to one scene). **Git first-add:** 2026-07-04.
> **Evidence:** scope is 4-side walk-traversability ACROSS the castle/OuterWorld scene cut, and its work products are re-bakes of `MainCastle_Hall.unity` + `OuterWorld.unity`. `Assets/Scenes/OuterWorld.unity` is absent from disk and from `git ls-files`.
> Only the `**Status:**` line was rewritten. The body below is UNTOUCHED — CLAUDE.md §15, *"frozen, never rewrite"*.
> **TO REVIVE:** nothing was deleted and not one line of the body below was changed. If this work is still wanted, re-date the WO (add a `**Minted:** <today>` line), re-point it at the live scene/system, and set `**Status:** READY TO IMPLEMENT`.

# WORK ORDER 607 — Castle-moat seam: 4-side walk-traversability (mirror South → W/N/E)

**Status:** SUPERSEDED by WO-608 (world merge to one scene) (era sweep 2026-08-17)
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

## ⚠ ROOT 1 — SUPERSEDED 2026-07-04 by CAPTURED DATA (the width-clamp theory below was WRONG)
**§12 finding (break-log 2026-07-04):** the South fail is a **VERTICAL weld gap, not a width problem.**
The captured trace shows South threshold low-end `y=5.84` — ABOVE the plinth edge `deckY≈3.08` (an
impossible ascending ramp) — while W/N/E measure `y=3.00` and weld (`RUNTIME_SEAM_NAV_OK`). Cause: on the
owner-tuned South side the bridge is "extended over the lip to the ground" (owner note 07-04), so the
all-layer downward threshold raycast (`RuntimeRegionGate.cs:352`) hits the RAISED bridge/lip instead of the
courtyard floor; the plain W/N/E clones hit the floor at 3.00. The 3.1m clamp width is a walkable lane for a
~0.5m agent — a red herring. **FIX APPLIED (RuntimeRegionGate.cs ~:350-372, uncommitted):** when the probe
lands above the deck, weld the low-end to the sampled navmesh Y at the threshold (fallback = deckY so the ramp
is at worst flat, never ascending). Only the broken (South) case changes; W/N/E keep their raw measure; the
lip, owner pose, and flag_14 are untouched. Instrumented (`THRESHOLD-PROBE` FlowTrace) so the fleet oracle
proves the flip to `RUNTIME_SEAM_NAV_OK[South]`. VERIFY: rebuild + bake MainCastle_Hall + fleet.

**✅ VERIFIED 2026-07-04 (fleet seed 2000, single-instance clean Player.log):**
`deck[South] THRESHOLD-PROBE: hit 'RuntimeSeam_BridgeDeck_Collider' rawY=5.84 deckY=3.08 navmesh@threshold=3.19 -> lowY=3.19 raw probe ABOVE deck (hit raised bridge/lip) -> welded to navmesh Y instead`
→ `RUNTIME_SEAM_NAV_OK [South facingYaw=0]` (+ W/N/E all OK). W/N/E kept their raw probe
(`CourtyardFloor_Nav` rawY=3.00 "probe kept") — no regression. The instrument NAMED the culprit
(RuntimeSeam_BridgeDeck_Collider = the owner's over-the-lip bridge), the fix path demonstrably
triggered, and the `RUNTIME_SEAM_NAV_FAIL` oracle went silent across a 4-seed fleet. ROOT 1 DONE.
NOTE: this is the RUNTIME_SEAM_NAV (runtime weld) oracle; ROOT 2 (W/N/E CastleMoatBuilder CHECK5
reachability) is a SEPARATE oracle, still deferred per owner.

## ROOT 1 (ORIGINAL, SUPERSEDED) — South-only `RUNTIME_SEAM_NAV_FAIL` = precise, side-specific bug (type A)
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
