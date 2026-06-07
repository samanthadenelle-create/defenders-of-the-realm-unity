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
