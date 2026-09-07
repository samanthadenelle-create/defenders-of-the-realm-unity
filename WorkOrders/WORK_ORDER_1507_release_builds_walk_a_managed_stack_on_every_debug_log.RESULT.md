# WO-1507 RESULT - Log and Warning stack walks are off for the Android player; the perf delta is not yet measured

**Status:** LANDED. **Tree contradicts the ticket:** its Status line reads "IN PROGRESS - uncommitted", but the
change is COMMITTED. (Status line not edited here - RESULT-only lane.)
**Commit:** `eb161dc98` (2026-09-06 20:10:29 -0500), title tail "stack traces off for Log".
**Files:** `ProjectSettings/ProjectSettings.asset:59`; suite `Assets/Editor/Regression/StackTraceLogTypeRegression.cs`
(tracked, same wave).
**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (20:04) PREDATES the 20:10 commit, so it does not cover it.
The only later compile, `Builds/cg-aab.log` (20:54), is RED - 42x `CS0103`, first two
`ManageTroopsTrainDoorRegression.cs(247,17)` and `ManageProgressiveDisclosureRegression.cs(228,41)`, the Manage
lane's half-written suites, unrelated to this setting. No `REGRESSION_OK` postdates the commit.

## 1. What landed, read at source

`ProjectSettings.asset:59` now reads `m_StackTraceTypes: 010000000100000000000000000000000100000001000000` -
six ints in `LogType` order: `Error=1, Assert=1, Warning=0, Log=0, Exception=1, [6th]=1` (`1` = ScriptOnly,
`0` = None). Exactly sec.2's shape: the two high-volume types stop walking a managed stack, Error/Assert/Exception
keep theirs. Pre-fix was `01` x 6. Nothing under `Assets/` calls `SetStackTraceLogType`, so the shipped asset
value is the only authority.

`StackTraceLogTypeRegression.cs` pins it from the asset text - markers `STACKTRACE_LOGTYPE_OK`/`_FAIL` - and
states in its own header that no FlowTrace call is removed or silenced (CLAUDE.md sec.12: flag off, never strip).

## 2. Acceptance

- [x] Log and Warning at None for Android; Error and Exception unchanged. Value pasted above.
- [ ] Before/after `gc=` and `fps=` from the same raid - **NOT MEASURED**. The ticket's `gc=26MB` at `fps=11` is a
      pre-fix capture; no post-fix device raid exists in the tree.
- [ ] An exception still produces a usable stack on device - **NOT PROVEN**. Needs a forced throw on a build
      carrying this asset; the Seeker's tester APK `2026.09.07.358574` predates it.
- [ ] `REGRESSION_OK n/n` on a fresh log - owed by the wave-two gate.

## 3. Housekeeping finding

`StackTraceLogTypeRegression.cs.meta` is UNTRACKED while its `.cs` is committed (`git status --short` reports
`?? ...StackTraceLogTypeRegression.cs.meta`). The wave-two commit must sweep the meta or Unity mints a fresh GUID
on the next seat's import.

## 4. Owed

One post-fix Android build; one raid capture with `gc=`/`fps=` beside the pre-fix pair; one forced throw whose
stack is read back off the device log.
