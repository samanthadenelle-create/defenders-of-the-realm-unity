# WO-1178 - A raw `Unity.exe` invocation bypasses the editor pin and silently costs a full rebuild

**Status:** READY — ⚠ **code HANDED BACK 2026-08-24 and AT LEAD REVIEW; NOT committed, NOT landed.** **Silo:** Tooling/gates. ⛔ Its diff touches `tools/run-unity-method.ps1` and an APK chain that calls it is executing, so the merge is fenced on that chain finishing. *(Status audit 2026-08-24: no commit exists for this ticket — verified against `git log`; body unchanged.)*
**Found:** 2026-08-24, while gating PROD-014. Cost: one wasted gate run plus a forced full recompile.

## What happened

`ProjectSettings/ProjectVersion.txt` was found rewritten **BACKWARDS**, `6000.4.8f1` -> `6000.4.7f1`.
Both editors are installed. Nothing in the session had asked for 4.7.

Restoring it to HEAD's 4.8 and running the compile gate produced a log whose **last line** was:

```
Clearing Bee directory 'Library/Bee', since bee backend hash is different,
previous hash was ... (Unity version: 6000.4.7f1),
current hash is ... (Unity version: 6000.4.8f1).
```

⚠ **The gate run spent itself on the forced full rebuild and quit before `CompileGate.Run` ever
executed.** So the log carried **no `COMPILE_GATE_OK`** - while the process **exited 0**.

⭐ **That is the canonical trap, and it fired THREE TIMES in ten minutes.** The re-run then hit my own
guard (`UNITY RUNNING - ABORT`, because the first Unity was still finishing) - and **that refusal
also exited 0**. Memory `gates-report-success-without-proving-it`: judge the MARKER on a FRESH log,
never the exit code. Two different non-runs both reported success.

**And the third:** the wrapper that finally DID run the gate printed **`NO LOG`** and an empty
`unity-exit=` - while the gate had in fact **PASSED**, marker and all, on a log written one second
later. Its `Test-Path` raced the file. ⛔ So today, three separate runners each returned a verdict
**unrelated to what actually happened**: two false greens and one false red. A false red is the
better failure only because someone looks; had I trusted it, correct work would have been re-run or
discarded.

## The actual hole

**All six repo scripts correctly pin `6000.4.8f1`** - verified:
`build-webgl-isolated.ps1`, `build-webgl.ps1`, `build-windows.ps1`, `run-tests.ps1`,
`run-unity-method.ps1`, `tools/run-unity-playmode.ps1` (and `tools/regression/checkin_gate.ps1`
pins it in `Get-UnityExe`).

⛔ **So the downgrade came from something invoking `Unity.exe` by full path, bypassing every one of
them.** ⚠ **This is the same shape as §16's raw `adb install`:** the gate lives in the scripts, and
anything that reaches the tool directly is outside it. A seat or agent picking an editor path by hand
is all it takes - and nothing warns, because opening the project under a different editor is a
perfectly legal thing to do.

## Proposed fix

1. ⭐ **A pre-run assert that costs nothing:** any script that starts Unity first checks that
   `ProjectVersion.txt` names the pinned version, and **FAILS LOUDLY** if not - naming both versions
   and the fact that proceeding will force a full Bee rebuild. Cheap, and it converts a silent
   ten-minute tax into one line.
2. ⚠ **Make a marker-less gate log an explicit FAILURE in the runner itself**, not something the
   reader has to notice. The runner should print the missing-marker verdict rather than returning a
   bare 0 - today every caller is expected to remember, and today two callers did not.
3. Consider `git`-tracking a guard that flags a `ProjectVersion.txt` downgrade in a diff, since it is
   a two-line file whose change is invisible in a busy status.

## Acceptance

- [ ] Starting Unity with a mismatched `ProjectVersion.txt` fails with a named error, not a rebuild
- [ ] A run that produces no `COMPILE_GATE_OK` on a fresh log reports failure from the runner
- [ ] Both proven by deliberately inducing each case - **watch each fail before trusting it**

## ⭐ LEAD RULING 2026-08-24 - Codex found the flaw in my own proposed fix. It is right.

**The objection:** a raw `Unity.exe` (4.7) launch begins while `ProjectVersion.txt` still correctly
says 4.8, and rewrites it **afterwards**. So a pre-run check **passes**, and the damage happens after
it. My proposed fix #1 does not close the hole it was written to close.

⛔ **And the hole cannot be closed.** Nothing in this repo can prevent an arbitrary external process
from launching an editor. A wrapper guards only what passes through the wrapper - the same truth
CLAUDE.md §16 states about raw `adb install`.

⭐ **So stop trying to PREVENT it and make it LOUD instead.** Revised scope:

1. **A hook refuses a `ProjectVersion.txt` DOWNGRADE.** The file is two lines; comparing versions is
   trivial and needs no network. An unpreventable event becomes an announced one.
2. ⭐ **The runner asserts the MARKER instead of returning a bare exit code** - and this is the item
   that would actually have saved today. Three runners returned a verdict unrelated to reality inside
   ten minutes: two exited **0 having done nothing**, one reported **`NO LOG`** while the gate had
   **passed**. ⚠ This half is worth more than the editor pin that prompted the ticket.
3. **Pre-run check stays** - demoted to what it honestly is: it catches the *already-downgraded*
   state before paying for a full Bee rebuild. It does not catch the downgrade happening.

⚠ **Acceptance amended:** the ticket previously required proving the raw-invocation hole closed.
It cannot be. It now requires proving the downgrade is **detected and announced**.
