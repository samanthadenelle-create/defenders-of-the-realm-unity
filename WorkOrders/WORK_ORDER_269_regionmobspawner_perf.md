# WO-269: RegionMobSpawner — replace FindObjectsOfType with NodeStateService lookup
**Linear:** [DEF-140](https://linear.app/defenders-of-the-realm/issue/DEF-140/regionmobspawner-findobjectsoftypeclaimablenode-on-every-enemy-spawn)
**Lane:** Combat/AI
**Status:** READY TO IMPLEMENT
**Priority:** Medium

## Acceptance Criteria
- [ ] `FindObjectsOfType<ClaimableNode>()` removed from `RegionMobSpawner`
- [ ] `NodeStateService` exposes a `GetAllNodes()` accessor
- [ ] Spawner uses `NodeStateService.Instance?.GetAllNodes()` instead
- [ ] No functional regression in enemy spawn behavior
- [ ] Brace balance check passed

## Files to Edit
- `Assets/_Modules/*/RegionMobSpawner.cs` — replace FindObjectsOfType call
- `Assets/_Modules/*/NodeStateService.cs` — add `GetAllNodes()` method

## Do NOT Touch
- Village.unity (never hand-edit)
- Files outside Combat/AI lane

## Dependencies
- None — standalone perf fix
