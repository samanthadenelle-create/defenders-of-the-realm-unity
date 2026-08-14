# WORK ORDER 947 — Cost-basket separation: regular structures = wood+iron; magical/ethereal = crystal-based; never all three

**Status:** DONE (catalog v18, 2026-08-14) — all four §4 pins + the §6 pin ANSWERED by the owner and applied to all 6 rows; the gate's exemption list is EMPTY. **One NEW pin remains OPEN and is deliberately unconverted: `arcane-tower` (§8).**
**Minted:** 2026-08-10 (CLI seat, main line — banner bumped 947 → 948 in the same edit)
**Silo:** Data (structures-catalog.json dual-copy) + one regression gate — no code-lane conflicts
**Type:** owner ECONOMY RULING, applied to data + enforced by a gate

---

## 1. The ruling (owner, 2026-08-10, verbatim intent)

> "The lens... is what are we building? If we are building regular structures, then it makes sense
> that they only cost... iron and wood. However, if it's magical based or ethereal based, then yes,
> it's crystal... Let's make a separation. So it doesn't touch all three."

Operationalized:
- **Regular structures** cost **wood + iron** (± food where already used). **Never crystals.**
- **Magical/ethereal structures** are **crystal-based**: **crystals + iron** (mapping the owner's
  "crystal and stone" to iron, the mineral — ⚠ owner may amend to crystals+wood). **Never wood+iron+
  crystals together.**
- **Invariant (gate-enforced): no structure's cost basket contains wood AND iron AND crystals.**

## 2. Audit of the live catalog (v15, both copies identical — run 2026-08-10)

Violators of the invariant / crystal-in-regular today:

| id | cost today | proposed side | proposed basket |
|---|---|---|---|
| `tower_wall_wizard` | wood:60, iron:30, crystals:70 | MAGICAL | crystals + iron (drop wood) |
| `tower_arcane_spire` | wood:40, iron:40, crystals:85 | MAGICAL | crystals + iron (drop wood) |
| `tower_siege_tower` | wood:160, iron:90, crystals:20 | REGULAR (mechanical) | wood + iron (drop crystals:20) |
| `tower_healer` | wood:110, iron:70, food:40, crystals:30 | ⚠ OWNER PIN — is healing magical? | if magical: crystals+iron(+food); if regular: drop crystals |
| `healing_caravan` | wood:150, iron:100, food:60, crystals:40 | ⚠ OWNER PIN — same question | same rule as tower_healer |
| `jeweler` | wood:50, iron:40, crystals:30 | ⚠ OWNER PIN — trade uses crystals as material, but it is a regular shop | recommend REGULAR: wood+iron (drop crystals) |

Also flagged for the pin:
- `arcane-tower` (the magic-upgrades building) costs **wood:60, iron:60 — no crystals at all.** Under
  the ruling it reads MAGICAL and should arguably become crystals+iron. Owner call.
- `mine_crystal` costs wood+iron — correct as-is (a regular mine that *produces* crystals).

All 21 remaining entries already conform (wood/iron ± food only).

## 3. Implementation (mechanical once §4 is pinned)

1. Edit `structures-catalog.json` cost baskets per the pinned table — **BOTH copies**
   (`Assets/Resources/Data/Canonical/` + `Assets/StreamingAssets/Data/Canonical/`), byte-identical,
   **version 15 → 16** in the same edit (the change-bumps-version discipline canon §10.3 wants).
2. Rebalance totals when dropping a component so the basket's rough value holds (e.g.
   `tower_arcane_spire` wood:40 folds into iron, not vanishes) — keep first-cost feel unchanged;
   numbers are owner-tunable afterward.
3. **New regression case** in the build-economy suite: parse the live catalog, FAIL on any entry whose
   basket contains wood AND iron AND crystals; additionally FAIL on crystals in any entry not on the
   pinned MAGICAL list (the list lives in the regression as the ruling's registry, dated + cited).
4. WO-911 v37 paid-basket + refund flow are unaffected (refunds return what was PAID, flat — basket
   composition is orthogonal). First-build FREE + timer grace unaffected.

## 4. Owner pins needed

1. Arcane pairing: crystals + **iron** (recommended — mineral flavor) or crystals + **wood**?
2. `tower_healer` / `healing_caravan`: magical or regular?
3. `jeweler`: recommend regular (wood+iron); confirm?
4. `arcane-tower`: move to crystal-based, or keep wood+iron as the one "mundane school of magic" shop?

## 5. IMPLEMENTATION LOG — 2026-08-14 (economy/data lane agent)

**Applied (1 of 6 rows):** `tower_siege_tower` — the ONE violator the catalog classifies
unambiguously *from its own fields*, so no owner pin was consumed:
- `displayName` = "Sky Ballista (Anti-Air)", `description` = "Wall-mounted spear thrower — fires
  spears at flying creatures", `element` = `None`, `projectileStyle` = `"bolt"`, and the top-level
  `_heightCadence` puts it in the **SIEGE ENGINE** group ("Machines, not architecture").
- Basket: build `w160 i90 c20 → w160 i110 c0`; L1→L2 `w192 i108 c24 → w192 i132 c0`;
  L2→L3 `w400 i225 c50 → w400 i275 c0`. Crystals folded **1:1 into iron**, so every basket TOTAL
  (270 / 324 / 675) and the tier-monotonic ladder are unchanged — composition moved, feel did not.
- `structures-catalog.json` **v16 → v17**, both canonical copies byte-identical
  (`md5 5e6a53225581b0c2bb0b4cb3524af68f`). A `_costBasketRule` block at the file head + a
  `_costBasketNote` on the row carry the ruling and the pending state.

**Gate shipped:** `Assets/Editor/Regression/CostBasketSeparationRegression.cs` `[cost-basket]`
(markers `COST_BASKET_OK` / `COST_BASKET_FAIL`), registered inside the fenced registry in
`DataRegression.RunAll`. It reads the **authored** baskets (`repo.cost` / `repo.upgradeCost`), not
`BuildModeController.CostFor` — CostFor folds in the buildCost-crystals fallback and the tower
softcap, neither of which is a statement about what a row is made of. Cases: `[invariant]` never
wood+iron+crystals · `[regular]` crystals only on the pinned MAGICAL set, **including the
all-zero-basket back door** that makes CostFor charge pure `buildCost` crystals · `[pins]` the
exemption list may only SHRINK (fails if a listed row stops violating, so a landed pin forces the
exemption's deletion in the same change) · `[applied]` `tower_siege_tower` stays converted with its
totals intact. `MagicalIds` is **deliberately EMPTY** — until §4 pin 1 lands, no row has an
owner-sanctioned crystal basket.

**NOT applied — the 5 rows carried as dated, cited exemptions:** `tower_wall_wizard`,
`tower_healer`, `healing_caravan`, `jeweler`, `tower_arcane_spire`. Deciding any of them is the
inference-fix CLAUDE.md §12 forbids; they wait on the owner.

## 6. NEW PIN raised 2026-08-14 — `tower_wall_wizard` is id-vs-data contradictory

§2 above classified this row **MAGICAL from its id**. The row's own data says otherwise:
`displayName` = **"Ballista"**, `element` = `None`, `projectileStyle` = `"bolt"`,
`behaviorId` = `DefenseTower` — per the owner rename ruling **2026-07-08** recorded verbatim in its
`orientation.note` ("the model IS a ballista (renamed)"). Read by nature it is a **mechanical
bolt-thrower like the two other ballistae**, i.e. REGULAR (drop crystals:70), not MAGICAL
(drop wood:60). The two readings send 70 crystals in opposite directions, so it is an owner call,
not an agent's. *(Separately: `tower_arcane_spire`'s MAGICAL side is not in doubt — `element`
`Aether`, `behaviorId` `ArcaneTower`, `projectileStyle` `spell`, and WO-1013 calls it arcane
outright — only its PAIRING is unpinned by §4 q1.)*

## 8. IMPLEMENTATION LOG — 2026-08-14 (economy/data lane agent, catalog v18) — THE PINS LANDED

**Owner rulings, verbatim (2026-08-14):**
1. *"Crystals and Iron"* → the MAGICAL basket is **crystals + iron** (not crystals+wood). §4 q1 CLOSED.
2. *"yes AoE healing"* → healing **IS** magical → `tower_healer` + `healing_caravan` are MAGICAL. §4 q2 CLOSED.
3. *"Crafting (can enbue preciouus sstones future release)"* → `jeweler` is a **CRAFTING** shop → **REGULAR**.
   ⚠ The owner flagged that a **FUTURE release may let it imbue precious stones** — that would be a
   re-classification **then**, and is expressly **not** a reason to make it magical today. §4 q3 CLOSED.
4. *"thats a baliista mechanical"* → `tower_wall_wizard` is **MECHANICAL → REGULAR**. The **DATA** reading
   beats the **ID** reading (displayName "Ballista", element None, projectileStyle "bolt" per the owner's
   2026-07-08 rename); `wizard` in the id is stale naming. §6 pin CLOSED.

**All 5 remaining rows applied, totals PRESERVED (the `tower_siege_tower` 1:1 discipline):**

| id | side | before | after | total |
|---|---|---|---|---|
| `tower_wall_wizard` | REGULAR | w60 i30 **c70** / w72 i36 c84 / w150 i75 c175 | w60 **i100** / w72 **i120** / w150 **i250** | 160 / 192 / 400 (unchanged) |
| `jeweler` | REGULAR | w50 i40 **c30** | w50 **i70** | 120 (unchanged) |
| `tower_arcane_spire` | MAGICAL | **w40** i40 c85 / w48 i48 c102 / w100 i100 c212 | i40 **c125** / i48 **c150** / i100 **c312** | 165 / 198 / 412 (unchanged) |
| `tower_healer` | MAGICAL | **w110** f40 i70 c30 | f40 i70 **c140** | 250 (unchanged) |
| `healing_caravan` | MAGICAL | **w150** f60 i100 c40 | f60 i100 **c190** | 350 (unchanged) |

- REGULAR rows fold the dropped **crystals 1:1 into IRON** (the v17 `tower_siege_tower` precedent).
- MAGICAL rows fold the dropped **wood 1:1 into CRYSTALS** — chosen because the ruling calls magical
  structures crystal-**BASED**; folding into iron would have left the mundane side dominant. Iron is left
  exactly as authored. Every basket total and every tier-monotonic ladder is byte-for-byte unchanged.
- `structures-catalog.json` **v17 → v18**, both canonical copies byte-identical
  (`md5 113f5485ed22740f9ee2040930479957`). `_costBasketRule` rewritten; a `_costBasketNote` carrying the
  owner's verbatim words added to each converted row.
- **`CatalogBootstrap.RegisterFallback` mirrored** for `tower_wall_wizard` + `tower_arcane_spire` (the only
  two of the five it registers) — `BuildEconomyRegression` gate 12 `[fallback-parity]` deep-compares every
  public `RepoProps` field, so an un-mirrored basket is a red build.

**Gate updated (`CostBasketSeparationRegression.cs`):** `MagicalIds` now holds the three magical ids;
`PendingPins` is **EMPTY** (the mechanism stays for any future pin — it is not a mute button); `[applied]`
became a table over **all six** converted rows, asserting the ruled side (regular → 0 crystals; magical →
0 wood **and** non-zero crystals) plus the preserved totals.

## 9. NEW OPEN PIN — `arcane-tower` ("Cathedral of Magic"), NOT converted

Cost today **wood:60 iron:60 crystals:0** — it does **not** violate the invariant, so it was never on the
exemption list and the gate is silent about it. But under pin 1 a magical building should be crystals+iron.
The evidence is genuinely split and an agent must not decide it:
- **Reads magical:** the name "Cathedral of Magic", `npcModel: Mage`, its baked twin
  `ArcaneTower_MagicUpgrades`, and it is the home of the magic research/upgrade tree.
- **Reads regular:** `behaviorId` is **`GameplayBuilding`**, not a caster — its own `_heightNote` says
  "*despite the id this is not a tower — it is a GameplayBuilding, the town's one civic landmark*". By the
  pin-3 logic the owner just used for the jeweler (a shop that *deals in* magic is still a shop), a civic
  building that *sells* magic upgrades is REGULAR.
**Owner call needed.** If magical: w60 i60 → **i60 c60** keeps the total at 120.

## 10. What NOT to touch (was §7; renumbered so the log sections read in order)

- Affinity/harvest math, Echo systems (WO-811 lane is live), the crystal SINKS (instant-finish,
  queue-slot pricing — WO-911 rulings), Gold/Coins, pack pricing.
