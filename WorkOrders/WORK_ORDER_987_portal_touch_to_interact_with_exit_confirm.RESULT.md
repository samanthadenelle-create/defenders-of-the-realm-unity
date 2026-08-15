# RESULT — WO-987 Portal touch confirm

**Status:** IMPLEMENTED — 2026-08-15  
**PO felt-verify owed**

## Change

`DungeonExitInteractable.cs`:
- Touch / in-range button → `RequestExitConfirm()` (not raw leave).
- Obsidian `ElarionUiKit.BuildConfirmModal`: **Continue to exit** (Gold, right) / **Cancel** (Quiet, left).
- Confirm Close/scrim → Cancel path; run state unchanged.
- FlowTrace: CONFIRM SHOWN faces, RESOLVED face=continue-to-exit|cancel, FAIL TO APPEAR warn.
- Actual scene leave only via `ExecuteLeave()` after Continue.

Works with WO-995 arm/grace (confirm only after armed).
