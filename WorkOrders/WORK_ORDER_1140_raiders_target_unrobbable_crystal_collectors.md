**Status:** LATENT GUARD - NOT AN ACTIVE DEFECT (corrected 2026-08-22). No crystal collector is authored today, so this cannot fire. Implement only if/when one is authored.

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


---

# ⚠ CORRECTED 2026-08-22 - THIS IS LATENT, NOT LIVE

The owner asked the right question: **"is there a crystal collector? Or just an assumption"** -
and it was an assumption. Verified at source afterwards:

- `HarvestResource.Crystals = 0` exists as an ENUM VALUE, which is what this ticket was built on.
- **No building yields it.** `Assets/Editor/CollectorStackPropCatalogBuilder.cs:104` authors picks
  for Wood / Food / Iron only and states outright: *"HarvestResource.Crystals: deliberately absent"*.
  `CollectorIncomeRegression.cs:753` branches on it defensively, which is prudence about a value
  that can be authored later - not evidence that one IS.

**So the defect described above cannot occur today.** Raiders cannot beeline an unrobbable crystal
collector because there is no crystal collector to beeline. The claim that it would read as a bug on
a felt-test was WRONG and is retracted.

**Why the ticket survives instead of being deleted:** WO-1139 made crystal collectors unlootable in
code, and `SiegeRoleValue` does NOT consult that exemption. The day anyone authors a crystal
collector, the mismatch is live and silent - a building advertising itself as a prize that can never
be robbed. Keeping this ticket is cheaper than rediscovering it then.

⛔ **Do NOT implement this speculatively.** The fix touches `SiegeRoleValue`, which is shipped and
tuned. Wire it in the SAME change that authors the first crystal collector, so the guard and the
thing it guards arrive together.

**The general lesson, recorded because it nearly cost a felt-test:** an ENUM VALUE is not a FEATURE.
A defensive branch on a value proves someone anticipated it, not that anything produces it. Check
for an authored instance before writing a ticket about behaviour around it.
