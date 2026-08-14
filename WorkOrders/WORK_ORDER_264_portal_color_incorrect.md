# WO-264: Portal color incorrect — pink/magenta instead of deep violet
**Linear:** [DEF-94](https://linear.app/defenders-of-the-realm/issue/DEF-94/portal-color-incorrect)
**Lane:** VFX/Audio
**Status:** DONE

> **DONE - verified in HEAD 2026-08-14 (phantom sweep).** The work is present at PortalVFXController.cs:92,46,658-703.
> Status had read READY because the landing commit did not flip this line in the same commit
> (CLAUDE.md §2), so the DERIVED board (BOARD.html) kept re-serving finished work.
> _Prior status line, preserved: Status: READY TO IMPLEMENT_

**Priority:** High

## Acceptance Criteria
- [ ] Portal material renders deep violet (no pink or magenta visible)
- [ ] `Defenders > Art > Fix Polyperfect URP Materials` resolves the issue when run
- [ ] Color persists after scene reload — not a one-session fix
- [ ] Confirmed in Play mode in WebGL build

## Files to Edit
- Portal material asset (URP Lit material — likely in `Assets/polyperfect/` or `Assets/_Modules/Village/`)
- Possibly `Assets/Editor/` URP material fix script if the conversion isn't running on portal assets

## Do NOT Touch
- Village.unity (never hand-edit)
- Files outside VFX/Audio lane

## Dependencies
- Related to WO-232 (rendering/URP sweep)
