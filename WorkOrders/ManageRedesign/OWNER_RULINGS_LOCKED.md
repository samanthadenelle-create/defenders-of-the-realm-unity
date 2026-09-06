# Owner Rulings — Locked for This Work Order Set

1. **UI architecture:** dumb UI. Model/VM owns logic and commands.
2. **Reuse:** prefer one common Manage presentation class/contract rather than three independent screens.
3. **Top-level Manage:** BUILD / ARMY / RESEARCH.
4. **Defense + Buildings:** merged into BUILD because they share the Builder queue.
5. **BUILD filters:** ALL / ECONOMY / DEFENSE / CRAFT / STORAGE / CIVIC.
6. **ALL scrolling:** allowed.
7. **BUILD density target:** ≥12 visible tiles when the dataset/filter contains ≥12.
8. **ARMY:** all 9 troops visible in one 3×3 grid.
9. **RESEARCH:** school-first, not a flat 17-row list.
10. **Heart:** becomes the realm-progression spine.
11. **Player-facing tier name:** Heart Level.
12. **Heart reach:** Heart upgrade may expand buildable reach; value must be data/model-driven.
13. **MAX:** property of upgrade track, not necessarily the item.
14. **Queue blocked:** first-class action state.
15. **Built-but-upgrade-gated:** do not label the owned item as locked; gate the upgrade action.
16. **Tile state:** mandatory.
17. **Global Queue:** P0.
18. **Direct prerequisite navigation:** P0.
19. **Auditor:** must be fixed for scrolled content, not waived.
20. **BUILD inventory count:** must be reconciled from live definitions before numeric tests are finalized.


---

## Provenance and reconciliation (added by the CLI seat, 2026-09-06)

Authored outside this repo and delivered by the owner as `Elarion_Manage_Redesign_Detailed_WorkOrders.zip`
on 2026-09-06. Filed verbatim; **the body above is the author's and has not been edited.** Everything the CLI
seat adds appears under a heading like this one.

**Numbering:** this set uses a **2000-block**, a THIRD namespace alongside the CLI main line and the UI seat's
reserved block. It is declared on `CLI_LANES_WO_NUMBERS.md` so it cannot collide (CLAUDE.md section 2 - the banner is the
sole authority). Do not renumber these into the main line.

**Supersedes:** `WORK_ORDER_1427` (why-can't-I) and `WORK_ORDER_1428` (the Manage card grows to the mockup).
Both were minted earlier the same day from the owner's playtest and her mockup; this program subsumes them and
goes further by replacing the rail model rather than enriching the card.

**Measured facts this set is consistent with** (from `docs/manage-flow-map/MAP.md`, run `Builds/flowmap1`):
43 rail rows across four areas, about two visible at a time; Buildings 6 + Defense 11 = 17, which is the number
the canon cites; the scroll auditor reporting `geometry=5 touch=5` on deliberately scrolled frames, which WO-2016
is right to call a fix rather than a waiver.


## Rulings added after delivery (CLI seat, 2026-09-06)

**21. THE TWO BARRACKS LEVELS ARE MERGED. The barracks BUILDING TIER gates troop unlocks.**

Owner ruling 2026-09-06, in answer to "there are two barracks levels, which way do you want to resolve it":
**"Merge them - the building tier gates troops."**

*Measured before the ruling, at source:*
- `GameState.BarracksLevel` (`GameState.cs:506`, save key `barracksLevel`, `SaveSchema.cs:613`) is a SEPARATE field
  from `GameState.BuildingTiers["barracks"]`, the ladder the player upgrades in Manage.
- It is raised in exactly one place: `BarracksProgression.ApplyBarracksUpgrade` (`:226-234`), the completion effect of
  a `BarracksUpgrade` job. That job is composed only by `BarracksPanelVM`, reachable only from
  `BarracksPanel.ShowBarracksUI` - which has **ZERO CALLERS**, proven four ways including a script-GUID search.
- Consequence: the field sits at its founding value of 1 forever (`GameStateService.cs:1235`), and **7 of the 9 troop
  types are unreachable by any player action** - Spearman, Field Cleric, Shieldguard, Outrider, Siege Catapult,
  Battlemage, Echo Legionnaire - along with 5 barracks-level rungs and 42 troop-level rungs.
- Upgrading the barracks BUILDING does nothing for the army, which is precisely the trap: two numbers spelled the same
  way on different scales, and the one the player can touch is not the one that matters. **Identical in shape to the
  village-tier defect fixed the same day (WO-1423).**

*What this ruling requires, and it lands on WO-2008 / WO-2009 / WO-2011:*
1. Troop unlock reads the barracks **building tier**. `BarracksService.IsTroopUnlocked` and
   `BarracksProgression.IsTroopUnlocked(troopId, level)` take their level from `ModifierService.TierOf("barracks")`.
2. `GameState.BarracksLevel` is retired as a GATE. Read-migrate it on load so existing saves do not regress - never
   delete a live save key without a migration (CLAUDE.md section 8).
3. `BarracksPanel` / `BarracksPanelVM` / `ShowBarracksUI` and the `BarracksUpgrade` job kind are then dead weight.
   Decide deliberately: delete them, or keep the panel as the troop DETAIL surface WO-2009 needs. **Do not leave an
   unreachable panel in the tree** - that is what caused this.
4. WO-2008's locked-tile CTA routes to the barracks BUILDING card in BUILD, which already exists and already works.
   No new screen, and ruling 18 (direct prerequisite navigation) is satisfied with a door that genuinely opens.
5. An oracle must fail the build if any troop's unlock level exceeds the barracks ladder's max tier - the same shape as
   `ProgressionReachabilityRegression`, which now guards the village-tier axis.

**22. THE CATHEDRAL LADDER IS PRICED IN STONE, NOT CRYSTALS. The DATA is corrected to match the CHARGE.**

Owner ruling 2026-09-06, verbatim: **"i think stone is better as getting crystals is very hard, we can always revisit
if we see."**

*The defect this settles* (`docs/PREREQUISITE_REGISTRY_2026-09-06.md`): the Cathedral of Magic tier 2 is AUTHORED as
2,560 Crystals in `building-tiers.json`, and the player is CHARGED 2,560 **Stone**. `BuildingUpgradeService.TierCost`
(`:190-199`) picks the lane by TIER INDEX - T1 Wood, T2 Stone, T3+ Iron - from `Max(costWood, costCrystal)`, so the
authored currency is ignored and the screens show the charged lane. The JSON lies; the charge is what the player feels.

**Ruling: the CHARGE is right and the AUTHORING is wrong.** Correct the data to say what is actually taken. Do NOT
"fix" the code to start charging crystals - crystals are the scarce currency (250 at founding, and the village-tier
ladder already costs 250 x next), and re-pointing this ladder at them would price the Cathedral out of reach.
Revisit later if play shows otherwise.

⚠ **Consequence to signpost, not to balance away:** stone's base bank is **2,000** and this rung costs **2,560**, so it
is unpayable until a **Silo** is built and raised - the same shape as Archer Tower L3 at 3,150 wood against a 3,000
wood ceiling. Nothing told the player that before tonight. The cap-aware refusal added in WO-1425
(`TownBankCapacity.StorageBlockMessage`) must name the Silo and the level here. **Do not lower the cost to fit the base
cap** - the owner rules on balance and the ladder is deliberate.

⚠ **The lane-picker itself is a latent trap beyond this one ladder.** Because `TierCost` derives the resource from the
tier INDEX rather than the authored key, EVERY tier-2 row in the game is charged Stone regardless of what its JSON
says, and `EconomySinkCapRegression` mis-attributes those costs when it scans. Reconciling that is WO-2005's job
(BUILD inventory reconciliation) - it must read the CHARGED lane, not the authored one, or every cost it reports is
wrong for tier 2 and above.

**23. ONE OF EACH STORAGE TYPE. Capacity grows by LEVEL, never by COUNT.**

Owner ruling 2026-09-06, verbatim: **"also cap only one of each storage type, the idea is they should level them"** /
**"if we decide one day we need more space we add another level easy."**

`lumberyard` (wood), `foundry` (iron) and `silo` (stone) become **singleton**: one placed instance each, and the player
raises capacity by upgrading it.

*Why this needs a ruling at all:* measured 2026-09-06, **none of the three container rows carries a singleton flag
today**, while `healing_caravan` does. So a player can place a SECOND lumberyard and gain another full container's
worth of wood ceiling. `TownBankCapacity.BuildSlots` sums capacity over every built container of that resource, so the
cap is currently a function of level AND count. That path is undiscoverable, unbalanced, and it makes the level ladder
pointless - why pay 14,400 wood for L5 to L6 when a second building is cheaper?

**The principle, stated so it survives:** capacity has ONE axis of growth. Raising the ceiling later is then a data
edit - add a rung, or raise a multiplier in `storage-caps.json` - not a change to how many buildings a town holds and
where they fit. Two axes would also mean the "which container do I need" copy from WO-1425 could no longer name a
single answer.

*Implementation notes, for whoever picks this up:*
1. Data-only: set the singleton flag on the three container rows in `structures-catalog.json`. ⚠ Canonical JSON is
   edited in BYTE mode with the LF count proven, and there are TWO copies (`Assets/Resources/Data/Canonical/` and
   `Assets/StreamingAssets/Data/Canonical/`) which must stay identical - a parity oracle reads both.
2. **Existing saves may already hold two.** Do not silently delete one. Decide and record: leave over-cap towns alone
   (grandfathered), or surface it. Never destroy a placed structure the player paid for.
3. ⚠ **Singleton has a known sharp edge, and it bit the caravan the same day:** `StructureSingleton.HasPlacedInstance`
   returns true **from the persisted BaseLayout record alone**, before it looks at live bodies. So a singleton whose
   death does not clean up its record can never be rebuilt. Containers are ordinary `Building`s and route through
   `Destructible.NotifyBroken` correctly, so they are safe - but any future singleton must be checked against that,
   and an oracle asserting "every singleton's death path routes through Destructible" would close the class.
4. `EconomySinkCapRegression`'s ceiling arithmetic assumes ONE container per resource. That assumption becomes true
   with this ruling instead of merely convenient - say so in the suite so nobody "generalises" it back.

**24. THE CATHEDRAL IS PRICED IN A SMALL MULTI-RESOURCE BASKET, NOT CRYSTALS. This SUPERSEDES ruling 22 and the
2026-08-14 cost-basket pin, for the Cathedral.**

Owner ruling 2026-09-06, verbatim: *"regarding the cathedral I just feel that it's too difficult to attain a large
number of crystals so switching to needing a smaller amount of multiple resources that are easily obtained. Might be
the way to go being that we can't get people to play the game so instead of making it challenging, let's try and
simplify it a little bit so maybe they're more engaged."*

**The reasoning is a product ruling, and it outranks the mechanics:** engagement first, challenge second. Record it as
the lens, not just the number - the same lens should be applied whenever a cost is defended on difficulty grounds.

*What this resolves.* Ruling 22 said the Cathedral's authoring should be corrected to STONE to match what
`BuildingUpgradeService.TierCost` already charges. Lane B then found that correcting it was **blocked** by an earlier
owner pin: `CostBasketSeparationRegression.cs:140-149` lists `arcane-tower` in `MagicalIds` on **owner pin 5,
2026-08-14** (*"cathedral of magic is where all magic upgrades are"*), and case `[tiers-basket]` FAILS any
`costWood > 0` on a magical id. Two owner rulings collided and the lane correctly stopped and touched nothing.

**Ruling 24 breaks the tie by changing the design rather than picking a side:** the Cathedral's ladder is no longer a
single-resource crystal basket at all. It becomes a SMALL basket of several EASILY OBTAINED resources.

*Consequences, all of which must move together in ONE commit (CLAUDE.md section 15):*
1. `building-tiers.json` (BOTH canonical copies, byte-mode, LF count proven) - the four `arcane-tower` rows are
   re-authored. **The owner sets the numbers; do not invent them.** Ask for the four baskets.
2. ⛔ **`CostBasketSeparationRegression`'s `MagicalIds` pin RE-POINTS WITH THIS RULING.** `arcane-tower` leaves the
   crystal-only set. Do NOT delete the suite - the wood+iron / crystal separation still holds for everything else
   (WO-947). Update its header to record that the Cathedral was carved out on 2026-09-06 and why, so the next seat does
   not "restore" it.
3. ⚠ `BuildingUpgradeService.TierCost` picks ONE lane by tier index (T1 Wood, T2 Stone, T3+ Iron) from
   `Max(CostWood, CostCrystal)`. **A multi-resource basket cannot be expressed through it.** Either the charge path
   learns to spend a real basket, or the ruling cannot be implemented as stated. This is the load-bearing engineering
   consequence and it must be settled before the data is touched - see WO-2005/WO-2007.
4. The stone-ceiling problem ruling 22 flagged (2,560 stone against a 2,000 base bank) disappears if the basket is
   small and spread. Re-check it after the numbers land rather than assuming.

*Open for the owner:* the four baskets themselves. Suggested shape to react to, NOT a decision: T1 wood+stone, T2
wood+stone+iron, T3 stone+iron, T4 all three plus a token crystal cost so the Cathedral keeps a magical flavour.

**25. BUILD GETS A "MANAGE PLACED" DOOR - move / upgrade / sell a structure already on the map.**

Owner ruling 2026-09-06, from a friend's playtest: *"he accidentally put a palisade down and he didn't mean to and now
he has no way to move the Palisade... we might need to add one more card, which is just move or manage so that they can
go to the map, select the piece, either move, upgrade or sell. I think right now we lost that option when we simplified
the UI."*

*Verified at source before recording, and it is the same disease as every other defect in this program:*
**the capability EXISTS and is fully wired; only the DOOR is missing.**
- `BuildModeController.BeginMoveSelected` (`:2775`), `CommitMove` (`:2815`), `SellSelected` (`:2431`) are all live.
- `BuildSelectionUI` (`BuildSelectionUI.cs:35`) raises `OnMoveRequested` (`:38`) and `OnSellRequested` (`:41`) from
  real buttons (`:173`), and `BuildModeController` subscribes to both (`:4086-4087`).
- So nothing needs building. The player simply cannot FIND it: the only route is enter build mode, then tap the exact
  placed structure. A player who mis-taps a palisade during placement has no reason to think "re-enter build mode and
  tap it" is the fix.

**Ruling: BUILD gains a MANAGE PLACED entry** that takes the player to the map in a select-a-placed-structure mode,
and the selected piece offers **MOVE / UPGRADE / SELL**. Whether it is a sixth filter chip, a card, or a persistent
affordance on the BUILD tab is a UI call for WO-2006; the requirement is that it is reachable WITHOUT already knowing
about build mode.

*Open questions, to answer before implementing - do not assume:*
1. **Is a placed WALL even selectable today?** The friend's case is a palisade specifically. `AutoPilotDriver.cs:2912`
   already carries a failure string *"tap on structure never showed BuildSelectionUI"*, so this seam has a known way to
   fail. Prove it with a wall before designing around it.
2. **Does SELL refund?** `Destructible.cs:174` says the destroy path *"mirrors the sell path minus the refund"*, so a
   sell refund exists. Confirm the amount and whether it differs from the WO-911 cancel-refund rule (100% of what was
   paid, flat).
3. Does MOVE cost anything, and is a moved structure's upgrade progress preserved? `CommitMove` writes the validated
   snap cell (`UpdateLayoutEntry`), so the record follows - but confirm the level does too.

⚠ **The through-line, stated because it keeps recurring:** this is the fifth capability found on 2026-09-06 that is
built, correct, and unreachable - alongside the barracks panel, the village-tier control, the Talent tree panel and the
kill-drop materials the player is granted but never shown. **`PanelDoorRegression` (shipped this wave) catches the
panel-shaped instances of this.** A capability that lives behind a MODE rather than a panel is invisible to it. Worth a
sibling oracle: every player-facing verb the code implements has at least one reachable affordance.

*Clarification, same session:* **"we used to have that method, but the problem is now when you go to build it gives you
a series of options... now we have no way to get directly to the raw screen where you can manage them."**

So this is a RESTORED FRONT DOOR, not a new feature. BUILD used to put the player on the MAP; it now puts them in front
of a CATALOGUE (the collection cards), and the map became a place you can only reach if you already know it exists.
The card goes **straight to the town in select-a-placed-structure mode**, with no catalogue in between.

⚠ **The owner's own mockup already contained this.** The Manage-Buildings mockup she pasted earlier the same day has an
**Edit** affordance pinned at the FOOT OF THE RAIL. It was logged at the time as an open question (*"the Edit
affordance at the rail's foot - what does it open? Reorder the list, or enter build mode?"*, WO-1428 section 7 item 1).
This ruling is the answer: **Edit opens the raw map in manage-placed mode.** Build to the mockup's placement.

**26. THE ECONOMY LOOP: collectors are SAFE, storage is EXPOSED, and early storage is CHEAP AND SELF-FUNDING.**

A design conversation on 2026-09-06 settled the shape of the resource economy. Recorded together because the parts only
make sense as one loop.

**26a. Collectors hold; storage is what can be taken.** Owner: *"there's a storage capacity in the storage items so
anything that's in their collectors is safe. Anything it's in storage has a liability to be taken from."* This inverts
the usual model deliberately - the collector is a SAFE HOLDING PEN, not a raid target. The risk begins when value moves
to storage.

**26b. Collectors CAP and then STALL; overflow goes to the matching storage automatically.** Owner: *"the collectors
had a cap as the collectors hit their cap. They couldn't produce anymore unless they had a storage to put it in the
overflow by default would automatically go to their matching storage."*
**Consequence that makes storage legible:** a storage upgrade is not "hold more" in the abstract, it is **UPTIME**.
Without storage, or with storage full, the collectors stop and the player stops earning. That is the strongest reason
you can give someone to upgrade a building.
⚠ **The collector's own cap is the dial that tunes the entire economy** - it decides how often value is forced from the
safe pool into the exposed one. Measure what it is today before tuning anything.

**26c. Raid loss falls on STORAGE ONLY. The attacker is rewarded without the defender losing collector contents.**
Owner offered three options; the ruling is the friendliest: *"you could just use it as a simple reward for the attacker
with no loss to the defender, which might be friendlier, make the loss only on the storage."*
Rationale, and it is the retention lens again: the main reason players quit this genre is opening the app to find
earned progress gone. Storage still bleeds, so 26a's risk/reward decision survives intact; the collector simply stops
being a second punishment. **It also gives defense a specific job** - walls and towers protect the STORAGE, which is
the thing worth taking.
⚠ **Guard rail:** a reward the defender does not pay for is free money. It needs a per-base cooldown or a diminishing
return, or farming one undefended target forever becomes optimal and raiding stops meaning anything.

**26d. Early storage levels are CHEAP and SELF-FUNDING; the curve scales hard later.** Owner: *"level one and level two
should be relatively cheap small caps... if it's for the wood storage it should only take a little bit of wood, if it's
stone storage only a little bit of stone, iron only a little bit of iron. Just make it easy in the beginning, it can
scale up incredibly hard the higher it goes, but in the beginning I think it makes sense to keep it simple to try to
get people hooked."*

*Measured, and it shows the current authoring works against this:* **all three storages cost wood AND iron today** -
lumberyard 800 wood + 320 iron, foundry 960 + 480, silo 960 + 240 - while a new town starts with **0 wood, 0 iron** and
80 stone (`GameStateService.cs:1184-1185`, `NestedTypes.cs:55`). So on day one every storage demands two resources the
player does not have, and the IRON storage demands iron, which the player cannot bank much of until the iron storage
exists. A cold start with a cross-dependency baked in.
**The rule: each storage's early rungs are paid in ITS OWN resource.** The thing already being produced pays for the
thing that lets you produce more of it. It also pre-empts the WO-1425 trap class - a player outgrows the early ceiling
before anything asks them to exceed it.

*Still to verify before any of this is tuned - do NOT assume:*
1. What a collector's cap is today, and how fast it fills relative to storage. `ResourceCollector`,
   `ResourceBuildingHarvester`, `OfflineHarvestService` and a `BankOverflowToastPresenter` all exist - some of this loop
   may already be built.
2. Whether raid loot genuinely spares iron and crystals today (`RaidScoring.ComputeLoot`). 26a and 26c rest on it.
3. Whether the tap-to-harvest gesture exists, and whether overflow is automatic. **Open design question:** CoC's tap is
   a RETENTION HOOK, not a chore - it is the reason to open the app. Automatic overflow is friendlier but removes the
   daily pull, which cuts against the owner's own "we can't get people to play" concern. A middle path was proposed and
   not yet ruled on: **overflow flows automatically so nothing is ever lost, but tapping pays a bonus** - reward showing
   up rather than punishing absence, the same shape as 26c.

⚠ **NAMING TO SETTLE BEFORE ANY OF THIS IS WIRED.** The owner referred to "Lumber Mill, Weaponsmith and Armorer" as the
three collectors; the catalog's three collector rows are `collector_lumbermill` (Lumber Mill), `collector_farm` (Quarry)
and `collector_forge` (Iron Mine), while Weaponsmith (`forge`) and Armorer (`armorer`) are separate buildings with their
own ladders. Those are two different sets of three and the difference is load-bearing for 26b.

### Ruling 26 — MEASURED 2026-09-06. Read this before tuning anything.

The loop was designed against unmeasured numbers. They are now measured, at source, and **one of them refutes a premise
the design rested on.**

**⛔ IRON CAN BE STOLEN. Only CRYSTALS are raid-safe.** `StakeRules.IsLootable()` (`:155-161`) makes
**Wood, Iron, Stone(Food) and Coins lootable**; Crystals, SKR and purchased goods are untouchable, pinned by
`SiegeUntouchableRegression:71`. The reasoning that "the crystal mine and iron mine could route through the Smith since
they can't be stolen" holds for crystals ONLY.

**Two thirds of the loop is ALREADY BUILT:**
- **26a (collectors are safe) — ALREADY TRUE.** Collector looting was REMOVED 2026-08-27.
- **26c (loss falls on storage) — ALREADY TRUE.** The defender loses from STORAGE via `StakeRules.ProtectedFloor()`
  and `PerAttackCap()` (WO-1026).
- **26b (cap and stall) — HALF TRUE.** The cap and the stall are real: `ResourceCollector.Accrue` clamps at
  `Math.Min(cap, ...)` (`:387`) and accrual stops at cap. **The AUTOMATIC OVERFLOW TO STORAGE IS NOT IMPLEMENTED.**
  The only deposit path is the manual `Collect()` tap (`:440-514`), which never burns - the remainder stays pending
  when the bank is full (WO-1392).

**The numbers, and the one that matters most:**

| Collector | Capacity | vs L1 bank (3,000) | vs L6 bank (34,000) |
|---|---|---|---|
| Quarry (`collector_farm`) | **7,500** | **2.5x** | 0.22x |
| Lumber Mill | **5,760** | **1.92x** | 0.17x |
| Iron Mine | **3,456** | **1.15x** | 0.10x |

⛔ **A FULL COLLECTOR IS BIGGER THAN AN EARLY BANK.** A full Quarry (7,500) cannot fit in a level-1 bank (3,000) - not
"is tight", *cannot fit*. This is the strongest possible argument for **ruling 26d** (cheap, self-funding early storage):
the early bank is not merely small, it is smaller than one collector. The relationship inverts by L6, where the bank is
34,000 against a 7,500 collector, so the SAFE pool becomes the small one.

**Time-to-full is EIGHT HOURS at every level, by design** - `ResourceCollector.ComputeCapacity` scales capacity by
throughput, so upgrading earns more without demanding you check in more often. That is a deliberate idle cadence and it
is worth preserving; do not "fix" it.
Yield per tick: Quarry `13 + 4*(lvl-1)`, Lumber Mill `10 + 3*(lvl-1)`, Iron Mine `6 + 2*(lvl-1)`
(`ResourceBuildingProgression.MakeBuilding`). Harvest interval by level: 50s / 42.5s / 35s / 27.5s / 20s (`:189`).

**So what is actually left to build for ruling 26:** the automatic overflow (26b's second half), the cheap self-funding
early storage costs (26d), and the tap-bonus decision. Everything else already works and merely needs explaining to the
player - which is the WO-1427/2013 job.

**27. ASYMMETRIC RAID LOSS. Destroying a collector costs the defender its LOCAL STASH; taking from storage costs the
defender NOTHING and rewards the attacker. This REVISES 26a and 26c.**

Owner ruling 2026-09-06, verbatim: *"Iron should be stealable because the only protected resource is crystals... you can
destroy the collector or the upgraders, in this case the building, whether it's Weaponsmith or the Armorer or the Lumber
Mill - if you destroy those then you lose your local stash, just your local tiny portion that's in your collector. If
they damage and start taking from your storage they get to claim that as a spoil of war, but you don't physically feel
the impact of losing from storage. Yours is safe. It's just a benefit for the attacker."*

| Target | Defender | Attacker |
|---|---|---|
| **Collector destroyed** | **LOSES the stash held in it** - real, small, local | gains it |
| **Storage raided** | **LOSES NOTHING** - the bank is untouched | **gains a spoil of war** |
| **Crystals** | never touched at all | never |

*Supersedes:* 26a's "collectors are safe" (they are now the ONLY real loss) and 26c's "loss falls on storage only"
(storage loss is now PHANTOM for the defender). 26c's *intent* - do not punish the defender for being away - survives
and is in fact strengthened.

**⚠ THE CONSEQUENCE THAT CHANGES THE DESIGN QUESTION.** The owner asked *"what percentage of your storage is completely
immutable and safe"*. Under this ruling the answer is **100% for the defender**, and the percentage stops being a
PROTECTION dial and becomes an **ATTACKER REWARD dial**. There is no balance tension left in it - you can be generous
without it costing anyone. Tune it for how good a raid should feel, not for how much a loss should hurt.

**⚠ OPEN, AND IT NEEDS DECIDING: what is DEFENSE for?** If storage cannot truly be taken, walls and towers protect a
collector's partial contents and nothing else. That may be enough. If it is not, the stake needs a second axis -
a trophy/ranking loss, or a raided base producing less for a period. **Do not discover after shipping that nobody
builds walls.**

**⚠ AND THE NUMBERS FIGHT THE INTENT TODAY.** The ruling calls the collector stash "your local tiny portion". Measured
2026-09-06: the Quarry holds **7,500** against a level-1 bank of **3,000** - the collector is **2.5x the entire early
storage**. Early on the "tiny local portion" IS the player's whole economy, and destroying one collector would be the
single most damaging thing that can happen to them. **The collector cap (26b) and the early storage costs (26d) must be
set TOGETHER with this ruling**, or the loss meant to be minor is total.

*Implementation notes:*
- `StakeRules.IsLootable()` (`:155-161`) already makes wood/iron/stone/coins lootable and crystals untouchable - the
  RESOURCE side of this ruling is already correct and needs no change.
- What does NOT exist: a defender-loses-nothing path. Today the defender genuinely loses from storage via
  `StakeRules.ProtectedFloor()` / `PerAttackCap()` (WO-1026). **Making storage loss phantom means the attacker's grant
  and the defender's debit stop being the same transaction** - that is a real change to the raid settlement, not a
  number tweak.
- Collector looting was REMOVED on 2026-08-27 and must be REINSTATED for the collector half - but only the collector's
  pending stash, and only on DESTRUCTION, not on a mere raid win.
