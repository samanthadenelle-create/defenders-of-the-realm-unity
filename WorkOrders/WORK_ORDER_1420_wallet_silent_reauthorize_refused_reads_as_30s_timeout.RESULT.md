# WO-1420 RESULT - a refused reauthorize now reports REFUSED, not a 30 s timeout

**Status:** FIXED - ON THE SEEKER `2026.09.07.358574` (installed 2026-09-06 19:20). Awaiting the owner's
felt-verify; nothing player-visible changes on the happy path.
**Commit:** `32659c0f6` (2026-09-06 16:51), bundled under a `feat(manage,build)` title; the WO Status was not
flipped in that commit and this RESULT closes the gap.
**Files:** `Assets/_Modules/Wallet/WalletService.cs:525-556` (the elapsed-time branch in the `TimeoutException`
catch: a refusal that returns in well under the 30 s budget is logged as REFUSED; only a catch at the budget is
TIMED-OUT), `Assets/_Modules/Wallet/TargetedLocalAssociationScenario.cs`, suite
`Assets/Editor/Regression/WalletConnectFailureAttributionRegression.cs` (new, 151 lines).
**Gates on fresh logs postdating the commit:** `COMPILE_GATE_OK` (18:48), `REGRESSION_OK 414/414` (18:50).

## Reconciliation with WO-1441 (read-only diagnosis, 2026-09-06)

Two distinct defects, not one. WO-1420 is a misattribution during a REFUSED reauthorize (the 00:49 boot,
F8 seq 4683). In the 12:50 boot on the same device the reauthorize SUCCEEDED (`Connect OK - CHKK...sfkC`,
2855.8 ms, `logs/debug/raid-no-abilities-2026-09-06.log:3055`) and the session was STILL never minted - that
is WO-1441, downstream of connect and independent of it.

Correction to this WO's §3.2: `MWA wallet closed its one-shot association endpoint` appears four times
(12:50:04.742 -> 06.256) on the SUCCESS path, so that line alone is not a refusal marker; the attribution
must key on elapsed time, which is what HEAD does.

## Acceptance
- [x] A sub-second refusal is reported as REFUSED with the elapsed time, pinned by the new suite.
- [x] `REGRESSION_OK 414/414`.
- [ ] Owner felt-verify on 358574 (a refused wallet sheet at boot should read REFUSED in the device log).
