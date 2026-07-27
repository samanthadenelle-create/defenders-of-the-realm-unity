# WO-779 — UI spacing / layout conformance sweep (kill the overlap/clip/truncation class)

**Status:** READY TO IMPLEMENT
**Minted:** 2026-07-26 (owner-requested; CLI-authored spec)
**Lane:** UI conformance (broad — ElarionUiKit + all panels). **Dispatch AFTER WO-778 queue UX lands** (both touch the queue HUD + layout.body; do not run concurrently). One careful lane, single agent.
**Anchor:** memories `build-hud-mobile-design`, `mobile-ui-touch-contrast-standard`, `headless-screenshot-verify-ui-before-build`; existing `UiObsidianConformanceRegression` / `UiMvvmConformanceRegression`.

## Why
The same UI defect keeps recurring one panel at a time — recent examples: Echo card flavor text flooding the lane picker (`c7d419b5`), pet roster text stacking on the FrameCore chrome (`9e60f842`), the WO-773 queue HUD clipping when parented to `chrome.content` with a fixed line budget. Root class: **content parented to chrome (title/Close band) instead of `layout.body`, fixed-height lists that clip instead of scroll, text with no wrap/ellipsis, and touch targets below the mobile minimum.** Fixing them individually is whack-a-mole. This WO makes conformance STRUCTURAL: audit every panel, fix to one discipline, and add a ratchet oracle so regressions fail the gate.

## Scope
1. **Audit** every code-built panel (ElarionUiKit `Frame*` masters + every screen/panel/HUD under `Assets/_Modules/**/UI`, `**/Hero`, `**/HUD`, `**/BuildMode`) for the defect class:
   - content parented to `chrome.content`/root instead of `layout.body` (collides with the title/Close chrome band);
   - fixed-count / fixed-height lists that clip when items exceed the budget (must scroll);
   - labels without wrap or ellipsis that overflow their cell (the Echo-flavor / pet-roster class);
   - touch targets below `MinTouchPx` (112, mobile standard);
   - low-contrast faces (panels must stay black; green/red button faces legible) per `mobile-ui-touch-contrast-standard`.
2. **Fix to one discipline:** all scrollable content hosts `layout.body` (never chrome), lists scroll past their visible budget, labels wrap or ellipsize within their cell, interactive elements ≥ `MinTouchPx`. Prefer fixing in the ElarionUiKit primitives so every panel inherits the correct behavior, then per-panel where a panel bypasses the kit.
3. **Ratchet oracle:** add `UiSpacingConformanceRegression` (or extend `UiObsidianConformanceRegression`) wired into `DataRegression.RunAll` that scans View construction for the banned patterns (parenting to chrome for scroll content; fixed-line list builders without a scroll host; known-overflow label sites) and reports offenders, starting from a tracked baseline that only goes DOWN (like the MVVM ratchet). Emit `UI_SPACING_OK`.

## Acceptance (data + visual)
- `UI_SPACING_OK` in `DataRegression.RunAll`; offender count at or below baseline (ratchet).
- **Headless screenshot-verify** (memory `headless-screenshot-verify-ui-before-build`): capture the previously-broken panels (Echo lane picker, pet roster, WORK QUEUE, Barracks Train/Upgrade) + a representative sample; open the PNGs and confirm no title/Close overlap, no clip, no text overflow, touch targets sized.
- Felt (owner): the recurring overlap/clip is gone on device.

## Do NOT touch
- Binding UI law (§8 code-built, no uxml).
- The MVVM ratchet (separate oracle) — this is the SPACING/LAYOUT ratchet.
- Gameplay logic — presentation layer only (HP B2B: presentation never touches the objects).
- Run only AFTER WO-778 (queue UX) is committed, to avoid contending on the queue HUD.
