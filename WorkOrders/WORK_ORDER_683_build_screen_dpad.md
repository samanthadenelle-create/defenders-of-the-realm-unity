**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

# WORK ORDER 683 — Build-screen D-pad: the HUD kit d-pad shows in build mode and moves the asset

**Status: READY TO IMPLEMENT** (owner rulings 2026-07-12 evening, verbatim: "we need the dpad from
the combat and friendly hud to show on the build and logic that connects the arrowpad to moving the
asset. and [the] square symbol rotate [buttons] need to be rotate left and rotate right or simply
right and left." Earlier same day: "the virtual dpad is only for the build screen"; "demo is
unplayable without it"; "that was the largest issue").
**Lane:** Build Mode / Input / HUD kit (touch verb surface). **Type:** NEW FEATURE (specced in
`docs/audit/input-controls.md` 2026-05-19, never built for build mode) — routed as a WO per §13.
**Priority: P0 for the mobile demo** (post player-defined-map pivot, build mode IS the demo; the
Pi target is mobile web).

## What the owner ruled (the three requirements)

1. **REUSE, don't reinvent:** the d-pad that already exists in the combat/friendly (town) HUD —
   the kit d-pad cross (WO-611 combat HUD v8) — must ALSO show on the build screen. Same
   component, same chrome. No new bespoke d-pad.
2. **Connect the arrowpad to moving the asset:** d-pad direction presses move the armed ghost /
   the structure being moved — the touch equivalent of the desktop arrow-key nudge.
3. **Rotate buttons become TEXT:** the current verb-bar labels "⟲ Rotate" / "Rotate ⟳" read as
   "square symbol" (glyphs don't render/read — and the colorblind rule applies: meaning by
   text/shape, never by glyph alone). Relabel to **"Rotate Left" / "Rotate Right"** (or simply
   **"Left" / "Right"** if width demands) — owner accepts either.

## Verified seams (read from source 2026-07-12 — extend these, don't greenfield)

- **The kit d-pad publishes `HudMoveInput.Move`** (`Assets/_Modules/HUD/Kit/HudMoveInput.cs` —
  "the kit's movement-input static (replaces VirtualDPadLean.Move)"), consumed cross-asmdef by
  reflection (the established HeroLocomotion pattern). Build mode can read the SAME static via the
  SAME loose-reflection pattern — no HUD↔Village asmdef edge (§5 law).
- **Build mode's existing arrow/WASD move read:** `BuildModeController.cs:~2049`
  (`kb.aKey.isPressed || kb.leftArrowKey.isPressed → move.x -= 1f` …). The d-pad vector is added
  into THIS same move vector — one merge point, desktop behavior unchanged.
- **Move verb entry:** `BeginMoveSelected` (:1574), wired from `BuildSelectionUI.OnMoveRequested`
  (:2269); probe seam `ProbeBeginMoveSelected` (:186) already exists for the fleet.
- **Verb bar:** `LeanTouchBuildDriver.EnsureBuilt()` (code-built uGUI, WO-677 rebuild) owns the
  ⟲/⟳/Cancel stack — the label change is here.
- **HUD show/hide in build:** `BuildModeHudBridge` hides combat HUD while building — the d-pad
  must be exempted (or re-hosted on the build canvas) so it SHOWS in build mode; verify which
  canvas owns the kit d-pad and how the bridge hides it before choosing.

## The fix (bounded)

- **Lane A — d-pad visible in build mode:** whichever is cleaner after reading the hide path:
  (1) exempt the kit d-pad from the build-mode HUD hide, or (2) instantiate the same kit d-pad
  component on the build overlay canvas. Same art/chrome as combat/town — the owner's ruling is
  it IS that d-pad.
- **Lane B — d-pad drives the asset:** in build mode, `HudMoveInput.Move` (read via the
  established reflection pattern) merges into the :2049 move vector — moves the armed ghost and
  the in-progress move identically to arrow keys. Dead-zone/sensitivity defaults per
  `docs/audit/input-controls.md` §3 (inner 0.18, curve t^1.6) where applicable to the kit pad.
- **Lane C — labels:** "⟲ Rotate"/"Rotate ⟳" → "Rotate Left"/"Rotate Right" (fallback "Left"/
  "Right" if the bar width forces it at narrow aspect). ASCII-only TMP glyph rule applies
  (landmine list: no glyph-only buttons).
  **Evidence (owner screenshot 2026-07-12, `Desktop\a pic.png`):** device shows `□ Rotate` /
  `Rotate □` — the ⟲/⟳ glyphs render as tofu boxes on the shipped TMP font. SAME defect on the
  palette cards' targeting chips: `□ Land + Air` (Ballista + Arcane Spire) — fix those chips'
  glyph to text/ASCII in the same pass. The shot also confirms the WO-677 uGUI verb bar renders
  on device (bar + Cancel + palette all visible) and that NO d-pad exists on the build screen.
- **Lane D — probe:** extend the WO-677 Lane-D fleet probe: enter build → arm → drive
  `HudMoveInput.Move` seam → assert ghost cell changed → move-commit → assert record moved.

## Acceptance

- [ ] Mobile web (device): build mode shows the SAME d-pad as combat/town HUD; pressing it moves
      the armed ghost / moving structure; PLACE commits at the nudged cell.
- [ ] Desktop: arrow keys unchanged; d-pad ignored when not touch (kit pad's own gating).
- [ ] Verb bar reads "Rotate Left" / "Rotate Right" in TEXT at both 16:9 and narrow aspect;
      Done/Cancel/PLACE all remain tappable (WO-677 Lane C seating preserved).
- [ ] Fleet probe green + `COMPILE_GATE_OK`; owner felt-pass ON A PHONE closes (PO closes, §13).

## What NOT to touch

- `PlaceConfirmedThisFrame` consumption order (07-12 fix, proven working in the db captures).
- The WO-677 uGUI verb-bar rebuild (extend labels only).
- `HudMoveInput`'s hero-movement consumers (HeroLocomotion path untouched).

*Cross-refs:* `docs/audit/input-controls.md` (the owner-directed control spec — sizing/dead-zone/
curve law) · WO-677 (mobile build-mode verbs) · WO-611 (combat HUD v8 d-pad cross) · WO-673 L5
(45° stepped rotation, both directions).

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
