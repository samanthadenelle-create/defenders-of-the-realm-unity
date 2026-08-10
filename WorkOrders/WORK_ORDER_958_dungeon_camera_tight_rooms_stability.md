# WORK ORDER 958 — Dungeon camera: stop fighting the player in small rooms

**Status:** DONE (implemented + gated 2026-08-10; RESULT filed; owner felt-pass closes it - every value is data in DungeonCameraProfile)
**Minted:** 2026-08-10 (CLI seat, main line — banner bumped 958 → 959 in the same edit)
**Silo:** Camera (SmartMobileCamera dungeon profile) — no overlap with live lanes
**Origin:** owner F8 seq 2289, 2026-08-10 11:23, dg_ember_deep, verbatim: *"the camera is fighting me
hard in here, when i get to smaller spots, can we tighten the camera up or stabilize it? its auto
rotating and it needs to keep more focus to the room as well as my direction."*

## 1. The authorities (canon, verify at source before touching)

- Composed `dg_*` dungeons bake NO camera rig: the camera is the runtime `GameplayCamera (ensured)` +
  `DeNelle.Village.SmartMobileCamera`, dungeon seat via `SmartMobileCamera.ApplyDungeonProfileIfNeeded`,
  seat + clear colour from the ONE authority `DeNelle.Core.World.DungeonCameraProfile`
  (scene test `HubScenes.IsDungeon`). `ff.dungeonfpv` is the parked FPV A/B — untouched by this WO.
- WO-919 gave composed rooms 4 m walls + a ceiling slab — the tight-room fighting likely involves
  the camera's collision/avoidance vs those, plus auto-yaw.

## 2. The felt asks (hers, translated)

1. **Stabilize:** damp/disable auto-rotation in dungeon context — the camera should not re-aim on its
   own while she is steering; her input owns yaw (read what drives auto-rotate in SmartMobileCamera
   and gate/damp it under the dungeon profile).
2. **Tighten in small spaces:** room-aware framing — shorter boom / raised pitch when the current
   room is small (room bounds are knowable: `RoomPrefabMeta` / the composed room the hero occupies),
   smooth transitions, no snap.
3. **Keep focus on the room + her direction:** framing biases toward facing direction without
   whipping; no wall-clip pops (verify how the boom handles the WO-919 ceiling slab).

## 3. Discipline

- §12: capture-first — one instrumented dungeon run logging the camera's live params
  (`[Flow:Camera]` Throttle: boom length, yaw source, avoidance hits, current room id/size) BEFORE
  tuning, so the "fighting" is a named behavior, not a vibe. Tune against the trace, then her hands.
- All values land in `DungeonCameraProfile` (the one authority) — owner-tunable, no scattered consts.
- HEADLESS CANNOT SEE FEEL (canon 08-09): implementation + trace proves binding; ONLY her felt-pass
  closes this. Ship behind the existing profile so town camera is untouched.

## 4. What NOT to touch

Town/overworld camera behavior · the FPV A/B seam · WO-919 room geometry · HeroLocomotion.
