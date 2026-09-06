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
