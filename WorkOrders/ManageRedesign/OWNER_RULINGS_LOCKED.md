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
