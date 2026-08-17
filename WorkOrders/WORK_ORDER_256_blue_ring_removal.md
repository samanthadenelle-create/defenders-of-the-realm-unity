> ⚠ **UNRESOLVED NUMBER COLLISION — WO-256 is claimed by more than one file and OWNERSHIP IS NOT DECIDED.**
> Co-claimants: `WORK_ORDER_256_blue_ring_removal.md`, `WORK_ORDER_256_double_wall_ring.md`
> Both files were added in the SAME commit (first-on-disk is a dead tie) and neither is cited by any other doc, RESULT file, or commit message — there is no evidence on either side. Both are also still READY.
> Flagged (not resolved) by the 2026-08-16 Sunday board-grooming pass — a wrong ownership call is worse
> than a flagged unknown, so this needs an **owner ruling**. Nothing renumbered or deleted. Until it is
> ruled, cite this WO by FILENAME, never by bare number.

# WO-256: Remove or fix blue ring/circle around hero while walking
**Linear:** [DEF-205](https://linear.app/defenders-of-the-realm/issue/DEF-205/blue-ringcircle-around-hero-while-walking-confusing-obscures-movement)
**Lane:** VFX/Audio
**Status:** READY TO IMPLEMENT
**Priority:** High — live on itch.io, player-facing

## Acceptance Criteria

- [ ] Blue ring is either removed entirely (if debug artifact) or made subtle and purposeful
- [ ] Hero movement is visually clear — no ground-level indicator obscuring walk direction
- [ ] Owner quote: "it's now the worst feature, I can never tell where I'm walking" — this must be resolved
- [ ] Confirmed on mobile WebGL (iOS Safari via itch.io)

## Files to Edit

- Search for `Projector`, `DecalProjector`, or a `LineRenderer`/`SpriteRenderer` child on the Hero prefab drawing a circle
- Likely a selection indicator or aggro range debug visual — check `HeroSelectionRing`, `HeroIndicator`, or similar component
- `grep -r "ring\|circle\|indicator\|projector" Assets/_Modules/Village/Hero/`

## Do NOT Touch

- VillageSceneBuilder.cs
- Any scene files
