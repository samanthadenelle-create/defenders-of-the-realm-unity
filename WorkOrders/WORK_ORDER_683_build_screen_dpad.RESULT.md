# RESULT — WO-683: build-screen d-pad + text rotate labels

**Status: IMPLEMENTED + GATED. Fleet DPAD probe verdict + owner felt-pass ON A PHONE pending
(PO closes).** Commit `c963a553` (build-mode lane, shared with WO-677).

## What shipped (owner rulings 2026-07-12, verbatim in the WO)

- **Lane A — the SAME kit d-pad as the combat/friendly HUD shows in build mode.** Re-hosted on
  the WO-677 verb-bar overlay canvas (`LeanTouchBuildDriver.EnsureBuilt` builds
  `ElarionUiKit.BuildVirtualDPad` — the identical Core kit builder the combat HUD uses; GO
  `"BuildDPad"`, left side at anchor (0.11, 0.60), clear of joystick zone/palette/PLACE).
  Chosen over exempting it from `BuildModeHudBridge`'s hide because that bridge fades the WHOLE
  HUD root. Touch-only by construction (desktop never installs the driver).
- **Lane B — d-pad moves the asset.** The pad publishes into `HudMoveInput.Set` (reflection,
  cached, warn-on-miss) and `BuildModeController` reads `HudMoveInput.Move` (the HeroLocomotion
  loose-reflection precedent) merged into the arrow-key move vector at the single :2049 merge
  point — dead-zone 0.18 per `docs/audit/input-controls.md` §3.1. D-pad = arrow keys for the
  armed ghost AND the in-progress move. `PlaceConfirmedThisFrame` order untouched.
- **Lane C — text labels.** Verb bar: "Rotate Left" / "Rotate Right" (ASCII — the ⟲/⟳ glyphs
  rendered as tofu `□` on device, owner screenshot `Desktop\a pic.png`). Palette targeting chips
  fixed in the same pass: "Land only" / "Land + Air" / "Air only" (glyph prefix removed).
- **Lane D — fleet probe.** `AssertBuildMoveChain` gains the DPAD link: arm → point → baseline
  `ProbeArmedGhostCell` → publish `HudMoveInput.Set(up/down)` → assert ghost cell changed →
  cancel. Fail lines name the dead link.

## Verification

- `COMPILE_GATE_OK` + DataRegression at baseline (3 known pre-existers, zero new).
- Brace/NUL green on all four files (LeanTouchBuildDriver 38/38, BuildModeController 333/333,
  BuildPaletteUI 50/50, AutoPilotDriver 1234/1234).
- Fleet run (4 bots, seeds 8200, exe 2026-07-12 evening) in flight at RESULT time — DPAD link
  verdict lands in `Builds/autopilot-tickets.md`; append here on harvest.
- Owner felt-checks on the new preview: d-pad visible in build mode (same chrome as combat HUD),
  pressing it moves the armed structure, labels read as text at mobile aspect.
