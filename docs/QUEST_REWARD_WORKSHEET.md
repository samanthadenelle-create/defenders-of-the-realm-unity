# Quest Reward Worksheet -- fill in the blanks

**Date:** 2026-08-25 | **For:** the owner | **You write in the `XP` and `Other` columns.**

Every quest in the shipping data, numbered 1..24, one row per stage. The `Pays now`
column is what the game pays TODAY, read straight out of the JSON. `XP` and `Other`
are blank on purpose -- they are yours. Nothing in this file is estimated or invented.

- **Source:** `Assets/StreamingAssets/Data/Canonical/quests.json` (byte-identical twin at
  `Assets/Resources/Data/Canonical/quests.json`, 35,402 bytes each). **24 quests, 63 stages.**
- **Rewards live on the STAGE, not the quest.** A four-stage quest has four separate
  reward slots. `grantsKeystone` sits on the stage itself, outside `reward`.
- **33 of 63 stages pay nothing at all.** Marked `NOTHING` in caps. Those rows are the
  point of this document -- the priority list is at the end.
- Givers and outcomes reused from `docs/QUEST_IMAGE_BRIEFS.md`. Its IMAGE lines are
  retired (the quest slot shows rewards now, not art); its giver research still stands.
  Where nobody is named in the data the row says `not authored`.

---

# Part 1 -- the two rulers

A number is meaningless without knowing how hard the thing was and how precious the
currency is. 220 crystals and 220 food are not remotely the same reward.

## 1a. What each currency actually costs you

### CRYSTALS -- the premium, real-money currency

Giving these away is giving away store inventory. `packs.json` sells them for actual
dollars. **Note:** `storeVisible` defaults to **true**
(`Assets/_Modules/Wallet/PackCatalog.cs:141`), so rows with no such key ARE on the shelf.
Every live crystal-bearing pack, from `pricing.usd` and `contents.economy.crystals`:

| Pack | USD | Crystals | Per $ |
|---|---:|---:|---:|
| Crystal Shard (crystals only) | 1.99 | 250 | 126 |
| Crystal Cluster (crystals only) | 2.99 | 700 | 234 |
| Crystal Vein (crystals only) | 4.99 | 1,600 | **321** |
| Starter's Hand (bundle) | 4.99 | 400 | 80 |
| Folk's Thanks (bundle) | 9.99 | 900 | 90 |
| Patron of Elarion (bundle) | 19.99 | 1,850 | 93 |
| Founder's Vow (bundle, founder-only) | 49.99 | 4,600 | 92 |

No first-time or bonus multipliers exist anywhere in the file. Nine further packs sit at
`storeVisible: false` and are not live.

**So a crystal is worth roughly 0.3 to 1.2 cents**, depending on which pack a player buys.
Quest rewards read against that:

| Reward | At the cheapest pack (321/$) | At the bundle rate (~90/$) |
|---:|---:|---:|
| 25 crystals | $0.08 | $0.28 |
| 100 crystals | $0.31 | $1.11 |
| 300 crystals (16.1, the largest in the file) | $0.94 | $3.33 |
| 1,905 crystals (the whole quest file) | $5.94 | $21.17 |

Crystals have real sinks, which is what makes them premium: instant-finishing an Obsidian
job (`BuildTimerConfig.InstantFinishPrice`, 1 crystal per minute, 10 minimum), buying an
extra queue slot (`BuildTimerService.TryBuySlot`, 250 base and rising), a talent respec
(`HeroTalentCatalog.respecCostCrystals = 300`), and every magical structure's build cost.

### MAGIC -- almost nothing to spend it on

Read this before authoring another magic reward.

- **Exactly one sink exists in the whole game:** the Forge's 6th "Arcane Forge" tier, cost
  **3 Magic**, once, hardcoded
  (`Assets/_Modules/Village/Buildings/Progression/ResourceBuildingProgression.cs:268-284`).
- Magic is a wallet field (`GameState.cs:294`), not a harvestable and not in `ResourceType`.
  No structure, research, crafting, ability, weapon, jeweler or talent catalog carries a
  magic cost. Magical buildings cost crystals + iron, never Magic.
- **The quest file grants 325 Magic across 9 stages, against 3 Magic of lifetime demand.**
  Roughly a hundredfold oversupply. Every magic reward in the tables below is, in practice,
  paying nothing. Whether that changes is a design call, not a data fact.

### FOOD -- being retired. Do not author new food rewards.

- **WO-1163** (`WorkOrders/WORK_ORDER_1163_resource_ladder_stone_and_tiered_costs.md`)
  retires food and reuses its slot for **stone**. Its own words: *"FOOD IS GONE. The SLOT
  is reused; the CONCEPT is retired."* Player-facing, every food string, icon, cost and
  reward becomes stone; in the save the slot keeps its wire position, so 1,800 food reads
  as 1,800 stone.
- Status today: **READY, bounced back 2026-08-25 with a money bug. Nothing has landed.**
  Stone is not yet a member of any resource enum
  (`Assets/_Modules/Core/ResourceType.cs:28-34` = Iron, Wood, Food, AetherCrystal).
- **Seven stages pay food today, 260 food in total** -- 1.2, 6.3, 7.1, 7.3, 10.1, 14.3 and
  17.3. Each is flagged `(retiring)` in the tables. Decide per row whether it converts to
  stone or becomes something else. Do not add new ones.

### WOOD and IRON -- the workhorse currencies, and quests never pay them

- Cost baskets separate by nature (WO-947, enforced headlessly by
  `Assets/Editor/Regression/CostBasketSeparationRegression.cs`): **regular structures cost
  wood + iron and never crystals; magical structures cost crystals + iron and never wood.**
  No row ever holds all three.
- **Quest rewards today pay only `crystals`, `food`, `magic` and `grantItemId`.** There is
  no wood or iron field on `QuestReward` at all. So the campaign hands out the premium
  currency and the retiring one, and never the two a player actually spends on building.
  That asymmetry is worth a decision of its own.

### WISDOM -- deliberately scarce, and quests do not pay it

- Sole sink: hero talent / skill-tree nodes (`WisdomCurrencyService.Unlock`; node costs
  1/2/3/5 across 83 nodes in `hero-talents.json`).
- Only two faucets survive (WO-763, now CLOSED): hero level-up (2 wisdom through level 8,
  then 3) and tier milestones (12/15/20/25 at L15/20/25/30, then 30 every 10 levels from
  L40). The per-wave and per-arena faucets were both zeroed and are regression-pinned.
- **`quests.json` has no wisdom field** -- the 24 authored quests cannot pay it.
- **One surviving faucet worth repeating here:** the daily-quest slot rows still author
  `rewardWisdom: 1` (`daily-quests.json` lines 15 and 29 -- the combat and wildcard slots).
  At runtime that field pays **crystals**, not wisdom
  (`DailyQuestRewardBridge.cs:252-266`, traced `"wisdom->crystals (WO-763)"`), while the
  HUD still labels it "Wisdom" (`DailyQuestHud.cs:399-401`). Copy and behaviour disagree.

## 1b. How hard was it

Difficulty is not a rating anyone assigned -- it is shown to you from the data:

- **Gating depth** (`requiresQuestId`). Only **three** quests in the file are gated:
  `forgemasters_act2` -> `act3` -> `act4`. Everything else is depth 1 and reachable from
  the start. That chain is the only real late-game structure, and clearing
  `forgemasters_act4` also unlocks **five legendary gear recipes** (`gear-recipes.json`).
- **`completeOn` kind and count.** `talk` and `panel` are conversation and UI taps;
  `wave` and `arena` are real fights; `build` costs resources. **Every count in the file
  is 1 except 15.1, which is 4 arena wins** -- the heaviest single stage in the game.
- **Build stages carry a real price.** From `structures-catalog.json` (`repo.cost`):

  | Build target | Cost | Asked for at |
  |---|---|---|
  | `wall_stone` | 120 wood + 240 iron | 12.2 |
  | `lumbermill` | 200 wood + 160 iron | 6.2 |
  | `mine_crystal` | 320 wood + 200 iron | 4.2, 8.1, 22.2 |
  | `barracks` | 600 wood + 320 iron | 10.2 |
  | `silo` | 960 wood + 240 iron | 7.2, 12.1 |
  | `healing_caravan` | **760 crystals** + 400 iron + 240 food | 12.3 |

  12.3 is the standout: it spends **760 crystals** -- more than twice the largest crystal
  reward in the entire file -- and pays `NOTHING` back.
- **Enemy tiers do not resolve.** Every `wave` and `arena` stage carries an empty
  `targetId`, so no stage names a specific enemy, level or tier. "Orc Raider" and
  "Warband Deathspeaker" are prose in the objective text, not data links: **unresolved**.
  For fight stages, the kind and the count are all the difficulty the data declares.

## 1c. Reading the two together

That is the point of having both. **A `wave` or `arena` stage paying crystals is a genuine
reward. A `talk` stage paying magic or food is paying nothing** -- magic has no sink and
food is being removed. When a row looks underpaid, read the quest's difficulty line and
the currency together: what did the stage cost the player, and is what it handed back
actually worth anything.

---

# Part 2 -- calibrating XP

XP is a **primary reward, not a garnish**. If it is the thing that decides which quest a
player picks up next, it has to be big enough to make that a real choice.

The hero curve, `Assets/_Modules/Village/Hero/HeroProgression.cs:109`:

    XpToNext(L) = 150 + (L-1)*350 + (L-1)^2*500

| Level step | XP needed |
|---|---:|
| 1 -> 2 | 150 |
| 2 -> 3 | 1,000 |
| 3 -> 4 | 2,850 |
| 4 -> 5 | 5,700 |

**Worked anchor.** The same number means very different things depending on when it lands:

| An XP value of | at level 2 (needs 1,000) | at level 4 (needs 5,700) |
|---:|---|---|
| 100 | 10% of the level | under 2% |
| 250 | a quarter of the level | ~4% |
| 500 | half the level | under 9% |
| 1,000 | the whole level | ~18% |

The curve is steep, so a flat value that feels generous early is invisible later. A stage
meant to feel the same at both ends has to scale with where its quest sits.

**One caveat, stated plainly:** there is **no `xp` field on quest rewards today.**
`QuestReward` (`Assets/_Modules/Core/Quests/QuestCatalog.cs:30-36`) carries only
`crystals`, `food`, `magic` and `grantItemId`, and `QuestRewardBridge` grants only those.
Authoring XP means a new field plus a grant path -- a ticket, not a data edit.

## A blank does not have to become a number

If a quest's payoff **is** progression, write that. `Unlocks Act 2`, `opens the rumor
board`, `first keystone`, `unlocks the five legendary recipes` are all legitimate reward
lines. Quest 13 pays no currency and its entire point is opening quest 14 -- that is a
real reward, it just is not a number. Say so in `Other` and move on.

---

# Part 3 -- the rundown

Header key: `type` / `id` / gate, then giver, end result, and a one-line difficulty read
(gating depth, type, heaviest stage). `NOTHING` = no crystals, no food, no magic, no item.
`(retiring)` marks a food payout. `KEYSTONE` is `grantsKeystone` on the stage.

---

## 1. A New Defender

`main` | `elarion.welcome` | no gate

**Giver:** the Village Elder (Heart of Elarion)

**End result:** the player has met the Elder and held one wave; the first keystone.

**Difficulty:** depth 1, opening main quest; heaviest stage = 1 wave. Entry-level.

| # | What the player does | Pays now | XP | Other |
|---|---|---|---|---|
| 1.1 | Talk to the Village Elder at the Heart | 50 crystals | | |
| 1.2 | Survive the first wave at the gate | 100 crystals, 20 food *(retiring)* + KEYSTONE | | |

## 2. The Forgemaster's Request

`gear` | `forgemaster.first-commission` | no gate

**Giver:** Borin Emberhand (the Forge)

**End result:** the Iron Longsword (`knight_iron`) -- the player's first real weapon.

**Difficulty:** depth 1, gear; heaviest = 1 talk + 1 panel open. Very light.

| # | What the player does | Pays now | XP | Other |
|---|---|---|---|---|
| 2.1 | Bring iron to Borin at the Forge | **NOTHING** | | |
| 2.2 | Open your pack and take up the Iron Longsword | item `knight_iron`, 10 magic | | |

## 3. Supply Run

`side` | `vendor.supply-run` | no gate

**Giver:** Coppin (the Marketplace)

**End result:** 25 crystals. The shortest quest in the file.

**Difficulty:** depth 1, side; heaviest = 1 talk. Trivial.

| # | What the player does | Pays now | XP | Other |
|---|---|---|---|---|
| 3.1 | Visit Coppin at the Store, take the supply run | 25 crystals | | |

## 4. The Last Ember

`gear` | `vendor.forge` | no gate

**Giver:** Borin Emberhand (the Forge)

**End result:** the forge is running and the first true blade is proven.

**Difficulty:** depth 1, gear; heaviest = 1 wave, plus a 520-resource Crystal Mine.

| # | What the player does | Pays now | XP | Other |
|---|---|---|---|---|
| 4.1 | Bring Borin wood and iron to relight the forge | **NOTHING** | | |
| 4.2 | Build a Crystal Mine, bring a flawless stone | 10 magic | | |
| 4.3 | Field-test the blade: clear an Orc Raider wave | 75 crystals + KEYSTONE | | |

## 5. Shields of the Fallen

`gear` | `vendor.armorer` | no gate

**Giver:** Halvard (Armorer's Hall)

**End result:** the player wears armor made from the fallen garrison's plate.

**Difficulty:** depth 1, gear; 1 arena + 1 wave. Two real fights, one payout at the end.

| # | What the player does | Pays now | XP | Other |
|---|---|---|---|---|
| 5.1 | Win an encounter beyond the walls for salvage | **NOTHING** | | |
| 5.2 | Have Halvard reforge the plate | **NOTHING** | | |
| 5.3 | Survive a wave in the new armor | 75 crystals + KEYSTONE | | |

## 6. Roots Run Deep

`side` | `vendor.lumbermill` | no gate

**Giver:** Old Pell (the Lumbermill)

**End result:** a Lumber Mill standing and a surviving sapling.

**Difficulty:** depth 1, side; 1 arena + 1 wave + a 360-resource Lumber Mill.

| # | What the player does | Pays now | XP | Other |
|---|---|---|---|---|
| 6.1 | Clear the blight from Old Pell's grove | **NOTHING** | | |
| 6.2 | Plant the sapling and build a Lumber Mill | **NOTHING** | | |
| 6.3 | Defend the sapling through one night raid | 50 crystals, 30 food *(retiring)* + KEYSTONE | | |

## 7. Full Bellies, Full Ranks

`side` | `vendor.granary` | no gate

**Giver:** Mother Wren (the Windmill)

**End result:** food flow restored and storage raised.

**Difficulty:** depth 1, side; no fights at all, but a **1,200-resource Silo**.

| # | What the player does | Pays now | XP | Other |
|---|---|---|---|---|
| 7.1 | Open the Mill upgrade panel and raise it | 40 food *(retiring)* | | |
| 7.2 | Build a Silo | **NOTHING** | | |
| 7.3 | Report the harvest rota to Mother Wren | 50 crystals, 50 food *(retiring)* + KEYSTONE | | |

## 8. Aether's Facet

`side` | `vendor.jeweler` | no gate

**Giver:** Sable (the Jeweler's Bench)

**End result:** a socketed gem and a decision made about the outside broker.

**Difficulty:** depth 1, side; no fights; a 520-resource Crystal Mine.

| # | What the player does | Pays now | XP | Other |
|---|---|---|---|---|
| 8.1 | Build a Crystal Mine, harvest a rare stone | **NOTHING** | | |
| 8.2 | Cut and socket your first gem at the bench | 15 magic | | |
| 8.3 | Decide about Sable and the outside broker | 75 crystals + KEYSTONE | | |

## 9. The Glimmer Road

`side` | `vendor.market` | no gate

**Giver:** Coppin (the Marketplace)

**End result:** a working trade route, and Brom's rumor board open for business.

**Difficulty:** depth 1, side; heaviest = 1 arena. Unlocks the rumor board.

| # | What the player does | Pays now | XP | Other |
|---|---|---|---|---|
| 9.1 | Clear the trade road to the next outpost | **NOTHING** | | |
| 9.2 | Establish a steady trade route with Coppin | 50 crystals | | |
| 9.3 | Expand the network until the rumor board opens | 75 crystals + KEYSTONE | | |

## 10. Last Call

`side` | `vendor.inn` | no gate

**Giver:** not authored

**End result:** a Barracks standing as the town's rally and respawn point.

**Difficulty:** depth 1, side; 1 wave + a 920-resource Barracks.

| # | What the player does | Pays now | XP | Other |
|---|---|---|---|---|
| 10.1 | Defend the hall through a surprise raid | 50 crystals, 30 food *(retiring)* | | |
| 10.2 | Build a Barracks as the rally point | 50 crystals + KEYSTONE | | |

## 11. Wild Hearts

`side` | `vendor.stable` | no gate

**Giver:** Fenn Wildmane (Echo Hollow); the Echo Warden closes it

**End result:** a bonded companion put to work harvesting.

**Difficulty:** depth 1, side; heaviest = 1 arena. Teaches the harvest loop.

| # | What the player does | Pays now | XP | Other |
|---|---|---|---|---|
| 11.1 | Track a wild echo beyond the walls and win | **NOTHING** | | |
| 11.2 | Train a pet ability with Fenn Wildmane | **NOTHING** | | |
| 11.3 | Ask the Echo Warden to set the pet to harvest | 50 crystals + KEYSTONE | | |

## 12. Rebuild Elarion

`main` | `vendor.steward` | no gate

**Giver:** not authored

**End result:** the town is rebuilt and the Warband Deathspeaker is beaten.

**Difficulty:** depth 1 by data, but the **most expensive quest in the file** -- three
builds totalling about 2,800 resources including **760 crystals** on the Healing
Caravan, then 1 arena. Heaviest stage = 12.4 (arena). Three of its four stages pay
nothing.

| # | What the player does | Pays now | XP | Other |
|---|---|---|---|---|
| 12.1 | Build a Silo (town tier 2) | **NOTHING** | | |
| 12.2 | Build a Stone Wall on the breach | **NOTHING** | | |
| 12.3 | Build the Healing Caravan (costs 760 crystals) | **NOTHING** | | |
| 12.4 | Rekindle the Heart, beat the Deathspeaker | 250 crystals, 50 magic + KEYSTONE | | |

## 13. Honest Steel

`main` | `forgemasters_act1` | no gate (opens the 4-act Forgemasters chain)

**Giver:** Borin Emberhand (the Forge)

**End result:** unlocks quest 14. **Nothing else -- this quest pays nothing at all.**

**Difficulty:** depth 1, main; heaviest = 1 talk. The lightest main quest, and the only
quest in the file that pays nothing across every stage.

| # | What the player does | Pays now | XP | Other |
|---|---|---|---|---|
| 13.1 | Meet the four crafts, hear the Aegis legend | **NOTHING** | | |

## 14. The Old Fire

`main` | `forgemasters_act2` | **gated behind `forgemasters_act1`** (act 2 of 4)

**Giver:** Halvard (Blacksmith) opens it; Borin Emberhand (the Forge) closes it

**End result:** the four crafts work as one again.

**Difficulty:** depth 2, main; heaviest = 1 talk. All four stages are conversation -- no
fight, no build.

| # | What the player does | Pays now | XP | Other |
|---|---|---|---|---|
| 14.1 | Hear Halvard's side of the old quarrel | **NOTHING** | | |
| 14.2 | Reconcile Old Pell and the forge | **NOTHING** | | |
| 14.3 | Gather all four to Mother Wren's table | 100 crystals, 60 food *(retiring)* | | |
| 14.4 | Take the master's word from Borin | 25 magic + KEYSTONE | | |

## 15. What Was Lost

`endgame` | `forgemasters_act3` | **gated behind `forgemasters_act2`** (act 3 of 4)

**Giver:** not authored

**End result:** all four techniques secured for the reforging.

**Difficulty:** depth 3, endgame; heaviest = **4 arena wins (15.1)**, the highest
`completeOn` count anywhere in the file -- and that stage pays nothing.

| # | What the player does | Pays now | XP | Other |
|---|---|---|---|---|
| 15.1 | Recover four techniques -- win **4** encounters | **NOTHING** | | |
| 15.2 | Bring all four to Mother Wren's table | 150 crystals, 50 magic + KEYSTONE | | |

## 16. The Reforging

`endgame` | `forgemasters_act4` | **gated behind `forgemasters_act3`** (act 4 of 4)

**Giver:** not authored

**End result:** the Aegis of Elarion is whole again. **Largest single payout in the
file.** Clearing it also unlocks **five legendary gear recipes** (`gear-recipes.json`).

**Difficulty:** depth 4 -- the deepest gate in the game; heaviest = 1 crafting panel.

| # | What the player does | Pays now | XP | Other |
|---|---|---|---|---|
| 16.1 | Choose the aether and reforge the Aegis | 300 crystals, 100 magic + KEYSTONE | | |

## 17. Wild Hearts: The Green Hearth

`side` | `petbond.sproutling` | no gate

**Giver:** not authored

**End result:** a bonded Flame Pup whose hearth fire quickens what the fields give back.

**Difficulty:** depth 1, side; heaviest = 1 arena.

| # | What the player does | Pays now | XP | Other |
|---|---|---|---|---|
| 17.1 | Cleanse a blighted harvest site | **NOTHING** | | |
| 17.2 | Walk the Flame Pup home past the fields | **NOTHING** | | |
| 17.3 | Bond the Flame Pup | 30 food *(retiring)* | | |

## 18. Wild Hearts: The Wounded Wolf

`side` | `petbond.craghound` | no gate

**Giver:** not authored

**End result:** a bonded Ice Wolf whose hide turns a blow meant for the Heart.

**Difficulty:** depth 1, side; heaviest = 1 wave.

| # | What the player does | Pays now | XP | Other |
|---|---|---|---|---|
| 18.1 | Protect a wounded wolf through a raid | **NOTHING** | | |
| 18.2 | Bring the Ice Wolf home to the Echo Hollow | **NOTHING** | | |
| 18.3 | Bond the Ice Wolf | 40 crystals | | |

## 19. Wild Hearts: Ice Wolf

`side` | `petbond.frostkit` | no gate

**Giver:** Fenn Wildmane (Echo Hollow), named at stage 2

**End result:** a bonded Ice Wolf whose bite leaves the cold behind in the wound.

**Difficulty:** depth 1, side; heaviest = 1 arena.

| # | What the player does | Pays now | XP | Other |
|---|---|---|---|---|
| 19.1 | Patient approach on the cold ground | **NOTHING** | | |
| 19.2 | Bring the wolf to Fenn for a warm stall | **NOTHING** | | |
| 19.3 | Bond the Ice Wolf | 40 crystals | | |

## 20. Wild Hearts: Flame Pup

`side` | `petbond.emberpup` | no gate

**Giver:** not authored

**End result:** a bonded Flame Pup whose bite sets enemies alight.

**Difficulty:** depth 1, side; heaviest = 1 arena.

| # | What the player does | Pays now | XP | Other |
|---|---|---|---|---|
| 20.1 | Stand in the Emberwastes heat beside the pup | **NOTHING** | | |
| 20.2 | Bring the Flame Pup home to the Echo Hollow | **NOTHING** | | |
| 20.3 | Bond the Flame Pup | 40 crystals | | |

## 21. Wild Hearts: The Cleansed Water

`side` | `petbond.mirewing` | no gate

**Giver:** not authored

**End result:** a bonded Aether Sprite whose light eases what the mire left behind.

**Difficulty:** depth 1, side; heaviest = 1 arena.

| # | What the player does | Pays now | XP | Other |
|---|---|---|---|---|
| 21.1 | Cleanse the Mirewood water at its source | **NOTHING** | | |
| 21.2 | Bring the Aether Sprite home to roost | **NOTHING** | | |
| 21.3 | Bond the Aether Sprite | 15 magic | | |

## 22. Wild Hearts: The Flawless Stone

`side` | `petbond.glimmermoth` | no gate

**Giver:** Sable (the Jeweler's Bench)

**End result:** a bonded Aether Sprite that scents richer crystal than any hand can find.

**Difficulty:** depth 1, side; no fights, but a 520-resource Crystal Mine.

| # | What the player does | Pays now | XP | Other |
|---|---|---|---|---|
| 22.1 | Get a flawless stone from Sable | **NOTHING** | | |
| 22.2 | Build a Crystal Mine, coax the sprite to it | **NOTHING** | | |
| 22.3 | Bond the Aether Sprite | 60 crystals | | |

## 23. Wild Hearts: The Caged Wolf

`side` | `petbond.stoneback` | no gate

**Giver:** not authored

**End result:** a bonded Ice Wolf that will carry and will cover. Shortest of the bonds.

**Difficulty:** depth 1, side; heaviest = 1 arena.

| # | What the player does | Pays now | XP | Other |
|---|---|---|---|---|
| 23.1 | Clear an Orc Raider camp, free the caged wolf | **NOTHING** | | |
| 23.2 | Bond the freed Ice Wolf | 50 crystals | | |

## 24. Wild Hearts: Aether Sprite

`side` | `petbond.aetherfox` | no gate

**Giver:** the Village Elder (Heart of Elarion), named at stage 2

**End result:** a bonded Aether Sprite whose aura lightens every ability the player
spends. **Richest of the bonds.**

**Difficulty:** depth 1, side; heaviest = 1 wave.

| # | What the player does | Pays now | XP | Other |
|---|---|---|---|---|
| 24.1 | Hold the restored ground through a wave | **NOTHING** | | |
| 24.2 | Present the Aether Sprite to the Village Elder | **NOTHING** | | |
| 24.3 | Bond the Aether Sprite | 100 crystals, 50 magic | | |

---

# Part 4 -- triage lists, so you can decide before you write

## 4a. The 33 stages that pay nothing -- the priority list

These are every stage where a player finishes the objective and receives no crystals, no
food, no magic and no item. Grouped by what the stage actually asked them to do.

**Fights that pay nothing -- 12 stages.** A won encounter or a survived wave, for zero:

    arena (10):  5.1  6.1  9.1  11.1  15.1 (4 wins)  17.1  19.1  20.1  21.1  23.1
    wave  (2):   18.1  24.1

**Builds that pay nothing -- 7 stages.** The player spent real resources here:

    6.2  7.2  8.1  12.1  12.2  12.3 (760 crystals)  22.2

**Conversations and fetches that pay nothing -- 14 stages.** Each is still a completed
objective, and 13.1 is a whole quest:

    2.1  4.1  5.2  11.2  13.1  14.1  14.2
    17.2  18.2  19.2  20.2  21.2  22.1  24.2

Full list in order, for reference:
`2.1, 4.1, 5.1, 5.2, 6.1, 6.2, 7.2, 8.1, 9.1, 11.1, 11.2, 12.1, 12.2, 12.3, 13.1,
14.1, 14.2, 15.1, 17.1, 17.2, 18.1, 18.2, 19.1, 19.2, 20.1, 20.2, 21.1, 21.2, 22.1,
22.2, 23.1, 24.1, 24.2`

## 4b. Quests whose FINAL stage pays nothing

The quest ends and the player is handed nothing. **Exactly one:**

| Quest | Final stage | What it does pay |
|---|---|---|
| 13. Honest Steel (`forgemasters_act1`) | 13.1 | nothing -- its only payoff is unlocking quest 14 |

Every other quest lands its payout on the last stage. Intermediate stages are where the
33 blanks live.

## 4c. Quests that pay nothing across EVERY stage

**Confirmed: exactly one, and it is `forgemasters_act1` ("Honest Steel").** It is a
single-stage quest, so 4b and 4c are the same row. No other quest in the file has all its
stages empty -- checked across all 24.

## 4d. Two things you may want to decide before filling anything in

1. **Nine stages pay magic (325 total) into a currency with one 3-Magic sink.** If magic
   stays as-is, those nine rows are effectively blank too, which would make the real
   "pays nothing" count **42 of 63**, not 33.
2. **Seven stages pay food (260 total) into a currency being retired by WO-1163.** Those
   need a conversion decision -- to stone, or to something else -- before they are worth
   re-balancing.
