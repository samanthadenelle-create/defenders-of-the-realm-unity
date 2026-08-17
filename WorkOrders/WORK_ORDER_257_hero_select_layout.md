> ⚠ **UNRESOLVED NUMBER COLLISION — WO-257 is claimed by more than one file and OWNERSHIP IS NOT DECIDED.**
> Co-claimants: `WORK_ORDER_257_atb_hero_icons.md`, `WORK_ORDER_257_hero_select_layout.md`
> Both files were added in the SAME commit (first-on-disk is a dead tie) and neither is cited by any other doc, RESULT file, or commit message — there is no evidence on either side. Both are also still READY.
> Flagged (not resolved) by the 2026-08-16 Sunday board-grooming pass — a wrong ownership call is worse
> than a flagged unknown, so this needs an **owner ruling**. Nothing renumbered or deleted. Until it is
> ruled, cite this WO by FILENAME, never by bare number.

# WO-257: Fix Hero Select screen layout — overlaps, clipping, spacing
**Linear:** [DEF-204](https://linear.app/defenders-of-the-realm/issue/DEF-204/hero-select-screen-layout-broken-intro-text-overlaps-titletagline)
**Lane:** UI/HUD
**Status:** READY TO IMPLEMENT
**Priority:** High — first screen players see on itch.io

## Acceptance Criteria

- [ ] Title, tagline, and intro/lore text occupy separate non-overlapping regions
- [ ] "Connect Wallet" and "Skip" buttons both fully visible and non-overlapping at 375–428px width
- [ ] Hero cards (Grom, Sylas, Thrain, Elara) evenly spaced / centered as a row
- [ ] No text truncation on any button label
- [ ] Confirmed on mobile WebGL (iOS Safari, 375px width)

## Files to Edit

- `Assets/_Modules/HUD/HeroSelectScreen.cs` or `LandingPageBuilder.cs` — layout code
- UI is code-built (UXML doesn't work in builds per CLAUDE.md §8)
- Fix vertical stacking order: title → tagline → lore text (with spacing)
- Fix button positioning: Connect Wallet and Skip need separate anchor zones
- Fix roster: use equal `flex` or calculated spacing for hero cards

## Do NOT Touch

- VillageSceneBuilder.cs
- Any scene files
- Dragon art or portrait rendering (those are fixed per DEF-134, DEF-131)
