# WORK ORDER 1163 — The resource ladder: Food becomes Stone, and tiers cost by depth

**Status:** READY — ⭐ **UNBLOCKED 2026-08-24: WO-1177 landed (`2c3ed6c24`) and released the shared `api/_lib/purchase-catalog.js` / `test/purchases.quote.test.js` lock**, and the owner ruled the food SKU ids **DO** rename — the remap is IN SCOPE here, not a follow-up. ⚠ Three files move together under the MIRROR LAW (server `USD_ANCHORS`, both canonical `packs.json` copies, the quote test's hardcoded resource-key list) or it is a red build. *(Status audit 2026-08-24.)* §6 fully answered (§6.1 + §6.2 ruled by the owner 2026-08-24; §6.3 ruled 08-23). ⛔ Still a save-schema-adjacent change on a LIVE build with an ACTIVATED pay path (WO-1159) — follow §7's sequencing exactly; not a lane to rush.

**Minted:** 2026-08-23 (CLI), banner bumped 1163 → 1164 in the same edit.
**Ruled by:** the owner, 2026-08-23, in conversation. Verbatim below.

---

## 0. THE RULING, in her words

> *"what if we make it iron wood and stone (remove food and set to stone) then have three producers (Iron Producer Wood Producer and Stone producer, then each for storage. seperate the collector to the node, the storage to the storage, and seperate the stores"*
> *"level 1 is wood and gold"* · *"lvl 2 is stone and gold"* · *"lvl 3 is iron and gold"* · *"troop training straight gold"*

## 1. Why this exists — the closed loop it kills

⛔ **Every tier-one producer is currently priced in the resource it PRODUCES:**

| building | costs | produces |
|---|---|---|
| `armorer` | wood 240, **iron 280** | **iron** |
| `collector_lumbermill` | **wood 160**, food 80, iron 120 | **wood** |
| `collector_farm` | wood 240, iron 80 | food |

So losing (or never building) the producer means losing the resource that buys it back. The owner hit the terminal case tonight: `I = 0`, the HUD reading *"14 iron short - go farm"*, the Armorer locked out of the palette, and — once unlocked — priced at 280 iron she had no way to earn. **A loop with no exit.**

The tier ladder dissolves it: a building's cost depends on its **tier**, not on what it makes.

## 2. The ruled ladder

| Tier | Costs |
|---|---|
| **Level 1** | **wood + gold** |
| **Level 2** | **stone + gold** |
| **Level 3** | **iron + gold** |
| **Troop training** | **straight gold** |

Each tier introduces the next resource; **gold is universal**. Wood reads as early game, stone as mid, iron as late.

⚠ **One residual circularity, and it is already covered:** a level-1 *wood* producer still costs wood. `freeBuildsUsed` (save v32) grants one FREE first placement per catalog id, and WO-1163's 150-gold restore (already authored on the three producers) covers the destroyed case. Confirm both paths hold before calling this closed.

## 3. Food → Stone: **FOOD IS GONE. The SLOT is reused; the CONCEPT is retired.**

> Owner, 2026-08-23: *"i create a stone node (replaces all food nodes)"* · *"then food is gone"*

⛔ **Read those two sentences together — they are not the same statement.** The owner authors a
**stone node that replaces the food nodes** (art + placement are hers), and **food ceases to exist**
as a player-facing resource: no food word, no food icon, no food cost, no food reward.

⚠ **But "food is gone" is a statement about the GAME, not about the SAVE.** Those come apart, and
conflating them is how this goes wrong:

- **Player-facing: food disappears completely.** Every string, icon, cost and reward becomes stone.
- **Persistence: the SLOT is reused, not deleted.** `Resources.Food` and `BuildJobData.Food` /
  `PaidFood` keep their wire position and read as stone. A player holding 1,800 food holds 1,800
  stone. **Nobody loses a balance and no pack stops delivering.**

Deleting the field instead would mean re-authoring 208 canonical rows by hand, migrating a v37
paid basket, and re-checking every purchasable pack — for a change the player cannot perceive.
**Retire the concept; reuse the slot.**

⛔ **This is the single most important implementation decision in the ticket.** Food and Stone are mechanically identical — a bulk resource with a cap, a producer and a container — so this is one word, not one economy.

### ⚠ BLAST RADIUS — CORRECTED 2026-08-24 (UI seat). The previous table was materially WRONG.

⛔ **The old table keyed on the string `"food"` and therefore missed the actual cost field, which is
`costFood`.** The two key sets are almost perfectly **disjoint** — 13 of the 14 C# files carrying
`costFood` contain zero `"food"`, and 26 of the 27 JSON files carrying `"food"` contain zero
`costFood`. So a `"food"`-keyed plan captured 692 mostly-irrelevant occurrences and **missed the only
two files that actually carry tiered costs.** Recounted at source with `rg --count-matches`
(occurrences, not lines — several C# lines carry two tokens).

⛔ **The old table also listed `building-tiers.json` at ZERO refs.** It holds **26** `costFood` — and
§3b already names it the **largest sink in the game** at 113,360 food. The two halves of this ticket
disagreed with each other.

⛔ **And the path was wrong: `Assets/Data/building-tiers.json` DOES NOT EXIST.** There is no
`Assets/Data/` directory at all. The canonical file is
**`Assets/Resources/Data/Canonical/building-tiers.json`**, with a **required mirror** at
**`Assets/StreamingAssets/Data/Canonical/building-tiers.json`**. ⭐ **Every canonical data file in
this repo is mirrored across those two trees. Both copies move together or they diverge silently.**

#### The `costFood` footprint — this is what the rename actually touches

| File | `costFood` |
|---|---|
| `Assets/Resources/Data/Canonical/building-tiers.json` | **26** |
| `Assets/StreamingAssets/Data/Canonical/building-tiers.json` | **26** (mirror) |
| `Assets/Resources/Data/Canonical/troops.json` | 9 |
| `Assets/StreamingAssets/Data/Canonical/troops.json` | 9 (mirror) |
| `Assets/Resources/Data/Canonical/packs.json` | 1 |
| `Assets/StreamingAssets/Data/Canonical/packs.json` | 1 (mirror) |
| **JSON subtotal** | **72 across 6 files** |

**C# — 32 occurrences across 14 files** (9 lowercase `costFood` = JSON-binding attributes and
regression assertions; 23 PascalCase `CostFood` = the properties):
`Assets/_Modules/Village/Hero/TroopTrainingVM.cs` (4) ·
`Assets/_Modules/Village/Buildings/Progression/BuildingUpgradeService.cs` (3) ·
`.../BuildingUpgradeVM.cs` (3) · `Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs` (3) ·
`Assets/_Modules/Village/Troops/TroopDef.cs` (3) ·
`Assets/_Modules/Core/State/BuildingTierCatalog.cs` (2) ·
`Assets/_Modules/Village/Troops/{ArmyMusterPanel,ArmyMusterService,BarracksProgression,BarracksService}.cs` (1 each) ·
`Assets/Editor/Regression/EconomySinkCapRegression.cs` (5) ·
`.../TroopRosterRegression.cs` (3) · `.../CostBasketSeparationRegression.cs` (1) ·
`Assets/Tests/EditMode/TroopTrainingVMTests.cs` (1).

**`costFood` code + data total: 104 occurrences across 20 files.**

#### The `"food"` footprint — separate axis, still real, still must be done

The 208 figure was also low. Recounted: **597** `"food"` in JSON across 27 files (`battle_monthly.json`
120 ×2 trees, `quests.json` 63 ×2, **`structures-catalog.json` 57 ×2 — never previously listed**,
`packs.json` 18 ×2, `gear-recipes.json` 8 ×2, `barracks.json` 6 ×2, `jeweler-recipes.json` 6 ×2,
`battle_monthly_packs.sample.json` 24, plus singles) and **71** in C# across 35 files.

⚠ **Note `building-tiers.json` and `troops.json` have `costFood` and ZERO `"food"`** — the exact
inverse of the old table's assumption. **The rename plan needs BOTH keys or it is incomplete.**

| | RENAME (food slot → stone) | DELETE + ADD |
|---|---|---|
| Save field | keeps its slot, reads as Stone | new field + purge + migration |
| `costFood` (104 occ / 20 files) | rides along as one field rename | 104 hand edits, incl. the 113,360-food ladder |
| `"food"` (668 occ / 62 files) | rides along via the alias | all re-authored by hand |
| Risk | low | high — **touches paid content** |

---

### ⭐ THE SKU IDS DO RENAME — owner ruling, and it pulls in two more files

⛔ **This ticket now touches the backend and its test.** Verified at source:

- **`api/_lib/purchase-catalog.js:103-105`** carries a three-rung food SKU ladder in the price map:
  `'impulse-food-small'` → 1.99 · `'impulse-food-medium'` → 2.99 · `'impulse-food-large'` → 4.99.
  Sibling ladders (`impulse-wood-*` `:97-99`, `impulse-iron-*` `:100-102`, `impulse-crystals-*`
  `:106-108`) confirm the naming is per-resource. **There is NO `impulse-stone-*` family** — that is a
  real gap the rename must close.
- **`test/purchases.quote.test.js:132`** — `const keys = ['wood', 'iron', 'food', 'crystals', 'coins'];`
  ⚠ **This file contains ZERO food SKU id strings** (every SKU literal in it is
  `'impulse-wood-medium'`, 13 occurrences). **So the existing quote tests would NOT catch a food SKU
  removal.** The test must gain stone SKU coverage, not merely have `'food'` swapped at `:132`.

⛔ **THREE FILES MOVE TOGETHER UNDER THE MIRROR LAW, OR IT IS A RED BUILD:** the client catalog
(`packs.json`, both trees), `api/_lib/purchase-catalog.js`, and `test/purchases.quote.test.js`. A SKU
id that exists on one side and not the other is a purchase that quotes and never delivers — which is
the exact failure `[purchased-grant-never-clamped]` exists to make loud.

⭐ **SEQUENCING: this ticket runs AFTER `WO-1177` returns** (`WORK_ORDER_1177_shortfall_discount_server_issued.md`).
1177 moves quoting server-side; renaming the SKUs underneath an in-flight quote path would land the
rename twice and reconcile neither.

⚠ **One thing the recount makes EASIER, not harder:** the JS layer has **zero** `costFood` — cost
fields are Unity-side only, so no server change is needed for the *cost-field* half. And
`api/game/save.js:62` (`GUARDED_BALANCES`) and `:492` (`TIME_DERIVED_BALANCES = ['iron','wood','food','stone']`)
**already list `stone` alongside `food`**, as does `api/game/load.js:32`. The server balance
whitelist is ready.

⚠ **Untracked build copies exist and will regenerate** (`Builds/`, `Library/Bee/Android/...`) — do not
hand-edit them. Noted only because `Builds/WebGL/.../troops.json` currently reads **8** `costFood`
against the source's **9**, i.e. the deployed WebGL data is already stale.

⛔ **PACKS THAT ADVERTISE FOOD ARE PURCHASABLE CONTENT.** `TownBankCapacity` states the law: *"a pack that advertises 5,000 food and delivers 1,920 is not balance, it is selling something and not delivering it"*, and `[purchased-grant-never-clamped]` **fails the build** if a paid grant is ever clamped. Whatever a card promises, it must still deliver after the rename.

⚠ The aliasing machinery already exists — `TownBankCapacity.WordOf` maps `"grain"` → Food today, so a word→enum alias is an established pattern, not a new one.

## 3b. THE SINK AUDIT — why food died, in numbers

> Owner, 2026-08-23: *"food never had real value now it finally dies and something with value comes"*.

⛔ **The audit CORRECTS an earlier partial answer given to the owner in conversation** (that "only two
structures cost food"). That was materially incomplete — it missed the largest sink in the game.

**Food's real sinks were enormous:**

| Sink | Food | Live? |
|---|---|---|
| `building-tiers.json` — all six ladders | **113,360** | LIVE (`FeatureFlags.BuildingUpgradePanel` defaultOn true) |
| `barracks.json` levels 2-6 | 7,750 | LIVE |
| Lumbermill level ladder (per instance) | 834 | LIVE |
| `collector_lumbermill` placement | 80 | LIVE |
| **one-time total** | **~122,000** | |

Plus **every troop** (footman 50 → echo-legionnaire 400), re-spent on every raid loss, and food is
lootable in sieges (`StakeRules.cs:83-88`, 50% of *uncollected pending*, never bank theft).

**⭐ AND YET IT WAS WORTHLESS — because the faucet outran all of it.** A single L5 farm produces
5,220 food/hr; the Echo multiplier is **linear** in roster size and the tier perk adds ×1.45:

| configuration | clears the ENTIRE 122k one-time budget in |
|---|---|
| L5 farm, 1 Echo | 23.4 hours |
| L5 farm, 6 Echoes | **3.9 hours** |
| L5 farm, 6 Echoes, ×1.45 perk | **2.7 hours** |

And everything above the bank cap is **DISCARDED** (`EconomyService.cs:463-466` — *"Overflow is
LOST"*). So food had prices but never scarcity. **A resource with costs and no constraint is
decoration with extra steps.**

> ## ⛔ THE LESSON STONE MUST NOT REPEAT
> Food did not fail for lack of sinks — it had 122,000 of them. It failed because **the sinks were
> ONE-TIME and the income was UNBOUNDED and COMPOUNDING.** Fixed budget vs a faucet that scales
> linearly with Echo count and multiplicatively with perks has exactly one outcome, and no amount of
> re-pricing the ladder changes it — doubling every cost buys about three more hours.
>
> **⚠ AND THE RULED LADDER MAKES THIS WORSE, NOT BETTER.** Troop training was food's *only*
> repeatable sink — the one thing re-spent on every raid loss, the only drain that scaled with play.
> The ruling moves troop training to **straight gold**. So stone would inherit ~122k of one-time
> costs and **NO repeatable drain at all**, i.e. it starts life in the exact state food died in.
>
> **⭐ ANSWERED BY THE OWNER, 2026-08-23 — and the answer is the ladder itself.**
> Verbatim: *"so losing a lvl 3 tower hurts on wood stone and iron"* · *"as well as gold"*.
>
> **THE REPEATABLE SINK IS LOSS, AND THE LADDER IS WHAT GIVES IT TEETH.** Because cost is keyed to
> TIER, a level-3 structure has cumulatively consumed **wood+gold (L1) + stone+gold (L2) +
> iron+gold (L3)**. Destroying it therefore costs **all three resources plus gold** to climb back —
> one loss drains every currency at once.
>
> That is precisely what food never had. It is **repeatable** (sieges recur), it **scales with
> investment** (the more you have built, the more a raid costs you), and it **cannot be outrun by a
> faucet**, because the drain grows with the same progression that grows the income. Compare the
> food failure directly: a fixed 122k budget against a compounding faucet had one outcome; a drain
> proportional to what you own does not.
>
> ⚠ **It also gives WO-753 teeth for the first time.** *"Destroyed items never rebuild - build fresh
> at full cost"* was a rule with nothing behind it while food was abundant. Under the ladder it is a
> real consequence.
>
> ⚠ **And it keeps the 150-gold producer restore coherent, because that exception is NARROW:**
> tier-one PRODUCERS get the gold-only escape so the economy can never deadlock (§1); towers, walls
> and everything else pay the full ladder. The carve-out protects against SOFT-LOCK, never against
> LOSS. ⛔ Do not widen it to structures generally — that would delete the sink this section just
> established.

**⛔ AND FOOD IS MONETIZED — three food-only SKUs are on sale**
(`packs.json:686/713/741` — 1,000 / 3,500 / 8,000 food at $1.99 / $2.99 / $4.99), and
`ShortfallPackOffer.cs:107,233` routes a food shortfall straight to a purchase. Those SKUs MUST
map to stone. A pack that advertises a resource the game no longer has is selling something it
cannot deliver — the `[purchased-grant-never-clamped]` law, and now a revenue-correctness
requirement rather than a convenience.

## 3c. THE STONE NODE — art ruled 2026-08-23

**Owner ruling:** reuse **`Assets/Resources/Harvest/crystals.fbx`** as the stone node body —
*"looks like stone and if says stone noone will care."* She has seen the model; the CLI has not.

**Wiring:** node art resolves through `HarvestSite.ResourceModelPath` (`HarvestSite.cs:356-363`),
which maps `MineResource.X` → `"Harvest/<x>"`. Point `Stone` at `"Harvest/crystals"`.
⚠ An unmapped resource falls through to a **primitive fallback** in `BuildVisual`, so the rename
can land BEFORE any art decision without breaking a scene — nodes render a placeholder shape.

⚠ **CONSEQUENCE TO BE AWARE OF, not a blocker: the CRYSTAL node uses this same FBX.** So both nodes
wear one body, separated only by tint (`crystal 0.35,0.72,0.95` vs a grey stone,
`HarvestSite.GetResourceColor`). Two considerations the owner should hold, having ruled:
- **Colour is the only separator, and this owner is red/green colourblind** — the project's standing
  rule is that meaning never rides on hue alone (WO-1132 ruled the opposite direction for chest
  drops: they read by SILHOUETTE, deliberately).
- **The risk points at CRYSTALS, not stone.** If the model reads as rock, then stone looks correct
  and the *premium* currency is the one that stops looking precious.
- Precedent for the class: `mine_crystal` and `healing_caravan` **already share `Structures/Well`** —
  flagged in the 2026-08-23 audit as "a crystal mine and a healing fountain wear one body".

**Recorded as a deliberate choice, so it is revisitable rather than invisible.** If stone earns its
own body later, it is a one-line repoint plus an FBX drop into `Assets/Resources/Harvest/` — that
folder is git-TRACKED (unlike `Resources/Structures`), so it ships everywhere with no zip transfer.

## 4. Node / storage / stores — mostly already built

The owner's three-way separation is the shape the code already has; the work is making the NAMES match:

```
Producer (node)      ──produces──▶  _pending AT THE NODE   (bounded by collector Capacity)
                                          │ Collect()
Town Bank (wallet)   ◀──banks────────────┘                 (bounded by baseCap 2000)
        ▲
        └── capacity EXTENDED by ── Lumberyard / Stone container / Foundry
```

Verified: `ResourceCollector.cs:3` — *"Accrues into Pending; Collect() banks to wallet; siege raids steal uncollected."* Containers carry `storageResource` + `storageCapacity` and **no** `collectorBuildingId`, so they produce nothing. **Do not let a container harvest** — two buildings claiming production is the phantom-resource hunt this separation exists to prevent.

## 4b. ⭐ THE VOCABULARY — RULED 2026-08-23. This closes §6.1.

| Resource | Producer | producer id | Storage | storage id |
|---|---|---|---|---|
| Wood | **Lumber Mill** | `collector_lumbermill` | **Lumberyard** | `lumberyard` |
| Stone | **Quarry** *(was Farm)* | `collector_farm` | **Stone Yard** *(was Silo)* | `silo` |
| Iron | **Iron Mine** | `collector_forge` | **Foundry** | `foundry` |

⛔ **THIS SUPERSEDES THE EARLIER "IRON IS THE ARMORER" RULING (same day), AND IT IS AN IMPROVEMENT
— read why, because the reason is structural.** Iron now has a DEDICATED producer (`collector_forge`
as the Iron Mine) and the Armorer returns to being purely the armour vendor. That is exactly the
identity-vs-capability split that broke the role table earlier: the Armorer was made to be BOTH the
armour vendor and the iron producer, a row claims exactly ONE role, and two oracles correctly
rejected it (see WO-1161). Under this vocabulary **no building wears two hats** and the whole
problem dissolves rather than being worked around.

**It also retires the last name collision.** `collector_forge` currently displays **"Forge"** — the
only "Forge" left once `forge` became "Weaponsmith". As **Iron Mine** it stops competing with
anything.

### ⚠ TWO THINGS THIS BREAKS IF THEY ARE NOT DONE IN THE SAME CHANGE

1. **`collector_forge` IS PALETTE-LOCKED** (`build-categories.json` Town `lockedIds`). As the Iron
   Mine it MUST be unlocked, exactly as `armorer` was earlier today. Otherwise iron has a producer
   no player can place — **the precise dead-end that started this whole thread.**
2. **`collector_forge.repo.satisfiedByStructureIds = ["armorer"]` IS NOW WRONG.** It was authored
   under the superseded ruling. Iron's gate must point at the Iron Mine itself, or the NEEDS cue
   names the wrong building again — the original defect, restored by our own fix.

## 4c. ⭐ CONTAINERS RETIRE AS BUILDINGS — capacity becomes a PRODUCER UPGRADE (ruled 2026-08-23)

**Owner:** *"we never ever placed a silo. We only had the farm. Then outside of the farm, we had the
storage for it."* → and on the consequence: *"i love it!"*

**PROVEN, not remembered.** Her live device ledger tonight:
`everBuiltStructureIds = [workshop, collector_lumbermill, collector_farm, pet-house, forge,
arcane-tower, market, jeweler, apothecary, jewelers-bench, barracks, tower_ground_archer,
tower_arcane_spire]` — **no silo, no lumberyard, no foundry.** The container family has never been
placed, by her or by anyone, and `storage-caps.json` grants the base cap unconditionally
(*"the non-building BASE STORE every save holds before any storage container exists"*), so nothing
ever required one.

### The ruled shape

| Was | Becomes |
|---|---|
| Lumberyard / Stone Yard / Foundry as **separate buildings** | **RETIRED from the palette.** Capacity is a PRODUCER UPGRADE. |
| Bank capacity from a placed container | `building.upgrade` on the Quarry / Lumber Mill / Iron Mine |
| Stock as an invisible wallet number | **PALLET STACKS beside the producer** — the visible store |

⭐ **THIS IS NOT A NEW IDEA — IT IS THE ORIGINAL RULING, RESTORED.** WO-707 already ruled the pallets
in her own words: *"I loved the idea of visually seeing your store"* — each storing building shows
its stock IN THE WORLD as pallet stacks beside it, growing and shrinking with the amount, with
**quantity reading by stack SIZE, never colour** (colourblind-safe by construction). The separate
container family was added afterwards and never landed.

It is also **exactly the WC3 tech-tree shape she ruled on 2026-07-16** (memory
`building-upgrades-warcraft3-style`): the economy building owns its own research, mid perks
quantitative, top-tier a qualitative capstone — with her own worked example being *"Lumber Mill =
efficiency with a top-tier AUTO-HARVEST capstone"*.

### What this buys

- **The FBX list collapses to TWO** — Quarry and Iron Mine (§4d). No container bodies needed.
- **Three fewer buildings to name, place, art and keep from colliding.** Today Lumberyard, Foundry
  and Silo are *visually identical* (all `Structures/GenericContainer`) — you cannot tell your stone
  store from your iron store by looking. That problem deletes itself.
- **The raid stake becomes visible.** A raider burning the Lumber Mill takes the pile you can SEE
  beside it, rather than an abstract wallet number.

### ⚠ Carry-overs, so nothing is lost in the retirement

1. **The capacity ladder survives, it just moves.** `levelCapacityMultipliers [1,2,4,8,16,32]` and
   WO-1108b's six-level climb (1k/2k/4k/8k/16k/32k) become the PRODUCER's capacity upgrade. Fold the
   definition, do NOT re-derive the numbers.
2. **⛔ DO NOT DELETE THE CATALOG ROWS.** `lumberyard` / `foundry` / `silo` ids are frozen save keys;
   retire them from the PALETTE (the WO-707 pattern) so any save that ever recorded one still
   replays. Nobody has placed one, but that is not a licence to remove a key.
3. **`TownBankCapacity` reads container capacity today.** Its never-zero floor (`AbsoluteMinBaseCap`
   1000) and its grandfathering law (*"an existing save over the cap is NEVER drained"*) must both
   survive the move.

## 4d. ⭐ THE PERK MAP — producers grant COMBAT research on their tier ladders (ruled 2026-08-23)

**Owner:** *"The lumber mill can strengthen arrows. Iron can strengthen armor and defenses and stone
can be used for increasing things like damage."* · *"the producers have those upgrades at the tiers.
Warcraft style."*

### ⛔ WHY THIS IS THE FIX, not a flourish — the producers were a CLOSED CIRCLE

Read the ladders as they stand in `building-tiers.json`:

| Ladder | Grants today |
|---|---|
| `lumbermill` | wood production +10 → +40% |
| `farm` | food production +10 → +45% |
| `forge` (Weaponsmith) | **resource efficiency** +10 → +22% ⚠ |
| `armorer` | troop health, troop damage, structure HP ✅ |

**Every producer perk makes MORE OF THE THING IT ALREADY MAKES.** Wood buys more wood. Food bought
more food. Nothing a producer granted ever mattered outside its own loop — which is the same defect
as food's 122k of prices: activity without consequence. **That is why the producers felt valueless,
and no amount of re-pricing fixes a closed circle.**

Note also `forge` grants **resource efficiency** — a weaponsmith improving resource yield. Scrambled
the same way the display names were, and fixed by the same ruling.

### The ruled map (REFINED 2026-08-23, owner)

**Every producer tier does TWO things — this is the WC3 two-tab panel, exactly as ruled 2026-07-16:**

> *"upgrading type by default increases production of each type"* · *"on each level unlocks better
> abilities"*

| Tab | Every producer, every tier |
|---|---|
| **Upgrade** | **+production of its own resource** — the baseline, automatic, every level |
| **Skills** | **an unlocked ability**, escalating per level — the reason to choose this ladder |

| Producer | Resource | Skills line |
|---|---|---|
| **Lumber Mill** | wood | **arrows + wood-related** |
| **Quarry** | stone | **damage + range** |
| **Iron Mine** | iron | **defense + strength** |

⚠ **Note what the "+production" baseline does NOT do on its own:** it is the closed circle §4d
describes — wood buying more wood. It is fine as a *baseline* precisely because the SKILLS half is
what carries the outward value. Ship the production bonus without the skills and this reverts to the
state food died in.

⭐ **The `armorer` ladder ALREADY has the iron shape** — troop health, troop damage, structure HP
across T1-T4 — which maps cleanly onto **defense + strength**. Do not author it fresh; **move the
tuned curve** onto the Iron Mine.

⭐ **The `armorer` ladder ALREADY has the iron shape** — troop health, troop damage, structure HP
across T1-T4. Do not author it fresh; **move the existing curve** to the Iron Mine. The numbers are
tuned; the building they hang on is what was wrong.

**Shape per WC3 canon** (memory `building-upgrades-warcraft3-style`, owner 2026-07-16): mid tiers
QUANTITATIVE (more / faster / cheaper), top tier a QUALITATIVE capstone that changes how the
building plays. The existing named capstones are good and should survive: `Eternal Grove`
(lumbermill), `Battle Forged` (armorer), `Forgefire` (forge), `Winds of Plenty` (farm → re-theme for
the Quarry).

### ⛔ THE BOUNDARY THAT KEEPS THIS FROM COLLIDING WITH THE BENCHES

| | Scope | System |
|---|---|---|
| **Producer perk** | ARMY-WIDE, permanent research — "all arrows +10%" | `building-tiers.json` ladder |
| **Bench upgrade** (Forge / Armorer) | PER-ITEM — levels *your* sword | `GearProgression.Improve` (WO-808) |

Different nouns, different sinks, no overlap. It also frees the Weaponsmith from granting resource
efficiency and returns it to what its name says.

⭐ **AND IT COMPLETES THE VALUE ARGUMENT.** Each resource now buys a DIFFERENT kind of power, so
gathering wood and gathering stone are different strategic choices rather than two flavours of one
number — and the upgrades CONSUME the resource, giving stone the repeatable sink §3b said it needed.

## 5. Naming, per the role table (WO-1161)

| Role (identity) | Display | id |
|---|---|---|
| `wood_producer` | Lumber Mill | `collector_lumbermill` |
| `stone_producer` | *(owner to name)* | `collector_farm` — the renamed slot |
| `armorer` | Armorer | `armorer` — **also the iron producer** |
| `wood_store` | Lumberyard | `lumberyard` |
| `stone_store` | *(owner to name)* | `silo` — the renamed container |
| `iron_store` | Foundry | `foundry` |

⚠ **Role is IDENTITY, not capability.** The Armorer is both the armour vendor and the iron producer; a row claims exactly ONE role. Production is expressed on the faucet side (`satisfiedByStructureIds`). Overloading role to `iron_producer` was tried and two oracles correctly rejected it — see WO-1161.

## 6. ⛔ ANSWER THESE BEFORE IMPLEMENTING

1. **The Farm and the Silo become what?** `collector_farm` currently displays "Farm" and `silo` displays "Silo" — both are food-flavoured. Stone needs a quarry/mason vocabulary. **Owner names them.**
2. **Do existing food balances convert 1:1 to stone?** A rename says yes automatically. Confirm that is intended — a player holding 1,800 food wakes up holding 1,800 stone.
3. ⭐ **ANSWERED 2026-08-23** — owner: *"troops go to pure gold sink"* · *"in the barracks"*.
   **TRAINING = PURE GOLD, at the Barracks.** All nine troop rows drop their food cost
   (`troops.json`: footman/archer 50 → echo-legionnaire 400) and price in gold alone; the derived
   `BarracksProgression.TroopUpgradeCost` curve (`def.CostFood × targetLevel`) follows.
   ⚠ **The BARRACKS BUILDING is a separate sink and follows the TIER LADDER** (L1 wood+gold /
   L2 stone+gold / L3 iron+gold), not the training rule. Its current food levels
   (0 / 80 / 290 / 860 / 2040 / 4480) reprice by depth like every other building.
   ⭐ **This makes gold the army currency end to end** — and it is the right shape, because troop
   loss is the most repeatable spend in the game (every raid re-musters), and gold is the one
   resource that is UNCAPPED and always earnable from kills. An army you cannot rebuild is a
   soft-lock; an army priced in the uncapped currency never is.
4. **Does anything still need a food-shaped resource?** The WO-1163 sink audit is running; fold its arithmetic in before deleting the concept.

## 7. Sequencing — do not reorder

1. Read the food sink/source audit (in flight) — it says whether food is load-bearing at all.
2. Owner answers §6.
3. Rename food → stone as a **data + alias** change; schema bump only if the wire shape actually moves.
4. Re-price tiers L1/L2/L3; troop training → gold.
5. Regenerate the codegen'd fallback (WO-1137 hash gate will demand it).
6. Full gate + a captured run proving a tier-1 build, a tier-2 upgrade and a troop train all charge the ruled basket.
7. **Owner felt-test.** No headless run can judge whether the ladder feels right.

---

## ⭐ OWNER RULING 2026-08-24

§6 is now **fully answered**. With §6.3 already ruled 2026-08-23, this ticket moves **SPEC → READY**.

### §6.1 — Farm / Silo rename: **`collector_farm` → "Quarry"** · **`silo` → "Stoneyard"**

⛔ **IDS STAY FROZEN. Only `displayName` changes.** `collector_farm` and `silo` remain the literal ids
on disk — they are **live save keys** joined on by `everBuiltStructureIds`, `BaseLayout`, baked scenes,
`vendors.json` and `dialogues.json`, on a **live store listing**. Renaming an id orphans every existing
player's building. Same discipline WO-1161 §3 held, for the same reason.

⚠ **Reconcile with §4b:** that table (ruled 08-23) wrote the storage display as **"Stone Yard"** (two
words). The owner's 08-24 wording is **"Stoneyard"** (one word) and, being later, it is the
authoritative string. Author **`"Stoneyard"`**; treat §4b's "Stone Yard" as superseded spelling, not as
a second name. The producer name **"Quarry"** is unchanged between the two rulings.

### §6.2 — Food → Stone converts **1:1**

A player holding 1,800 food wakes holding **1,800 stone**. That is intended.

Owner, verbatim:

> *"Don't use migration itself as a balance lever."*

⛔ So the conversion carries **no haircut, no multiplier, no rounding rule, no cap-clamp beyond the
existing stone ceiling**. If the resulting stone economy is wrong, it is **rebalanced afterwards
through faucets and sinks** — levers that are visible, tunable and reversible. Burying a balance change
inside a one-way migration hides it from every future reader and cannot be undone.

⭐ Worth carrying forward as a general rule: **a migration's only job is to preserve value. Balance
happens in data the player can see move.**

### §6.4 — folded in, not re-opened

§6.4 (*"does anything still need a food-shaped resource?"*) is answered by §3b's sink audit, whose
arithmetic §7 step 1 already requires be read before the concept is deleted. Nothing new is owed here.

### Sequencing is unchanged

§7 stands **exactly as written** — audit → rename as data+alias → re-price tiers → troop training to
gold → regenerate the WO-1137 codegen'd fallback → full gate + captured run → **owner felt-test**.
READY means the questions are answered, not that the sequencing may be shortened.
