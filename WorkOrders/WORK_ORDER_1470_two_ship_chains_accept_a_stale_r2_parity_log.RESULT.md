# WO-1470 RESULT - both chains now require the proof to POSTDATE the bytes; the r2-ship early exit is still open

**Status:** FIXED at the two callers. The `r2-ship.ps1` early-exit residual is OPEN and named below.
**Commit:** `4ec1a861d` (2026-09-06 20:11). `b30e551ce` (20:12) later corrected this WO's own Status
line, which had contradicted itself; that pass printed `BOARD_CHECK_OK`.
**Files:**
- `overnight-apk-build.ps1:57` (`$startedAt`), `:113-126` - `$parityFresh` gate; a stale log now writes
  `R2_PARITY_STALE` to the status file, and `:131` no longer re-inlines the raw
  `python tools\r2_sync.py --push ServerData` remedy, pointing at `tools\r2-ship.ps1` instead.
- `morning-ship-chain.ps1:158` (`$r2StartedAt`), `:168-176` - freshness `Die ... 16` and a separate
  marker-absent `Die ... 16`. Before this the step read the marker with NO freshness check at all.

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes).
`Builds/reg-quiet.log` (20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites
green, 0 skipped)` - NOT `REGRESSION_OK`. The two reds were a UI-MVVM conformance violation on
`Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs:252-253` and a hollow-pass marker at
`Assets/Editor/Regression/NightMarketNoWalletRegression.cs:761`; both were fixed at source and committed
in `eb161dc98` (20:10), i.e. AFTER both logs. Neither gate log postdates `eb161dc98` or the current
working tree, so the wave-two gate is owed. Both scripts are PowerShell; the commit body records
`PARSE_OK` under PowerShell 5.1.

## What landed

The invariant is now enforced at both callers: the proof must postdate the bytes it claims to prove.
Marker-on-a-fresh-log was always the point of section 16 and neither chain had checked the fresh half.

## Acceptance

- [x] Both chains refuse on a stale log - `overnight-apk-build.ps1:117-122` and
      `morning-ship-chain.ps1:168-173`.
- [ ] Proven by touching the log back and running each. NOT RUN, neither the refusal nor the success
      path.
- [ ] `r2-ship.ps1` leaves no passing marker on any early exit. STILL OPEN, re-measured at HEAD:
      `tools/r2-ship.ps1:127-130` still prints `R2_SHIP_FAIL: tools\r2_sync.py not found` and
      `exit 16` BEFORE any deletion of `Builds/r2-parity.log`, so a bail leaves yesterday's
      `R2_PARITY_OK` on disk. The freshness assertions at the two callers neutralise it in practice;
      the file itself is unfixed. The only uncommitted change to `r2-ship.ps1` is the WO-1486 `-Prune`
      lane, which does not touch this path.
- [ ] Both doc lines corrected in the same commit. NOT DONE. `4ec1a861d` touched only the three `.ps1`
      files, and both lines still read the same at HEAD: `BATCH_STATE.md:351` ("`r2-ship.ps1:115`
      verifies ONE explicit target") and `CLI_LANES_WO_NUMBERS.md:20` (same clause). The log in fact
      verifies all three targets.

Needs no device capture. Owed: the two doc lines, the `r2-ship.ps1` early-exit ordering, and one live
run of each chain proving both the refusal and the success path.
