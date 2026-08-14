# WORK ORDER 990 — Retire the `tower_healer` row (never buildable) — but KEEP the `HealerTower` behaviour

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-14 (CLI)
**Silo:** Catalog / data hygiene
**Source:** OWNER RULING, 2026-08-14 — *"i do not know what the town healer is"* → *"retire"*

---

## Why the owner didn't recognise it

Because it has never been buildable.

- `tower_healer` appears in **no build category**. `Assets/Resources/Data/Canonical/build-categories.json`
  lists `healing_caravan` and does **not** list `tower_healer`.
- `Assets/Editor/Regression/BuildCardArtRegression.cs:64` states it plainly:
  `"tower_healer",  // legacy Support verb only - not reachable from Town/Def`
- It has an empty `description`, so even where it surfaced it would say nothing.

The row *looks* live — real cost ladder, real `behaviorId`, working code behind it — which is exactly
why it kept being treated as a live building.

## What it cost

WO-947 spent **owner pin 2** (*"yes AoE healing"*) partly on this row. An agent then reported it as
*"Support, not locked"* and **player-reachable**, and that claim was passed to the owner as a felt
economy warning (that the Healer Tower would now need crystal income early). **The data refutes it** —
nothing lists the row, so no player can build it and there is no early-economy consequence.

This is the **third id-over-data misread on 2026-08-14**, after `tower_wall_wizard` (WO-989) and
`arcane-tower`. The pattern: a row whose *identity* implies one thing while its *data* says another,
trusted on the identity.

## ⛔ RETIRE THE ROW. DO NOT DELETE THE BEHAVIOUR.

`Assets/_Modules/Village/Catalog/StructureFactory.cs:935` `case "HealerTower":` carries this header:

> *"HealerTower - WO-891. The FIRST instance of the general support/offensive FIELD pattern, and the
> proof of its thesis: a new structure is stats plus TWO TAGS. It copies range / fireRate / magnitude
> off entry.repo exactly the way DefenseTower's case above does, then hands SupportFieldStructure an
> element tag (presentation) and an effect tag (gameplay). NOT a clone of HealingFountain..."*

And `:925` holds a commented-out `case "SlowFieldTower":` — the intended next sibling of that pattern.

**Deleting this code discards the worked example the pattern is meant to be copied from.** The row is
dead weight; the behaviour is documentation that compiles. Retire the first, keep the second, and say
so at the call site so nobody "finishes the cleanup" later.

## Scope

**Remove:**
- The `tower_healer` row from **both** catalog copies — `Assets/Resources/Data/Canonical/structures-catalog.json`
  and `Assets/StreamingAssets/Data/Canonical/structures-catalog.json`. **Resources WINS at runtime.**
  Verify byte-identical (md5) after, and report the hash.
- Its entries from `Assets/Editor/Regression/CostBasketSeparationRegression.cs` — it is currently in
  `MagicalIds` and in the `[applied]` case table (added earlier today under WO-947). Removing the row
  means those references must go with it, or the suite fails looking for a row that no longer exists.
- Its entry in `Assets/Editor/Regression/BuildCardArtRegression.cs:64`, whose comment exists solely to
  excuse an unreachable row.
- Check `CatalogBootstrap.RegisterFallback` for a `tower_healer` mirror — `BuildEconomyRegression`
  gate 12 `[fallback-parity]` deep-compares every public `RepoProps` field against the catalog, so a
  stale mirror entry for a deleted row is a red build.

**Keep, with a note added:**
- `StructureFactory.cs:935` `case "HealerTower":` and its header — annotate that the catalog row was
  retired 2026-08-14 by owner ruling, that the case is retained deliberately as the reference
  implementation of the WO-891 field pattern, and that it is currently **unreferenced by any catalog
  row**. Without that note the next reader deletes it as dead code.
- The commented-out `case "SlowFieldTower":` at `:925`.

## Save safety

Catalog ids are persisted (save schema v36 `everBuiltStructureIds`; base layouts replay by id), so
removing a row is normally a migration hazard — see WO-989.

**Here it is almost certainly safe, because the row was never reachable**, so no save should carry one.
**Verify rather than assume:** confirm no baked base layout, no fixture, and no test save references
`tower_healer`. If any does, keep a read-side tolerance that drops the id with a logged warning rather
than throwing — a save that fails to load is far worse than a missing decoration.

## Acceptance criteria

- `tower_healer` appears nowhere except the retained `StructureFactory` note.
- Both catalog copies byte-identical; catalog version bumped.
- Re-parse the catalog and confirm the cost-basket invariant still holds: **zero** rows carrying
  wood AND iron AND crystals, and the crystal-carrying rows are exactly the intended magical set.
- `COMPILE_GATE_OK`, and every suite that named the row updated in the same change.
- The `HealerTower` case still compiles and is still reachable by `behaviorId` if a future row uses it.

## Sequencing

⚠ Land **after** the `arcane-tower` (Cathedral of Magic) lane, which is editing these same catalog
rows and the same regression file. Two lanes on one catalog will collide.

## ANSWERED — the disposition, and where the idea actually lives (OWNER, 2026-08-14)

> *"the healing tower idea is what caravans replaced. this way they can eventually be unlocked to
> recover damage like for tree of life and nearby troops"*

So this is **supersession, not deletion of an idea**. `healing_caravan` is the Healer Tower's
replacement, and it inherits the concept. Two consequences, both binding on how this ticket is worded:

1. **Retiring `tower_healer` is correct and final.** It is not "an unbuilt feature we might want back" —
   its role is filled. Do **not** resurrect the row, and do not treat the retained `HealerTower` code
   as a parked feature. It is kept **only** as the WO-891 field-pattern reference (see above).
2. **The AoE field-heal capability is a FUTURE CARAVAN UNLOCK**, not a new tower. The owner's design:
   caravans can eventually be unlocked to **recover damage — for the Tree of Life (the Heart of
   Elarion) and nearby troops.** That is a healing *field* around the caravan, distinct from
   `HealingFountain`'s bespoke job of topping the Heart up out of battle.

3. **A CARAVAN IS MOBILE — VERY SLOW, BUT MOBILE** (owner, 2026-08-14: *"by a caravan its mobile, but
   very slow"*). This is the whole reason a caravan replaced a tower rather than re-skinning one: a
   tower is a fixed point, a caravan trades placement permanence for **reach**. Very slow movement is
   the cost that balances a heal field that can go where it is needed.
   ⚠ **Current data does not reflect this:** `healing_caravan` carries `behaviorId: HealingFountain` —
   a static, bespoke singleton. So mobility is **design intent, not shipped behaviour**. Do not assert
   in any doc that the caravan moves today. Specced as **WO-991**.

⚠ **Do not implement that unlock in this ticket.** It is a separate, reachable-building feature and
deserves its own spec. Record it here so the intent is not lost with the retired row — and note the
irony worth remembering: the retained `HealerTower` case is the *worked example* of exactly the
support-FIELD pattern that future caravan unlock will need. That is the strongest argument for keeping
the code while deleting the row.
