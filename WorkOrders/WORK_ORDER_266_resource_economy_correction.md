<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

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
