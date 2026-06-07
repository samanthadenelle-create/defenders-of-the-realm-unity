# WORK_ORDER_328 — Investigate + fix the recurring NullReferenceException spam (likely root cause)

**Status: READY TO IMPLEMENT** · **PRIORITY: HIGH (suspected root of several "nothing happens" bugs)**
**Branch:** feat/tower-core-loop · **Lane:** 0 (NOW) · **Origin:** owner playtest 2026-06-06 (seen in nearly every screenshot)

## Problem
The dev console floods with **`NullReferenceException: Object reference not set to an instance of an object`**
continuously across town, DTT, nodes, build preview, and the admin panel. A per-frame (Update/LateUpdate) or
common-path null is throwing every frame. This very likely **causes or compounds** multiple reported failures:
WO-314 (build preview), WO-317 (DTT), WO-325 (node does nothing), WO-327 (trigger wave) — an exception mid-handler
aborts the rest of the action.

## Goal
Identify the **single root null** (or the few) behind the spam and fix/guard it, clearing the console flood and
ideally unblocking the dependent interactions.

## Scope
- Reproduce with full stack traces (Open Log File / Player.log) — get the **class + line + method** of the
  top recurring NRE (the console truncates; the log has the stack).
- Fix the root (a missing reference/binding) at source; add null-guards on the offending per-frame/common path.
- Re-test the dependent bugs (314/317/325/327) — note in the RESULT which ones the root fix resolves.

## Acceptance criteria
- [ ] Root NRE identified with stack trace (class/method/line) and documented in the RESULT.
- [ ] Console no longer floods with NullReferenceException in town/DTT/node/build-preview/admin.
- [ ] Per-frame/common path is null-guarded so a missing optional can't spam/abort.
- [ ] RESULT notes which of WO-314/317/325/327 the fix also resolves (re-verify them).
- [ ] Brace check; CompileGate OK; Windows build SUCCESS; verify in a play session.

## Do NOT touch
- No `.unity` edits. Fix at source + guard; don't blanket try/catch to hide it. Coordinate with 314/317/325/327 owners.
