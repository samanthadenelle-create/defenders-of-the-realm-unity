# WO-1543 RESULT - hold on touch, longer guard

**Status:** IMPLEMENTED - 2026-09-06 uncommitted, awaiting gate. Edit-only lane; no Unity run, no commit.
Line numbers re-read at source after the last edit (CLAUDE.md 11B).

## WHAT LANDED

1. **`EndStateVM.HoldOnInteraction`** - `Assets/_Modules/Village/UI/EndState/EndStateVM.cs:135`
   (reasoning `:117-134`). Defaults **false**, and that default is the whole of acceptance 4.
   Opt-in sites: exactly two - `FromRaidVictory` (`:443`) and `FromRaidRetreat`.
2. **The guard learns to see the player** - `EndStateView.AutoDismissAfter`
   (`Assets/_Modules/Village/UI/EndState/EndStateView.cs:2670`). Flag false = the original two-line
   countdown, byte for byte. Flag true = accumulate `Time.unscaledDeltaTime`, **reset to zero on any
   interaction**, then fire. `InteractedThisFrame` `:2718` reads the new Input System only (mouse
   press/hold/scroll, touchscreen press) - no legacy `Input.*`.
3. **12s -> 30s** - `RaidVictoryController.cs:69`, with the reason in-code above it.
   `RaidDeployController.NonVictoryReturnSeconds = 30f` (`:922`) matches deliberately: **one timing
   rule for both raid screens**, this ticket's coordination note with WO-1561.

## CANCEL vs RESTART - RESTART, and why (acceptance 5)

**Restart.** A *cancel* means one stray tap pins the screen open forever, re-opening the exact
softlock the guard exists to prevent - this ticket's own section-3 warning. Restart keeps the backstop
alive while giving a reading player unlimited time: the safer reading of "hold on touch". Recorded
in-code at `EndStateVM.cs:123` so the next seat inherits the reasoning.

**Divergence flagged:** the dispatch brief said *"first touch cancels the auto-return"*. The ticket
itself leaves cancel-vs-restart to the implementer and asks for the reasoning to be recorded. Restart
was chosen; this is a deliberate, recorded choice, not drift.

## ACCEPTANCE 4 - TEMPLATES CHECKED, BY NAME

`FromBattleDefeat` (2.5s), `FromHeroDeath` (6s), `FromGameOver` (0s - its deliberate "Retry must be
chosen" opt-out), `FromOutpostVictory` (4s) and the wave-clear banner: **none sets
`HoldOnInteraction`, so none changed.** Case H asserts all four by name plus the `FromGameOver` zero.

## THE IMPLEMENTATION NOTES, ANSWERED

- **`:2771` "nothing in this file has ever stopped a coroutine"** - still true. Nothing is stopped; the
  countdown re-arms from inside itself, so no handle and no `StopCoroutine` was introduced.
- **`OnSceneLoaded` / `CloseFromArbiter` / `OnDestroy`** - untouched. A held screen whose world moves
  underneath it dies exactly as an unheld one does (the coroutine dies with the GameObject) and still
  says so through `AbandonedPrimaryWarn`.
- **Log flooding** - a held screen is touched every frame while a finger rests on it, so the re-arm
  line goes through `FlowTrace.Throttle` at `HoldTraceEverySeconds = 2f` (`EndStateView.cs:2662`);
  unthrottled it would evict the boot window out of the device logcat ring (memory
  `logcat-ring-buffer-destroys-evidence`).
- **The stranding watchdog** would have become the new un-stoppable timer. Closed inside WO-1561.

## ORACLE - no `DataRegression.cs` edit; suite already registered

`RaidPayoutVisibilityRegression.CaseH_RaidEndStatesHoldOnTouch`
(`Assets/Editor/Regression/RaidPayoutVisibilityRegression.cs:239`) covers **both** halves: the guard
still exists on both raid templates (`AutoDismissSeconds > 0`), the hold is set on both, and the four
sibling templates are unchanged.

## OWED

- `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n>` on fresh logs, judged by the marker.
- **RED-before-green owed, not claimed** - Case H cannot compile against the pre-change tree
  (`HoldOnInteraction` did not exist). That build failure is the honest red; it has not been run.
- A fresh capture of the victory screen; no post-raid result PNG exists in the repo.
- Owner felt-verify + close.

## FOR THE CLI - consequence surfaced, not solved

Hold + 30 s lets a raid screen sit for minutes while the world keeps simulating at `timeScale=1`
(measured in `RaidDeployController`'s watchdog comment). That widens the WO-1437 window: hero death
behind a held victory screen replaces it and the result is lost. `HoldWorld` on the raid templates is
the obvious candidate, but it touches `WorldHoldLivenessRegression` and is a separate ruling.
