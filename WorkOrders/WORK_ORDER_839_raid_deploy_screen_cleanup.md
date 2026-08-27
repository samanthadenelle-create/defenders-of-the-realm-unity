# WORK ORDER 839 — Raid Deploy screen: cleaner layout (header, button row, preview)

**Status:** CLOSED 2026-08-27 — owner felt-tested PASS on APK 2026.08.27.343739 (dungeon review).
**Implementation note (2026-08-02):** root cause refined — the sweep-9413 factory relocation DID re-seat FrameCore's
inherited footer above the Close, but kept its designed ~0.065 height; that band is too thin for the `MinTouchPx=112`
button floor, so `ClampMinTouch` grew Auto Recommend/DEPLOY past the band into the Close underneath. Fix = explicit
raised 0.13-height FrameCore footer + a new sub-header zone (kit-level, `ZonesFor`); #6 shipped as a flippable
`GateDeployAtZeroTroops` flag (default false = scout-enabled, owner confirm pending); §3 dev-guard fixed in
`BreakCaptureHarness` (note box + freeze entry compiled out of release; release F8 falls back to the no-freeze
capture path). New contract pins: `Assets/Editor/Regression/RaidDeployUiRegression.cs` (needs DataRegression.RunAll
registration — sole-committer lane). Party row untouched beyond re-anchor + WO-774.0 forward-note (spectator ruling pending).
**Author:** UI/QA triage (read-only RCA, §13) — Claude UI
**Lane:** HUD/UI — `RaidDeployScreen.cs` + a shared-kit zone fix in `ElarionUiKit.cs` (FrameCore).
**Origin:** owner felt-test 2026-08-02, "RAID: Small Raider Camp" — *"Raid screen needs cleaner."*

---

## 1. ROOT CAUSE (fix first — it drives #1 and #5)
The screen uses `RpgUiCatalog.FrameCore` (`RaidDeployScreen.cs:113`). In `ElarionUiKit.ZonesFor`, the **FrameCore case
(`ElarionUiKit.cs:399-407`) defines medallion + header + body only — it never sets `z.footer`**, so FrameCore silently
inherits the default thin footer `(0.08, 0.030, 0.92, 0.095)` (`ElarionUiKit.cs:319`). That default footer **overlaps
the forced bottom-center Close band** `DefaultCloseZone (0.360, 0.050, 0.640, 0.125)` (`ElarionUiKit.cs:288`, forced
`:455`). The screen's own comments (`:335-337`) assume a re-seated footer that FrameCore does NOT get.

**Fix:** give `FrameCore` an explicit **raised footer** zone (above `DefaultCloseZone`) — and, ideally, a small
**sub-header** zone — in `ElarionUiKit.ZonesFor` (`:399-407`). ⚠ SHARED-KIT: FrameCore is used by other panels —
after the change, headless-capture a couple of other FrameCore screens to confirm no regression, OR scope the footer
override to this screen if a per-screen zone override exists. Do this before re-anchoring screen content.

## 2. Issues + fixes (all verified in `RaidDeployScreen.cs`)

### #1 — Header clutter (title / "Target: 4:30" / green "Regular" pill / "YOUR FORCES" collide)
Four elements stack into the top: title in the chrome header (`:111-113`); green difficulty pill at body
x0.00–0.20 / y0.945–1.00 (`:156-159`) — landing under the medallion socket; stars + "Target: mm:ss" at body
y0.945–1.00 (`:168-171`); "YOUR FORCES" at y0.855–0.915 (`:179-180`). The body-top sub-row butts directly under the
title = effectively two stacked header rows; the pill collides with the medallion socket.
**Fix:** move badge/stars/Target into the (new) header sub-title zone beside/below the title; push the left column
(from "YOUR FORCES" down) to start at body yMax ≈ 0.90; re-anchor the difficulty pill off the top-left corner so it
clears the medallion socket.

### #5 — Bottom button row (Auto Recommend + DEPLOY + Close overlap; gold slivers)
`BuildDeployBar` (`:338-356`): Auto Recommend footer x0.00–0.32 (`:341`), DEPLOY x0.615–0.985 (`:351`), and Close is
NOT here — it's the kit default band (panel x0.36–0.64, y0.05–0.125) which, because of the ROOT CAUSE, overlaps the
footer and clips DEPLOY's left edge (0.597 < 0.64). The **gold slivers** = `DeployGlow` (`:346-349`), a flat gilt rect
x0.60–1.00 / y0.00–1.00 deliberately larger than the DEPLOY button, so a hard gold edge pokes out on every side.
**Fix:** with FrameCore's raised footer (root cause), Close drops into its own band below; widen Auto Recommend +
DEPLOY to share the footer with a real gap and stop straddling the Close band; replace the oversized `DeployGlow`
rect with a soft glow sprite INSET fully behind DEPLOY (e.g. x0.605–0.995) so no hard rectangle edge shows.

### #3 — Empty middle (dead seam + bare lower band)
Two-column split with nothing between: left x0.00–0.48, right x0.52–1.00; the x0.48–0.52 seam is empty and the
right preview well stops at y0.36 (`:314`) with only two text lines below. **Fix:** close the seam (widen both
columns) and fill the lower band — the file's own TODO (`:30-31`) is a scout-report/intel area for the middle.

### #4 — "Battle Preview (enemy base)" is a stub
Explicit placeholder well + text (`:314-318`; comment "RaidBaseGenerator thumbnail goes here later"). **Fix:** wire
the `RaidBaseGenerator` thumbnail into the well, or show a framed "preview coming" state instead of raw placeholder copy.

### #6 — DEPLOY not gated at 0 troops (`OWNER CONFIRM`)
DEPLOY stays interactable at 0 troops — bound only to `_vm.CanDeploy` (scene existence), `:355`; intentional so the
player can enter to SCOUT with no troops (`:353-354`). The "No troops trained yet" empty state does not disable it.
`OWNER CONFIRM`: keep DEPLOY enabled for scouting (default) — vs. grey it out at 0 troops (add `&& _vm.DeployableCount
> 0` at `:355` + a troop guard/toast in `OnDeploy` `:372`). Ties to the WO-833 "don't offer what you can't do"
philosophy, but scouting is a deliberate feature — hence a flag, not an assumption.

## 3. SEPARATE — release-safety bug (route to diagnostics owner, NOT this screen)
The "What looks wrong? (Enter = save, Esc = save blank)" field over the header is the F8 capture harness's IMGUI note
box: `BreakCaptureHarness.cs:512-536` (`OnGUI`, drawn at ~y=Screen.height*0.18). The always-on "⚑ Flag" button IS
wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD` (`:537-550`), but **the note-entry box (`:512-536`) sits OUTSIDE
that guard**, and the harness installs on every non-WebGL platform (`:102-114`) — so the capture field can render on a
**non-development player build**. **Fix (own tiny WO recommended):** move the note-box block inside
`#if UNITY_EDITOR || DEVELOPMENT_BUILD` (or gate the whole capture `OnGUI` on it). Flagged so it doesn't ship.

## 4. Files to edit
- `Assets/_Modules/Village/Hero/RaidDeployScreen.cs` — #1, #3, #4, #5, (#6 if owner opts to gate).
- `Assets/_Modules/Core/UI/ElarionUiKit.cs` — ROOT CAUSE: FrameCore footer (+ optional sub-header) zone. SHARED — verify.
- `Assets/_Modules/Core/Diagnostics/BreakCaptureHarness.cs` — §3 dev-guard fix (separate concern; own WO ok).

## 5. Acceptance criteria (headless UI-capture, editor CLOSED)
- [ ] `RunCaptureHeadless` (or felt): header reads cleanly — title, difficulty, Target, and "YOUR FORCES" no longer
      overlap; the pill clears the medallion.
- [ ] Bottom row: Auto Recommend, DEPLOY, Close each in their own space — no overlap, no gold slivers around DEPLOY.
- [ ] No dead center seam / bare lower band; Battle Preview shows real art or an intentional framed state.
- [ ] (If owner opts) DEPLOY greys out at 0 troops with a clear reason; else documented as scout-enabled.
- [ ] The dev capture field no longer renders on a non-development build (§3).
- [ ] `CompileGate` green; other FrameCore panels unregressed by the kit footer change.

## 6. Do NOT
- Do NOT re-anchor screen content before fixing the FrameCore footer zone (you'll fight the shared default band).
- Do NOT hand-edit scenes. Do NOT change raid combat logic — this is layout + one dev-guard fix only.
- Do NOT widen the DeployGlow further to "cover" the slivers — inset/replace it instead.
