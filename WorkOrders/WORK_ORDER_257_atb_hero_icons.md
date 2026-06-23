# WO-257: ATB battle UI — replace party pills with hero character icons
**Linear:** [DEF-115](https://linear.app/defenders-of-the-realm/issue/DEF-115/wo-213-atb-battle-ui-replace-party-pills-with-hero-character-icons)
**Lane:** UI/HUD
**Status:** READY TO IMPLEMENT
**Priority:** Medium

## Acceptance Criteria
- [ ] ATB party slots show hero character icons, not pills
- [ ] Graceful fallback if a portrait is missing (keep pill, log warning)
- [ ] UI built in code (UXML doesn't render in builds — CLAUDE.md S8); no new console errors
- [ ] Brace balance gate passes

## Files to Edit
- `Assets/_Modules/BattleATB/BattleHud.cs` — party-member slot rendering
- Load per-hero portrait from `Resources/Heroes/<class>` via `HeroPortraitGenerator` / `HeroPortraitRenderer`

## Do NOT Touch
- Village.unity (never hand-edit)
- Village HUD files (DEF-112's WO-178 targets village HUD, not ATB BattleHud — confirm disjoint)
- Files outside UI/HUD lane

## Dependencies
- Parallel-safe: disjoint from VillageSceneBuilder, DEF-109, DEF-112
