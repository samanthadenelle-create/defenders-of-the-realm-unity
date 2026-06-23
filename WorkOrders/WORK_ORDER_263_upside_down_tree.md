# WO-263: Upside-down tree asset reappeared in scene
**Linear:** [DEF-96](https://linear.app/defenders-of-the-realm/issue/DEF-96/upside-down-tree-asset-reappeared-in-scene)
**Lane:** World/Environment
**Status:** READY TO IMPLEMENT
**Priority:** High

## Acceptance Criteria
- [ ] Upside-down tree GameObject is absent from Village scene hierarchy after rebake
- [ ] `VillageSceneBuilder.cs` contains no call that places that specific asset at an inverted rotation
- [ ] Scene rebaked and tree does not reappear
- [ ] Confirmed in Play mode — no inverted tree visible from any camera angle

## Files to Edit
- `Assets/Editor/VillageSceneBuilder.cs` — find and fix the tree placement call with inverted Y rotation

## Do NOT Touch
- Village.unity (never hand-edit — fix via VillageSceneBuilder then rebake)
- Files outside World/Environment lane

## Dependencies
- VillageSceneBuilder.cs is a serialization bottleneck (CLAUDE.md S9) — coordinate with any other World/Environment WOs touching VSB
- Requires a Village rebake after fix
