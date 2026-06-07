# WO-270: ClaimableNode.DestroyNode() — cache Building list and EconomyService
**Linear:** [DEF-139](https://linear.app/defenders-of-the-realm/issue/DEF-139/claimablenodedestroynode-findobjectsoftypebuilding-on-every-raze)
**Lane:** Combat/AI
**Status:** READY TO IMPLEMENT
**Priority:** Medium

## Acceptance Criteria
- [ ] `FindObjectsOfType<Building>()` removed from `DestroyNode()`
- [ ] `List<Building> _spawnedBuildings` maintained on `ClaimableNode`, populated in `OutpostBuildPanel.OnBuild()`
- [ ] `FindObjectOfType<EconomyService>()` removed from `FinishPlayerRaze()`
- [ ] `EconomyService` reference cached in `Awake()`
- [ ] No functional regression in raze behavior
- [ ] Brace balance check passed

## Files to Edit
- `Assets/_Modules/*/ClaimableNode.cs`
- `Assets/_Modules/*/OutpostBuildPanel.cs` (if needed to populate building list)

## Do NOT Touch
- Village.unity (never hand-edit)
- Files outside Combat/AI lane

## Dependencies
- None — standalone perf fix. Can be batched with WO-250.
