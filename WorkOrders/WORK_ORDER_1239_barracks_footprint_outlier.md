# WORK ORDER 1239 - 'barracks' reads as a footprint outlier; the family median is the suspect

**Status:** FIXED 2026-08-26 - gated `COMPILE_GATE_OK` + `REGRESSION_OK 294/294 suites` (Builds/g3-c, Builds/g3-r). AWAITING OWNER FELT-VERIFY to close.
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

## ✅ PROVEN CAUSE (2026-08-26, replaces the hypothesis below)

**The band moved. `barracks` did not.**

Measured, not reasoned - two gate logs, same suite, same 27 rows, same ids:

| | `Builds/wo1211-reg.log` (GREEN, 08-25 21:15) | `Builds/gate-r3` (RED, 08-26 17:23) |
|---|---|---|
| population | **27** measured base visuals | **27** measured base visuals |
| median widest | **4.32 m** (`pet-house`) | **3.78 m** (`mine_crystal` / `healing_caravan`) |
| 2.0x band | **8.64 m** | **7.56 m** |
| `lumberyard` / `foundry` / `silo` | 5.83 m each | **2.91 m each** |
| `barracks` | **7.64 m** | **7.64 m** |

WO-1224 Slice A (commit `3cd28c86c`) set `heightMul: 0.5` on the three shared `GenericContainer`
rows. `heightMul` feeds a UNIFORM fit scale, so halving it halved their **footprint** too:
5.83 -> 2.91 m. Three of 27 rows dropped below the middle, the median fell off `pet-house` (4.32)
onto the `mine_crystal`/`healing_caravan` pair (3.78), and the 2x band came down 8.64 -> 7.56 m -
**past `barracks`, which has measured 7.64 m in every green log and was never edited.**

WO-1224 itself predicted this exactly (that file, line 131): *"Slice A moved the structure family
median 4.32 m -> 3.78 m, which moved the cadence oracle's 2x band to 7.56 m and made `barracks`
(unchanged at 7.64 m) read as an outlier."*

### ❌ THE ORIGINAL HYPOTHESIS WAS WRONG - recorded, not deleted

The section below guessed that the two FBX models committed that day (`CrystalMine.fbx`,
`HealingCaravan.fbx`) had entered the family and moved the median. **They had not.** `mine_crystal`
and `healing_caravan` are both present, at 3.78 m each, in the **GREEN** 08-25 log - they landed in
commit `3eb499b88`, which is *older* than the `heightMul` commit. The population size did not change
at all between the two runs; only three existing members shrank. The instinct - *"the median moved,
not the building"* - was right; the named mechanism was wrong. A corrected record beats a clean one.

## ⚠ UNPROVEN HYPOTHESIS (SUPERSEDED 2026-08-26 - see above; kept for the record)

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
