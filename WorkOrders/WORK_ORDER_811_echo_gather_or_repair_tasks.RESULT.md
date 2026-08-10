# RESULT — WO-811: the REPAIR task lands; the gather half was already WO-830's

**Verified:** 2026-08-10 (CLI orchestrator; implementing agent + gates)

## Reconciliation (the aged-spec pass the one-week threshold demanded)

- Scope 1/3 (gather picks) — ALREADY SHIPPED broader by WO-830 (5 pickable resources, affinity
  match-bonus): untouched, unregressed. The "blank grey bar" card defect died with WO-852/883
  (a regression actively forbids the note band's return). What was missing was exactly REPAIR.

## What landed (13 files)

- Token `repair:<level>` joins the v33 grammar — NO schema bump; an old build reads it as Idle
  (NormalizeToken default, regression-pinned).
- `EchoRepairService` (new): rate-based consumer on the single harvest clock (offline catch-up
  capped 4h), most-damaged-first, spends REAL materials through WallRepairController's existing
  pricing/spend seams (never conjures hitpoints; broke = loud "waiting for materials"), destroyed
  structures excluded (WO-753 rules inherited), battle-time accrues nothing.
- Rate math lives in `EchoBonusCalculator.RepairFractionsPerSecond` (single math source; NO affinity
  term — the WO-830 "Repairs removed" ruling is asserted by regression). Balance knob
  `repairFractionPerHour` (default 2.0) additive in code; `echoes-balance.json` authoring left for
  the owner's tuning pass.
- Card: "Repair structures" chip (6th task row, MinTouch law), text-marked selection, honest status
  lines incl. "nothing to repair right now".
- Coverage: `EchoSpecializationRegression` grammar+math groups, `EchoResourcePickerRegression`
  Group 6, `EchoRepairTaskTests` (5 EditMode locks) — all green in the 2026-08-10 11:38 run.

## Not proven headlessly

The live damaged-structure fixture (the tick path is proven to the seam; the internals are the same
code RepairAll exercises) — owner felt-check: assign an Echo to Repair with a damaged wall and watch
it mend + charge materials. The 6-row card render needs a `UI_CAPTURE` PNG pass (queued with the
next capture run).
