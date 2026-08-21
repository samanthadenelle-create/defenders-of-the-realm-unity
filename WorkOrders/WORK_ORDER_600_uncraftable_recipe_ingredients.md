<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-03
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-03) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-600 — Uncraftable recipes: 5 ingredients drop from no loot table

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
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

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `5 ingredients absent from loot-tables.json` — recipes uncraftable. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict. ⚠ NOTE FOR ANYONE REOPENING: the 2026-08-21 read-only audit had classified this one OPEN - STILL VALID, with the evidence cited above. The owner's review supersedes that call (owner statements are ground truth). The audit line is left in place deliberately, so if this work turns out to be needed, the evidence for it is still here rather than erased.
