# WO-602 — P1: no way back into town (return crossing invisible/broken)

**Status:** READY TO IMPLEMENT — P1, blocks the core loop (venture out → CANNOT return home)
**Origin:** owner felt-test 2026-07-03 ~01:15: "I cannot get back to town. There is no way to get
back in to test build." She exited the castle, played the world, and found no discoverable or
functioning re-entry.

## Suspects (verify from capture, §12)
1. RuntimeRegionGate threshold triggers run `suppressPrompt=true` (audit: the "invisible seam") —
   from OUTSIDE there is no affordance at all: no prompt, no glow, no sign saying "this is the door."
2. The return warp itself may not fire from the outside approach (lift/threshold geometry, trigger
   radius, or one-directional wiring) — instrument and capture an outside→in attempt headless.
3. The south bridge/ramps physically lead to the gates, but nothing communicates enterability.

## Required outcome
- **Function:** walking up any of the four gates from outside RELIABLY warps the hero into the
  courtyard (symmetric with the inside→out crossing).
- **Affordance (owner canon — no invisible triggers):** the re-entry reads from a distance: the
  gate arch glows/signs with the shared interaction affordance from the OUTSIDE face too; the
  same treatment as other world targets (InteractableSign / attention glow family).
- **Oracle:** extend the fleet exit phase to a ROUND TRIP — exit castle → walk away → return →
  assert hero back on the courtyard navmesh (y≈liftY) within a bound; FlowTrace.Fail
  "HOME_RETURN_FAIL" otherwise. Exit-only coverage is how this shipped unfelt.

## Acceptance
- [ ] Round-trip fleet oracle green on all 4 gates
- [ ] Outside affordance visible in a headed screenshot
- [ ] Owner felt-verify: leave town, wander, come home without instructions
