# WORK ORDER 1164 — One Store: all selling moves to one tabbed shop, buildings keep their benches

**Status:** SPEC — READY TO IMPLEMENT once §5 is answered. ⛔ Keep this SEPARATE from WO-1163: one ticket changes what RESOURCES are, this one changes where COMMERCE lives. Tangling them makes both impossible to felt-test.

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

1. **Is the Store WALKED TO or a HUD BUTTON?** The owner's "for mobile, simplify" implies a HUD
   entry reachable anywhere (the CoC shape). ⚠ But this decides whether the town is *a place you
   traverse* or *a menu you manage* — a bigger call than the plumbing, and it interacts with the
   bottom action bar, which is at SIX visible faces with `MaxVisibleFaces` already reduced from 7.
   **If it needs a bar face, that is a seventh, and the bar's face budget is a ruled constraint.**
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
