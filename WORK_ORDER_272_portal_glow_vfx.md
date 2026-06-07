# WO-272: Portal interior glow VFX — signal it's worth exploring
**Linear:** [DEF-100](https://linear.app/defenders-of-the-realm/issue/DEF-100/portal-interior-glow-vfx-signal-its-worth-exploring)
**Lane:** VFX/Audio
**Status:** READY TO IMPLEMENT
**Priority:** Medium

## Acceptance Criteria
- [ ] Portal arch interior shows a looping particle or additive-glow effect when idle
- [ ] Effect scale or emission multiplier increases >=1.5x when player enters a 3m trigger radius
- [ ] No frame-rate drop >5fps on mobile WebGL with effect active
- [ ] Uses existing VFX assets from VFXManager — no new external imports required
- [ ] No UXML / UIDocument

## Files to Edit
- New script: `Assets/_Modules/VFX/PortalGlowEffect.cs` — proximity trigger + emission control
- `Assets/_Modules/BattleATB/VFXManager.cs` — add portal glow prefab reference if needed

## Do NOT Touch
- Village.unity (never hand-edit)
- Files outside VFX/Audio lane

## Dependencies
- DEF-94 (portal color fix) should land first so materials are correct
