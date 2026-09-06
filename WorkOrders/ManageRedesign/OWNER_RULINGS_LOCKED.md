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
