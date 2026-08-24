# WORK ORDER 1163 — The resource ladder: Food becomes Stone, and tiers cost by depth

**Status:** SPEC — READY TO IMPLEMENT once §6 is answered. ⛔ **Save-schema-adjacent change on a LIVE build with an ACTIVATED pay path** (WO-1159). Not a lane to rush.

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

**Blast radius, counted:** **208** `"food"` references in canonical data — `battle_monthly.json` **120** · `quests.json` **63** · `packs.json` **18** · `barracks.json` **6** · `storage-caps.json` **1** — plus the persisted save fields `BuildJobData.Food` and `PaidFood` (the WO-911 paid basket, schema **v37**).

| | RENAME (food slot → stone) | DELETE + ADD |
|---|---|---|
| Save field | keeps its slot, reads as Stone | new field + purge + migration |
| Packs / battle pass / quests | all 208 refs ride along | all 208 re-authored by hand |
| Risk | low | high — **touches paid content** |

⛔ **PACKS THAT ADVERTISE FOOD ARE PURCHASABLE CONTENT.** `TownBankCapacity` states the law: *"a pack that advertises 5,000 food and delivers 1,920 is not balance, it is selling something and not delivering it"*, and `[purchased-grant-never-clamped]` **fails the build** if a paid grant is ever clamped. Whatever a card promises, it must still deliver after the rename.

⚠ The aliasing machinery already exists — `TownBankCapacity.WordOf` maps `"grain"` → Food today, so a word→enum alias is an established pattern, not a new one.

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
3. **Barracks levels currently cost food** (0 / 80 / 290 / 860). Under "troop training straight gold", do those become gold, or does the barracks *building* keep tier costs while *training* goes gold? They are different sinks.
4. **Does anything still need a food-shaped resource?** The WO-1163 sink audit is running; fold its arithmetic in before deleting the concept.

## 7. Sequencing — do not reorder

1. Read the food sink/source audit (in flight) — it says whether food is load-bearing at all.
2. Owner answers §6.
3. Rename food → stone as a **data + alias** change; schema bump only if the wire shape actually moves.
4. Re-price tiers L1/L2/L3; troop training → gold.
5. Regenerate the codegen'd fallback (WO-1137 hash gate will demand it).
6. Full gate + a captured run proving a tier-1 build, a tier-2 upgrade and a troop train all charge the ruled basket.
7. **Owner felt-test.** No headless run can judge whether the ladder feels right.
