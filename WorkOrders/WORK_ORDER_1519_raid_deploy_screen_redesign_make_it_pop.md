# WO-1519: raid deploy screen redesign - hierarchy, art and chips, so the screen pops

**Status:** READY TO IMPLEMENT - owner ask, 2026-09-06 20:14
**Silo:** `RaidDeployScreen` + `RaidDeployController` (the deploy modal).
**LANDS AFTER** tonight's RaidDeployScreen / Controller commit, and BUILDS ON WO-1462 (backdrop),
WO-1463 (magenta flag) and WO-1464 (overlaps). Those are the layout defects; this is the design pass.
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1519 -> 1520 in the same edit).

## 1. EVIDENCE

Owner ask, verbatim:

> "screensshot, can we make this screen pop?"

Device frame `Logs/device/screens/owner-screen-20260906-201443.png` (build 358574, 20:14):

```
title    "RAID: THE FORSAKEN CAMP"
         green "Regular" pill + three gold diamonds + "Clock: 3:00"
LEFT     "YOUR FORCES" - three TINY hero portraits (Thrain, Grom, Sylas)
         "Army: 10 / 10 slots" COLLIDING with the hero row
         Footman x7 / Archer x3, small medallions, then a LARGE EMPTY BLACK AREA below
RIGHT    "ENEMY BASE / Scout the camp / Recon ~2:30 / Power 196"
         "ECHO GUIDE Corvin, the Void Echo" - quote cut at "...and it w..." - CHANGE button
         "SCOUT REPORT" as four lines of prose
BOTTOM   EDIT ARMY and BEGIN ASSAULT at EQUAL weight
through  the town and the hero visible through the panel (no backdrop - WO-1462 pending)
```

Nothing on the screen is sized by its importance: the decision the player is about to make (assault this camp
with this army) is carried by the same weight as a CHANGE button and four lines of prose.

## 2. FIX SHAPE (design direction)

The owner is red/green colourblind: **never carry meaning in hue alone; the greyscale check is the gate.**
Ask her about behaviour, never about palettes.

1. Backdrop from WO-1462 - land that first.
2. **ENEMY BASE becomes ONE HERO CARD**: the camp's art (the raid selection card art, or a scene capture),
   the boss portrait from `Portraits/`, and Power + Recon as two BIG numerals.
3. **YOUR FORCES**: hero portraits at the kit's LARGE medallion size with the class word under each; troop
   rows as portrait + count CHIPS. The army count becomes a band, `ARMY 10/10 FULL`, composed by the VM
   (the WO-1517 word), never overlapping the hero row.
4. **SPOILS as three icon+number chips** (wood, iron, gold) using the resource sprites - cap-aware and
   repeat-aware per WO-1461, so the number shown is the number that will bank.
5. Difficulty = the WORD plus the diamonds. The coloured pill is retired (hue-only meaning).
6. The Echo line fits, or is trimmed at a CLAUSE boundary, via `FitSingleLine`. The quote is optional.
7. **BEGIN ASSAULT is the single gold primary** at the kit's primary size; EDIT ARMY is a secondary.
8. No empty black band: derive both column heights from their CONTENT, not fixed heights.

## 3. WHAT NOT TO DO
- Do not pick hues. Take the kit's existing roles; if a distinction needs a second channel, use shape, weight
  or a word.
- Do not fix the overlaps here - that is WO-1464. This ticket is the hierarchy pass on top of it.
- Check the boss portrait renders before relying on it: WO-1509 found the Orc Necromancer FBX has no albedo.
  Whether its 2D portrait asset is affected is UNPROVEN - open it and see.

## 4. ACCEPTANCE
- [ ] Headless `RaidDeploy_2670x1200.png` captured, OPENED and looked at.
- [ ] A GREYSCALE copy of that PNG still reads - every distinction survives without hue.
- [ ] `RaidSelectionLayoutRegression` sibling case: no overlap, every text inside its box, backdrop present.
- [ ] The spoils chips match what actually banks (shared with WO-1461's acceptance).
- [ ] `REGRESSION_OK n/n` on a fresh log.
