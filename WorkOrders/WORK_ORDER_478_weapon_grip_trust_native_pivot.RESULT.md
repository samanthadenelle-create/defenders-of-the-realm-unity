# WORK_ORDER_478 — RESULT

**Status:** VERIFIED (compile gate) — pending PO felt-verify on `knight_starter` / `sword_A`  
**Date:** 2026-07-05  
**Branch:** `wip/village2-and-f8-tickets` @ `C:\EoA`

## What changed

| File | Change |
|---|---|
| `Assets/_Modules/Core/FeatureFlags.cs` | Added `ff.weapongripinfer` (default **OFF**) — restores deprecated geometry inference when ON |
| `Assets/_Modules/Village/Hero/EquipmentController.cs` | WO-478 default: native melee → `SeatNative` + calibration nudge; inference gated behind flag |
| `WorkOrders/WORK_ORDER_435_weapon_grip_orientation.md` | Banner: **SUPERSEDED by WO-478** |
| `WorkOrders/WORK_ORDER_478_weapon_grip_trust_native_pivot.md` | Status → **IMPLEMENTED** |

## Behaviour

- **`knight_starter` / `sword_A` (native Blink):** `AttachLoadedProp` routes through `SeatNative` (trust grip-at-origin + scale). Rotation = `vis.gripEuler` × `MeleeGripNudge` — **not** `ComputeMeleeGripRotation`.
- **Non-native melee** (`sword_D`, `sword_F`, `staff_*`, etc.): unchanged geometry path (`NormalizeInto` + `SeatHiltLowerHalf` + `ComputeMeleeGripRotation`).
- **Legacy path:** `PlayerPrefs "ff.weapongripinfer" = 1` restores pre-WO-478 inference for native melee (marked DEPRECATED in code comments).

## Instrumentation (§12)

- `LogGripSeatDiagnostics` — dumps prop/gripRoot local pos/euler after seat
- `SeatHiltLowerHalf` / deprecated `SeatByHandle` — branch logging when inference runs
- Attach log line includes `trustNative=` and `infer=` flags

## Verification

| Gate | Result |
|---|---|
| Brace balance (`FeatureFlags.cs`, `EquipmentController.cs`) | OK |
| `DeNelle.Editor.CompileGate.Run` | `COMPILE_GATE_OK` (`Builds/compile-gate-wo478.log`) |

## PO felt-verify

1. Equip `knight_starter` on KnightV3 (`ff.knightv3` ON)
2. Hilt in palm, blade out — no 180° flip, no mid-hilt float
3. Compare with `ff.weapongripinfer=1` — should reproduce old wrong grip (rollback test)

## Rollback

`PlayerPrefs.SetInt("ff.weapongripinfer", 1)` restores the deprecated geometry inference path without reverting code.