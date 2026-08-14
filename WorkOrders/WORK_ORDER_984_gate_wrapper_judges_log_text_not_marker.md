# WORK ORDER 984 — The Unity method wrapper judges success by LOG TEXT, not by a MARKER

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-14 (CLI)
**Silo:** Tooling / verification harness
**Lane:** Infrastructure — file-disjoint from all gameplay lanes

---

## Why this exists

`run-unity-method.ps1` is the wrapper every seat uses to run the compile gate, the regression
suites, the bakes, the captures and the builds. It decides pass/fail by **scanning the log for
error text**. Its own header states this plainly:

> *"We therefore ignore the wrapper exit code and poll until no 'Unity' process remains, then judge
> success from the log (compile errors / exceptions / 'Aborting batchmode')."*

That was a defensible design when Unity's fork-on-launch quirk made the real exit code useless and
**no markers existed**. Markers exist now, and since 2026-08-02 they are distinct per entry point:
`COMPILE_GATE_OK`, `REGRESSION_OK <n>/<n> suites`, `CHECKIN_SUITE_OK`, `SESSION_GUARDS_OK`,
`UI_CAPTURE_OK`.

**Absence of an error is not evidence of success.** A run that never happened logs no errors.

## Proof (captured 2026-08-14, three independent ways)

> ## ⚠ PROOF 1 IS MISATTRIBUTED — corrected 2026-08-14 by the implementing agent, which RAN it
> `powershell -File tools\run-unity-method.ps1 ...` (a path that does not exist) exits **-196608** from
> PowerShell, and **127** via bash — **not 0**. The `exit 0` recorded below came from the **calling
> layer** that reported the background task, not from this script, which never ran at all. **Nothing
> inside `run-unity-method.ps1` could have influenced it.**
> The lesson stands unchanged and is arguably sharper: *an exit code was trusted, and it was produced by
> a layer nobody had checked.* But the mechanism is not the one written below, and a fix aimed at proof 1
> would have been aimed at the wrong file.
>
> **THE STRONGEST PROOF IS ACTUALLY THIS ONE**, found while demonstrating the fix: a **STALE** log from
> an earlier run contained BOTH `COMPILE_GATE_OK : 1` **and** `Exiting batchmode successfully`. The old
> script would have read that stale file and **exited 0** — certifying a run that never happened, using
> evidence from a different run. That is the defect in its purest form, and it is why the mtime check
> matters more than the marker check.

1. **Nonexistent script path exits 0.** ⚠ **See the correction banner above — this row is wrong.**
   `powershell -File tools\run-unity-method.ps1 -Method DeNelle.Editor.CompileGate.Run`
   The runner lives at **repo root**, not under `tools/`. PowerShell reported
   *"The argument 'tools\run-unity-method.ps1' to the -File parameter does not exist"* — and the
   command **exited 0**. Nothing ran. A seat trusting the exit code reports a green gate.

2. **Missing mandatory parameter exits 1 — but only because PowerShell caught it.**
   The same call with the correct path but no `-LogName` failed with
   *"Cannot process command because of one or more missing mandatory parameters: LogName."*
   That non-zero exit came from PowerShell's own parameter binding, **not** from any check the
   wrapper performs. It is luck, not verification.

3. **Reading the wrong log inverts the verdict.**
   Checking `Builds\build.log` (the *build* log) after a *gate* run reported
   `COMPILE_GATE_OK : 0` on a tree that was in fact clean. The correct log
   (`Builds\compile-gate-wo1007.log`) held `COMPILE_GATE_OK : 1`, `error CS : 0`.
   Nothing in the harness ties a run to the log that run produced.

## Why this is the most expensive instance of the pattern

This is the same defect class as the 44 rows in `docs/reference/HOLLOW_ASSERTIONS_REGISTRY.md`
and the same class as `INSTRUMENTATION_STANDARD` §1.4b — *"a trace field that cannot report failure
is a bug, not a nicety."*

It is **worse than a hollow trace**, because it sits in the tooling that every other verification
depends on. A hollow trace makes one system's state unknown. A hollow gate makes **every downstream
"verified" claim unfounded** — including the ones this project uses to decide what reaches the owner.

Two sibling instances found the same day, for context on how common the shape is:
- The regression suite held **159/159 green** across a bake that silently reverted an owner ruling
  (`label: "Extract"` → `"Leave"`, 13 occurrences). The suite is a ratchet, not a reviewer.
- WO-983's acceptance criterion greps for `SKIPPED - active loops 20/20`. The live string is
  **em-dashed** and the cap is no longer 20 — the check **could not fail**.

## The fix

Require the caller to declare what success looks like, and fail closed when it is absent.

- Add a **mandatory** `-ExpectMarker <string>` parameter (e.g. `COMPILE_GATE_OK`).
- After the run, FAIL (non-zero, with a named reason) when **any** of these hold:
  - the log file **does not exist**;
  - the log's mtime is **older than the run's start time** (stale log from a previous run — this is
    the failure mode that makes a crashed run look like a passed one);
  - the expected marker is **absent** from the log;
  - the log is implausibly small (a truncated/aborted run).
- Print the decisive evidence on both paths — marker found/absent, log path, mtime, size — so the
  caller never has to guess which log was judged. Follow §1.4b: the success line and the failure
  line must not be able to read the same.
- Do **not** remove the existing error-text scan. It catches real failures that still emit a marker.
  Add the marker check as an **additional** gate, not a replacement.

## Acceptance criteria

A deliberately-broken invocation must exit **NON-ZERO**. Today none of these do:

| Case | Required |
|---|---|
| Script path wrong / does not exist | non-zero |
| `-Method` names a class or method that does not exist | non-zero |
| Log file absent after the run | non-zero |
| Log present but **stale** (mtime before run start) | non-zero |
| Marker absent though the run completed | non-zero |
| Healthy run, marker present, log fresh | **zero**, and prints marker + log path + mtime |

Prove each row by running it, and paste the exit codes into the RESULT. **Do not assert the
behaviour from reading the script** — that is precisely the error this ticket documents (CLAUDE.md
§12: static reading LOCATES, it never CONCLUDES).

## Files

- `run-unity-method.ps1` (repo root)
- Callers that pass a known marker may adopt `-ExpectMarker` in the same pass; callers not updated
  must keep working, or the ticket has broken the fleet to fix the harness.

## What NOT to touch

- Do not change the fork-aware wait/poll logic — that solves a different, real problem
  (memory `unity-batchmode-relaunch-quirk`).
- Do not touch any gameplay `.cs`, any `.unity` scene, or any catalog data.
- ASCII-only in the `.ps1` — Windows PowerShell 5.1 reads BOM-less files as ANSI, so em-dashes and
  smart quotes corrupt and break the parse. (This bit the headed-capture harness the same day.)
