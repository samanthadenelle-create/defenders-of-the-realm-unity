# PROD-019 RESULT — Knight heater seat locked

**Status:** DONE 2026-08-30 (owner: "we HAVE it" / "100%" / persist Play)

## What was wrong

Not attach-fail. Addressable `gear/weapon/ShieldWithItemLogic` attached. Three extra writers still ran on `fullOverride`, then `ApplyHoldPose` restamped attach locals every frame, and `GearLoadout.Refresh` → `HandleGearChanged` re-entered `EquipOffHand` while the Addressable was in flight (bumped generation, rebuilt the prop).

## Locked seat (Offset Forge `ShieldWithItemLogic`, both copies)

| | |
|---|---|
| parent | `EquipmentProp_OffHand` under `Socket_Shield` (LeftLowerArm). Mesh child is `EquipmentProp_OffHand_Mesh`. |
| pos | (-0.103, 0.164, -0.238) |
| rot | (1.915, -48.302, -127.941) |
| scale | 0.71 |
| fullOverride | true |

## What holds it

- Attach: no `NormalizeInto`, no `vis.gripPos` add, no `ApplyGlobalWeaponYaw`, no snap/off-bone.
- HoldPose restamps the captured Offset Forge locals every frame.
- Same-id in-flight Addressable skips so body-wire Refresh cannot rebuild.
- `AttachmentOffsetRegression` fails if those numbers move.

## Proof

- Isolated `debug.txt`: `idempotent skip … IN-FLIGHT Addressable (generation=1)` then one `AttachOffHandProp`.
- Persist Play: Inspector stayed on the row after stop/start.
- Owner back + front shots; "we HAVE it" / "100%".
