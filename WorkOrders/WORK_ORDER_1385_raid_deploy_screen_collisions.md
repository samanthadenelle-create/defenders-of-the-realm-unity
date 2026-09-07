# WO-1385: the raid deploy screen - Echo Guide collides with CHANGE and the enemy line; portraits behind labels; truncated quote; raw yellow slab

**Status:** CLOSED 2026-09-07 - owner felt-test PASS (validated 2026-09-07T14:03:03, build 2026.09.07.359076). PRIOR STATUS: FIXED - in 65d5a7eae, on the Seeker in build 2026.09.05.355952 (bands Enemy/Guide/Scout disjoint, portrait labels below plates, BEGIN ASSAULT kit button; RaidDeployUiRegression pins green in 378/378). Awaiting owner felt-test on the deploy screen.

**Owner (2026-09-04 23:06, felt-test on the Seeker, build 355905), verbatim:** "screenshot. yuck"

**Evidence:** `docs/qa/seeker-raid-deploy-2026-09-04.png` (2670x1200, adb screencap 23:06). Screen:
"RAID: THE FORSAKEN CAMP", difficulty pill "Regular", three star glyphs, "Clock: 3:00".

## What the screenshot shows (each item visible in the PNG)
1. **ECHO GUIDE block collides with two neighbours**: "ECHO GUIDE / Elowen, the Nature Echo / quote"
   is drawn over the tail of "Assault to recon - deploy troops on the field" and the CHANGE button
   sits on top of the Echo name line. Three elements share one band.
2. **Hero portraits are behind their name labels**: "Thrain" / "Grom" text is painted over the two
   olive portrait plates, glyph half-hidden.
3. **The Echo quote is truncated mid-sentence** ("...sowed this ground, an") with no ellipsis, no wrap.
4. **BEGIN ASSAULT is on a raw flat yellow slab**, not a kit frame; ARMY READY? beside it is a kit
   frame - two visual languages on one row.
5. YOUR FORCES: "Army: 3 / 10 slots" and the Footman x3 row are fine; SCOUT REPORT is fine.

## Rulings to honour
- One band per element: ENEMY BASE line, ECHO GUIDE block (name + quote + CHANGE on its own row,
  quote wrapped to two lines with `FitBlock`, ellipsis if it still overflows), then SCOUT REPORT.
- Portraits: label BELOW the portrait plate, never on it; plate >= MinTouchPx if tappable.
- BEGIN ASSAULT: the kit's primary button (`BuildObsidianButton` Yellow) on the same row geometry as
  ARMY READY?; the yellow slab goes.
- ASCII-only; no meaning by colour alone; touch >= MinTouchPx. MVVM: the VM already carries the
  guide line and report; this is View layout only.

## Acceptance
- [ ] Headless capture of the deploy screen at 2670x1200 with a guide selected: no two elements
      overlap (`AuditGeometry`), quote fully visible or ellipsised, one button style on the row.
- [ ] `RaidDeployUiRegression` green; add a geometry pin if the screen has no capture case.
- [ ] Owner felt-test.
