> ⚠ **UNRESOLVED NUMBER COLLISION — WO-255 is claimed by more than one file and OWNERSHIP IS NOT DECIDED.**
> Co-claimants: `WORK_ORDER_255_hero_backwards_walk.md`, `WORK_ORDER_255_terrain_seam_height_mismatch.md`
> Both files were added in the SAME commit (first-on-disk is a dead tie) and neither is cited by any other doc, RESULT file, or commit message — there is no evidence on either side. Both are also still READY.
> Flagged (not resolved) by the 2026-08-16 Sunday board-grooming pass — a wrong ownership call is worse
> than a flagged unknown, so this needs an **owner ruling**. Nothing renumbered or deleted. Until it is
> ruled, cite this WO by FILENAME, never by bare number.

# WO-255: Hero travels backwards + walk animation not playing
**Linear:** [DEF-155](https://linear.app/defenders-of-the-realm/issue/DEF-155/wo-174-hero-travels-backwards-walk-animation-not-playing)
**Lane:** Combat/AI
**Status:** READY TO IMPLEMENT
**Priority:** Urgent

## Acceptance Criteria

- [ ] Hero faces movement direction when walking (forward vector matches velocity)
- [ ] Walk animation plays when hero is moving (Speed parameter > 0.1)
- [ ] Idle animation plays when hero is stationary
- [ ] Confirmed on mobile WebGL

## Files to Edit

- `Assets/_Modules/Village/Hero/HeroBodySwapper.cs` — likely transform.forward vs velocity mismatch
- `Assets/_Modules/Village/Hero/HeroLocomotion.cs` — animator Speed parameter wiring
- Check Animator Controller has Speed float parameter connected to Walk state transition

## Do NOT Touch

- VillageSceneBuilder.cs
- Any scene files
