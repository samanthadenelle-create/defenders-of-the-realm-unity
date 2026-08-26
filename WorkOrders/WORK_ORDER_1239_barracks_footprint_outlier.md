# WORK ORDER 1239 - 'barracks' reads as a footprint outlier; the family median is the suspect

**Status:** READY TO IMPLEMENT
**Silo:** Structure art / catalog
**Severity:** P3. It blocks the regression marker and nothing else. No player-facing report.
**Origin:** CLI batch gate, 2026-08-26 17:23 (`Builds/gate-r3`). The ONLY failure in
`REGRESSION_FAIL: 1 failure(s) (291/292 registered suites green, 0 skipped)`.

---

## PROOF

```
STRUCTURE_CADENCE_FAIL: 1 issue(s):
'barracks' FOOTPRINT OUTLIER: the fitted model is 7.64 m across - 2.0x the family median of
3.78 m, over the 2.0x band. THE CAUSE IS ALMOST NEVER heightMul. Fit-to-height is a single-axis
promise run as a UNIFORM scale (VisualFactory.Fit: localScale *= target / bounds.size.y), so a
model whose FIT-TIME pose is FLAT divides by a tiny number and drags its footprint up with it -
measured height 4.00 m.
```

The oracle prescribes its own check order, and it is better than anything a fresh reader would
invent - **follow it**:
1. Is the model upright AT FIT TIME? `orientation.euler` is applied PRE-fit via
   `SkinOptions.LocalRotation`, so a wrong euler chooses which axis `Fit` divides by.
2. If the art really is flat-and-wide and correctly posed, author `repo.maxFootprint` on the row
   (a ceiling in metres, default 0 = disarmed) - that is what `collector_farm` does.

⛔ **DO NOT lower `heightMul`.** It shrinks the BUILDING as well as the footprint, which is the
"shrunk farm" the owner already rejected in commit `31b41d19`.

## ⚠ UNPROVEN HYPOTHESIS - investigate, do not assume

This suite was **GREEN this morning**: `STRUCTURE_CADENCE_OK - 27 structure base visual(s) measured`.
It is red now, and **`barracks` itself was not edited today**.

The oracle compares against a **FAMILY MEDIAN**. Two new models were committed to
`Assets/StructureContent/` today (`CrystalMine.fbx`, `HealingCaravan.fbx`). **If they entered the
measured family, the median moved - and `barracks` could cross the 2.0x band without changing at
all.** 3.78 m is a suspiciously small median for a structure family.

**This is a hypothesis with supporting circumstance, NOT a proven cause** (CLAUDE.md section 12:
static reasoning LOCATES, it never CONCLUDES). **First action: print the measured set and the
median, morning vs now.** If the population changed, the honest fix may be the median, the band, or
`maxFootprint` on `barracks` - and possibly nothing is wrong with `barracks` at all.

If the population did NOT change, discard this paragraph entirely and follow the oracle's check
order from step 1.

## Acceptance

1. `REGRESSION_OK <n>/<n> suites` on a fresh log, count read off the marker.
2. The RESULT states the measured population size and median **before and after**, so the next
   reader can tell a real outlier from a shifted baseline.
3. If `maxFootprint` is authored, say why that value - it is a metre ceiling, not a magic number.
4. ⭐ If the CAUSE turns out to be the median moving, **say so plainly and consider whether the
   oracle should report population size in its OK line too.** An oracle whose threshold silently
   depends on who else is in the room will do this again.

## What NOT to touch

- ⛔ `heightMul` on `barracks` or anything else. See above; already owner-rejected.
- ⛔ `VisualFactory.Fit`'s uniform-scale behaviour. It is a known, documented property that the
  oracle's message explains; changing it re-scales every structure in the game.
- ⛔ The 2.0x band, unless the investigation proves the band itself is the defect - and then say so
  with the population data.
