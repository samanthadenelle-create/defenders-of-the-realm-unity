# WORK ORDER 1161 — The structure ROLE table: name a building by what it IS, settled by data

**Status:** IMPLEMENTED 2026-08-23 — `COMPILE_GATE_OK` (0 `error CS`), `DataRegression` **269/270**. ⛔ **ONE RED IS DELIBERATE AND NEEDS AN OWNER RULING** — `[tutorial-agree]`, see §5. Not closed by the CLI.

**Minted:** 2026-08-23 (CLI), banner bumped 1161 → 1162 in the same edit.
**Provenance:** owner felt-test — *"Iron - NEEDS: Forge"* on the Echo harvest picker while `forge` sat in her own ever-built ledger, i.e. **an instruction that could not be satisfied by obeying it.**

---

## 0. The owner rulings this implements, in the order given

| When | Ruling |
|---|---|
| 2026-07-13 | WO-707: one building per trade; **ids are load-bearing — remap displayNames, never ids** |
| 2026-08-17 | *"would it be better to set them with an enumeration so we can reference as we want"* + **function is the authority**: *"which sells weapons, that is the weaponsmith use the JSON data"* |
| 2026-08-23 | *"just get at building.enum.displayname"* · *"you could even point to a db table to settle them"* · *"the idea is staying fluid"* · **"if we add a building we do not want to have to manually code it"** |

⛔ **THE FIRST TWO WERE RULED AND NEVER BUILT.** WO-707 was **swept closed** by the 0-800 range pass on 2026-08-21 as *"completed or immaterial"* — the rename it ruled was never applied. The 08-17 enum was recorded in memory and no code was ever written. Every session since has re-diagnosed the same confusion from scratch and then patched a string. **That is why this kept recurring: nothing ever reached disk.**

## 1. The defect, verified at source

Four rows answered to two words:

| catalog id | displayed | what it actually DOES (vendors.json) |
|---|---|---|
| `forge` | **"Armorer"** ❌ | sells **weapons** |
| `armorer` | **"Blacksmith"** ❌ | sells **armour** |
| `workshop` | **"Weaponsmith"** ❌ | **not a vendor** — crafting station |
| `collector_forge` | **"Forge"** ❌ | the iron faucet |

The Echo picker resolved iron's gate to `collector_forge`, whose displayName is "Forge" — so it printed **"Iron - NEEDS: Forge"** to a player whose ledger already read `everBuilt=[… forge …]`. Proving line captured from the device:

```
[Flow:Harvest] existence gate CLOSED for 'forge' (liveCollector=no,
  everBuilt=[workshop, collector_lumbermill, collector_farm, pet-house, forge, …])
  - NEVER BUILT, so it earns nothing (phantom-income gate)
```

## 2. What was built — an OPEN table, not a switch

- **`Assets/_Modules/Core/Catalog/StructureRole.cs`** — compile-checked NAMES for the roles code branches on. ⚠ **Deliberately NOT a C# `enum`.** The first draft was, and it was wrong: a real enum freezes the vocabulary, so a new building with a new role would need a code edit — exactly what the owner ruled out. It is a string vocabulary; **the enum-shaped ergonomics survive, the freeze does not.**
- **`Assets/_Modules/Core/Catalog/StructureRoles.cs`** — the indexable table:
  ```csharp
  StructureRoles.By[StructureRole.Armorer].DisplayName   // named, compile-checked
  StructureRoles.By["newtype"].DisplayName               // brand-new role, ZERO code
  ```
  Both are the same call. **Adding a building is a DATA edit** — author a row with `"role": "newtype"` and it resolves immediately: no enum member, no case label, no registration, nothing recompiled.
- **`CatalogEntry.role`** — a new, optional field. Absent = unroled = exactly the prior behaviour, so nothing regresses on the ~200 rows left alone.

⛔ **THERE IS NO ROLE → ID MAP IN THE CODE, BY CONSTRUCTION.** The table settles it; the resolver only *indexes* what the data claims. A `case "armorer": return "armorer";` would write one fact twice — the shape that already produced the stale WO-number block (§2), the retired assembly table (§5), the hardcoded repo root (§0), the drifted R2 push (§16) and WO-1137's 3-of-28 fallback. **To move a role onto another building, edit the catalog.**

⛔ **AND IT REFUSES AMBIGUITY LOUDLY.** Two rows claiming one role would otherwise resolve by catalog order — silently, differently after the next regenerate. The index keeps the first and reports the collision via `FlowTrace.Fail`, so it lands in the flight recorder.

## 3. The data (both canonical copies, byte-identical, `structures-catalog.json` v24 → **v25**)

Names straightened **from vendors.json**, so this is derivable rather than three guesses:
`forge` → **"Forge"** · `armorer` → **"Armorer"** · `workshop` → **"Crafting Station"**.

11 rows roled: `weaponsmith` `armorer` `jeweler` `marketplace` `crafting_station` `food_faucet` `wood_faucet` `iron_faucet` `wood_store` `iron_store` `food_store`.

⚠ **The ids were NOT touched** — they are frozen save keys joined on by `everBuiltStructureIds`, BaseLayout, baked scenes, vendors.json and dialogues.json, on a **LIVE** store listing. Renaming `forge` would orphan every existing player's building.

## 4. Evidence

| Gate | Result |
|---|---|
| `Builds/wo1161-gate3.log` | **`COMPILE_GATE_OK`**, 0 `error CS` |
| `Builds/catalog-fallback-gen.log` | **`CATALOG_FALLBACK_GEN_OK`** — WO-1137's codegen'd fallback regenerated against catalog v25 (it went stale on the hash and said so, which is the codegen working) |
| `Builds/wo1161-reg2.log` | **269/270 green, 1 red** (§5) |

## 5. ⛔ THE ONE RED — and it is a FIND, not a break. Owner ruling owed.

`[tutorial-agree]`: *"the armor nudge's objective ('Next along the road: an Armorer') no longer names 'Forge' (catalog row 'forge')."*

**The tutorial had the crossing baked into it.** `tutorial-steps.json` (both copies, order 1060) triggers on `build.structure_placed:`**`workshop`** — described in its own note as *"once the weapons roof stands"* — and then points the ARMOUR nudge at **`forge`**, noting *"catalog id 'forge', display 'Armorer'"*. Both references are to the wrong row; the beat only ever read correctly **because the labels were crossed to match it.** Straightening the names is what exposed it.

The truthful chain is: weapons roof = `forge` (role `weaponsmith`) → then suggest armour = `armorer` (role `armorer`).

⚠ **NOT CHANGED HERE ON PURPOSE.** That file carries an explicit owner pin: *"THE CHAIN'S ORDER PAST THESE TWO BEATS IS AN OWNER CREATIVE PIN — propose the full sequence to the owner before authoring more."* Rewriting a teaching sequence is a creative call, and the last thing this cluster needs is a fourth silent guess. **Left RED deliberately** — per the 08-21 precedent, *a gap nothing checks is a gap nobody fixes.*

## 6. Still open after this

- **The harvest gate mapping itself.** The owner ruled iron = **Armorer**, food = Farm, wood = Lumbermill, crystals = none (level-6 Echo). `EchoCardVM.FaucetBuildingIdFor` still routes iron to `ForgeId` → `collector_forge`. Repointing it needs care: `MayHarvest` resolves through `collectorBuildingId`, so pointing the cue at `armorer` without moving the faucet binding would swap one lie for another — the cue would name a building that, once built, still would not open the gate. **Do that as its own change, with a captured run proving the gate flips.**
- `collector_forge` still displays "Forge", now duplicating the corrected `forge`. It is no longer player-facing once the cue resolves by role, but the duplicate should go when the faucet binding moves.
- `collector_lumbermill` and `lumbermill` **both** display "Lumber Mill" — the same latent trap, dormant only because the owner happened to build the right one.
