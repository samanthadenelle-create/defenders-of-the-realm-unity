# WO-256: Double wall ring — remove BuildWallRing, keep BuildWallPerimeter only
**Linear:** [DEF-106](https://linear.app/defenders-of-the-realm/issue/DEF-106/double-wall-ring-buildwallring-buildwallperimeter-both-active-causing)
**Lane:** World/Environment
**Status:** READY TO IMPLEMENT
**Priority:** Medium

## Acceptance Criteria
- [ ] `BuildWallRing()` and `BuildGates()` calls removed from `VillageSceneBuilder.cs`
- [ ] Only `BuildWallPerimeter` / `BuildCastleFortification` builds the wall
- [ ] Scene rebaked — no double-wall z-fighting visible at any angle in Play mode
- [ ] Wall renders as a single clean ring with no overlapping geometry
- [ ] Brace balance check passed

## Files to Edit
- `Assets/Editor/VillageSceneBuilder.cs` — remove old `BuildWallRing()` and `BuildGates()` calls

## Do NOT Touch
- Village.unity (never hand-edit — rebake via batchmode after fix)
- Files outside World/Environment lane

## Dependencies
- VSB is serialization bottleneck — coordinate with WO-244, WO-246, WO-249, WO-255
- Related to WO-232 (rendering sweep), WO-233 (collision/NavMesh sweep)
