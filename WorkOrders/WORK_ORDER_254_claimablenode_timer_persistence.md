> ⚠ **NUMBER COLLISION — this document does not own WO-254; `WORK_ORDER_254_hero_hover_exploit.md` does.**
> Referred to hereafter as **WO-254-B (ClaimableNode timer persistence)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> the two files were added in the **same commit**, so first-on-disk is a tie; ownership decided on **cross-references** (the winner is the file the rest of the corpus cites).
> Banner only — nothing was renumbered or deleted.

# WO-254: ClaimableNode repopulation timer not persisted to GameState
**Linear:** [DEF-142](https://linear.app/defenders-of-the-realm/issue/DEF-142/claimablenode-repopulation-timer-datetime-lost-on-quit-not-persisted)
**Lane:** Combat/AI
**Status:** READY TO IMPLEMENT
**Priority:** Low

## Acceptance Criteria
- [ ] `_lastClearedTime` persisted as Unix timestamp via `NodeStateService`
- [ ] Key format: `node_{id}_clearedAt` alongside existing node state flush
- [ ] On quit/restart, recently-cleared nodes correctly observe the 30-min repopulation window
- [ ] Brace balance check passed

## Files to Edit
- `Assets/_Modules/*/ClaimableNode.cs` — serialize `_lastClearedTime`
- `Assets/_Modules/*/NodeStateService.cs` — add `clearedAt` key to `Flush()`

## Do NOT Touch
- Village.unity (never hand-edit)
- Files outside Combat/AI lane

## Dependencies
- Can be batched with WO-250/251 (ClaimableNode perf fixes)
