# VISUAL AUDIT — Web↔Exe UI Parity (live PM board)

**Owner directive (2026-07-20 night, BINDING):** every UI screen must be visually correct from the
Web UI view — **zero overlapping text, even spacing, matches the Blink mockup, and a 100%
screen-for-screen match between the WebGL (web) build and the Windows (exe) build.** Iterate until
every screen passes; then rebuild web UI and validate ONCE. **If a single screen fails, restart the
entire exercise.** Do not stop until complete.

## Method (the loop)
1. **Capture web** — localhost WebGL DevBuild, `?uicapture=1&trace=1`, webbot (headed Playwright)
   drives UICaptureMode through every panel + `page.screenshot()` per `[UICap] SHOWN <panel>` →
   `panel_<name>.png`. Real browser pixels + live `[Flow:*]` console per panel.
2. **Capture exe** — Windows DevBuild launched `-uiCapture` → UICaptureMode writes real PNGs to
   `Builds/UICaps/<panel>.png`.
3. **QA fleet** — agents inspect, per panel: web shot vs Blink mockup vs exe shot →
   {overlap? spacing? mockup-match? web==exe?} → PASS/FAIL + defect.
4. **Fix fleet** — one agent per FAIL panel (file-disjoint layout fixes).
5. **Re-capture → re-QA.** Any FAIL → restart from step 1. All PASS → one final rebuild+validate.

## Roles
- Lead/PM: this CLI session (Unity batchmode, sole committer, board owner, master inspection).
- QA: read-only vision agents (batch of panels each).
- Fix: edit-only agents (one panel each, file-disjoint).

## Infra status
- [x] UICaptureMode WebGL `?uicapture=1` trigger + web-paced signals (`[UICap] SHOWING/SHOWN/DONE/SWEEP COMPLETE`, 1.2s dwell) — gate-green
- [x] webbot `--uicapture` mode: screenshot on `[UICap] SHOWN` → `panel_<file>.png` + trace pairing (`out/uicapture-report.json`)
- [x] Panel set = UICaptureMode `Scenarios[]` (store, inventory, hero_skilltree, crafting, building_upgrade, cosmetic_shop, party_shop, rumor_board, hero_loadout, consumable/jeweler_crafting, equipment, game_guide, settings, music, help, bug_report) + echo roster (= the "pet" surface) + founding card + pause + build HUD. *(no dedicated Pet UI exists — Echo roster is it)*
- [~] WebGL DevBuild (building, PID 35292) → serve localhost → webbot sweep
- [ ] Windows DevBuild + `-uiCapture` → `Builds/UICaps/*.png` (exe reference)

## Build-mode FUNCTIONAL acceptance (owner 2026-07-20, runs after visual pass)
open Build UI → large carousel → **select item minimizes carousel** → touch UI or arrowpad to move →
**first tap places** → left/right to **rotate** → **accept/handle finalizes** → **return to carousel
maximized with the item placed, greyed out for singleton items.** Prove via localhost `[Flow:*]`.

## Known web↔exe difference sources (what the parity check targets)
- Texture resolution: web uses the WebGL platform override (DXT/crunch/512 via TextureBatchOptimizer);
  exe uses Default/desktop → web may look softer. Layout (overlaps/spacing) is identical (same code).
- Safe-area: browser chrome/notch occludes top/bottom on web only (no `Screen.safeArea` anywhere).

## Known foundational issue (from build-mode SME RCA)
- **No `Screen.safeArea` handling anywhere** — top/bottom controls (build-mode X-Done y0.86–0.99,
  Cancel y0.035–0.155) fall under mobile-web browser chrome/notch → "closing not closing." Global fix:
  safe-area insets on the HUD/panel canvases. Will confirm via localhost `[Flow]` data, then fix.

## Panel ledger (filled once the registry + first capture land)
| Panel | Web shot | Exe shot | Mockup | QA verdict | Fix | Notes |
|---|---|---|---|---|---|---|
| _(pending first capture)_ | | | | | | |

## Iteration log
- Iter 0 (setup): building capture infra + panel registry. Side task: APK shrink 462→383MB (committed).
- Iter 1 (pipeline PROVEN): webbot `--uicapture` drives UICaptureMode through all 16 panels + screenshots
  each from the real WebGL canvas. Fixed 3 pipeline bugs to get here: (a) WebGL `?uicapture=1` trigger
  (jslib `UICap_HasFlag`), (b) webbot SHOWN regex `$`-anchor vs Unity's multiline console, (c) viewport
  set to phone-landscape 2340x1080. **First real web pixels of every panel captured.**
- Iter 1 findings (raw, capture NOT yet clean):
  - **Canvas probe: buffer FULLY fills viewport** (2340x1080 = display) → the SME's WebGL-template
    canvas-sizing theory is REFUTED; template left untouched.
  - **Capture BLEED confirmed** (Settings shot showed the music panel + HUD): `PanelManager.CloseAll`
    closes only 1 arbiter slot + never hides the HUD. FIX IN PROGRESS (hide HUD via
    `VillageHudController.SetHudVisible`, hard-close all reflected panels, real settle) — must land before
    any per-panel QA is trustworthy.
  - "Bottom-band + right-cutoff" render seen in raw shots is NOT yet judged real — it may be HUD-bleed +
    half-open panels. Re-judge only on CLEAN isolated captures.
- Iter 2 (capture isolation fixed, render band NOT cracked): HUD-hide + panel-isolation landed — shots
  are now clean single-panel (no HUD/panel bleed). BUT the **bottom-band render persists**: the game
  (3D world AND UI) renders into the lower ~50% of the canvas, black top ~48%, right cutoff.
  **Ruled OUT with data (5 hypotheses):** SwiftShader-vs-real-GPU (identical), viewport 1600x900 vs
  2340x1080 (identical fraction), capture isolation/HUD-bleed (fixed, band remains), canvas buffer size
  (probe: buffer==display 2340x1080), framebuffer-fit `matchWebGLToCanvasSize`+resize (no change).
  Contradiction unresolved: buffer measures full-size yet render is banded.
- **ESCALATED (§13, past 2 failed attempts): headed-web render fidelity is an open blocker.** Not spun
  further. Handed to owner with hypotheses below.

## OPEN BLOCKER — headed-web capture renders game in a bottom band
Remaining hypotheses (untested / owner-input):
1. **Is the band even on the REAL device?** The webbot is Playwright/Chromium; the owner's reported bugs
   were specific overlaps, never "half the screen is black." The band may be a Playwright+this-Unity-build
   interaction, NOT her Pi Browser/phone. **Cheapest next test: open the deployed build in a NORMAL browser
   tab (not Playwright) or on the phone — if it fills the screen, the band is purely a capture artifact.**
2. A Unity camera viewport-rect / URP aspect-fit / letterbox rendering to a sub-rect (needs a camera-rect
   + URP-asset trace at runtime on WebGL).
3. A full-screen black overlay mis-anchored to the top half (needs a UI hierarchy dump at capture time).

## RECOMMENDED PIVOT for the LAYOUT audit (works today)
The **edit-mode `RunCaptureHeadless`** path renders panels FAITHFULLY (already used to fix the Echo card +
Pause menu — centered, clean, no band). And the **Windows DevBuild + `-uiCapture`** writes faithful
`Builds/UICaps/*.png` for all 16 panels. So the core ask (every screen: no overlap, even spacing, matches
mockup) can proceed on the faithful exe/edit-mode capture NOW; the web↔exe PIXEL parity specifically waits
on the render-band resolution (hypothesis 1 first). Owner to choose: fix headed-web render vs pivot to
exe/edit-mode faithful capture for the layout pass.
