# WO-1508 RESULT - the probe is rate-limited and the three drop paths are named; the mask is unchanged

**Status:** PARTIALLY IMPLEMENTED IN THE TREE, NOT GATED. All four acceptance items are open; one (the layer
mask) was simply not done.
**Commit:** none - uncommitted in the working tree as of 2026-09-06 21:00, awaiting the wave-two gate.
Landed together with the WO-1450 log throttle, as section 3 of the ticket required.
**Files:** the ticket names `Assets/_Modules/Village/AI/Enemy.cs`; **that path does not exist**. The file is
`Assets/_Modules/Village/Enemies/Enemy.cs`, and that is what was edited.
- `Enemy.cs:278-279` - `ProbeIntervalSeconds = 0.25f` and `_nextProbeAt` (new cadence gate fields);
  `:1866-1871` is the gate itself, consulted ONLY where `_currentTarget` is already null, so a skipped frame
  takes the same `_attackCooldown = 0f; return;` path a failed probe already took.
- `Enemy.cs:287` + `:1894-1896` - `_lastProbeTargetId`, so the acquire trace fires on a target CHANGE.
- `Enemy.cs:1823-1827` (path=not-alive), `:1849-1854` (path=out-of-reach, prints the measured distance) and
  the pooled-reset site - permanent throttled `FlowTrace` lines naming WHICH release fired.
- `Assets/Editor/Regression/EnemyProbeCadenceRegression.cs` (new, untracked), registered at
  `DataRegression.cs:1675`. Its header declares it a SOURCE LINT that cannot prove line counts or frame cost.

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes). `Builds/reg-quiet.log`
(20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites green, 0 skipped)` - NOT
`REGRESSION_OK`. The two reds (UI-MVVM violation on `BuildPreviewModal.cs:252-253`; hollow-pass at
`NightMarketNoWalletRegression.cs:761`) were fixed at source in `eb161dc98` (20:10), AFTER both logs. Neither
log postdates `eb161dc98` or the working tree, so the wave-two gate is owed. Measured:
`grep -c enemy-probe-cadence Builds/reg-quiet.log` returns **0** - the new suite has never executed.

## What landed, and what did not

The cadence gate caps retries at 4/sec/enemy instead of 60, which addresses the measured cost. The three
`_currentTarget = null` sites now each carry a distinct throttled trace, which is what the next capture needs.
**The layer mask was not touched.** `mask = ~0` is still live at `Enemy.cs:2536` (forward SphereCast) and
`:2598` (`OverlapSphereNonAlloc`); the diagnostic at `:2476` still reads `mask=~0(all layers)`.

## Acceptance

- [ ] The firing null path NAMED from a captured trace line - **open**. The instrumentation was added, but no
      post-fix capture exists, so no line can be quoted. The ticket required the trace BEFORE the edit
      (CLAUDE.md section 12); the edit landed first.
- [ ] Probe rate and `fps` measured before and after - **open**. Only the pre-fix figures exist (6,479 hit
      lines vs 194 throttled entries, `fps=11` at 13 enemies).
- [ ] `mask` narrowed from `~0` - **NOT DONE**. Still `~0` at `Enemy.cs:2536` and `:2598`, read at source.
- [ ] `REGRESSION_OK n/n` on a fresh log - **not run** (see the gates line).

**Still needs a device capture:** a post-fix raid on `RaidBase_raider_camp_small` with the same enemy count,
to quote which of the three drop paths fires and to compare probe rate and `fps` against the captured
`fps=11`. Without it this ticket cannot close, and the mask remains an outstanding edit.
