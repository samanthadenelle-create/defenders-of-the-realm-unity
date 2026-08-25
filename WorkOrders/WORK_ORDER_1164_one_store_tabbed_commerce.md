# WORK ORDER 1164 — One Store: all selling moves to one tabbed shop, buildings keep their benches

**Status:** SPEC — §5.1 is RULED (HUD entry is a **TAB IN BAG**, the town Store building stays, both doorways open the one `PackStore`), but **§§5.2–5.3 remain OPEN**: vendor-NPC disposition and the four vendor-targeted quest stages. ⚠ Also unresolved: the ticket promises separate game-currency and Realm stores yet names the shared destination `PackStore` (the real-money surface) — say whether `PackStore` is EXTENDED, RENAMED, or merely the opening mechanism. ⛔ Land WO-1163 first (§6). *(Status audit 2026-08-24: lead-verified bucket correction; body unchanged.)*

**Minted:** 2026-08-23 (CLI), banner bumped 1164 → 1165 in the same edit.
**Ruled by:** the owner, 2026-08-23.

---

## 0. THE RULING

> *"If all the stores are doing now is selling, why don't we have just the store which has the
> different tabs and all the selling is routed directly through there? This way we don't need a
> storefront for the armor. We don't need a storefront for the weaponsmith. We don't need a store
> that only sells potions. We can sell everything out of that same store. And then the only other
> store would be the realm one, which is your PACKS."*
> · *"For mobile, it might be better and easier to simplify it as one."*

**Two stores, total:**
- **The Store** — everything sold for GAME currency, tabbed: weapons · armour · rings/amulets · consumables+materials
- **The Realm Store** — packs, REAL money. Already separate; untouched by this ticket.

## 1. ⭐ WHY THIS IS WORTH DOING — it deletes a bug class, not just a screen

**The entire vendor-naming catastrophe of 2026-08-23 existed BECAUSE buildings were vendors.**
`forge` sold weapons, so it needed a shop identity, so it needed a name — and the names were crossed
three ways for weeks (`forge` displayed "Armorer", `armorer` displayed "Blacksmith", `workshop`
displayed "Weaponsmith"). That cost a full day: five naming authorities to reconcile, a role table
to build, four oracles to re-point, and an owner told to build a Forge she already owned.

**Take selling out of the buildings and that class cannot recur.** A building with no shelf needs no
shop name, no `vendors.json` row, and no vendor NPC identity. It is a subtraction, and subtractions
do not drift.

**And the mobile case is independent of that:** on a phone, walking to four buildings to buy four
kinds of thing is friction with no payoff. Matches the owner's standing tiebreaker (what would Clash
of Clans do — one shop, reachable, tabbed).

## 2. ⛔ THE CUT — sell moves, UPGRADE STAYS

This is the load-bearing distinction and the ticket fails if it is missed:

| Concern | Today | After |
|---|---|---|
| **Selling** gear/goods | 4 building storefronts | **the Store, tabbed** |
| **Upgrading** gear | the trade building's bench | **unchanged — stays in the building** |
| Producing / storing | producers + containers | unchanged (WO-1163) |

WO-707 ruled each trade building as *that trade's UPGRADE vendor*. That ruling **survives**: the
Forge remains where weapons are upgraded, the Armorer where armour is, the Arcane Tower where magic
is. They simply stop having a shelf.

⚠ **Which gives them a CLEANER identity than they have now** — "the weapon upgrade bench" is
unambiguous in a way "the shop that sells weapons but is displayed Armorer" never was.

⚠ **Do NOT delete the trade buildings.** They keep upgrades, they keep their place in the town, and
their ids are frozen save keys in `everBuiltStructureIds` / BaseLayout on a LIVE build.

## 3. What actually changes

- **`vendors.json` stops being a building registry and becomes a TAB definition.** The hard part is
  already done — the four rows carry exactly the category bands the tabs need (`forge` → weapon,
  `armorer` → armor, `jeweler` → ring+amulet+gem, `market` → consumable+material). The stock QUERY
  model (categories, `classFilter`, `maxReqLevel`, `onlyEquippable`, `perLevelCap`, `emptyLine`,
  `footerLine`, `lockedPreviewLevels`) carries over per-tab **unchanged**.
- **`VendorStockContract` / `VendorStockResolver`** already route by role after today's work
  (WO-1161), so a tab resolves the same way a building did.
- **The potions-only storefront retires** — the owner asked for this directly on 2026-08-17
  (*"can we have the crafting station also sell the potions so we dont need a store that only sells
  potions?"*). This supersedes that with a better answer: the Store sells them, not the workshop.

## 4. ⚠ THE BLAST RADIUS — frozen join keys, the same ground that made today slow

None of this is hard; all of it is *referenced*, which is what bites:

1. **Vendor NPCs join by `StructureId` into `dialogues.json`** (`CastleVendorNpcInjector`). Four
   vendors' worth of NPC placement, talk routes and `AnchorRoles` seating bodies at building doors.
   ⛔ Ids are FROZEN — do not rename them to reflect the new reality.
2. **Quests target vendors by exact string.** `vendor.forge`, `vendor.armorer`, `vendor.jeweler`,
   `vendor.lumbermill` stages exist and `StoryQuestSignalBridge` matches `targetId` by **exact
   equality** — it consults no resolver. A "go to the Forge and buy X" stage must be re-pointed or
   re-authored, not left to resolve.
3. **Tutorial beats** reference vendor placement/talk signals.
4. **`ShortfallPackOffer`** routes a resource shortfall to a purchase surface — confirm it lands on
   the right store after the split (game currency vs packs).

## 5. ⛔ ANSWER BEFORE IMPLEMENTING

1. ⭐ **RULED 2026-08-23 — BOTH.** Owner: *"having the ability to walk up to a store has value, but
   I also agree... it's on HUD. For mobile."*
   **The Store building STAYS in the town** (walk up and interact — the town remains a place, not a
   menu) **AND a HUD entry opens the same panel** (the mobile affordance).
   ⛔ **ONE DESTINATION, TWO DOORWAYS — never two implementations.** The precedent is already in
   this codebase and it was won the hard way: `PlacedStructureUpgradeService` is the SINGLE start
   path for upgrades with several doorways into it, after a second resolution site told a player a
   level-1 tower was "fully enhanced — tier 0 of 0". **Do not build a second store panel for the
   HUD.** Both routes open the one `PackStore` surface.
   ⚠ **SUB-QUESTION STILL OPEN — where the HUD entry lives.** The calm(town) action bar is at SIX
   visible faces and `MaxVisibleFaces` was deliberately cut 7 → 6; `ButtonCount` stays 7 for
   enum/array identity. A Store face re-opens that ruled budget. Precedent for the alternative:
   **Map left the bar and became a tab inside Bag** (flag-gated), with `ActionBarButtonId.Map` left
   dormant at ordinal 4 — the face arrays are indexed by ordinal, so **nothing may be renumbered.**
   Options: a 7th bar face (re-opens the budget) · a tab inside an existing face · a persistent
   corner chip outside the bar. **Owner's call.**
2. **Do the vendor NPCs stay at their buildings?** They can remain as flavour/talk without a shelf,
   move to the Store, or retire. This is a felt/creative call.
3. **What happens to the four quest stages that send the player to a vendor?**

## 6. Sequencing

⛔ **Land WO-1163 first.** It is already fully ruled and touches resources; this touches commerce.
Running both at once means a felt-test cannot attribute what broke.

1. Tabs in the Store, reading `vendors.json` per-tab.
2. Remove the shelf from the four buildings; **keep the upgrade bench.**
3. Re-point quests + tutorial references.
4. Decide NPC disposition (§5.2).
5. Gate + captured run proving each tab stocks what its old storefront did.
6. **Owner felt-test** — on the phone, which is the only place the mobile argument can be judged.

---

## ⭐ OWNER RULING 2026-08-24

### §5.1 sub-question — the HUD entry is **A TAB IN BAG.**

Not a 7th action-bar face, not a corner chip. This is the precedent the ticket itself named: **Map
left the bar and became a tab inside Bag**, with `ActionBarButtonId.Map` left dormant at ordinal 4.
Store follows that road.

Consequences, and they are why this option was chosen:

- The calm(town) bar stays at **SIX visible faces**; `MaxVisibleFaces` stays **6** and `ButtonCount`
  stays **7** for enum/array identity. The ruled face budget is **not re-opened**.
- ⛔ **NOTHING IS RENUMBERED.** The face arrays are indexed by ordinal; no existing
  `ActionBarButtonId` value moves, and no new one is minted for Store.

### The town **Store building STAYS**

Unchanged from the 08-23 ruling: walk up and interact — the town remains a place, not a menu.

### ⛔ TWO ENTRANCES, **ONE** STORE IMPLEMENTATION

The Bag tab and the town building **open the same `PackStore` surface**. ⛔ **Never fork them.**

This is not a style preference; the codebase already paid for the lesson.
`PlacedStructureUpgradeService` is the SINGLE start path for upgrades with several doorways into it,
and it became that only after a second resolution site told a player a level-1 tower was *"fully
enhanced — tier 0 of 0"*. A second store panel would re-buy that exact defect in the one place where
the wrong answer costs real money. **Do not build a second store panel for the HUD tab.**

### Still open (unchanged by this ruling)

- §5.2 — vendor NPC disposition (stay as flavour / move to the Store / retire). Felt/creative call.
- §5.3 — the four quest stages that send the player to a vendor.

Neither blocks starting: §6's sequencing already puts tabs + shelf removal ahead of them.
