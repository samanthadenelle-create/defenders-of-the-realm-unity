# WO-1500: the fresh-save FTUE has had no capture and no log since 2026-09-01

**Status:** READY TO IMPLEMENT
**Silo:** FTUE / capture. Plus one Bag decision.
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1500 -> 1501 in the same edit).

## 1. EVIDENCE

Across ALL FIVE logs captured on 2026-09-06:

```
[Flow:Onboard*]                        zero lines
raid.first_completed already latched
echoes=4/6
```

Every one is a RETURNING save. The only evidence of minute one through minute ten is a set of PNGs from
September 1 - five days and many merged lanes ago, including the whole Manage 2000-block.

The first ten minutes of the game are therefore unobserved, which for a retention-limited product is the
worst place to be blind (memory `retention-is-the-business-problem`).

A concrete first-minute defect is already visible in source:

```
InventoryUIBuilder.cs:597, 662-665   the Bag "Map" rail is a LABELLED entry that cannot open
```

(`FeatureFlags.MapTab` was deleted 2026-09-05, so the label outlived its door.)

## 2. FIX SHAPE

- One AutoPilot FRESH-SAVE headless run, PNGs opened, covering title -> founding -> first build -> first quest.
  Make it a standing fleet lane so a fresh save is captured every night, not on request.
- Decide the Bag Map rail: HIDE it, or label it as coming. A labelled entry that does nothing on the first
  screen a new player explores is the worst of the three options.

## 3. WHAT NOT TO DO
- Do not judge the FTUE from the September 1 PNGs. They predate the Manage program entirely.

## 4. ACCEPTANCE
- [ ] A fresh-save headless run with `[Flow:Onboard*]` lines present; PNGs opened in the RESULT.
- [ ] The fresh-save lane added to the nightly fleet.
- [ ] The Bag Map rail hidden or labelled; a Bag capture opened.
- [ ] `REGRESSION_OK n/n` on a fresh log.
