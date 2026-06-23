# WO-255: World-map lip — Village/OuterWorld terrain seam height mismatch
**Linear:** [DEF-126](https://linear.app/defenders-of-the-realm/issue/DEF-126/world-map-lip-village-outerworld-terrain-seam-height-mismatch-ledge)
**Lane:** World/Environment
**Status:** READY TO IMPLEMENT
**Priority:** Medium

## Acceptance Criteria
- [ ] No visible step or height gap at the Village/OuterWorld boundary seam from any approach angle
- [ ] Player character crosses the boundary without a physics bump (no velocity spike or stutter)
- [ ] Terrain height at the seam matches within +/-0.05 Unity units
- [ ] Confirmed in WebGL build walking in all four cardinal directions across the boundary

## Files to Edit
- Terrain height data at Village/OuterWorld boundary
- `Assets/Editor/VillageSceneBuilder.cs` if terrain height is set programmatically (VSB bottleneck)

## Do NOT Touch
- Village.unity (never hand-edit — fix via terrain tool or VillageSceneBuilder)
- Files outside World/Environment lane

## Dependencies
- VSB is serialization bottleneck — coordinate with other World/Environment WOs
