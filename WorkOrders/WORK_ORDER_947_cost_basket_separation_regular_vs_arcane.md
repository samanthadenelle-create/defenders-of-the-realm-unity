# WORK ORDER 947 — Cost-basket separation: regular structures = wood+iron; magical/ethereal = crystal-based; never all three

**Status:** DONE — **FULLY APPLIED, NO OPEN PINS** (catalog v19, 2026-08-14) · **+ §12 AMENDMENT 2026-08-17** (the PURCHASE boundary; changes no cost row, no catalog bump, exemption list still empty). All four §4 pins + the §6 pin landed in v18 (6 rows); the **final** §9 pin — `arcane-tower` / "Cathedral of Magic" — was ruled MAGICAL by the owner and applied in **v19** (§11). The gate's exemption list is EMPTY and stays empty; `MagicalIds` holds **four** ids.
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

## 9. ~~NEW OPEN PIN~~ — `arcane-tower` ("Cathedral of Magic") — **ANSWERED 2026-08-14, see §11**

Cost today **wood:60 iron:60 crystals:0** — it does **not** violate the invariant, so it was never on the
exemption list and the gate is silent about it. But under pin 1 a magical building should be crystals+iron.
The evidence is genuinely split and an agent must not decide it:
- **Reads magical:** the name "Cathedral of Magic", `npcModel: Mage`, its baked twin
  `ArcaneTower_MagicUpgrades`, and it is the home of the magic research/upgrade tree.
- **Reads regular:** `behaviorId` is **`GameplayBuilding`**, not a caster — its own `_heightNote` says
  "*despite the id this is not a tower — it is a GameplayBuilding, the town's one civic landmark*". By the
  pin-3 logic the owner just used for the jeweler (a shop that *deals in* magic is still a shop), a civic
  building that *sells* magic upgrades is REGULAR.
~~**Owner call needed.**~~ **ANSWERED — MAGICAL.** w60 i60 → **i60 c60**, total 120. See §11 for the ruling and the reasoning it overrides. *(The "reads regular" bullet above is preserved verbatim as the record of a reading the owner OVERRULED — do not treat it as live guidance.)*

## 11. FINAL RULING + IMPLEMENTATION LOG — 2026-08-14 (economy/data lane agent, catalog v19)

**Owner ruling, verbatim:**
> *"cathedral of magic is where all magic upgrades anre and can unlock new teirs of spells"*

→ `arcane-tower` is **MAGICAL**. Basket becomes **crystals + iron**.

### Why this OVERRIDES the §9 "reads regular" analysis — recorded so nobody re-litigates it

A prior agent applied the owner's own pin-3 jeweler logic (*"a shop that DEALS IN magic is still a shop"*)
and concluded **REGULAR**, citing `repo.behaviorId: "GameplayBuilding"` and the row's own `_heightNote`
(*"despite the id this is not a tower — it is a GameplayBuilding, the town's one civic landmark"*). That
reading was reasonable on the surface evidence — which is exactly why it is written down here.

**The owner's distinction:**
- The **jeweler SELLS** things that happen to be precious. It *deals in* value; it is not *made of* it.
- The **Cathedral is WHERE MAGIC UPGRADES LIVE AND WHERE NEW SPELL TIERS UNLOCK.** It is the **ENGINE of
  magical progression**, not a vendor that deals in magic. That is a difference in KIND, not in degree.
- **`behaviorId: GameplayBuilding` describes its BEHAVIOUR** — it is not a firing tower, it hosts an
  interaction — **NOT its cost class.** Likewise the `_heightNote` is a *sizing* note and the id is stale
  naming. None of the three is evidence about what the building is MADE of.

The surface evidence genuinely points both ways, so the ruling is the tiebreak. **Do not re-classify this
row off the id, the `behaviorId`, or the `_heightNote`.** The distinction is mirrored verbatim into the
row's new `_costNote`, into `_costBasketRule`, and into the gate's header + `AppliedRows` entry.

### Applied (total PRESERVED, same 1:1 discipline as the three v18 magical rows)

| id | side | before | after | total |
|---|---|---|---|---|
| `arcane-tower` | MAGICAL | **w60** i60 c0 | i60 **c60** | 120 (unchanged) |

- **No `upgradeCost` ladder on this row** (singleton, no `maxLevel`) — exactly **ONE** basket to transform.
- `structures-catalog.json` **v18 → v19**, both canonical copies byte-identical:
  **`md5 b1e207690290dc2cedcfcf7b1aef47fc`** (64,393 bytes, 0 NUL bytes).
- `_costBasketRule` rewritten: the last pin is spent, WO-947 is fully applied, no open classification
  questions. A `_costNote` carrying the owner's verbatim words + the overruled reading was added to the row.
- **`CatalogBootstrap.RegisterFallback` checked — `arcane-tower` is NOT in the fallback mirror.** That file
  registers exactly three rows (`tower_ground_archer`, `tower_wall_wizard`, `tower_arcane_spire`), so
  `BuildEconomyRegression` gate 12 `[fallback-parity]` has nothing to compare here. **No mirror edit needed**
  (verified by grep, not assumed — an un-mirrored basket would be a red build).

### Gate updated (`Assets/Editor/Regression/CostBasketSeparationRegression.cs`)

- `MagicalIds` += **`arcane-tower`** (with the ruling verbatim in the comment) → **four** ids.
- `AppliedRows` += `arcane-tower` → `Magical = true`, `Totals = { 120 }`, `Why` = the ruling + the
  overruled reading. Case 4 `[applied]` therefore now asserts **0 wood AND crystals > 0 AND total 120**
  for this row — a revert or a stealth re-balance trips it.
- `PendingPins` **left EMPTY** and the case-3 shrink-only mechanism **untouched** — not weakened, not bypassed.
- Header + docstrings updated (pin 5, v19). **No FlowTrace / Guard call was stripped** (CLAUDE.md §12).

### Verified from the re-parsed DATA (not from the diff)

Both copies re-parsed after the edit:
- **(a) ZERO rows carry wood AND iron AND crystals together** — 42 baskets scanned, 0 violators.
- **(b) The crystal-carrying rows are EXACTLY the four magical ids:**
  `arcane-tower`, `healing_caravan`, `tower_arcane_spire`, `tower_healer`.

### Blast radius (REPORTED, not changed)

- **Player-reachable NOW, not locked.** `arcane-tower` is `type: Resource` → it lands in the **Town**
  palette, and `build-categories.json` Town `lockedIds` = `[jeweler, mine_crystal, mill, lumbermill,
  armorer, collector_forge]` — `arcane-tower` is **not** among them and is **not** in `visibleLockedIds`.
- **Early-resource impact is near-zero, for a reason worth knowing.** `StartingBudget.StrategicWood /
  StrategicIron` are both **0** (v32 replaced the founding seed with first-build freebies), so nothing is
  seeded either way. More to the point, `BuildModeController.FreeBuildAvailable` **lane 3** makes the
  **first placement of each distinct NON-tower id FREE** — and `IsTowerEntry` is FALSE for this row
  (`type: Resource`, `behaviorId: GameplayBuilding`). Since the row is `singleton: true`, the normal player
  builds the Cathedral **free, once**. The new crystal cost is only ever felt on a **re-build after a sell
  or a destroy**, where the player now needs **60 crystals instead of 60 wood**.
- **⚠ A STALE COMMENT, flagged not fixed:** `BuildModeController.FreeBuildAvailable`'s doc comment (lane 2)
  claims *"so **arcane-tower** / tower_arcane_spire / ... is charged from the very first placement"*. That is
  **false for `arcane-tower`** — `IsTowerEntry` tests `type == Tower` or `behaviorId` in
  {`DefenseTower`,`ArcaneTower`}, and this row is `Resource` / `GameplayBuilding`, so it takes the lane-3
  freebie. The comment was wrong before this change and is unaffected by it; it is called out because a
  reader pricing this row will otherwise trust it.
- **Sell/refund:** `RefundCostFor` derives from `CostFor`, so a sold Cathedral now refunds in the crystal
  basket rather than wood — mechanically consistent, no special case needed.
- **In-flight queue jobs** stamped their **paid basket** at commit (save v37 `paidWood/.../paidCrystals`), so
  a job enqueued before this change still refunds exactly what it paid. No migration needed.

## 10. What NOT to touch (was §7; renumbered so the log sections read in order)

- Affinity/harvest math, Echo systems (WO-811 lane is live), the crystal SINKS (instant-finish,
  queue-slot pricing — WO-911 rulings), Gold/Coins, pack pricing.

---

## 12. ★ AMENDMENT 2026-08-17 — the purchase boundary is NOT the cost boundary

**Trigger:** WO-1037 §3 was ruled **OPTION (b)** by the owner on 2026-08-16 — *"we should have small
instant packs"* · *"small wood only"* · single-resource impulse packs for **Wood, Iron, Food, Crystals**,
small/medium/large, exactly one economy key per SKU, $2/$5 tiers with $5 the ceiling. WO-1037 flagged
that this "needs a WO-947 amendment" because **real money can now buy the REGULAR basket**, which this
WO was written on the assumption it never would.

### 12a. What this amendment does NOT change — read this before touching anything

**The §2 invariant stands, untouched and still gate-enforced:**

> **No structure's cost basket contains wood AND iron AND crystals.**
> Regular structures cost **wood + iron**. Magical/ethereal structures are **crystal-based**.

- **No structure's cost basket changes.** Not one row.
- **`MagicalIds` still holds four ids.** The gate's exemption list is **still EMPTY and stays empty**.
- **Catalog stays at v19.** This amendment authors no data and requires no catalog bump.
- Nothing in §1–§11 is reopened. WO-947 remains **DONE**.

### 12b. What it establishes — a second, orthogonal axis

WO-947 separated **what a structure COSTS**. It never spoke to **what real money may BUY**, and the two
were silently conflated when WO-1037 called this a conflict. They are different boundaries:

| axis | question | ruled by |
|---|---|---|
| **COST boundary** (WO-947 §2) | which resources does this structure charge? | WO-947 — unchanged |
| **PURCHASE boundary** (this amendment) | which resources can real money grant? | WO-1037 §3, option (b) |

**The ruling:** real money may grant **any single harvestable resource** — wood, iron, food or crystals —
as a one-key impulse pack. Buying wood does not make a Lumber Mill cost crystals, and does not make
crystals a regular resource. The separation was always about **charging**, never about **granting**.

### 12c. Guardrails carried over (these are the load-bearing half)

1. **Exactly ONE economy key per SKU.** A wood pack grants wood and nothing else. A multi-resource
   bundle would re-mix the baskets through the back door and IS forbidden — that would be a genuine
   §2 violation dressed as a store item.
2. **$5 ceiling, $2 and $5 tiers** (standing owner pricing ruling, memory
   `solana-store-early-access-pack-pricing`). Small must feel impulse-priced.
3. **Packs grant resources, never structures, never levels, never queue completions** that the basket
   rules would otherwise price in crystals. Money buys the input, never the outcome.
4. **The offer only appears against a real shortfall** (WO-1037's whole premise) — it is a shortfall
   remedy, not a storefront.

### 12d. Why this is genre-normal and not a character change

The concern WO-1037 raised — *"money now buys the regular basket — a real change to the game's economic
character"* — is worth stating and then answering plainly: this is the standard CoC model (gems buy
resources), the ceiling is $5, and **crystals were already purchasable**, so money already reached the
economy. What (b) changes is only *which* resource the player may spend that money on — and letting them
pick the one they actually lack is strictly friendlier than forcing a crystal detour.

⚠ **The line that would change the character, and is therefore NOT ruled here:** selling *time* or
*outcomes* directly — instant structure completion for cash, or a pack that grants a finished upgrade.
Those touch the crystal SINKS (WO-911 rulings) and §10 keeps them out of scope. If that is ever wanted
it is a **new** owner ruling with a new date, not an extension of this amendment.

### 12e. Acceptance

- [ ] The §2 gate still passes with an **empty** exemption list (this amendment must not require one).
- [ ] Every pack SKU grants exactly one economy key — assert it, do not trust authoring.
- [ ] No structure cost row differs from v19.
