# WO-1496 RESULT - the seven orphan suites are registered, the fleet judges markers, and the silent skip declares itself

**Status:** FIXED in the working tree. All three halves landed.
**Commit:** uncommitted in the working tree as of 2026-09-06 21:00, awaiting the wave-two gate.
**Files:**
- `Assets/Editor/Regression/DataRegression.cs:1707-1738` - the seven suites that existed in the tree
  and were registered nowhere are now registered under `Guard.Try`: `RepairProbeRegression` (`:1707`),
  `CombatFoundationRegression` (`:1708`), `ArenaCombatOracle` (`:1709`),
  `GearAddressableGroupRegression` (`:1716`), `AssetMoveManifestRegression` (`:1722`),
  `EnemyArtCoverageRegression` (`:1734`), `BlankStartCensusRegression` (`:1738`).
- `Assets/Editor/Regression/DataRegression.cs:1710-1716` - the `GearAddressableGroup` duplicate is
  REPORTED, not collapsed: the inline `CheckGearAddressableGroup` (`:233`, defined at `:4366`) asserts
  the same rule on the same asset as the registered oracle. Two authorities on one rule is the finding;
  the comment records it rather than silently picking one.
- `Assets/Editor/Regression/RegressionMarkerRegression.cs:75,597` - the enumeration check. An oracle
  absent from `DataRegression.RunAll` is a file that never runs, and the suite now says so by name.
- `run-autopilot-fleet.ps1:167-238` - the fleet no longer asserts FILE EXISTENCE and no longer judges
  by exit code. `$markerMissing` and `$abortedRuns` are counted per instance (`:186-217`) and the run
  passes only when both are zero (`:235`); otherwise `FLEET_RUNS_FAIL` (`:238`). `:251` stamps the
  start time so the emitter's marker is judged on a FRESH log, and `:271-279` emits `FLEET_EMIT_FAIL`
  when `AUTOPILOT_TICKETS_OK` is absent from a fresh log - marker absence on a fresh log is a FAILURE,
  not an unknown.
- `Assets/Editor/Regression/DestroyedStructureRegression.cs:214-219` - the batchmode early return now
  emits a DECLARED `RegressionOutcome.PartialSkip` instead of a prose line, so it can no longer count
  toward the green fraction while testing nothing. `:60` routes the note out on the reason string.

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes).
`Builds/reg-quiet.log` (20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites
green, 0 skipped)` - NOT `REGRESSION_OK`. The two reds were a UI-MVVM conformance violation on
`Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs:252-253` and a hollow-pass marker at
`Assets/Editor/Regression/NightMarketNoWalletRegression.cs:761`; both were fixed at source and committed
in `eb161dc98` (20:10), i.e. AFTER both logs. Neither gate log postdates `eb161dc98` or the current
working tree, so the wave-two gate is owed.

## Acceptance

- [x] Zero suite files unregistered - the seven named suites read at `DataRegression.cs:1707-1738`,
      with the enumeration check at `RegressionMarkerRegression.cs:597`.
- [x] The fleet refuses on a missing marker - `run-autopilot-fleet.ps1:271-279`. The proof by deleting
      a marker line from a log is NOT stated.
- [x] Skips reported separately - `DestroyedStructureRegression.cs:219` emits `PartialSkip`, and the
      marker line already carries a skip term (`reg-quiet.log` at 20:07 printed `0 skipped`).
- [ ] `REGRESSION_OK n/n` on a fresh log with the NEW n stated. NOT PRODUCED. The last run counted 419
      registered suites, and that count PREDATES these seven registrations, so the new n is unknown
      until the wave-two gate runs.

Needs no device capture. It needs the wave-two regression gate to produce the new suite count, one
fleet run proving the marker refusal, and a ruling on the reported `GearAddressableGroup` duplicate.
