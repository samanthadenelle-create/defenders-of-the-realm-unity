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
- Iter 0 (setup): building capture infra + panel registry. Builds so far: APK shrink 462→383MB (side task).
