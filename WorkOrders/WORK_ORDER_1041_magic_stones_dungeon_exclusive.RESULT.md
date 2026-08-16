# RESULT — WO-1041 dungeon-exclusive gem drops

**Date:** 2026-08-16  **Seat:** CLI (commit `eff761fcc`, shipped in the same lane as WO-1042)
**Status:** DONE — pending PO felt-verify

Owner, 2026-08-16: *"a stone or a weapon or a ring"* · *"rings exist"* · *"**but magic stones should be
dungeon exclusive**"*.

## What shipped

⚠ **THE THESIS WAS ALREADY VOID IN THE SHIPPED TREE.** `vendors.json`'s jeweler carried category `"gem"`
and `VendorStockResolver` stocked all three crystals at 20/20/18 g — the "dungeon-exclusive" gems were
**on sale for gold** before any drop existed. A rarity rule authored on top of that would have been
decorative.

- New **`DungeonExclusiveItems`** (`Assets/_Modules/Core/Catalog/DungeonExclusiveItems.cs`) is the single
  authority; `VendorStockResolver` consults it, so exclusivity is enforced on the **gem AND material
  bands**, not just one list.
- The **drop side** is `DungeonRunGrade` + `DungeonRunPayout` (`DungeonController`,
  `DungeonTreasurePanel`): a run drops the rough stone, and the run grade raises the polish odds — the
  source WO-1042 needed and did not have.
- Oracle **`DungeonGemExclusivityRegression`** locks it: a vendor row re-acquiring a dungeon-exclusive id
  or category fails the gate.

## Deliberately NOT done

- **No new drop system.** This is a drop table plus a rarity rule, exactly as the WO scoped it after §2
  was measured — the stone loop itself is WO-1042's (and WO-553's) machinery.
- No change to the existing ring chain or to gold pricing of non-exclusive vendor stock.

## Owner decision left open

- The **grade → odds** mapping is tuning, authored in `jewel-polish.json`, and has not been felt-tested.
