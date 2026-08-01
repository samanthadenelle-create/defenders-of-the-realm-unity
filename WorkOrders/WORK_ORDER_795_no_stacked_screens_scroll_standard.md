# WORK ORDER 795 — UI standard: screens never stack; scroll when content exceeds

**Status: PARTIALLY SHIPPED — wave 1 (4461f9ee), wave 2 (583bc0ac), modal truce (8ba7154a), capture coverage (749914b1). Remaining panels of the 16-panel audit READY.**
> ⚠ Cross-reference 2026-08-01: overlaps WO-779 (55-screen UI spacing/layout conformance sweep, not yet run) — reconcile 779's rubric with these shipped waves + `docs/qa/UI_REVIEW_2026-08-01.md` findings before further panel work.
**Origin:** owner F8 seq 466, 2026-07-30 19:02 (Main_Castle_Overworld): "all screens should
never stack. Need to all be seen, scrollable if needed"
**Silo:** UI / HudKit + Obsidian panels
**Type:** standard + audit (owner-directed UX law, generalizes the WO-787a login unstack)

## The rule (BINDING for all panels once implemented)

1. No panel/screen content may overlap another element of the same panel, and no two
   modal panels may render stacked on top of each other. One modal at a time —
   PanelManager already tracks open panels; opening a second modal must close or defer
   the first (never draw over it).
2. When a panel's content exceeds its body rect, the body becomes a vertical
   scroll region (code-built ScrollRect — UXML does not work in builds). Content is
   never clipped, squeezed to overlap, or pushed off-panel.
3. Mobile-contrast standard still applies (black panels, MinTouchPx=112): scroll
   affordance = a thin parchment scrollbar + fade hint at the cut edge.

## Scope

- Audit every code-built Obsidian panel (BuildObsidianPanel consumers) for
  (a) body content taller than the body rect at 2340x1080 and 1920x1080,
  (b) any second-modal-over-modal path (PanelManager open while IsOpen).
- Add a shared scroll-body helper to the Obsidian kit (one implementation, all panels
  opt in) rather than per-panel ScrollRects.
- Wire the two capture resolutions into UICaptureLaunch for any panel found over-tall,
  so the screenshot gate proves fit.

## Acceptance

- [ ] No panel renders overlapping rows at either capture resolution (screenshot-verified).
- [ ] A deliberately over-stuffed test panel scrolls instead of stacking.
- [ ] Opening panel B while modal panel A is open never draws B over A.
- [ ] UiObsidianConformanceRegression gains a stacking/fit oracle (fails on regressions).

## Do NOT touch

- HUD posture rows / hud-areas.json (WO-778 lane), login panel (already unstacked, WO-787a).
