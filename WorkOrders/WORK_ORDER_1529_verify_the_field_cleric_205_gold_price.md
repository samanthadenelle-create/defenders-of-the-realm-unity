# WO-1529: verify the Field Cleric's 205 gold price - intentional, or a typo

**Status:** SPEC - needs an owner decision after the evidence is gathered
**Silo:** canonical `troops.json` (and its twin).
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1529 -> 1530 in the same edit). From her review of
`docs/RAID_BALANCE_AUDIT_2026-09-06.md`.

## 1. EVIDENCE

Owner, verbatim:

> "Field Cleric costing 205 gold looks suspiciously low compared with... Spearman at 850, Shieldguard at 1,150
> and Outrider at 1,500... verify whether 205 is intentional or a typo"

The ladder as authored:

```
Field Cleric      205
Spearman          850
Shieldguard     1,150
Outrider        1,500
```

205 is not merely the cheapest - it is a quarter of the next rung, on a HEALER, which is the unit type whose
value rises with army size. If it is a typo the likely intent is 1,050 or 2,050; but that is a guess and this
ticket does not act on guesses.

## 2. FIX SHAPE

- `git blame` the row and read the authoring notes. Report what the history says: deliberate, or a slip.
- Put the finding to the owner with the ladder above. **She decides.**
- If it IS a typo: one number, changed in BOTH twins, edited from HEAD bytes with the LF count proven
  (memory `canonical-json-edits-binary-only-verify-newlines`).

## 3. WHAT NOT TO DO
- Do not "fix" the price on the strength of it looking wrong. A cheap healer may be a deliberate on-ramp for
  the Camp II tank/healer step (WO-1528).
- Do not rewrite `troops.json` in text mode.

## 4. ACCEPTANCE
- [ ] `git blame` output and any authoring note quoted in the RESULT.
- [ ] The owner's decision recorded in this file.
- [ ] If changed: both twins updated, LF counts proven, and the canonical-JSON oracles green.
- [ ] `REGRESSION_OK n/n` on a fresh log.
