<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-267: Gate crossing — player has no intel before stepping into OuterWorld
**Linear:** [DEF-152](https://linear.app/defenders-of-the-realm/issue/DEF-152/gate-crossing-player-has-no-intel-before-stepping-into-outerworld)
**Lane:** UI/HUD
**Status:** READY TO IMPLEMENT
**Priority:** High

## Acceptance Criteria
- [ ] When hero is within 6m of a gate and facing outward, Sylas says a contextual line:
  - Quiet outside: "Clear out there. For now."
  - Enemies nearby (AlertIntelSystem detects threat within 60m): "Something's moving out there. Be ready."
  - First ever exit: "Once you're through the gate, you're on my ground. Stay close."
- [ ] Lines display via existing TownsfolkBubble / WandererDialogue system
- [ ] No new UXML — code-built UI only

## Files to Edit
- Gate proximity trigger script (new or extend `Gate.cs`)
- Sylas companion dialogue script (extend existing dialogue system)
- AlertIntelSystem integration (if WO-241 is landed)

## Do NOT Touch
- Village.unity (never hand-edit)
- VillageSceneBuilder.cs
- Files outside UI/HUD lane

## Dependencies
- DEF-151: Camera clip fix should land first
- WO-241: AlertIntelSystem provides threat data for the HUD strip (better tier)
- WO-238: Sylas ambient lines (gate hints slot into this system)
