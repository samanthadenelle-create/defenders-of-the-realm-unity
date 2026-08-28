# WORK ORDER 1260 — 'pause-menu' WorldHold leaks when the app is backgrounded

**Status:** FIXED 2026-08-28 — foreground-age accounting implemented and regression-proven; owner device felt-check remains before CLOSE.
**Minted:** 2026-08-28 (CLI, F8 device triage seq 3627 + 3631)
**Silo:** Core/UI
**Evidence (captured):** device `SM02G4061955851`, `Main_Castle_Overworld` — TWICE in one evening:
- seq 3627 (20:22Z): `[Flow:Pause] ⛔ STUCK WORLD HOLD: 'pause-menu' outstanding for 506s (limit
  180s). Its owner never disposed it — the most likely cause is the app being backgrounded
  mid-flight and an await that never resumed. Force-releasing…` (stack: `WorldHold.WatchdogTick`)
- seq 3631 (later, session t=302s): same message at 302s.
Captures: `logs/f8-inbox/capture-device-20260828-131839-seq3627.md`, `...131840-seq3631.md`.
Related symptom: seq 3624 (18:14Z) classified SOFTLOCK — no movement for 180s with input=True,
worldLive=True; consistent with the world frozen under an undisposed hold before the watchdog fired.

## RCA direction (instrument first, §12)
The watchdog's own hypothesis is credible and repeated: the pause-menu flow acquires
`WorldHold` and its disposal lives past an `await` that never resumes after
`OnApplicationPause(true/false)` on Android. Find the pause-menu acquire site, trace its dispose
path across a background/foreground cycle (FlowTrace Enter/Exit around the await), and prove the
dead step on device or in the AutoPilot fleet with a simulated pause cycle before editing.
Likely fix shape: tie the hold's lifetime to the menu's visible lifecycle (dispose in OnDisable /
OnApplicationPause) rather than a linear await chain — one owner, deterministic release.

## Acceptance
1. Background the app with the pause menu open, return after >60s: world resumes the moment the
   menu closes; no STUCK WORLD HOLD line ever fires.
2. The watchdog force-release stays (it is the net, not the fix).
3. Regression guarding the acquire/dispose pairing of the pause-menu hold.

## RCA + Result — 2026-08-28

The pause hold already had deterministic disposal in `Resume()` and `OnDestroy()`. The repeated
302s/506s failure was the watchdog aging holds with `Time.unscaledTime` across an Android OS
suspension. On the first resumed frame it treated the entire background interval as foreground
leak time and force-released a legitimate still-visible pause menu.

- `WorldHoldWatchdog.OnApplicationPause` now forwards suspension/resume to the clock owner.
- `WorldHold` rebases every outstanding handle by the measured suspended duration. The existing
  180-second foreground watchdog and force-release behavior remain intact.
- Driven regression simulates a 300-second suspension and proves the handle age shifts by exactly
  300 seconds; it also pins the real lifecycle callback wiring.
- `COMPILE_GATE_OK`.
- `WORLD_HOLD_OK`, including `[background-age]`, inside `REGRESSION_OK 315/315 suites`.
- Device close condition remains: background with Pause open for >60s, return, tap Resume, and
  confirm no `STUCK WORLD HOLD` line. That felt-check closes the ticket; it does not make the
  compiled/regression-tested fix READY again.
