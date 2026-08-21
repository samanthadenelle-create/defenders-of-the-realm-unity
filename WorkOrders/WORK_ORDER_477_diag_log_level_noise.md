**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

# WORK_ORDER_477 — Stop diagnostics logging at ERROR level (dev-tools "errors" + log spam)

**Status: READY TO IMPLEMENT** (held until editor closed) · F8 tickets #3 + #7.
**Type:** EXISTING (log-level misuse) · **Silo:** Diagnostics (tiny, code-only)

## Root cause (RCA agent, code-proven) — ONE cause behind both tickets
`FlowTrace.Fail` → `Debug.LogError` (FlowTrace.cs:127/378), and `BreakCaptureHarness.OnLog` records **only**
Error/Exception/Assert (BreakCaptureHarness.cs:183). So any `Fail` used for a *diagnostic dump* shows up as a
console error AND a break-log "ticket." Two systems mis-use `Fail` for non-failures:

- **DevTapDiag** — `DialogueService.cs:177`: `ReleaseOrphanedAdvanceInput()` ends with `FlowTrace.Fail("DevTapDiag", "...disabled 0...")` — a pure state dump, fired on EVERY dialogue-complete / walk-away (wired at :362, also :150), even when it disabled 0 actions. (Sibling `CompanionDialoguePresenter.ReleaseYarnInputCapture` logs the same via `Step` — inconsistent.)
- **FloorDiag** — `MagentaGuard.cs`: `Sweep()` (boot + every sceneLoaded, :67-72) emits ERROR lines via `FlowTrace.Fail("FloorDiag", …)` at :223 (terrain count), :244 (terrain dump), :250 (recovery), :256 (LIGHTING), :268 (up to 12 GROUND dumps). Several sweeps per load → dozens of fake "errors."

**Ticket #3 ("errors clicking dev tools") is NOT an input block** — proven: DevBootstrap.cs:76-79 already sets
`panelSettings.sortingOrder=9000` (the shipped fix), and `DevPanelController` only errors if an invoked action
throws (:816); the open/click path throws nothing. What the owner saw was the DevTapDiag/FloorDiag ERROR noise
coinciding with their clicks. Same root as #7.

## Fix (downgrade non-failures; keep real failures as Fail)
- `DialogueService.cs:177` — `Fail` → `Step` (it's a no-op state dump).
- `MagentaGuard.cs` :223, :244, :256, :268 — `Fail` → `Step` (pure inventories); :250 (recovery) → `Warn`.
- KEEP `MagentaGuard.cs:279` (the sweep-threw catch) as `Fail` — that's a genuine failure.
- Since the pink-floor RCA is closed (commit 554e2a98), the FloorDiag dump block MAY be disabled/removed outright instead — owner's call (leave the FLOOR-FIX repaint Step in MagentaGuard, only quiet the diagnostic dumps).

## Acceptance
A normal MainCastle_Hall session + dev-tools open produces **no ERROR-level lines** from DevTapDiag/FloorDiag in
the console or break-log; real failures still log as errors; dev panel still opens/clicks (was never blocked).

## NOT touch
The actual input-release behavior (only its log level); the FLOOR-FIX repaint Step; genuine `Fail` catches.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
