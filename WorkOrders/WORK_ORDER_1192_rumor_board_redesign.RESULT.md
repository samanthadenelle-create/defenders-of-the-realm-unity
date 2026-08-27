# WORK ORDER 1192 - RESULT: Previous control (owner bounce 2026-08-27)

**Status:** LANDED in tree, not committed. Owner felt-test still required. WO Status left READY TO IMPLEMENT (do not close).

Owner felt-test 2026-08-27 Needs Work: **"A previous button would be nice."** This pass does **not** redo the Night Market / full visual redesign. It adds a real Previous control to the v3 poster board.

## What landed

- `RumorBoardPanel` head row is now **Previous | Next | Close**. Previous is an `ElarionUiKit.BuildObsidianButton` labelled `"Previous"` (full word, ASCII), host height `HeadBandPx` (120) and width from `PageButtonWidthPx` (glyph-measured at FontBody, divided by the kit's 0.04..0.96 inset, 10% bold slack, floored at `MinTouchPx` 112). Title's right edge insets by that host plus two `HeadGapPx` so it never paints through the new face.
- `RumorBoardVM.PrevPage()` steps one page of three **backward and WRAPS** (`(_pageIndex - 1 + pages) % pages`), the keep-going pair of `NextPage`.
- Swipe matches the new face: left = Next, right = Previous, both wrap.
- `RumorBoardLayoutRegression` case 5 `[previous]`: the control exists, is wired to `PrevPage`, is `>= MinTouchPx` on both axes, and `'Previous'` MEASURES inside the host inner width (so FitSingleLine cannot ellipsis it to `Pr...`). Portrait and landscape aspects. Head-row / zero-overlap cases now include the Previous band. No LayoutOracle allow-list entry. No capture-harness layout.

`RumorBoardPanelBootstrap` was not touched (opener already registered). No quest illustration art. No Unity run, no commit.

## How Previous behaves

- Always visible in the head row, left of **Next >**, same yellow obsidian style.
- Tap **Previous** -> `RumorBoardVM.PrevPage()`: show the prior window of three rumors. On page 0 it wraps to the last page. On a one-page board it is a no-op (stays on page 0), same as Next.
- Swipe right across the posters makes the same trip. Swipe left still goes Next.
- The letter overlay's **Back** is unchanged (closes the letter on that poster; it is not this control).
- Close stays the kit's shared labelled Close, re-seated, canonical box.

## Evidence

- Brace balance + ASCII + no NUL on every touched `.cs`:
  - `Assets/_Modules/Village/Hero/RumorBoardPanel.cs` (47/47)
  - `Assets/_Modules/Village/Hero/RumorBoardVM.cs` (45/45)
  - `Assets/Editor/Regression/RumorBoardLayoutRegression.cs` (87/87)
- Unity / `COMPILE_GATE_OK` / `RUMOR_BOARD_LAYOUT_OK` **not run** (this bounce forbade Unity).

## Still open - do not close WO-1192

Owner felt-verifies Previous on device (word fully visible, tap goes back, wrap feels right) and CLOSES. The rest of the v3 redesign (posters / Next / Close / letter overlay) is already in tree from the prior pass.
