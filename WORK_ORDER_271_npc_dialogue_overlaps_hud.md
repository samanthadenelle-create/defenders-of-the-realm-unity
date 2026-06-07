# WO-271: NPC dialogue box overlaps and obscures HUD during gameplay
**Linear:** [DEF-149](https://linear.app/defenders-of-the-realm/issue/DEF-149/npc-dialogue-box-overlaps-and-obscures-hud-during-gameplay)
**Lane:** UI/HUD
**Status:** READY TO IMPLEMENT
**Priority:** Medium

## Acceptance Criteria
- [ ] NPC dialogue box Canvas SortOrder is lower than the HUD layer
- [ ] Wave counter, compass, health bar, and d-pad all remain fully visible when any dialogue box is active
- [ ] No HUD element is obscured at 375px mobile width
- [ ] Confirmed on Chrome mobile WebGL during active dialogue

## Files to Edit
- Dialogue box Canvas component — adjust SortOrder
- HUD Canvas component — verify SortOrder is higher than dialogue
- `Assets/_Modules/HUD/VillageHudController.cs` if sort order is set in code

## Do NOT Touch
- Village.unity (never hand-edit)
- Files outside UI/HUD lane

## Dependencies
- None — standalone UI fix
