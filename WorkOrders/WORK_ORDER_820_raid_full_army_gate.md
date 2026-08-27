# WORK ORDER 820 — Raids gated on full army (grey + drillmaster redirect) + over-queue fix

**Status:** CLOSED 2026-08-27 — owner felt-tested PASS on APK 2026.08.27.343739 (dungeon review).
**Owner ruling (verbatim):** "Raid should be greyed out unless a full army is ready queued otherwise
they should go to drillmaster to queue troops"
**Silo:** HUD/Raid/Troops

## The rule
**Ready = deployable slots + queued-training slots >= army cap** (`MaxArmySize` = 10 + perk bonuses).
- Wounded troops do NOT count (they cannot deploy); troops still in the training queue DO count
  (the owner's "or queued" clause — a player who just queued a full army is not punished for the timer).
- Headless/no-GameState always reads Ready (AutoPilot never false-blocks).

## What shipped
- `RaidEntryGate.RaidArmyStatus` {Ready, DeployableSlots, QueuedSlots, CapSlots, Version} — Core seam,
  published Village-side from `BuildTimerService.PublishStatus` (1 Hz, version bumps only on change).
- HUD Raids button: dims to `ElarionUi.Disabled` when not Ready but STAYS tappable; tap -> toast
  "Your army is not full. Visit the drillmaster to queue more troops." + opens the drillmaster
  training panel directly (`TroopDialogueCommands.ShowTrainingUI`). Authoritative check lives in
  `RaidSelectionScreen.Open()` (Village-side recompute — the HUD tint is presentation only).
- **Over-queue exploit fixed:** `BarracksService.EnqueueTraining` previously checked cap against the
  live roster only — 20+ units could be queued against cap 10 and all landed (grant-on-complete is
  unconditional by design). Now: roster slots + committed Train-channel slots + the unit must fit the
  cap; refused units are neither charged nor enqueued. New `CommittedTrainingSlots()` is the shared
  authority (enqueue check, status publisher).

## Acceptance criteria
- [ ] COMPILE_GATE_OK + REGRESSION_OK; existing troop/VM tests untouched and green.
- [ ] Empty army: Raids dimmed; tap opens the training panel with the toast.
- [ ] Queue a full army: Raids un-dims while troops still train (queued counts).
- [ ] Post-raid with wounded: Raids re-dims until recovery/retrain refills deployable slots (felt-check fairness — flagged risk).
- [ ] Enqueue 20 Footmen at cap 10 from empty: exactly 10 enqueue, wallet charged for 10 only, trace logs enqueued/requested.

## Do NOT touch
- `FeatureFlags.RaidContinuousWalk` path (OFF by WO-771 lock; its no-gate short-circuit is out of scope).
- Army recovery mechanics (`TickRecovery` / wounded model) — the gate reads them, never mutates.
