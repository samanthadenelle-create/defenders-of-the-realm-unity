# WO-1401: Hero Skill Tree - the three quick-swap slot buttons paint over the rail's own hint line

**Status:** IN PROGRESS 2026-09-05 - minted from the 05:13 UI_CAPTURE geometry audit; edit-only lane in flight

Lane: `lane/skilltree-geo` (worktree of D:\eoa at 9b47c9ad9). Pre-existing: `Assets/_Modules/Village/Talents/HeroSkillTreePanelMvvm.cs` last commit 3b3f28354 (2026-09-03 14:46). Found by the harness, not by eye: `Builds/ui-capture.log` (2026-09-05 05:13) reads `UI_CAPTURE_OK 91` AND `UI_GEOMETRY_FAIL x9 over 91 canvases` AND `UI_TOUCH_FAIL x9 over 91 panels (88 clean)` - all nine findings are this one panel at its three capture aspects, and they are the SAME defect counted twice (the touch tally classifies a `BUTTON OVER TEXT` prefix as touch debt, `UICaptureLaunch.cs:5682`).

## Evidence (verbatim from `Builds/ui-capture.log`, lines 20996-21102; the rects are ROOT-canvas reference px)
```
[UICap-GEO] BUTTON OVER TEXT [HeroSkillTree_1920x1080 @1920x1080] 'ObsidianPanel/PanelContent/TalentWorkspace/QuickSwapRail/ObsBtn_1
EMPTY' (x -214..-102, y -374.8..-262.8) covers 'ObsidianPanel/PanelContent/TalentWorkspace/QuickSwapRail/QuickSwapHint' ("Select an owned skill, then tap a slot (1-3).") (x -707.6..707.6, y -271.8..-242.8) by 112x9 ref px.
[UICap-GEO] BUTTON OVER TEXT [HeroSkillTree_1920x1080 @1920x1080] 'ObsidianPanel/PanelContent/TalentWorkspace/QuickSwapRail/ObsBtn_2
EMPTY' (x -56..56, y -374.8..-262.8) covers 'ObsidianPanel/PanelContent/TalentWorkspace/QuickSwapRail/QuickSwapHint' ("Select an owned skill, then tap a slot (1-3).") (x -707.6..707.6, y -271.8..-242.8) by 112x9 ref px.
[UICap-GEO] BUTTON OVER TEXT [HeroSkillTree_1920x1080 @1920x1080] 'ObsidianPanel/PanelContent/TalentWorkspace/QuickSwapRail/ObsBtn_3
EMPTY' (x 102..214, y -374.8..-262.8) covers 'ObsidianPanel/PanelContent/TalentWorkspace/QuickSwapRail/QuickSwapHint' ("Select an owned skill, then tap a slot (1-3).") (x -707.6..707.6, y -271.8..-242.8) by 112x9 ref px.
[UICap-GEO] BUTTON OVER TEXT [HeroSkillTree_2340x1080 @2340x1080] 'ObsidianPanel/PanelContent/TalentWorkspace/QuickSwapRail/ObsBtn_1
EMPTY' (x -214..-102, y -338.2..-226.2) covers 'ObsidianPanel/PanelContent/TalentWorkspace/QuickSwapRail/QuickSwapHint' ("Select an owned skill, then tap a slot (1-3).") (x -781.2..781.2, y -235.2..-206.2) by 112x9 ref px.
[UICap-GEO] BUTTON OVER TEXT [HeroSkillTree_2340x1080 @2340x1080] 'ObsidianPanel/PanelContent/TalentWorkspace/QuickSwapRail/ObsBtn_2
EMPTY' (x -56..56, y -338.2..-226.2) covers 'ObsidianPanel/PanelContent/TalentWorkspace/QuickSwapRail/QuickSwapHint' ("Select an owned skill, then tap a slot (1-3).") (x -781.2..781.2, y -235.2..-206.2) by 112x9 ref px.
[UICap-GEO] BUTTON OVER TEXT [HeroSkillTree_2340x1080 @2340x1080] 'ObsidianPanel/PanelContent/TalentWorkspace/QuickSwapRail/ObsBtn_3
EMPTY' (x 102..214, y -338.2..-226.2) covers 'ObsidianPanel/PanelContent/TalentWorkspace/QuickSwapRail/QuickSwapHint' ("Select an owned skill, then tap a slot (1-3).") (x -781.2..781.2, y -235.2..-206.2) by 112x9 ref px.
[UICap-GEO] BUTTON OVER TEXT [HeroSkillTree_2670x1200 @2670x1200] 'ObsidianPanel/PanelContent/TalentWorkspace/QuickSwapRail/ObsBtn_1
EMPTY' (x -214..-102, y -333.5..-221.5) covers 'ObsidianPanel/PanelContent/TalentWorkspace/QuickSwapRail/QuickSwapHint' ("Select an owned skill, then tap a slot (1-3).") (x -791.6..791.6, y -230.6..-201.5) by 112x9 ref px.
[UICap-GEO] BUTTON OVER TEXT [HeroSkillTree_2670x1200 @2670x1200] 'ObsidianPanel/PanelContent/TalentWorkspace/QuickSwapRail/ObsBtn_2
EMPTY' (x -56..56, y -333.5..-221.5) covers 'ObsidianPanel/PanelContent/TalentWorkspace/QuickSwapRail/QuickSwapHint' ("Select an owned skill, then tap a slot (1-3).") (x -791.6..791.6, y -230.6..-201.5) by 112x9 ref px.
[UICap-GEO] BUTTON OVER TEXT [HeroSkillTree_2670x1200 @2670x1200] 'ObsidianPanel/PanelContent/TalentWorkspace/QuickSwapRail/ObsBtn_3
EMPTY' (x 102..214, y -333.5..-221.5) covers 'ObsidianPanel/PanelContent/TalentWorkspace/QuickSwapRail/QuickSwapHint' ("Select an owned skill, then tap a slot (1-3).") (x -791.6..791.6, y -230.6..-201.5) by 112x9 ref px.
UI_GEOMETRY_FAIL x9 over 91 canvases -- see the [UICap-GEO] lines above; each names the panel, the element and the numbers.
UI_TOUCH_FAIL x9 over 91 panels (88 clean) -- each line names the panel, the control and the numbers. Author the band above the floor; do not rely on the clamp.
```
The overlap is the SAME 112x9 at every aspect. That is the signature of a fixed-pixel band colliding with a FRACTION band: only the hint's fraction changes with the rail's height, and the rail's height is a constant.

## The code path (read at source this session)
- `HeroSkillTreePanelMvvm.cs:2200-2229 BuildQuickSwapBar`: the rail (`QuickSwapRail`, a `HorizontalLayoutGroup`) is pinned bottom-anchored at `offsetMin.y = BodyPadPx + BandGapPx` (14) and `offsetMax.y = ... + AbilityRowPx` (14 + 132) -> the rail is **132 ref px tall**. Layout: `childControlHeight = true`, `childForceExpandHeight = false`, `childAlignment = LowerCenter`, `padding.top = 34`.
- `:2270-2274 RenderQuickSwapBar`: each slot button gets `LayoutElement min/preferred = ElarionUiKit.MinTouchPx` (112). With Lower alignment the group seats a 112-tall child at the rail's BOTTOM regardless of padding, so **the slots own y 0..112 of the rail** (log: -374.8..-262.8 = 112 tall).
- `:2220-2228`: the hint is `ElarionUiKit.Label(rail, ..., y0: 0.78f, y1: 1f, ...)` with `LayoutElement.ignoreLayout = true` - a **FRACTION** of the rail: 0.78 x 132 = 103 -> the hint owns y **103..132** (log: -271.8..-242.8 = 29 tall). 103 < 112, so the hint's bottom 9 px lie inside the slot band. `112 x 9` is exactly `LayoutOracle.cs:143-159` measuring `Overlaps(br, tr, OverlapPadPx=2)`.
- `:1988`: the graph well's floor is `AbilityRowPx + BandGapPx * 3` (156) - 10 px above the rail's top (146); any growth of the rail has to raise it or the nodes (Buttons) will cover the hint next.
- This is the exact WO-841/852/865 failure class the file's own header (`:93-114`) forbids: a text band expressed as a fraction of a parent whose other band is fixed px.

## What the player experiences
The three round slot bezels paint over the bottom of the sentence that tells them what the slots are for ("Select an owned skill, then tap a slot (1-3)." and, after a learn, "<name> learned - assign it to a numbered quick-swap slot."). The descenders/bottom of the hint disappear behind ObsBtn_1..3 at every phone aspect, so the one line that teaches the equip loop is the one line that is clipped. Also the harness's `UI_GEOMETRY_FAIL` / `UI_TOUCH_FAIL` stay red on a panel that is otherwise clean, hiding the next real finding.

## Fix shape - bands disjoint BY CONSTRUCTION inside TalentWorkspace
- New fixed-px constants on the view (public, so `SkillsPanelLayoutRegression.ReadLayout` can read them):
  `QuickSwapSlotBandPx = ElarionUiKit.MinTouchPx` (the slot's own LayoutElement size),
  `QuickSwapHintBandPx = 40` (one TMP line box at the FontFloor 30 x 1.25 = 37.5, FontMicro 32 fits),
  `QuickSwapRailPx = slot + BandGapPx + hint` (160), `QuickSwapRailBottomPx = BodyPadPx + BandGapPx`,
  `GraphWellFloorPx = rail bottom + rail + BandGapPx` (182).
- The rail is `QuickSwapRailPx` tall; the hint is pinned FROM THE TOP with the existing `PinBandFromTop(hint, 0, QuickSwapHintBandPx)` (fixed px, never a fraction); the slots stay bottom-seated by the layout group, whose `padding.top = hint + gap` now states the same band arithmetic. Slot [0..112] and hint [120..160] cannot intersect for ANY rail width or aspect.
- The graph well floor moves to `GraphWellFloorPx` so the grown rail never sits under a node plate (the graph is a ScrollRect; it scrolls, nothing is lost).
- The rail-host + hint construction is factored into one `public static` builder and the slot sizing into one `public static` helper, so the regression measures THE view's construction, not a copy of it. Kit primitives only (`ElarionUiKit.Label`, `BuildObsidianButton`); the VM is untouched (MVVM - geometry is the View's); ASCII-only added lines; `MinTouchPx` respected (slot band IS the floor); `FlowTrace.Step("SkillTree", ...)` states the resolved bands once at build.

## Acceptance
- [ ] `RunCaptureHeadless` on a fresh log: `UI_CAPTURE_OK` with ZERO `[UICap-GEO]` lines naming `HeroSkillTree_*`, and the `UI_TOUCH_*` tally no longer lists the panel (91 canvases, 9 fewer findings).
- [ ] `SkillsPanelLayoutRegression` gains case 7 `[rail]` - a REAL-geometry pin: it builds the view's own rail host at the reference body, seats three slot buttons through the view's own sizing helper, settles layout, runs `LayoutOracle.Audit` and asserts (a) no `BUTTON OVER TEXT` / `BUTTONS OVERLAP` finding, (b) every slot rect clears the hint rect by `>= BandGapPx`, (c) a text placed at the graph well's floor is clear of the rail, (d) the constants' arithmetic holds. **RED first**: reverting the hint pin to `PinBandFromTop(hintRt, QuickSwapHintBandPx + BandGapPx, QuickSwapHintBandPx)` (drops the hint into the slot band) must fail the case.
- [ ] `REGRESSION_OK <n>/<n>` unchanged in count (a new CASE inside an existing suite, not a new suite); `COMPILE_GATE_OK`.
- [ ] Owner felt-check on the Seeker: open the Talent Tree; the hint sentence is fully readable above the three slots.

## Not in scope
- The hint's WORDING (VM `QuickSwapStatus`), the slot count (3), the bezel art, node lattice, spend popup (WO-1342), the WISDOM chip.
- `SkillsPanelLayoutRegression` case 2's stale "full-bleed graph body" arithmetic (it certifies a 481 px well the panel no longer has) - flagged here, not rewritten in this lane.
- Any change to `LayoutOracle` / the capture harness; the oracle was right.
