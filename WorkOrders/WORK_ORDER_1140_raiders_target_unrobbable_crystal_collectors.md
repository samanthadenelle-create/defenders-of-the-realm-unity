**Status:** READY TO IMPLEMENT

# WORK ORDER 1140 — Raiders beeline for crystal collectors that can no longer be robbed

**Minted:** 2026-08-22 (CLI, banner bumped 1140 -> 1141 in the SAME edit)
**Lane:** Combat/AI targeting. **Class:** consequence of a ruling, caught before it shipped.
**Surfaced by:** the WO-1139 collector-loot rewire, which made crystal collectors unlootable.

## THE DEFECT

`ResourceCollector.SiegeRoleValue => 0.85f * (1f + FillFraction * 0.75f)`

`EnemyBrain` (`EnemyBrain.cs:1597`) checks `ISiegeLootTarget` **before** the generic
`IDamageableStructure` fallback, and `Enemy` (`Enemy.cs:2286`) scores by the same value. So a
**fuller collector is a more attractive target** - deliberately, and correctly, per WO-664.

**WO-1139 then ruled crystal collectors UNLOOTABLE** (a player cannot distinguish harvested crystals
from PURCHASED ones - same wallet - so any crystal loss reads as losing bought currency). The steal
is refused in two independent places.

**The targeting was not told.** A crystal collector still advertises a HIGH role value that scales
with how full it is, so raiders preferentially attack the one building in the town that **cannot
yield them anything**. The fuller it is, the harder they chase the nothing.

## WHY IT MATTERS BEYOND TIDINESS

1. **It inverts the mechanic the player is being taught.** WO-1139's whole player-facing rule is
   *"what you have collected is safe; what is still in the building is at risk."* A raider mobbing a
   crystal collector and taking nothing teaches the opposite of that, twice: it looks like a threat
   that isn't, and it pulls raiders AWAY from the wood/iron/food collectors that ARE the mechanic.
2. **It wastes the siege.** Attacker attention is the scarce resource in a defence. Aiming it at an
   empty target makes the siege easier in a way the player cannot perceive or learn from - the
   worst kind of difficulty change, because it is invisible.
3. ⚠ **It will read as a BUG on the owner's felt-test**, not as balance: enemies swarming a building,
   destroying it, and the report saying nothing was taken.

## THE FIX (small, but pick the shape deliberately)

The single authority for "is this worth robbing" is already `StakeRules.IsLootable` /
`ResourceCollector.IsResourceLootable` (WO-1139 extracted them as pure statics precisely so they are
drivable). **`SiegeRoleValue` should consult that same authority** rather than a second copy of the
rule - a parallel "which resources are lootable" list is exactly the duplicated-state failure this
repo keeps paying for.

Suggested shape (the implementing seat should verify at source):
- If the collector's resource is **not lootable**, its role value drops to the ORDINARY structure
  value - it stays a legitimate target (it is still a building in the way, and destroying it still
  denies future income), it simply stops advertising itself as a PRIZE.
- ⛔ Do NOT make it untargetable. A building raiders refuse to touch is its own visible weirdness,
  and a crystal collector is a real obstacle worth destroying.
- ⛔ Do NOT hardcode `HarvestResource.Crystals` at the targeting site. Ask the authority, so a future
  exemption change moves ONE place.

## ⛔ FENCE AT TIME OF MINTING
`Enemy.cs`, `EnemyBrain.cs` and the VFX files were being edited by another lane on 2026-08-22
(WO-874 elite VFX wiring). Confirm that work is committed before starting, and re-verify the two
line references above rather than trusting them.

## ACCEPTANCE

- [ ] A full CRYSTAL collector no longer outranks a full wood/iron/food collector as a raid target
- [ ] It is still targetable and destroyable as an ordinary structure
- [ ] `SiegeRoleValue` consults the SAME lootability authority as the steal - no second list
- [ ] Regression pins it: a lootable collector's role value scales with fill, an unlootable one's
      does NOT. ⚠ MEASURE, do not restate - do not derive the expectation from `SiegeRoleValue`'s own
      expression, or the suite cannot fail.
- [ ] Owner felt-check: raiders visibly go for the silo and the lumbermill, not the crystal building
