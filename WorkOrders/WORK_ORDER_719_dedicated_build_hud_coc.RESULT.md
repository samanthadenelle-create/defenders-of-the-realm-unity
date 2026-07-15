# WO-719 — Dedicated Build HUD (CoC) — RESULT

**Status:** IMPLEMENTED (overnight 2026-07-15) · felt-verify pending (owner). **Commit:** `96a9cf18`. Branch `wip/village2-and-f8-tickets`.

## What landed (exceeded the overnight stretch target)
Reconciled Grok-03 guidance + internal SME map + owner rulings into one CLI-owned plan (`docs/BUILD_HUD_RECONCILED_SPEC_2026-07-14.md`), then built it:

- **`BuildHudController.cs` (NEW)** — one **landscape** ElarionUiKit canvas (1920x1080) that OWNS/orchestrates the build chrome: `BuildWalletRow`, "BUILD MODE" label, top-right Exit, and the **single PLACE intent bar** (Rotate L / Rotate R / PLACE / Cancel — shown only when armed/moving). Three states: Browse / Placing / Selected.
- **Single intent bar — dual rotate REMOVED.** The duplicate Rotate that lived in both `BuildPlaceButton` and the `LeanTouchBuildDriver` verb bar is gone; one bar family now.
- **`BuildWalletRow.cs` (NEW)** — all pools (Wood/Iron/Food/Crystals/Gold) via `ElarionUi.CompactNumber`, subscribes `GameStateService.ResourcesChanged` + `EconomyService.OnChanged`. Replaces the old crystals-only header.
- **`BuildTabRow.cs` (NEW)** — kit Town/Defenses/Walls tabs (Walls gated by `FeatureFlags.WallsTab`), gold-underline active tell (position/shape, not color-only).
- **LeanTouch camera** — `BuildModeController` got `_camYaw` + `PanFocusBy`/`AdjustZoom`/`AdjustYaw` (45deg snap); `LeanTouchBuildDriver.Update` now drives those (pinch=zoom, twist=rotate view) instead of writing the camera transform (which the controller overwrote every frame — the old gestures were dead).
- **Backup d-pad** re-hosted **bottom-left** (GO name "BuildDPad" preserved for probes); publishes `HudMoveInput`.
- **Carousel enlarged for phone** (owner felt-test) — tiles 160→260px, dock 1280x300→1560x440.
- Panels kept near-black (WO-562, owner confirmed). ASCII-only. Placement/grid/economy/save behavior unchanged (BuildModeController stays the brain).

## Acceptance
- Dual-rotate gone: **YES.** Wallet chips (multi-pool): **YES.** One canvas owner: **YES.** Landscape (owner ruling): **YES.**
- `COMPILE_GATE_OK` (clean first try). No new DataRegression failures attributable to this lane.
- **Deviation (honest, CLI call):** kept the tab-row + card carousel inside `BuildPaletteUI`'s own canvas (probe-verified) rather than physically re-parenting every overlay canvas under one GameObject — `BuildHudController` is the single OWNER/orchestrator; full re-parent was high-regression risk on a clean lane. Structure-Info sheet (deferred-arm) NOT built (kept immediate-arm). Orient button left dev-gated.

## Verify
Live preview: https://defenders-of-the-realm-v2-er71p62s5.vercel.app · desktop exe `Builds\Windows\DefendersOfTheRealm.exe`. Owner felt-pass via `UI_REVIEW/PAIRWALK_716.md`.
Probes to run on next fleet: `AssertTutorialFirstTower`, `AssertBuildMoveChain` (DPAD link), `AssertTouchVerbBarRenderable`.
