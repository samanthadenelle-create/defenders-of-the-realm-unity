# WO-1419 RESULT - the Heart plate paints ember-medallion flame icons, not ASCII pips

**Status:** FIXED 2026-09-05 (Codex dev lane, gated by the CLI; device build follows after the owner's reboot)
**Owner ruling:** "i hate those [*] items when we should use some icon, we have over 4000" (2026-09-05 evening).

## What landed
- `Assets/_Modules/Core/State/HeartfireCharges.cs` - `FlameStates(charges, max)` -> clamped `bool[]` (the state model);
  `FlameRow` kept as the ASCII trace serializer only.
- `Assets/_Modules/HUD/Kit/HudKitController.cs` - Image slots (one per max charge) left of the unchanged word label,
  sprite `ItemIcons/cons_emberfire_bomb` (candidate A of the three-candidate survey; the CLI picked it against the
  greyscale gate - the ember medallion reads as a CHARGE TOKEN; the skull emblem was wrong tone, the fireball tile
  opaque). Lit = alpha 1.0; spent = alpha 0.25 AND neutral 0.55 fill - the states differ by fill, never hue alone.
  Repaint only on `force || countMoved`; the once-per-count trace remains.
- Oracles moved WITH the ruling: `HeartfireRegression.cs` bracket assertions now bind `FlameStates`;
  `HudLabelFitRegression` Case 10d measures three 26 px icons in the left band + `PlateLabel` in the right band.
- New `Assets/Editor/Regression/HeartfirePipsRegression.cs` (5 cases, RED recipes): `[no-ascii-pips-on-plate]`,
  `[slot-count]`, `[sprite-loads]`, `[states-differ-in-greyscale]`, `[plate-copy-unchanged]`; registered in
  `DataRegression.cs` beside the Heartfire suite.

## Evidence
- Codex hand-back (`batch_results_state.md:700`), applied three-way onto `9d1e7fb2a` clean; braces 23/23, 359/359,
  48/48, 188/188, 9/9; NUL 0; guid `946b14ed753f4378ad03edd331835b0c` unique.
- `COMPILE_GATE_OK` (`Builds/c5`, 22:22, 0 `error CS`); `REGRESSION_OK 386/386` (`Builds/r5`, 22:25) incl.
  `[heartfire-pips] HEARTFIRE_PIPS_OK`, `[heartfire]`, `[hud-label-fit]` green.
- `ADAPTIVE_HUD_CAPTURE_OK 9/9` (`Builds/caphud`, 22:27); `AdaptiveHudPeaceful_2670x1200.png` OPENED: three ember
  medallions + "Heartfire 3/3 (raids)" on the plate, the WO-1407 objective line above it, "Next wave in 14m 9s".

## Open for the owner
- Icon size: 26 px slots read small at 2670x1200; her call on the device (a one-constant change).
- Device felt-test closes.
