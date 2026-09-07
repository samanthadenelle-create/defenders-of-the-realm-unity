# WO-1405: every Manage row prices the tap and never says what it buys; `grid 5, 16` is a developer coordinate on a player screen

**Status:** IMPLEMENTED - Defense half landed 2026-09-06 (CompassSideOf, ManageRowBenefitRegression); gated; capture + Seeker still owed; Buildings/Troops half landed earlier in 3c677027e/949e848a0

## Evidence
- Device frames (build 355952) - SEEN (`REVIEW_MERGED.md` row 4): `docs/qa/UI_REVIEW_2026-09-05/04-manage-defense.png`
  `Arcane Spire - grid 5, 16 - L1 -> L2 / Iron 540`; `05-manage-buildings.png` `Armorer -> T1 / Wood 1000, 670 gold`;
  `06-manage-research.png` `Arcane Basics / 1200 gold / Ready - takes 13m 0s`; `07-manage-troops.png`
  `UPGRADE TO L4 / 6m 0s . Ready`. Not one row names an effect.
- The upgrade PAGE does: `14-research-door-result.png` reads `Mage spell power +5%, arcane tower damage +5%` and
  "Thrain's base kit awakens" - the benefit string exists and the list does not surface it.
- Both reviewers: `REVIEW_A_independent.md` A-1 / A-4 / A-7, `REVIEW_B_independent.md` A2 / A4 / A7.
- CODE: `Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs:763` builds
  `string location = "grid " + placed.cellX + ", " + placed.cellZ;` - the coordinate is authored into the row VM.

## What the player experiences
Cost and wait on every row, benefit on none. A player who reads `Iron 540` with no reason attached taps BACK;
`grid 5, 16` tells them the screen was built for someone else.

## Fix shape (one mechanism)
One benefit line per row, read from the SAME catalog string the upgrade page renders (the tier-benefit text
`BuildingUpgradePanelMvvm` shows) - the row VM (`ManageScreenVM`) gains a `Benefit` string; the panel
(`ManageScreenPanel`) renders it under the cost with a kit label. No new data is authored. Applies to Defense,
Buildings, Research rows and the Troops card's UPGRADE line (troop tier effect from the troop catalog).
`grid x, y` -> display name + compass side (`Arcane Spire - north side`), derived from the placement's cell
against the Heart at (0,0,0) - words, never a coordinate, in `ManageScreenVM` only.

```
Arcane Spire - north side     L1 -> L2
+5% arcane tower damage                 Iron 540   [ UPGRADE ]
```
Trace: `FlowTrace.Step("Manage", "row id=<id> benefit='<text>' location='<text>')` per built row; an empty
benefit is `FlowTrace.Warn("Manage", "no benefit string for <id>")`, never a silent blank.

## Acceptance
- [ ] RED first: `ManageRowBenefitRegression` - on the operational fixture every Defense/Buildings/Research row
      VM has a non-empty `Benefit`; no row string contains `grid `; the Troops UPGRADE row names an effect.
      Fails on the current tree (`ManageScreenVM.cs:763`).
- [ ] Headless: `RunManageOperationalCaptureHeadless` -> `MANAGE_OPERATIONAL_CAPTURE_OK 12/12`; `ManageDefense`,
      `ManageBuildings`, `ManageResearch`, `ManageTroops` PNGs opened; benefit line legible, no `...`.
- [ ] Device: Manage > each tab; rows read a benefit and a compass side; screencaps read.

## Not in scope
The launcher chips and Troops header (WO-1406); the upgrade page itself; the queue drawer tiles' single-letter
glyphs (`T / B / R`) and `Barracks:2:0` ids - fold into this ticket ONLY if the same VM feeds them, else a follow-up.

## Owner ruling
- Section 2 #4 Benefit-line? - written to the default YES.
- Section 2 #5 Gridref? - written to the default NO (display name + compass side).
