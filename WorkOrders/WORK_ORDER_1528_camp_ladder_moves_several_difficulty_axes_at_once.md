# WO-1528: the camp ladder jumps several difficulty axes at once - build the bridge camps

**Status:** SPEC - owner design direction 2026-09-06 20:33. **DEPENDS ON WO-1527** (the Barracks cap curve).
**Silo:** raid scene configs - the four camps.
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1528 -> 1529 in the same edit). From her review of
`docs/RAID_BALANCE_AUDIT_2026-09-06.md`.

## 1. EVIDENCE

Today the ladder is:

```
Regular   9 defenders
Hard      15 defenders + Trolls + iron walls + arcane spire
```

Four axes move in one step - count, unit type, wall material and a magic structure. The audit's phrase is
*"several difficulty axes moving at once"*, and a player who clears Regular has no way to learn which of the
four beat them at Hard.

## 2. THE OWNER'S LADDER (verbatim on the four camps)

```
Camp I     10 slots    basic
Camp II    15 slots    introduces tank / healer
Camp III   20 slots    introduces magic / siege
Camp IV    25+ slots   late composition puzzle
```

**One axis moves per step.** Each camp teaches exactly one new thing, and the slot count is the spine that
carries the player between them.

## 3. FIX SHAPE

- Re-author the camp ladder to that shape. The Iron Bastion (or a new camp) becomes Camp II / III, so the
  jump from Regular to Hard is broken into two learnable steps.
- Each camp's config names WHICH axis it introduces, so a later reader can see the intent without diffing
  defender lists.
- Slot counts follow the WO-1527 curve - which is why this depends on it. Authoring camps against a cap that
  is about to change would need doing twice.

## 4. WHAT NOT TO DO
- Do not tune the existing two camps into four by scaling numbers. The point is what each camp TEACHES, not
  how hard it is.
- Do not start before WO-1527 lands, or before the WO-1520 retest proves Camp I is clearable at 10 slots.

## 5. ACCEPTANCE
- [ ] Four camps authored, each introducing ONE new axis, named in its config.
- [ ] Slot requirements match the WO-1527 curve.
- [ ] A captured clear of each camp at its intended slot count.
- [ ] `REGRESSION_OK n/n` on a fresh log.
