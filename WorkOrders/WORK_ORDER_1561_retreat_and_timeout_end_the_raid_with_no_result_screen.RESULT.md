# WO-1561 RESULT - retreat and clock-expiry now REPORT

**Status:** IMPLEMENTED - 2026-09-06 uncommitted, awaiting gate. Edit-only lane; no Unity run, no commit.
All line numbers below were re-read at source after the last edit (CLAUDE.md 11B).

## WHAT LANDED

1. **`EndStateVM.FromRaidRetreat`** - `Assets/_Modules/Village/UI/EndState/EndStateVM.cs:542`
   (`RewardShortSentence` `:499`). Same template as the victory (`EndStateKind.Defeat` emblem/tag):
   razed %, stars, clock, spoils rows, wounded count. **No new screen class.**
2. **`RaidDeployController.DoRetreat(string reason)`** -
   `Assets/_Modules/Village/Troops/RaidDeployController.cs:815`. Settle -> reconcile -> rally clear ->
   save -> **show**. `ShowNonVictoryResult` `:850` (latched `:865`), `RetreatReturnHome` `:904` (the
   latched route the screen's primary action owns), guard duration `:922`. `OnRaidTimeExpired` `:423`
   passes `TimeoutReason`: same settlement, same screen, its own title and lead sentence.
3. **Spoils are MEASURED, not requested** - `GrantRetreatLoot` `:998` is now an instance method taking
   the wallet before/after either side of `eco.Grant`, exactly like `RaidVictoryController.GrantLoot`.
   `LogRetreatCredit` `:1059` reports credited/requested per axis and **Warns** on a shortfall;
   `_retreatRewardShort` puts the caveat on screen in words (acceptance 4).
4. **Wounded count** - captured where it is already computed, `ReconcileRaidEnd` `:1235`.
   Unreconciled = -1 and the line is omitted rather than printing an unprovable zero.

## THREE TRAPS THIS LANE FOUND AND CLOSED

- **The watchdog would have become the new un-stoppable timer.** WO-1543 makes both raid screens hold
  indefinitely on touch, so **no fixed grace can be "comfortably longer"** and raising the number only
  moves the yank. The countdown is now suspended while `EndStateView.IsShowing` (`:362`); the
  `SettledExitGraceSeconds` doc (`:270`) records that the retired "longer than 12s" coupling is
  deliberately NOT replaced by a bigger number. **Tighter, not looser:** the net fires 30 s after the
  last end state CLOSED with the player still in a raid scene - exactly "the route home was eaten".
- **A parameterless `DoRetreat()` forwarder was written and removed** (`:774`): `RaidExitParityRegression`
  PIN 1 locates the method by signature, so the forwarder would have handed the oracle an empty body
  and silently disarmed every exit-parity assertion. The suite's pattern follows the new signature.
- **`ShowNonVictoryResult` is latched** (`:865`). Both settle calls are already idempotent, but a
  second `EndStateView.Show` destroys the standing panel with a `destroyed WITHOUT firing its primary
  action` **Warn** - F8-captured, i.e. manufactured triage noise from a non-bug.

## ORACLES - no `DataRegression.cs` edit; both suites already registered

- **Fixture:** `RaidPayoutVisibilityRegression.CaseG_NonVictoryExitsReportToo` (`:150`) builds the real
  VM and asserts razed %, stars, clock, three banked spoil rows, `2 troops return wounded`,
  `Every troop came home` at zero losses, no rows on a zero payout, the bank-short sentence, both exit
  titles, not-a-Victory, and that the guard survives.
- **Source-lint:** `RaidExitParityRegression` PIN 1 (`:133`, `:146`, `:161`) FAILS if `DoRetreat`
  routes home with no end state, calls `SceneRouter.GoCastle` directly again, or if
  `ShowNonVictoryResult` feeds anything but `_retreatCredited`.

## OWED - this lane cannot do it

- `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n>` on **fresh** logs, judged by the marker.
- **RED-before-green is owed, not claimed.** Case G cannot compile against the pre-change tree
  (`FromRaidRetreat` did not exist) - the honest red, recorded as a claim to prove, not a run.
- A **fresh capture of the retreat result screen**; no post-raid result PNG exists in the repo.
- Owner felt-verify + close.

## SCOPE / RISK NOTES

- **WO-1526 is NOT in the tree** (`READY`; `RaidScoring.RaidDeathEndsRaid` still `true`). Hero death
  untouched - `HeroHealth` is not in this silo. Stars and razed % are parameters, so a 2-star cap
  lands with no change here.
- **For the CLI:** hold-on-touch + 30 s lets a raid screen sit for minutes while the world keeps
  simulating at `timeScale=1` (measured in the watchdog comment). That widens the WO-1437 window -
  hero death behind a held screen replaces it. `HoldWorld` on the raid templates is the candidate but
  touches `WorldHoldLivenessRegression` and needs its own ruling.
- This ticket's "WO-1543 is blocked" line is **superseded** - it was ruled the same day and landed in
  this batch; both screens share one timing rule.
