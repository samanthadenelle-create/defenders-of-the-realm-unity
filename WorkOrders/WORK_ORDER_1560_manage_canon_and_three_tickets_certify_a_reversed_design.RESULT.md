# WO-1560 RESULT - three Manage tickets and both canon files certify a design that was reversed

**Status:** IMPLEMENTED 2026-09-06 (documentation only, UNCOMMITTED in the shared tree)

**Date:** 2026-09-06

## This was done as WO-1534 Part B1 - one lane, not two

WO-1560 is the record-repair half of WO-1534 split into its own ticket. A collision lane renumbered it
twice while the work was in flight; **the intermediate numbers are deliberately not written here.
WO-1535 now belongs to the enemy stat-table SSOT ticket** (`WORK_ORDER_1535_finish_the_enemy_stat_table_ssot_migration.md`),
so quoting the old number would point a future reader at an unrelated lane - the duplicated-state failure
CLAUDE.md section 2 describes. WO-1560 is the only live number for this work, and the work was executed
once, under WO-1534 Part B1.

**The evidence lives in `WorkOrders/WORK_ORDER_1534_the_raid_loop_never_closes_and_manage_cannot_reach_it.RESULT.md`,
section "Part B1"** - the verified file:line citations, the six files edited, the exact status lines
before and after, and the three findings that contradicted the ticket. It is not duplicated here.

## Acceptance (section 6), item by item

1. **All three tickets carry a banner and a status that no longer certifies an unmet acceptance** - DONE.
   `WO-2001` leads `SUPERSEDED` (its whole objective was reversed). `WO-2005` and its `.RESULT.md` keep
   the `FIXED` lead on purpose - CLAUDE.md section 13 makes FIXED "awaiting the owner's felt test", and
   promoting it to Done would skip that - with the CIVIC/six-filter acceptance named superseded in the
   line. `WO-2006` keeps `DONE` (grid, rail removal and tile-state model all stand) with the `>=12`
   acceptance at `:64` named superseded, plus its still-OPEN colourblind acceptance named as open.
2. **The WO-2005 banner records the claim was true at `a6bbc523d` and reversed by `32659c0f6`** - DONE,
   on both the ticket and the RESULT, citing `git show a6bbc523d:.../BuildFilter.cs` (`Civic` at `:59`,
   six `Chips` entries at `:76`).
3. **Rulings 5 and 7 and the three canon lines carry supersession notes naming the mockup** - DONE, all
   naming `docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png` and commit `32659c0f6`, with
   `BuildFilter.cs:57`/`:87-89` and `ManageScreenVM.cs:3500-3507` as the code authorities.
4. **No body rewritten** - DONE. Banners only, per CLAUDE.md section 15.
5. **`python tools/board_build.py` reports `BOARD_CHECK_OK`, 0 contradictions** - DONE:
   `BOARD_CHECK_OK 0 unlabeled, 0 missing status lines, 0 status contradictions, mint numbers readable`.

## Two deviations, both deliberate and both for the committer to weigh

- **Section 4.2 convention.** This ticket asks that the `OWNER_RULINGS_LOCKED.md` notes go under a new
  dated heading rather than beside the rulings. The dispatch that ran this lane asked for banners "at
  rulings 5 and 7", so they are inline blockquotes directly beneath those two rulings. **No ruling text
  was altered**, and the file's provenance sentence ("the body above is the author's and has not been
  edited") was amended in the same edit so it stays true. Moving them to a heading is a five-minute
  change if the committer prefers this ticket's convention.
- **Section 5's open question is REFUTED, not open.** "Barracks, Store, Echo Hollow and Healing Caravan
  are reachable only under ALL" is false at HEAD. `structures-catalog.json` authors
  `barracks=[DEFENSE]`, `healing_caravan=[DEFENSE]`, `market=[ECONOMY]`, `pet-house=[ECONOMY]`,
  `arcane-tower=[CRAFT]`, and the chip actually reads that field: `CatalogEntry.cs:87` ->
  `BuildInventoryModel.cs:283` -> `For(chip)` `:200-206` -> `Tiles(chip)` `:229-234` ->
  `ManageScreenVM.cs:3905` (`_activeFilter` set `:3248`, validated `:3242`). The owner's ruling on this
  call ("re-home the four service structures", 2026-09-06) is therefore **already satisfied by the tree
  and owes no code change.** The full chain is recorded in WO-1534 section B1.

## Scope held

No `.cs` file, nothing under `Assets/`, nothing under `api/`, no commit. Six documentation files edited
plus two RESULT files written; all LF, all additions ASCII.
