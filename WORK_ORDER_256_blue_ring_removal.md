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
