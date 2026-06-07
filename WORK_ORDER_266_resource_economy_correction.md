# WO-266: Resource economy — Wood/Food/Iron/Crystals harvestable; Magic = building-upgrade axis only
**Linear:** [DEF-121](https://linear.app/defenders-of-the-realm/issue/DEF-121/wo-230-resource-economy-woodfoodironcrystals-harvestable-magic)
**Lane:** Monetization/Backend
**Status:** READY TO IMPLEMENT
**Priority:** High

## Acceptance Criteria
- [ ] `EconomyService` tracks Wood, Food, Iron, Crystals as the four harvestable resources
- [ ] No "Magic" MineNode, pickup, or harvest path exists in code
- [ ] Pet auto-harvest raises resource count in `GameState` for all four resource types
- [ ] At least 1 building has a Magic-gated upgrade tier that unlocks a tech-tree node
- [ ] `GameState` save/load round-trips all four resource values correctly

## Files to Edit
- `Assets/_Modules/Core/Economy/EconomyService.cs` — resource type definitions
- Resource node scripts — remove Magic as harvestable
- Building upgrade scripts — wire Magic as tech axis
- `Assets/_Modules/Village/Pets/` — pet auto-harvest wiring

## Do NOT Touch
- Village.unity (never hand-edit)
- Files outside Monetization/Backend lane

## Dependencies
- Related to WO-230
