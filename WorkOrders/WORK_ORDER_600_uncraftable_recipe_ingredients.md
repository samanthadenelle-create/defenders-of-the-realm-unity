# WO-600 — Uncraftable recipes: 5 ingredients drop from no loot table

**Status:** READY TO IMPLEMENT
**Lane:** 6 (Economy/data) — loot-tables.json + consumable-recipes.json only
**Origin:** DataRegression 2026-07-02 (16 failures, pre-existing — verified absent in HEAD too; surfaced
by the first full regression run after the vendor-stock asserts were added)

## The failures (all one class)
`ing_moonbloom`, `ing_spring_water`, `ing_cloth_scrap`, `ing_quickfoot`, `ing_shadowcap` are referenced
by 10 recipes in `consumable-recipes.json` (mending salve, emberfire bomb, swiftstep elixir, suppressing
smoke, heartward draught, field poultice, hearthfire stew, warden's campfire, purifying draught) but
appear in NO loot table — the recipes are permanently uncraftable.

## Fix (pick per ingredient, data-only)
Either (a) author drop entries in `loot-tables.json` (fitting sources: herbs/moonbloom/shadowcap from
overworld forage or enemy herb drops; cloth_scrap from humanoid orcs; spring_water from a harvest node),
or (b) make the ingredient vendor-purchasable via the new `vendors.json` market goods query, or
(c) retire the recipe if the content isn't wanted. Whatever mix: `DataRegression.RunAll` must go
REGRESSION_OK with zero uncraftable entries. Both JSON mirrors (Resources + StreamingAssets) in sync.

## Acceptance
- [ ] REGRESSION_OK (0 uncraftable-ingredient failures)
- [ ] Every kept recipe obtainable end-to-end (drop or purchase path exists)
- [ ] Mirrors identical
