# WO-1507: release builds walk a managed stack on EVERY Debug.Log, while FlowTrace is enabled in release

**Status:** FIXED 2026-09-06 in eb161dc98 (ProjectSettings m_StackTraceTypes + StackTraceLogTypeRegression) (was: IN PROGRESS - lane handed back edits 2026-09-06 (uncommitted, awaiting the wave-two compile + regression gate); prior: READY TO IMPLEMENT)
**Silo:** `ProjectSettings/ProjectSettings.asset` (Android stack trace types). No code deletion.
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1507 -> 1508 in the same edit).

## 1. EVIDENCE

```
ProjectSettings/ProjectSettings.asset:59
  m_StackTraceTypes: 010000000100000001000000010000000100000001000000
```

That is `ScriptOnly` for ALL SIX log types - Log, Assert, Warning, Error, Exception, and the sixth - so a
plain `Debug.Log` walks a managed stack and allocates strings in the shipped player.

And instrumentation is on in release:

```
FlowTrace.cs:47   Enabled = true
```

Combined with the 320-lines-per-second probe (WO-1450) that is 320 stack walks plus 320 string allocations a
second, which is consistent with the observed `gc=26MB` at `fps=11`.

## 2. FIX SHAPE

- Set Log and Warning to **None** for the Android player; keep Error and Exception at ScriptOnly so crashes
  stay diagnosable.
- Measure `gc=` and `fps=` on the same raid before and after; both numbers in the RESULT.

## 3. WHAT NOT TO DO
- **Do NOT strip FlowTrace.** CLAUDE.md sec.12 is explicit: instrumentation is permanent; it may be flagged
  off, never removed. This ticket changes the STACK TRACE setting, not the traces.
- Do not set Error/Exception to None to squeeze more; a crash with no stack costs far more than it saves.

## 4. ACCEPTANCE
- [ ] Log and Warning at None for Android; Error and Exception unchanged. Setting value pasted.
- [ ] Before/after `gc=` and `fps=` from the same raid.
- [ ] An exception still produces a usable stack on device (proven with a forced throw).
- [ ] `REGRESSION_OK n/n` on a fresh log.
