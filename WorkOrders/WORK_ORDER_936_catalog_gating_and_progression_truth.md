# WORK ORDER 936 — Catalog gating + progression truth pass

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).

> **PARTIAL - re-scoped 2026-08-14 (phantom sweep).** Remaining work: Finding A is UNTOUCHED (BuildPaletteVM.cs:183). Finding B is struck as a false alarm.
> Everything else in this WO is present in HEAD. The named remainder IS the ticket now - do not
> re-implement the shipped part.
> _Prior status line, preserved: Status: READY TO IMPLEMENT_

**Minted:** 2026-08-09 (CLI seat) — number from the `CLI_LANES_WO_NUMBERS.md` banner (bumped 936 → 937 in the same edit)
**Lane:** Catalog / progression data. **Presentation untouched** — this is about what the data PROMISES vs what the code DELIVERS.
**Provenance:** Owner questions during the WO-1010 art pass, 2026-08-09 — *"where do players upgrade wood?"* and
*"Jeweler is gated just till it's unlocked correct?"*. Both turned out to expose gaps, not answer questions.

---

## 0. Why this exists

Three separate findings, one shared shape: **the data advertises a progression the code cannot deliver.** None
is visible to any gate — every one passes compile, passes 132 suites, and reads correct to anyone skimming the
JSON. They only surface when someone asks "so how does a player actually do this?"

---

## 1. FINDING A — `LockedIds` has no unlock path (jeweler is permanently hidden)

`BuildCategoryRegistry.LockedIds` is built once from `build-categories.json` and thereafter only READ.
`BuildPaletteVM.Rebuild` does `if (_lockedIds.Contains(e.id)) continue;` — **unconditional**. Nothing anywhere
mutates the set, and no unlock/progression system feeds it.

The code comment distinguishes intent clearly:
- *"Jeweler stays **unlock-gated**"* → meant to be TEMPORARY
- *"the palette **retires** mine_crystal / mill / lumbermill / armorer / collector_forge"* → meant to be PERMANENT

Both are implemented identically, so the temporary one is permanent in practice. The Jeweler can never appear
regardless of what the player does.

`StrategicPlacementRegression:194` asserts `town.LockedIds.Contains("jeweler")`, and :617 already carries an
`unlockGateExempt` notion — so the current state is deliberately pinned, and un-gating means updating that suite
too. That is a feature: it cannot flip silently.

**Deliverable:** either (a) implement the unlock path — a gated id becomes available when its condition is met
(perk owned / tier reached / quest complete) — or (b) rename the concept so the data stops implying a door that
does not exist (e.g. `RetiredIds` vs `UnlockGatedIds`, with only the latter consulted against player state).
**Do not silently un-gate the jeweler**: it is a real building with a live vendor surface
(`PanelId.JewelerCrafting`, `jewelers-bench`), so making it placeable is a design change, not a bug fix.

---

## 2. FINDING B — the three stockpiles cap the economy with no way up

`lumberyard` (wood), `foundry` (iron) and `silo` (food) each declare `storageCapacity` + `maxLevel: 3` in
`structures-catalog.json`. But `building-tiers.json` lists tiers for only: `arcane-tower, armorer, barracks,
forge, lumbermill, farm`. **None of the three stockpiles has a single tier row.**

So each advertises three levels while nothing defines what level 2 or 3 grants, or what it costs. Per WO-837 the
stockpiles ARE the capacity-cap progression and are the only enemy raid targets — so a wood cap frozen at 500
with no upgrade path is a hard ceiling on the whole wood economy, and the same for iron and food.

**Deliverable:** author tier rows for `lumberyard` / `foundry` / `silo` (capacity per tier + cost + any HP bonus),
OR drop `maxLevel` from the catalog rows so the data stops promising levels that do not exist. **Numbers are an
owner call — bring options, do not invent balance.**

---

## 3. FINDING C — the live Lumber Mill upgrades through a retired row

The placeable building is `collector_lumbermill`; its `collectorBuildingId` points at `lumbermill`, which is the
row that actually owns the tier tree (*"Restore the Mill"*: wood production +10%, structure HP +20%, 900 wood +
300 food). `lumbermill` is in Town's `LockedIds` — retired from the palette.

This WORKS today. The risk is that the live building's entire progression is defined on a row the palette hides,
so a future "clean up the retired rows" pass silently deletes the Lumber Mill's upgrades. It is a live dependency
that looks like dead data.

**Deliverable:** make the indirection explicit and guarded — a regression asserting that every
`collectorBuildingId` target still exists and still owns tier rows, so deleting a retired row that is load-bearing
fails the gate instead of the game.

---

## 4. Cross-cutting: display names are not identities

Every one of the above cost real time today because labels were read as identities. In this catalog:

| display name | ids |
|---|---|
| "Lumber Mill" | `lumbermill` (retired) **and** `collector_lumbermill` (live) |
| "Armorer" | id `forge` |
| "Forge" | id `workshop` (now displays "Weaponsmith") |
| "Blacksmith" | id `armorer` (retired) |

The author of this WO mis-stated the live/retired status of Lumber Mill and Jeweler on this basis, twice, in one
session. **Any claim about what is live must come from the id + a `LockedIds` check, never from the name.**

**Deliverable (cheap, high leverage):** a doc block or oracle that publishes the id ⇄ displayName map, so the next
reader does not re-derive it wrongly.

---

## 5. Acceptance criteria

- [ ] `LockedIds` either gains a real unlock path, or the concept is split so permanently-retired and
      temporarily-gated ids are no longer indistinguishable in the data.
- [ ] `lumberyard` / `foundry` / `silo` either have authored tier rows, or no longer claim `maxLevel: 3`.
- [ ] A regression asserts every `collectorBuildingId` target exists AND owns tier rows.
- [ ] The id ⇄ displayName collisions are documented where a reader will hit them.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites`.
- [ ] Any balance numbers are OWNER-RATIFIED, not authored by the implementing seat.

## 6. What NOT to touch

- Build-screen presentation (WO-1010 owns it).
- The retired ids' catalog rows — saves replay them (WO-707); retiring from the palette is not deleting.
- `StrategicPlacementRegression`'s existing jeweler assertion, except as a deliberate part of an un-gating change.

---

# ADDENDUM — 2026-08-09, implementation pass over FINDINGS B + C

**Finding A was deliberately not touched** (needs the owner ruling the WO asks for).

## 7. FINDING B IS A FALSE ALARM — the stockpiles already upgrade, and it already works

> **⚠ DO NOT AUTHOR `building-tiers.json` ROWS FOR `lumberyard` / `foundry` / `silo`, AND DO NOT
> REMOVE THEIR `maxLevel: 3`. Both "fixes" in §2's deliverable would BREAK working behaviour.**

§2 concluded "nothing defines what level 2 or 3 grants, or what it costs" from the absence of
`building-tiers.json` rows. That inference is wrong: `building-tiers.json` is the **city tier ladder**,
one of *three* progression systems in this project. The stockpiles use a different one. Verified at
source, end to end:

**The COST is defined** — `BuildModeController.UpgradeCostFor` (`Assets/_Modules/Village/BuildMode/BuildModeController.cs:2590-2612`)
falls back to "the build cost × the level being left" when `repo.upgradeCost` is absent. This is the
documented contract on `RepoProps.upgradeCost` (`Assets/_Modules/Core/Catalog/RepoProps.cs:64-73`):
*"a row can opt into upgrades with just `maxLevel` and no explicit table."* The three stockpiles do
exactly that. Effective curve today, derived from each row's `repo.cost`:

| id | L1→L2 | L2→L3 | total L1→L3 |
|---|---|---|---|
| `lumberyard` | 50 wood + 20 iron | 100 wood + 40 iron | 150 wood + 60 iron |
| `foundry` | 60 wood + 30 iron | 120 wood + 60 iron | 180 wood + 90 iron |
| `silo` | 60 wood + 15 iron | 120 wood + 30 iron | 180 wood + 45 iron |

**The GRANT is defined** — `storage-caps.json` authors `levelCapacityMultipliers: [1.0, 2.0, 3.0]`
(exactly three entries, matching `maxLevel: 3`), consumed by `TownBankCapacity.CapacityAtLevel`
(`Assets/_Modules/Core/Economy/TownBankCapacity.cs:385-390`) and summed into the town bank ceiling at
`:690-706`. Wood cap: **2000** with no lumberyard → **2500** at L1 → **3000** at L2 → **3500** at L3.

**The PATH is reachable in normal town play** — HUD Build button → tap the placed stockpile →
`ShowSelectionPanel` (`:2203-2222`) → `BuildSelectionUI` renders "Lumberyard (Lv 1/3)" with an active
Upgrade button (`Assets/_Modules/Village/BuildMode/BuildSelectionUI.cs:94-116`) → `UpgradeSelected`
(`:2292`) takes the generic inline tier-bump branch. No feature flag gates this branch.

**The LEVEL persists correctly** — written to `GameState.BaseLayout[i].level` via
`BuildModeController.UpdateLayoutLevel` (`:3259-3274`), read from the same field by `TownBankCapacity`
(`:690-706`). Write and read agree; the move path preserves `level`.

So the premise behind §2 — *"a wood cap frozen at 500 with no upgrade path is a hard ceiling on the
whole wood economy"* — does not hold. **Removing `maxLevel: 3` would CREATE that hard ceiling**, which
is the precise failure §2 set out to prevent. Authoring tier rows would be worse still: it would give
these rows a *second*, divergent ladder (`BuildingUpgradeVM` prefers the city ladder whenever a
`building-tiers.json` row exists), competing with the `BaseLayout.level` the capacity math already
keys off.

**Acceptance criterion §5 line 2 should be struck** and replaced with: *the stockpiles' `maxLevel: 3`
is load-bearing and is now regression-pinned.*

### 7a. THE REAL DEFECT THIS SEARCH TURNED UP (new, needs its own ticket)

`ManageScreenVM.BuildDefenseBrowse` (`Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs:567-591`)
builds a browse row for any placed structure with `repo.maxLevel > 1` — so a level-1 lumberyard
produces a row reading **"Lumberyard -> L2"** priced correctly at 50 wood + 20 iron. But its CTA is
`OpenUpgradePanel(id)` (`:1085`) → `PanelRouter.Open(PanelId.BuildingUpgrade, "lumberyard")` →
`BuildingUpgradeVM`, whose family check (`BuildingUpgradeVM.cs:144-145`) matches **neither** the city
tier catalog nor `ResourceBuildingProgression`, so it falls to `BuildUnknown()` (`:870-876`) and
renders **"This building has no enhancements."** at tier 0/0 with no purchase affordance.

**Two shipped surfaces disagree about the same building**: build mode sells the upgrade, the Manage
screen prices it and then dead-ends. The row is right; the destination is wrong. Fix is either
(a) point that CTA at the build-mode level ladder, or (b) exclude `repo.maxLevel`-only ids from
`BuildDefenseBrowse`. **Authoring `building-tiers.json` rows is NOT the fix** — see above.
(In-world F-press is clean: `BuildingInteractable.StructureHookIdFor` returns null for all three ids,
so no third surface is involved.)

## 8. FINDING B — OWNER OPTIONS (numbers deliberately NOT authored by the implementing seat)

The gap is not real, so there is nothing that *must* change. These are tuning choices only. Each is
shaped to a field the game **already reads**, so whichever is picked is live without new code.

**Option 1 — RATIFY THE IMPLICIT SCALER (no data change).** Keep the derived curve in the table above.
*Reasoning:* it is already coherent (cost scales linearly, capacity scales linearly), it is the
documented intent of `RepoProps.upgradeCost`, and it needs zero authoring. *Cost:* the curve is a
side effect of build cost — retuning a stockpile's build price silently retunes its upgrade price.

**Option 2 — AUTHOR AN EXPLICIT `upgradeCost` TABLE** on each of the three catalog rows. Shape (this
is the schema, not a proposal of values — `upgradeCost[0]` is L1→L2, `[1]` is L2→L3):
```json
"maxLevel": 3,
"upgradeCost": [
  { "wood": <L1→L2>, "food": <…>, "iron": <…>, "crystals": <…> },
  { "wood": <L2→L3>, "food": <…>, "iron": <…>, "crystals": <…> }
]
```
*Reasoning:* decouples upgrade price from build price and lets the third level cost more than a
doubling (the usual CoC-shaped ramp). *Cost:* three more rows to keep in sync across the Resources +
StreamingAssets copies. **An authored entry only wins if non-zero** (`UpgradeCostFor:2599`), so a
half-filled table silently falls back to the scaler — author all steps or none.

**Option 3 — RETUNE THE CAPACITY CURVE** via `storage-caps.json` `levelCapacityMultipliers` (today
`[1.0, 2.0, 3.0]`), and/or `baseCap` (today 2000/2000/2000). *Reasoning:* this is the grant side, and
it is one number per level shared by all three containers. *Constraint that must be respected:* the
`_orderNote` ordering law in that file requires every container's capacity **at its max level** to stay
**below `baseCap`**, or the pallets invert and drain first. At `storageCapacity 500 × 3.0 = 1500 < 2000`
that holds today; raising the top multiplier past 4.0 (or any per-row `storageCapacity` increase)
breaks it and trips `TownBankCapRegression [order-intent-pallets-last]`.

**Recommendation from the implementing seat: Option 1 (do nothing).** The system is coherent and
shipping; §7a is the defect actually worth spending on.

## 9. FINDING C — DELIVERED: `CollectorLadderRegression`

New oracle `Assets/Editor/Regression/CollectorLadderRegression.cs`, registered in
`DataRegression.RunAll` inside the fence as `[collector-ladder]`.

**§3's deliverable as written could not be implemented, and the WO is wrong about why.** It asks to
assert that every `collectorBuildingId` target *"still exists"* in the catalog. That assertion **fails
on today's green tree**: `collector_farm` points at `farm`, and **`farm` is not a row in
structures-catalog.json at all** (only `collector_farm` is). The Farm works fine, because a collector
target needs a **ladder**, not a catalog row. Asserting catalog existence would have failed honest data
and taught the next reader the wrong dependency. The oracle pins the real load-bearing edge instead:

- `[target-owns-tier-rows]` — every `collectorBuildingId` target owns ≥1 tier row in
  `building-tiers.json`. This is the Finding C tripwire.
- `[tier-ladder-contiguous]` — tiers run 1,2,3… with no gap and no duplicate. A whole-row delete is the
  loud case; dropping one tier out of the middle is the quiet one, and it dead-ends the ladder
  permanently because `BuildingUpgradeVM` only ever offers `CurrentTier + 1`.
- `[no-chained-indirection]` — a target must not itself be a collector (`ResolveUpgradeId` hops once).
- Green output **names** the load-bearing-but-palette-retired rows, so the hazard is stated on success,
  not only on failure.

**Why a missing row is silent (the thing that makes this worth a gate):** all three targets satisfy
*both* families — `farm`/`lumbermill`/`forge` are rows in `building-tiers.json` **and** the hardcoded
trio in `ResourceBuildingProgression` (`:173-175`). `BuildingUpgradeVM` checks the city ladder first
(`:144-145`). Delete the `lumbermill` tier row and nothing throws and nothing blanks: the panel falls
through to the legacy 5-level yield curve and draws a plausible, *entirely different* progression, with
no log line and no symptom a playtester would file.

**Also corrected:** §4's table says "Lumber Mill" maps to `lumbermill` (retired) and
`collector_lumbermill` (live). True — but the retired one is where the live one's upgrades live, which
is the whole of Finding C and is worth stating in that table rather than only in prose.

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `BuildPaletteVM.cs:305-306,126` — unlock path landed via WO-964. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
