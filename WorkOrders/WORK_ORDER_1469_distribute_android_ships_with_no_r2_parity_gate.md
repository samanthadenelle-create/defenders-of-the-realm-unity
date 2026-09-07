# WO-1469: distribute-android.ps1 ships to Firebase testers with NO R2 parity gate

**Status:** CLOSED 2026-09-07 - owner felt-test PASS (validated 2026-09-07T14:07:34, build 2026.09.07.359076). PRIOR STATUS: FIXED - 2026-09-06: distribute-android.ps1 calls tools/r2-ship.ps1 and requires a FRESH R2_PARITY_OK before firebase distribute (also with -Build:$false); PARSE_OK
**Silo:** `distribute-android.ps1` (repo root). Tooling only.
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1469 -> 1470 in the same edit).

## 1. EVIDENCE

```
distribute-android.ps1:26-35   schema parity check only - nothing about R2
distribute-android.ps1:50-57   -Build calls run-unity-method directly
distribute-android.ps1:66-72   firebase appdistribution:distribute
```

There is no call to `tools/r2-ship.ps1` anywhere in the file. This is the fourth path to a device that
bypasses the sec.16 gate, and it is the one that reaches TESTERS.

The correct shape already exists in the tree: `google-play-aab-build.ps1:314-337`.

## 2. FIX SHAPE

- Call `tools/r2-ship.ps1` before distributing, and require a FRESH `R2_PARITY_OK`: assert
  `(Get-Item Builds/r2-parity.log).LastWriteTime` is at or after the run's start time, else exit non-zero.
- Copy the ASSERTION shape from the AAB script, not the push/verify commands - those stay hardcoded exactly
  once inside `r2-ship.ps1` (CLAUDE.md sec.16).

## 3. WHAT NOT TO DO
- Do not re-inline the `--push ServerData` / `--verify-catalog ServerData/Android` pair. That is the exact
  duplicated-state failure sec.16 was written to close.
- Do not add an override flag.

## 4. ACCEPTANCE
- [ ] `distribute-android.ps1` calls `tools/r2-ship.ps1` and asserts log freshness.
- [ ] Proven by touching an old `r2-parity.log` and showing the script REFUSE, and by a clean run succeeding
      (memory `prove-the-success-path-not-just-the-refusal`).
- [ ] No push/verify strings duplicated outside `r2-ship.ps1` (grep pasted in the RESULT).
