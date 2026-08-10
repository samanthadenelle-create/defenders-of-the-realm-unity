# WO-958 RESULT — the dungeon camera stops fighting the player in small rooms

**Status:** IMPLEMENTED — FELT-VERIFY OWED (headless cannot see feel)
**Landed:** 2026-08-10 (wave-3 lane; verified, gated and committed by the CLI seat)

## The named behaviour behind "it's auto rotating"

`SmartMobileCamera`'s facing-recenter, at VILLAGE tuning: suspended while steering, so every PAUSE in a
tight room swung the seat behind her facing 0.4 s later at up to 220 deg/s, stiffness 4. In a corridor
that reads as the camera taking the wheel.

## What changed — every number in the ONE authority

`Core/World/DungeonCameraProfile.cs` gained the whole WO-958 block (dungeon context only; the town
camera never sees any of it):

- Recenter → lazy idle drift: delay 1.25 s, max 70 deg/s, stiffness 1.4. Kept ALIVE (not disabled)
  because a dead recenter world-locks the seat behind corridor walls — the exact WO-385 failure it
  exists to prevent.
- Small-room seat: `SmallRoomMaxExtent 12 m` (catches every 1-cell room/corridor, spares a 2×2 hall),
  `SmallRoomCameraDistance 2.4` (vs 3.2), `SmallRoomCameraHeight 2.15` (vs 1.9) over the same look-at =
  the raised pitch. `RoomSeatSmoothTime 0.55 s`.
- Facing focus: `FacingLookAhead 0.8 m`, biased by FACING never velocity, routed through the existing
  lead SmoothDamp so a spin moves the aim under a metre, eased.
- Ceiling safety: pitch band narrowed to [-5, 20] plus a hard hero-relative backstop at
  `CeilingHeightRef - 0.5`. A `min()` is continuous, so engaging it eases rather than pops.

Room awareness needed data across an asmdef wall (Village cannot reference Dungeons; reflection is
banned), so it flows DOWN through Core:

- NEW `Core/World/DungeonRoomSense.cs` — pure storage + planar containment query.
- NEW `Dungeons/DungeonRoomSensePublisher.cs` — sceneLoaded hook measuring each `RoomPrefabMeta` room
  with the ONE shared `DungeonRoomBounds.Compute` (WO-797). Additive loads never wipe a live room set;
  a single load into a room-less world clears it. Hand-built / outpost dungeons publish 0 rooms and get
  the standard seat (traced, not silent).
- `SmartMobileCamera.cs:909` consumes it (`DungeonRoomSeat`), sticky by 0.35 m of doorway slack so the
  room id cannot flap. Village values are snapshotted on entry (`:516-521`) and restored exactly on exit
  (`:573-580`).

## §12 evidence feed

`[Flow:Camera]` heartbeat every 2 s while in a dungeon: boom, seat (h,d), yaw source
(input / recenter / hold), room id + size + small flag, cumulative ceiling clamps; plus one Step line
per room change. **That is what to grep from a capture run.**

## Gate (real, this run)

- `Builds/gate-settle4.log` → `COMPILE_GATE_OK`, zero `error CS`
- `Builds/regression-settle3.log` → `REGRESSION_OK 143/143 suites` (`[dungeon-cam-958]` green)

## Oracle — what it proves

`DungeonCameraTightRoomRegression` (`DUNGEON_CAM_958_OK`): the NUMBERS (both seats rotated to the pitch
cap clear the 4 m WO-919 slab; the small-room seat is strictly tighter and fits a 10 m cell; the
classifier catches 1-cell and not 2×2; the recenter stays inside a lazy-drift band; transitions
smoothed; lead sub-metre), the BINDING (every call site present and dungeon-gated, with an EXACT village
restore so town cannot be contaminated), and the SEAM (one bounds math, Core-only blackboard, no
Dungeons reference from the camera).

## Honest limits

It never runs the camera. It cannot tell you whether 1.25 s / 70 deg/s actually stops the fight, whether
2.4 m reads as "tight" or as "in my back", or whether the facing lead feels like focus or drift. **Only
your felt-pass closes this.**

## Owner pin

Walk `dg_ember_deep` with F8 capture on. Every number above is one edit in one file.
