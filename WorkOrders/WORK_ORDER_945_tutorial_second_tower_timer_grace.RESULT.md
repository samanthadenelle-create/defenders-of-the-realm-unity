# RESULT — WO-945: onboarding build grace — every not-yet-Onboarded build gets the 5s grace

**Verified:** 2026-08-10 (CLI orchestrator; implementing agent + gates)

- `BuildTimerService`: typed `BuildGraceReason` (None/FirstBuild/Onboarding) replaces the bare bool
  through a new overload (old overloads intact — `ObsidianQueueRegression` typed-lookup pin safe);
  pure `GraceAdjustedDurationMs` seam (build-only, only-ever-shortens). Distinct trace:
  `ONBOARDING grace on '<id>' ... (not-yet-onboarded rule, WO-945)`.
- `BuildModeController`: `GraceReasonFor(firstEverBuild, notYetOnboarded, isPallet)` — pallet
  carve-out beats both rules in both states; `!state.Onboarded` reuses the exact wave-gate flag read.
- Regression: `BuildEconomyRegression` §9b `[grace]` — GREEN in the 2026-08-10 11:38 run
  ("decision + duration + carve-out + only-shortens; grace 5s vs tier1 90s").
- Fix (b) (gating the teaching wave on construction COMPLETION) deliberately NOT taken — step-signal
  surgery on the WO-1012 P3 beats; with (a), exposure is ≤5s. Assessment with file:line cites in the
  agent report (tutorial-steps.json:81-98, TutorialFlow.cs:163).
- Proof of the felt defect: Player.log 51090 (tower #1 graced 90s→5s) vs 55450 (tower #2 full 90s) —
  now impossible while !Onboarded. Owner felt-verify: rerun the tutorial; both towers build in 5s.
