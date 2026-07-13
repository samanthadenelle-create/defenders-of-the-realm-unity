# RESULT — WO-677: mobile build-mode Move/Sell unreachable (touch verb bar)

**Status: IMPLEMENTED + DEVICE-CONFIRMED RENDERING. Owner felt-pass of the full Move flow
pending (PO closes).** Commit `c963a553` (shared lane with WO-683).

## Root cause — CONFIRMED (matches the spec's suspect #4)

The touch verb bar (Rotate pair + Cancel) was a UIToolkit `UIDocument` adopting a sibling's
PanelSettings — the one UI class banned on web builds. Rebuilt as **code-built uGUI** on its own
Screen-Space-Overlay canvas (the proven BuildPlaceButton pattern); `AdoptPanelSettings` and the
UIDocument dependency deleted. Buttons register via GraphicRaycaster so `finger.IsOverGui` still
suppresses world taps over the bar.

## Also shipped (spec lanes B/D)

- Idle select-loop step-in/out traces (raycast miss / non-structure hit / select all name
  themselves) — the "tap on my tower does nothing" class is no longer silently uncapturable.
- `RequestUiCancel()` latch + `ProbeBeginMoveSelected()` probe seams; idle-latch drop guard.
- Fleet probe drives the real path: arm → place via UI latch → cancel via bar seam → idle →
  select → move → commit.

## Verification

- `COMPILE_GATE_OK`; db-proven working on device: owner screenshot 2026-07-12 shows the bar +
  Cancel rendering, and WebTrace sessions show healthy chains
  (`Armed placement → finger tap → PlaceConfirm: UI PLACE button latch consumed → Place()`).
- Label text superseded same-day by WO-683 Lane C ("Rotate Left"/"Rotate Right").
