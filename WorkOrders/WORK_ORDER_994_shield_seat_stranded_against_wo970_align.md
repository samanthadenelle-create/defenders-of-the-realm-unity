# WORK ORDER 994 - The shield's authored seat is stranded against a base WO-970 moved

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-14 (CLI)
**Silo:** Gear / equip seating
**Source:** OWNER REPORT - *"still same problem when porting from dungeon with Shield position"*

---

## Root cause, proven from captured data

WO-970 (`af5e2e7d8`, 2026-08-10 19:27) fixed `AlignAxesYLongXNarrowZWide` so a weapon's long axis
finally reaches +Y. Same mesh, same authored delta, before and after:

```
PRE-FIX  (WO-970 SS2)   NormalizeInto 'EquipmentProp_OffHand': aligned b1=(0.01, 0.002, 0.008)   X-long
POST-FIX (2026-08-14)   NormalizeInto 'EquipmentProp_OffHand': aligned b1=(0.002, 0.01, 0.008)   Y-long
```

The inner prop rotation moved ~90 degrees. `shield_A`'s delta - `rot=(-160,-180,-84)`, dialled
**2026-07-07** in `Assets/Resources/OffsetForge/offsets.json` - was authored on top of the OLD align
and has never been re-dialled.

Owner F8, **1h36m after** that commit: seq2325 *"the shield is **now** mid body"*, seq2326 *"broken
shield carried back on exit"*.

## Why the prior fix did not hold - one sentence

WO-970 named the pin but excused this exact file:

> *"`shield_A` is `fullOverride: true` = absolute in the socket frame, so it is immune either way."*

**That is FALSE at source.** `fullOverride` writes the delta onto `gripRoot`
(`EquipmentController.cs:1615-1617`); `NormalizeInto` rotates `prop`, which is gripRoot's CHILD
(`WeaponBoundsOrient.cs:135`). Final orientation = `gripRoot(authored) . prop(AlignAxes)`.
Absolute on the OUTER frame is not immunity to an INNER rotation. That sentence is why this survived.

## The matched pair

- **Half A (moved):** `WeaponBoundsOrient.AlignAxesYLongXNarrowZWide` - `WeaponBoundsOrient.cs:116-143`
- **Half B (stranded):** `shield_A` `rot=(-160,-180,-84)` + `shield_A@sheathed` `rot=(2,180,-78)`

## The trace is hollow - this is why 4 days of captures show nothing

`EquipmentController.cs:1612` echoes `offsets.json` verbatim. It prints **byte-identical text
whether the shield lands on the arm or 90 degrees through her chest**, and has printed the same string
since 2026-07-07 - unchanged straight through the regression.

The line claiming to be landed proof (`:1691`) is worse: it prints position but **no rotation** (the
only thing that changed), **no world bounds**, and runs **before `ApplyHoldPose()` at `:1701`** - so
for a sheathed hero it logs a pose the prop does not keep.

## Two real port-specific amplifiers (proven; which one the owner sees is UNPROVEN)

**(a) Different heroes, different heights.** Dungeon Keeper `height=2m -> propScale=51.025`; town hero
`height=1.8m -> propScale=45.924`. The shield is **11% larger in the dungeon** against a FIXED
position delta.

**(b) Drawn-vs-sheathed size asymmetry, flagged by WO-970 and never ticketed.** Back path
`:1847` calls `CompensateParentScale` **unconditionally**; hand path `:1860` guards it on
`_offHandParentCompensate`, which `:1619` sets **false** for `fullOverride`. Captured:
`parent='CC_Base_L_Hand' -> 0 lines` vs `parent='SheatheSocket_Back' -> 41 lines`. **One prop
renders at two sizes** - 1.666x between in-hand and on-back.

## Fix

**A - DATA (owner's hands, not an agent's).** Re-dial `shield_A` + `shield_A@sheathed` in the
Seating Editor against the corrected align. These are manual/CANON values. **An agent must NOT compute
a compensating euler** - that recreates the same stranded pair one layer up.

**B - CODE.** `:1847` must take the same `_offHandParentCompensate` guard as `:1860` (mirror for
the main weapon at `:1819` vs `:1834`). A prop must not render two sizes.

**C - TRACE (mandatory).** Replace `:1691` with a MEASURED world pose emitted **after**
`ApplyHoldPose()`: parent name, `propLocalEuler` AND `gripLocalEuler` **separately** (so a moved
base is distinguishable from a bad dial), world euler, encapsulated world bounds, bone lossyScale,
compensated bool, DRAWN|SHEATHED. Without C the next capture is blind again.

**D - REGRESSION.** Assert `aligned b1` is Y-longest after every `AlignAxes` (WO-970 stated the
invariant and shipped no test), and that drawn vs sheathed world bounds match for a `fullOverride` prop.

**E - CANON.** Banner the false "immune either way" line in WO-970. It is load-bearing.

## What NOT to do

- Do NOT hand-compute a corrective rotation for `shield_A`. Re-dial it.
- Do NOT delete the hollow `:1612` line - retoken it (CLAUDE.md SS12: never strip FlowTrace).
- Do NOT touch `AlignAxesYLongXNarrowZWide`. Half A is correct now.