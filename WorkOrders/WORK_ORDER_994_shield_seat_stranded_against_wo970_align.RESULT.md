# RESULT — WO-994 shield port seam

**Status:** IMPLEMENTED — 2026-08-15  
**PO:** re-verify dungeon→town with shield equipped.

## Change

`EquipmentController`: on `SceneManager.sceneLoaded`, after 2 frames:
- `InvalidateHeroHeightCache()`
- `EquipBestForHero()` + `ApplyHoldPose()`

Owner pin: seat is good until **port only** — this re-seats against hub body height without re-dialing offsets.
