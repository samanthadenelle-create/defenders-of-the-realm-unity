<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-03
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-03) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-598 — Vendor wares: per-shop content mapping (the honest shelf)

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Lane:** 6 (Economy/Progression) — data + service, no scene files
**Origin:** owner F8 sweep 2026-07-02 (flags 03, 05, 08, 11) + UI audit + monetization review "honest shelf" slice

## The captured evidence
- flag_03: "Market sells consumables and crafting materials? why are we showing what we are wearing and armor weapons" — Market opens the generic equip-shop UI (armor/weapons tabs + paper-doll context) instead of a consumables/materials shelf.
- flag_08: The Forge lists **Apprentice Wand / Oakheart Staff / Arcane Scepter / Voidcaller Staff — all `Class: Mage`** — to a Knight, in a Knight-only V1 (roster canon: Knight + orcs only).
- flag_11: "jeweler sells weapons?" — Jeweler Wares shows BUY/SELL armor/weapons tabs, "No wares in stock", and "No gear here fits Grom yet."
- flag_05: jeweler is a bare `Interact: Jeweler` panel-pop with no NPC (covered by the NPC card WO, listed here for the content tie-in).

## Rule (owner canon)
Every vendor's shelf is **data-mapped to its trade**, drawn from the item catalog:
- **Market** → consumables + crafting materials (no equip UI, no paper-doll)
- **Forge** → weapons + armor **filtered to obtainable-by-current-roster** (V1 = Knight: no Mage-class listings; `Requires Lv N` rows OK — aspiration is fine, wrong-class is not)
- **Jeweler** → rings + amulets (the v26 equip slots) + gem/crafting stock — never weapons
- A vendor with no valid stock for the player shows an authored "come back later" line, never a raw empty grid.

## Implementation sketch
1. Data: per-vendor `stock` definition (catalog query: categories + class filter + tier gate) in the canonical data JSON (vendor/shops registry — extend what `PlayStructure`/vendor injectors already key off; owner thinks in data structures: shelf = a query row, not code).
2. Service: one `VendorStockResolver` (VM-side) resolves the query against the item catalog + roster; Views bind the result (no state-pulls in the View).
3. The shop UI variant per trade: Market gets the consumables list layout (no equip tabs); Forge/Jeweler keep gear layout with correct category tabs.
4. Regression: DataRegression asserts every vendor's query resolves ≥1 item OR has an authored empty-line; asserts no `Class: Mage` item can appear in a Knight-only roster build.

## Acceptance criteria
- [ ] Market: consumables/materials only, no armor/weapons tabs
- [ ] Forge: Knight-usable weapons/armor only (level-gated rows allowed)
- [ ] Jeweler: rings/amulets/gems only
- [ ] No vendor ever renders "No wares in stock" raw — authored line instead
- [ ] Stock defined in data, resolvable + regression-gated; zero hardcoded shelf lists in Views

## Do NOT touch
- PackStore/premium packs (separate monetization lane)
- The store chrome redo (UI audit lane — this WO is content, that one is presentation)

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `VendorStockResolver.cs:113; VendorRegistry.cs:28` — stock resolver shipped. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
