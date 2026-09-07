# WO-1469 RESULT - the tester path now carries the R2 content gate, and it is fresh-log judged

**Status:** FIXED. The fourth path to a device - the one that reaches TESTERS - no longer bypasses the
CLAUDE.md section 16 gate.
**Commit:** `4ec1a861d` (2026-09-06 20:11).
**Files:** `distribute-android.ps1:24` (`$startedAt = Get-Date`), `:65-99` (the gate block):
- `:76` delegates to `tools\r2-ship.ps1` - the ONE file, per the owner ruling 2026-08-20.
- `:83-85` `$parityFresh` asserts `(Get-Item Builds\r2-parity.log).LastWriteTime -ge $startedAt`.
- `:87` requires BOTH freshness and the `R2_PARITY_OK` marker; `:91-99` refuse with a distinct message
  for STALE vs MISSING-MARKER and `exit 3`.

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes).
`Builds/reg-quiet.log` (20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites
green, 0 skipped)` - NOT `REGRESSION_OK`. The two reds were a UI-MVVM conformance violation on
`Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs:252-253` and a hollow-pass marker at
`Assets/Editor/Regression/NightMarketNoWalletRegression.cs:761`; both were fixed at source and committed
in `eb161dc98` (20:10), i.e. AFTER both logs. Neither gate log postdates `eb161dc98` or the current
working tree, so the wave-two gate is owed. This is a PowerShell file: no Unity suite covers it, and
the commit body records `PARSE_OK` under PowerShell 5.1.

## What landed

The gate applies even with `-Build:$false`. That is the load-bearing half: bundle names are
content-hashed, so an APK built yesterday still resolves TODAY'S remote catalog and no previous push
can cover this release. The assertion SHAPE was copied from `google-play-aab-build.ps1`; the push and
verify commands were not, and stay hardcoded exactly once inside `tools/r2-ship.ps1`.

## Acceptance

- [x] `distribute-android.ps1` calls `tools/r2-ship.ps1` and asserts log freshness -
      `distribute-android.ps1:76` and `:83-87`.
- [ ] Proven by touching an old `r2-parity.log` and showing a REFUSE, plus a clean run succeeding.
      NOT RUN. The success half in particular is unproven, and memory
      `prove-the-success-path-not-just-the-refusal` is the reason that matters: a pin guard that
      aborted every good run once shipped while exiting 0.
- [x] No push/verify strings duplicated outside `r2-ship.ps1`. Measured this session: the only
      INVOCATIONS in the tree are `tools/r2-ship.ps1:138` (`--push ServerData`) and
      `tools/r2-ship.ps1:202` (`--verify-catalog "ServerData/$name"`). Every other hit is comment or
      usage text in `tools/r2_sync.py` and the warning comment at `distribute-android.ps1:72`.

Needs no device capture. It needs one live `distribute-android.ps1` run, plus the deliberate
stale-log refusal, before the acceptance can close.
