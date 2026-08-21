<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-271: NPC dialogue box overlaps and obscures HUD during gameplay
**Linear:** [DEF-149](https://linear.app/defenders-of-the-realm/issue/DEF-149/npc-dialogue-box-overlaps-and-obscures-hud-during-gameplay)
**Lane:** UI/HUD
**Status:** CLOSED — DEPRECATED, audit-verified obsolete (2026-08-21 backlog audit).
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

> **AUDIT 2026-08-21 (agent fleet, read-only):** DEPRECATED. Evidence: `DialogueView.cs:201-206 deliberate band` — later ruling reversed ask. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
