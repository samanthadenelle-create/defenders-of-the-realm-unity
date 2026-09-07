# WO-1493 RESULT - SESSION_GUARDS_OK is a stage now, and its fraction is measured instead of printed

**Status:** FIXED in the working tree. All three halves landed: the gate stage, the derived count, and
the oracle rule that stops the next literal.
**Commit:** uncommitted in the working tree as of 2026-09-06 21:00, awaiting the wave-two gate.
**Files:**
- `Assets/Editor/Regression/SessionRegression.cs:117` - `log.AppendLine($"SESSION_GUARDS_OK
  {passed}/{total} checks")`. Both halves are now variables. The header at `:15-18` records what it
  used to be: the string literal `"SESSION_GUARDS_OK 6/6 checks"`, a LABEL and not a MEASUREMENT, which
  would have printed 6/6 whether six checks ran, one ran, or none did.
- `tools/regression/checkin_gate.ps1:19-20,31-33,380,390-401` - stage 5 of 9, `SESSION GUARDS`. It
  invokes `DeNelle.Editor.SessionRegression.RunAll` through `run-unity-method.ps1` with
  `-ExpectMarker 'SESSION_GUARDS_OK'` at `:396`, then greps the SHAPED marker
  `SESSION_GUARDS_OK <p>/<n> checks` at `:401` - a bare token does not satisfy it.
- `Assets/Editor/Regression/RegressionMarkerRegression.cs:684-725,752` - RULE 6, the audit-G8 case: a
  marker whose count fraction is a source literal FAILS. Its own summary line reports
  `0 hardcoded fractions`. The note at `:1050-1051` records that G8 is now held closed by this rule
  rather than by a one-off check.

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes).
`Builds/reg-quiet.log` (20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites
green, 0 skipped)` - NOT `REGRESSION_OK`. The two reds were a UI-MVVM conformance violation on
`Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs:252-253` and a hollow-pass marker at
`Assets/Editor/Regression/NightMarketNoWalletRegression.cs:761`; both were fixed at source and committed
in `eb161dc98` (20:10), i.e. AFTER both logs. Neither gate log postdates `eb161dc98` or the current
working tree, so the wave-two gate is owed.

## What landed

The ticket's finding was that one of the three distinct gate markers CLAUDE.md section 8 established
had never fired anywhere - no stage in any chain invoked `SessionRegression.RunAll`. The suite was not
deleted; the defect was that nobody called it. Stage 5 judges by the marker on a fresh log, never by
exit code, which is the `gates-report-success-without-proving-it` rule this repo keeps re-learning.

## Acceptance

- [ ] `SESSION_GUARDS_OK <n>/<n> checks` on a FRESH log, with n derived, and the log mtime pasted. NOT
      PRODUCED. `checkin_gate.ps1` has not been run since the change, so the marker still appears in
      zero logs and the suite has still never executed. This is the acceptance that matters and it is
      open.
- [x] The gate stage exists - `checkin_gate.ps1:390-401`. That the gate goes RED when a session guard
      fails is unproven; no deliberate failure was injected.
- [ ] `REGRESSION_OK n/n` on a fresh log. Owed with the wave-two gate.

Needs no device capture. It needs one `tools/regression/checkin_gate.ps1` run so stage 5 fires for the
first time, and the emitted fraction pasted back here.
