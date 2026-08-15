# RESULT — WO-985 KeeperRelative yaw fragment

**Status:** IMPLEMENTED — 2026-08-15

## Change

`DungeonHero.cs`:
- `ModelYawOffset` **90 → 0** with comment that it pairs with `FaceHeading` (no model offset), not the camera rig.
- Branch remains **dead** (`DungeonStickBasis = CameraRelative`); if ever enabled it no longer double-rotates.

Gait forensics hollow-field fix landed under WO-966 RESULT (MeasuredRootSpeed).

## Not claimed

Correct-under-movement capture (needs live player + WO-988 healthy run).
