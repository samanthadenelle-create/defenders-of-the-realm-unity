# WO-1534 RESULT - the raid loop never closes, and three Manage tickets are DONE against a design that was reversed

**Status:** READY - PARTIAL: Part B1 landed 2026-09-06 (documentation only, no .cs touched); Parts A, B2-B5 READY behind the wave-two gate.

**Date:** 2026-09-06

## Part B1 - the record now matches the screen

Every claim below was re-read at source this session (CLAUDE.md section 11B); line numbers are as measured,
not as the ticket quoted them.

**Verified before editing**
- `Assets/_Modules/Core/Catalog/BuildFilter.cs:57` - "THERE IS NO CIVIC CHIP. Do not add one back";
  `:87-89` `Chips = { All, Economy, Defense, Craft, Storage }` (the ticket cited `:59` / `:87-90`).
- `git show a6bbc523d:Assets/_Modules/Core/Catalog/BuildFilter.cs` - `Civic` at `:59`, six `Chips` entries
  at `:76`. WO-2005's RESULT was TRUE when written; it went stale under a legitimate reversal.
- `Assets/_Modules/Core/Manage/ManageWorkspacePanel.cs:412` - "THERE IS NO TAB ROW AT ALL ANY MORE -
  BuildTabs IS DELETED, NOT EMPTIED" (`:410` records that the door MOVED).
- `Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs:3500-3507` - BUILD grid `GridColumns = 5`,
  `GridRows = 2` = **10** tiles, against WO-2006's `>=12` acceptance at `:64`.
- `docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png` present (2190985 bytes). Implementing commit `32659c0f6`.

**Files edited (documentation only)**
- `WorkOrders/ManageRedesign/WO-2001_MANAGE_INFORMATION_ARCHITECTURE.md` - banner + status now
  `SUPERSEDED ...` (was `DONE - landed in bb51b8b9c (verified 2026-09-06)`). Its whole objective was reversed.
- `WorkOrders/ManageRedesign/WO-2005_BUILD_INVENTORY_RECONCILIATION_AND_FILTERS.md` and its `.RESULT.md` -
  banner; the `FIXED` lead is KEPT on purpose (it awaits the owner's felt test, CLAUDE.md section 13) with
  the CIVIC/six-filter acceptance named superseded.
- `WorkOrders/ManageRedesign/WO-2006_BUILD_GRID_AND_TILE_STATE.md` - banner; only the `>=12` line at `:64`
  is superseded, so the `DONE` lead is kept and the clause is appended. The colourblind-legibility
  acceptance on the same list is UNMET rather than superseded, and the status line now says so.
- `WorkOrders/ManageRedesign/OWNER_RULINGS_LOCKED.md` - `STALE:` banners under rulings 5 (CIVIC) and 7
  (`>=12` tiles). The provenance sentence ("the body above ... has not been edited") was amended in the same
  edit so it stays true - section 15.
- `WorkOrders/ManageRedesign/00_MANAGE_REDESIGN_CANON.md` - `STALE:` banners at the `>=12` target
  (section 1), the CIVIC filter row (section 3) and the per-filter `>=12` target (section 3).

No ticket BODY was rewritten (section 15). No `.cs`, no file under `Assets/`, no `api/`. No commit.

## What contradicted the ticket

1. **Acceptance 3 is moot as written.** WO-2001/2005/2006 were never board ROWS: `tools/board_build.py`
   globs `WorkOrders/*.md` at ONE flat level and keys on the `WORK_ORDER_` prefix, so `ManageRedesign/WO-2001_*.md`
   fails both tests (`:637-660`); the subdirectory is only swept for `**Status:**` PRESENCE (`:669-676`).
   No row for WO-2001/2005/2006 exists in `BOARD.html` before or after; the only `2001` hits are the
   WO-1427/1428 rows, whose status prose cites the program range "wo-2001..2017". The board was never
   showing three false greens - the ticket FILES were. `python tools/board_build.py` was re-run anyway.
   Measured after regeneration: `BOARD_CHECK_OK 0 unlabeled, 0 missing status lines, 0 status
   contradictions`; the single `MALFORMED_STATUS_MARKER` names WO-1381 and is pre-existing.
2. **The dispatched WO-1534 status wording would have gone green.** "PART B1 DONE ..." leads with a
   non-canonical word, so `classify_status` falls to `:183`, where `has_result or "DONE" in s` buckets it
   **Done** - rendering five open parts as finished. A READY-led equivalent is used instead; the reasoning
   is recorded in an HTML comment beside the status line.
3. **The B1 "only under ALL" consequence is refuted**, and a peer seat had already recorded the data half.
   The last link was walked here: `CatalogEntry.cs:87` -> `BuildInventoryModel.cs:283` -> `For(chip)`
   `:200-206` -> `Tiles(chip)` `:229-234` -> `ManageScreenVM.cs:3905` (`_activeFilter` set `:3248`,
   validated `:3242`). The owner's ruling B1 ("re-home the four service structures") is therefore **already
   satisfied by the tree and owes no code change**.

4. **A peer seat minted the same lane as its own ticket: WO-1560**,
   `WorkOrders/WORK_ORDER_1560_manage_canon_and_three_tickets_certify_a_reversed_design.md` (was READY TO
   IMPLEMENT, untracked). It was renumbered twice by a collision lane while this ran; **the intermediate
   numbers are deliberately not repeated here - WO-1535 now belongs to the enemy stat-table SSOT ticket,
   so citing it would point a future reader at the wrong lane.** WO-1560 is the only live number.
   Read read-only: this work satisfies all five of its section 6 acceptance items.
   ONE deviation - its section 4.2 asks that the OWNER_RULINGS notes go "under a new dated heading, do not
   touch rulings 1-20", while the dispatch here asked for banners "at rulings 5 and 7". The banners are
   inline blockquotes beneath those two rulings; **no ruling text was altered**, and the file's provenance
   sentence was amended in the same edit so it no longer claims an unedited body. Flag for the committer.
5. **WO-1534.md was edited concurrently by a peer seat mid-task** - the section B1 refutation paragraph
   (`:282-291`) appeared between two of this lane's reads. `git diff` on that one file therefore mixes
   both seats' lines.

## Also recorded in the WO

The owner's 2026-09-06 rulings on all four section-D calls (A1 named camp + door, A2 warning not a lock,
A4 first touch cancels the timer with a ~30 s guard, B1 re-home) are appended as a dated "D. RULINGS"
block; the original questions are left in place for provenance.
