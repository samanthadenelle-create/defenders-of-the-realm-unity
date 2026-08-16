# RESULT — WO-994 shield port seam (2nd pass)

**Date:** 2026-08-16  **Seat:** CLI (trace-first diagnosis + fix wave; commits `f11a80ffe`, `e2add86cc`, `a36a6c344`)
**Status:** IMPLEMENTED - committed; PO felt-verify = ONE dungeon->town port on the new exe with the shield equipped

## What changed (details live in the WO body — see its DIAGNOSTIC SPEC + TRACE RESULTS + ADDENDUM sections)

1. **Trace-proven diagnosis (WO sec "TRACE RESULTS 2026-08-16").** A full instrumented
   dungeon->town run (desktop exe, autopilot DungeonLoop, 31MB trace) decided the candidates:
   - **Candidate A (registry reload asymmetry) ELIMINATED** — `registryProbe` showed correct
     `shield_A` / `shield_A@sheathed` values at every probe, all segments.
   - **The 2026-08-15 scene-load re-seat was DEAD CODE** — `OnDisable` unsubscribed `sceneLoaded`
     before the callback could ever fire on a live instance; the shipped 08-15 fix never executed.
   - The fresh-rebuild attach path is healthy (byte-identical correct seats every segment); the
     idempotent off-hand early-out (Candidate C) is live and no-ops any survive-path re-seat.
2. **Fix landed (data-justified):** in `CoReapplyGearAfterSceneLoad`, `_currentWeaponId` +
   `_currentOffHandId` are cleared before `EquipBestForHero()` so the survive-path re-seat is a
   REAL re-attach (fresh NormalizeInto + registry seat at the new height) instead of a no-op.
3. **Seat-drift tripwire (permanent, WO ADDENDUM):** every off-hand seat write records a snapshot;
   a differing write logs old+new+writer (`WO-994 seatWrite by=...`); the scene-load checkpoint
   `FlowTrace.Fail`s on an UNLOGGED writer (`WO-994 SEAT DRIFT`) with screenshot capture.
4. **Coverage:** new `[attachment-offset]` suite (`Assets/Editor/Regression/AttachmentOffsetRegression.cs`,
   markers `ATTACHMENT_OFFSET_OK/FAIL`) — pins the `shield_A` registry rows without pinning canon
   eulers, plus source-lint that the tripwire/probes/fullOverride application stay wired.
5. NO seat numbers, offsets, guards, or frame counts changed (owner ruling respected).

## Files

- `Assets/_Modules/Village/Hero/EquipmentController.cs` (probes, tripwire, id-clear fix)
- `Assets/_Modules/Village/Hero/AttachmentOffsetRegistry.cs` (`Count` accessor)
- `Assets/_Modules/Village/Hero/HeroBodySwapper.cs` (frame stamp on marker-add)
- `Assets/Editor/Regression/AttachmentOffsetRegression.cs` (new)

## Verification

- Brace + NUL gate green on all files; batch-gated + committed by the CLI seat.
- Captured trace run (three boot->dungeon->hub segments) is the proof of diagnosis — see the
  WO's TRACE RESULTS section for the exact lines.

## PO felt-verify

Play ONE dungeon->town port with the shield equipped on the NEW exe; screenshot AFTER the
transition (hub-only does not test it). The tripwire is permanent — if it still breaks, the
last `seatWrite` line before the break names the exact step.
