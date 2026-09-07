# WO-1527: the army cap moves onto Barracks progression - base 10, +5 per tier (Option C)

**Status:** SPEC - owner ruling 2026-09-06 20:33, but **NOT YET**. Option A stands today; this is the
long-term model. One open question below.
**Silo:** Core/Catalog army authority (`ArmyStorage` cap), `building-tiers.json` Barracks track, and the
perk row.
**BLOCKED** until the WO-1520 retest passes.
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1527 -> 1528 in the same edit). From her review of
`docs/RAID_BALANCE_AUDIT_2026-09-06.md`.

## 1. THE RULING

Verbatim:

> "Adopt Option C as the long-term progression model... A Barracks-driven army-cap curve makes progression
> legible"

and on the perk:

> "remove the one-time +5 capacity perk or change its job... repurpose it into something like 'Command
> Logistics: +1 deployment preset' or 'Reinforcements train 10% faster'"

and, importantly, on sequencing:

> "Keep Option A as the immediate ruling. Do not raise the cap yet."

**So this ticket does not raise anything today.** It lands after the WO-1520 retest proves the easy camp is
clearable at 10 slots. Shipping the cap raise first would mask whether the mechanical fixes worked.

## 2. THE OPEN QUESTION (one word from the owner)

Which job does the repurposed perk take?

- **A: "Command Logistics: +1 deployment preset"**
- **B: "Reinforcements train 10% faster"**

Both are her own candidates; the ticket does not pick.

## 3. FIX SHAPE

- `cap = base + perTier * (barracksTier - 1)`, both values read from the catalog. **Never a literal** - this
  is the WO-1108b lesson (`RepoProps.MaxStructureLevel` replaced eight hardcoded 3s).
- Base 10, +5 per Barracks tier, authored in `building-tiers.json`.
- The one-time +5 "Expanded Capacity" perk becomes whichever of A or B she picks.
- The Train screen words from WO-1517 show the cap AND `next +5 at Barracks L<n>` - the legibility half of the
  ruling.
- Migration keeps EXISTING armies legal: a player over the computed cap is not culled.

## 4. WHAT NOT TO DO
- Do not raise the cap before the WO-1520 retest. That is her explicit sequencing.
- Do not leave the +5 perk alongside the curve; two authorities on army capacity is exactly the duplicated
  state this repo keeps paying for.

## 5. ACCEPTANCE
- [ ] The perk question answered in writing, either way.
- [ ] Cap derived from the catalog; a source case fails on any literal army cap.
- [ ] Regression: cap is 10 at Barracks L1, 15 at L2, 20 at L3.
- [ ] Migration case: an army above the computed cap survives and is not culled.
- [ ] The WO-1517 Train screen shows the cap and the next step.
- [ ] `REGRESSION_OK n/n` on a fresh log.
