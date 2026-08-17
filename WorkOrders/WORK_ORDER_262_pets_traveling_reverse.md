<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-262: Pets traveling in reverse along waypoint path
**Linear:** [DEF-95](https://linear.app/defenders-of-the-realm/issue/DEF-95/pets-traveling-in-reverse)
**Lane:** Combat/AI
**Status:** READY TO IMPLEMENT
**Priority:** High

## Acceptance Criteria
- [ ] Pet moves forward along its waypoint path (velocity direction matches forward vector)
- [ ] Completing the loop returns pet to waypoint 0 without reversing direction
- [ ] Fix confirmed in Play mode on all pet types (Sprite, Flame Pup, Ice Wolf)
- [ ] Confirmed on mobile WebGL

## Files to Edit
- Pet movement/waypoint script (likely `Assets/_Modules/Village/Pets/` or `Assets/_Modules/Core/Pets/`) — fix forward vector vs movement direction mismatch

## Do NOT Touch
- Village.unity (never hand-edit)
- Files outside Combat/AI lane

## Dependencies
- Related to WO-234 (animation sweep) — coordinate if both touch pet scripts
