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
