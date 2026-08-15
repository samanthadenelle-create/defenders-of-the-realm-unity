# RESULT — WO-966 Hero facing offset

**Status:** IMPLEMENTED (code) — 2026-08-15  
**PO felt-verify:** Mage run north in town + dungeon; Knight must stay correct.

## Change

1. **`HeroBodySwapper.BuildLegacyResourcesBody`** — non-Knight `forwardYaw` **0** (was guessed **-90**). Knight stays **+15**.
2. **`WireHeroBody`** — `alignFacingToRoot: true` for non-Knight legacy; runs **`AlignBodyFacingToRoot`**.
3. **`AlignBodyFacingToRoot`** — measures forward from **shoulders** (same axis as `HeroFacingAudit` / `MeasureForwardYawNeeded`); hips only as fallback. Logs via FlowTrace.
4. **`HeroGaitForensics`** — when `Velocity≈0` but `MeasuredRootSpeed>0` (dungeon CC owner), use measured speed + root yaw so **bodyErr is not hollow**.
5. **`HeroFacingAudit`** mirror constants updated to non-Knight **0**.

## Shared surface (WO-985 note)

Dungeon Keeper uses the same `HeroBody` swap — one edit, both scenes.

## Acceptance still owed

Owner F8 while *moving*: `bodyErr` within ~15° for Mage/Ranger/Knight separately.
