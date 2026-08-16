# RESULT — WO-1107 build mode's right edge never fit the Seeker

**Date:** 2026-08-16  **Seat:** CLI (commit `8e7ce0090`)
**Status:** DONE — pending PO felt-verify

Owner F8 seq 2503 (*"the done should match same style and stack above defense and town button"*) and her
approval *"Get it done. Do it that way."*

## What shipped

- **MEASURED:** the right-edge column claimed **1080 ref px** of a canvas that is only **965.4** tall at
  the Seeker's 2670×1200 — over by ~115 px — so Done overlapped the Town tab, and **had since before
  today** (the old 76 px corner plate did it in a narrower sliver).
- **Fixed by BOTH changes, and both were required:** the D14 verb rail laid out **HORIZONTALLY**
  (band 132×384 → 384×132, y 114..246, reading order `[OK][Rot][X]` preserved) **AND** quick-tab height
  132 → **112** (the MinTouch floor).
  ⚠ The rail move ALONE was insufficient — with 132 px tabs the binding tenant becomes the CAROUSEL DOCK
  (98..401), leaving Done 17.6 px over. Both, or neither. Now **923 required vs 941.4 available =
  42.4 px headroom**; the clamp never fires at 2670×1200.
- **Done is now `ElarionUiKit.BuildObsidianButton`** (Style1/Yellow, matching the tabs it stacks with)
  instead of a hand-rolled 76 px gilt plate — 260×112 at the touch floor, keeping the `CloseButton` name
  and the exit wiring.
- **Deleted `BuildHudController`'s hand-copied `QuickTabStackTopPx`** — it now reads `BuildPaletteUI`'s
  published value. Duplicated geometry is how the two files disagreed about one column.
- The first-run hint seats beside or above the row by **measured** canvas width; `BuildPaletteUI`'s
  band-math comment, which reasoned against an assumed 1080-tall canvas, now warns never to seat off 1080.
- Source is tagged `COLUMN-FIT 2026-08-16` — grep that token.

## Deliberately NOT done — recorded, not hidden

- ⚠ **Ultrawide DESKTOP 21:9 still overflows** (2560×1080 → 935.3 available, 3440×1440 → 931.7) by
  ~12–15 px and the clamp WILL fire there. **Not a shipping target.**

## Verification

Gate green after the fact: `Builds/data-regression-wave14.log` (2026-08-16 17:42) —
`183/187 registered suites green`, 4 known-red baselines, **none in this lane**.

## Owner decision left open

None. Felt-verify only: on the Seeker, Done must sit above the Defense/Town quick tabs in the same
Obsidian style, with nothing clipped at the right edge.
