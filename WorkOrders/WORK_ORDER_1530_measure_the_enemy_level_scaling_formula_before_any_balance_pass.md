# WO-1530: measure the enemy level-scaling formula BEFORE any balance pass

**Status:** READY TO IMPLEMENT - owner direction 2026-09-06 20:33. **DO THIS FIRST** of the balance work.
**Silo:** Village/Enemies scaling + `RaidGarrisonSpawner` level assignment.
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1530 -> 1531 in the same edit). From her review of
`docs/RAID_BALANCE_AUDIT_2026-09-06.md`.

## 1. EVIDENCE

Owner, verbatim:

> "make enemy scaling the very next measurement, because that is the biggest unknown capable of making all the
> napkin DPS comparisons lie"

The one measured data point says the lie is real: at camp level 3 the hero took **15 per hit against a listed
10**. Whatever the formula is, the authored number is not what the player meets, so every damage and
time-to-kill figure in the balance audit is computed against values that do not occur in play.

This is the whole reason the ticket exists: it is not a bug report, it is the measurement that has to precede
the other tickets. WO-1526, WO-1527, WO-1528 and the Hard/Extreme tuning all rest on it.

## 2. FIX SHAPE

- Read the scaling formula AT SOURCE and write it down. Do not infer it from observed damage.
- Add a PERMANENT `FlowTrace` line at defender spawn naming, per defender: base health/damage -> scaled
  health/damage, and the level that produced them. Never stripped (CLAUDE.md sec.12).
- Capture ONE raid and read the lines.
- Record the formula in `docs/RAID_BALANCE_AUDIT_2026-09-06.md` so the audit's other numbers can be recomputed
  against reality.

## 3. WHAT NOT TO DO
- **Do not change any scaling number in this ticket.** It measures. A tuning change made in the same pass
  makes it impossible to tell what the formula was.
- Do not proceed with the Hard/Extreme balance pass until this lands.

## 4. ACCEPTANCE
- [ ] The formula quoted from source, with file:line.
- [ ] The spawn trace lands and a captured raid's lines are pasted, showing base -> scaled for each defender.
- [ ] The 15-vs-10 discrepancy is EXPLAINED by the measured formula, or recorded as still unexplained - either
      is an acceptable outcome; an unproven explanation is not.
- [ ] The formula written into the audit doc.
- [ ] `REGRESSION_OK n/n` on a fresh log.
