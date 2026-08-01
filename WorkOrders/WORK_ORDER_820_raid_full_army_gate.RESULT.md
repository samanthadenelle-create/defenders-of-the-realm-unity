# RESULT — WO-820 Raids full-army gate + over-queue fix

**Shipped:** 2026-08-01, commit `db963472`.
**Gates:** COMPILE_GATE_OK (wave3b) + REGRESSION_OK (wave3b) — markers postdate every edit (one
compile red caught pre-commit: BuildJobData struct null-compare, fixed).

## What shipped
- Rule: Ready = deployable slots + queued Train-channel slots >= MaxArmySize (wounded excluded,
  queued counts). Headless/no-GameState always Ready (AutoPilot never false-blocks).
- Core seam: RaidEntryGate.RaidArmyStatus published 1 Hz from BuildTimerService (version-on-change).
- HUD: Raids button dims to ElarionUi.Disabled when not Ready, STAYS tappable; tap toasts
  "Your army is not full..." and opens the drillmaster training panel (ShowTrainingUI).
- Over-queue exploit closed: EnqueueTraining counts committed Train-channel slots; refused units are
  neither charged nor enqueued; CommittedTrainingSlots() is the shared authority.

## Post-commit hardening (WO-823 Phase A — same day)
Readiness math consolidated into `ArmyReadiness.Compute` (single source); Publish + Open rewired to
it, first-hub-frame publish added (kills the bright-button flash window). 820 behavior unchanged.

## PO felt-verify still open
- [ ] Empty army: Raids dim; tap opens training with the toast.
- [ ] Queue a full army: Raids un-dims while the timer runs.
- [ ] Post-raid wounded: re-dims until recovery/retrain — felt-check the fairness (review flag);
      if harsh, WO-823 Phase E (FirstRaidMinDeployableSlots soft gate) is specced but NOT built.
- [ ] Enqueue 20 at cap 10: exactly 10 land, wallet charged for 10.
