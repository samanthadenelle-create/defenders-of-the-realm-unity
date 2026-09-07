# WO-1540 RESULT - 2026-09-07 (edit-only lane; uncommitted, awaiting gate)

## THE TICKET'S PREMISE IS FALSE. No suite leaks `ff.barracks`.
- `FeatureFlags.cs:1110` = `Get("barracks", defaultOn: **true**)` - WO-771 flipped it ON on
  2026-07-26 (comment at `:1107-1109`). `Get` (`:1373-1379`) returns that default when the key
  is absent, so the census failed on a PRISTINE PlayerPrefs, in every environment, every order.
- Grep over `Assets/Editor` for `ff.barracks`: **zero writers** (2026-09-07). The only `ff.*`
  setters in the folder - `ff.enemystructureaware` (DataRegression.cs:2229), `ff.regionroam` /
  `ff.raidwalk` (OverworldCombatGateRegression.cs:62-92), `ff.mergedworld`
  (SceneRoutingRegression.cs:102-129) - all restore in a `finally`.
- Acceptance 1 ("name WHICH suite"): the answer is **none**, with the file:line above.
  Acceptance 3 ("census after the setter suite") is unrunnable: there is no setter.

## The real defect: the oracle asked the wrong thing, twice.
`BlankStartCensusRegression.cs:195-203` asserted a FLAG, with stale "default OFF" prose, while
`FindInScene` (`:364-370`) includes INACTIVE objects - so a correctly-suppressed twin still
entered the branch. `Builds/reg-wave3h.log:9366-9367` shows the SAME run suppressing it
(`maySurface=False ... -> Suppressed`) while `:13854` reported it as an EXTRA structure.

## Landed (neither forbidden move: flag not pinned OFF, default unchanged)
| What | Where |
|---|---|
| Section 3 now asks the WO-834 authority `StructureSingleton.MayBakedTwinSurface("barracks", ...)` against the census fixture | `Assets/Editor/Regression/BlankStartCensusRegression.cs:199-239` (+ header bullet `:30-34`) |
| Flag snapshot/restore/diff helper; key set REGEXED from `FeatureFlags.cs`, never listed | `Assets/Editor/Regression/FeatureFlagSnapshot.cs` (new) |
| Capture before the START fence | `Assets/Editor/Regression/DataRegression.cs:306-320` |
| Restore + DIFF after the END fence (red names the drifted key; green logs a non-`[` line so it is not miscounted as a suite) | `Assets/Editor/Regression/DataRegression.cs:1777-1786` |
| Pin: a dummy suite leaks `ff.petcombat`; oracle asserts the drift is NAMED and the next reader sees the compiled default | `Assets/Editor/Regression/FeatureFlagSnapshotRegression.cs` (new), registered at `DataRegression.cs:1759` |

## Honest limit (say it, do not imply otherwise)
This DETECTS and RESTORES flag drift across the fence; it does **not** isolate suite N from N-1.
`RunAll` between the fences is ~200 flat `if (!X.Run(out var r))` lines - **there is no loop**, so
per-suite wrapping means editing every registration line: the lead's call, not this lane's. Worth
having at fence level anyway - batchmode PlayerPrefs is the Windows registry, so a leaked key
otherwise survives into the owner's next editor session.

## Verification
- Braces balanced 4/4; NUL scan clean; new files ASCII (pre-existing non-ASCII untouched).
- Suite count consistent: the new registration matches `RegressionMarkerRegression`'s in-fence `.Run(out ...)` regex (+1 expected, +1 tag line).
- **NOT run**: no Unity in this lane. `REGRESSION_OK n/n` on a fresh log is the lead's gate.
