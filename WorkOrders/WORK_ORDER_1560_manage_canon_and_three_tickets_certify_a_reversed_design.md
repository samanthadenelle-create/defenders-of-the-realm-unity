# WO-1560: three Manage tickets and both canon files certify a design that was reversed — banner them

**Status:** IMPLEMENTED 2026-09-06 (documentation only, UNCOMMITTED in the shared tree) - landed as WO-1534 Part B1; evidence in `WorkOrders/WORK_ORDER_1534_the_raid_loop_never_closes_and_manage_cannot_reach_it.RESULT.md`, section "Part B1". *(was: READY TO IMPLEMENT - "DO THIS FIRST. It is documentation only, costs about an hour, and while it stands every Manage review pays the tax in section 3.")*
**Priority:** P1 (highest leverage in the WO-1534 set)
**Silo:** `WorkOrders/ManageRedesign/*.md` — **DOCUMENTATION ONLY. Not one `.cs` file is touched.**
**Parent:** WO-1534 §B1. **Source:** read-only review 2026-09-06 (CLI seat), every line re-read at source.
**Minted** from the banner (`CLI_LANES_WO_NUMBERS.md`, renumbered to the banner's hundred-and-second-pass reconciliation, 2026-09-06 22:12).

---

## 1. THE DEFECT

A later owner mockup (`docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png`) reversed the Manage tab IA to a
hub, and reversed the filter set from six chips to five. **The reversal is LEGITIMATE — the mockup is the
spec.** The defect is that **nothing recorded it**, so three tickets still certify the superseded design
and two canon files still assert superseded values.

| File | Says | The code ships | Proof |
|---|---|---|---|
| `WO-2001_MANAGE_INFORMATION_ARCHITECTURE.md` — **DONE, verified** | `:71` *"BUILD, ARMY, RESEARCH are one tap from each other"*; title is *"Replace Manage Hub With Direct BUILD/ARMY/RESEARCH Tabs"* | **no tab row**; navigation is the hub + back arrow — i.e. the hub it was written to REPLACE | `ManageWorkspacePanel.cs:412` — *"⛔ AND THERE IS NO TAB ROW AT ALL ANY MORE - `BuildTabs` IS DELETED, NOT EMPTIED"* |
| `WO-2005_..._FILTERS.RESULT.md` — **FIXED** | `:16` *"**6 filters implemented:** ALL, ECONOMY, DEFENSE, CRAFT, STORAGE, CIVIC"* + a CIVIC membership table at `:26` | **five** chips | `BuildFilter.cs:87-90` `Chips = { All, Economy, Defense, Craft, Storage }`; `:59` *"⛔ THERE IS NO CIVIC CHIP. Do not add one back."* |
| `WO-2006_BUILD_GRID_AND_TILE_STATE.md` — **DONE, verified** | `:64` *"≥12 tiles visible when inventory/filter size allows"* | **5 × 2 = 10**, authored | `ManageScreenVM.cs:3500-3507` |
| `OWNER_RULINGS_LOCKED.md:7` (ruling 5) | filters include **CIVIC** | five chips | as above |
| `OWNER_RULINGS_LOCKED.md` (ruling 7) / `00_MANAGE_REDESIGN_CANON.md:19, 52, 61` | **"at least 12 visible tiles"**, and CIVIC in the filter list | 10 tiles, five chips | `CAPTURE_LOOP_GOAL.md:82` specifies *"5 columns x 2 rows = 10 tiles visible"* |

**The tab reversal is independently visible in two device frames three hours apart:**
`Logs/device/screens/owner-screen-144143.png` (14:41) carries the BUILD | ARMY | RESEARCH row;
`Builds/ui-capture/ManageFlow_BUILD_gridtop_2670x1200.png` (18:39) does not.

## 2. ⚠ BE FAIR — WO-2005's RESULT WAS TRUE WHEN IT WAS WRITTEN

**Checked, do not skip this when writing the banner.**
`git show a6bbc523d:Assets/_Modules/Core/Catalog/BuildFilter.cs` has `Civic` at `:59` and **six** entries
in `Chips` at `:76`. CIVIC genuinely shipped in WAVE 0 (`a6bbc523d`, 11:20); `32659c0f6` (16:51, *"the
Manage screens rebuilt against the owner's mockup"*) removed it five hours later.

**Nobody wrote a false claim.** All three rows are the same shape: a correct record that went stale under
a legitimate reversal that nothing bannered. **Write the banners that way** — this is the failure
CLAUDE.md §15 exists to prevent, not bad work by a seat.

## 3. WHY THIS IS P1 — THE TAX IS MEASURED, NOT ASSERTED

The review that produced this ticket derived **four** confident findings from the stale canon and had to
refute all four by opening the code (WO-1534 §C): "CIVIC is missing" and "the density target is missed"
both came straight from these files. **The next seat will do the same, and may not check.** Three false
greens on the board also mean the owner's felt-tests keep re-finding "fixed" things unfixed.

## 4. WHAT TO DO

1. **`WO-2001`, `WO-2005`'s RESULT, `WO-2006`** — each gets a supersession banner naming the mockup, the
   commit that reversed it, and the code that implements the current shape. Change each **Status** so it
   no longer certifies an unmet criterion.
   ⛔ **DO NOT rewrite the bodies.** CLAUDE.md §15 freezes dated tickets and RESULT files: *"If one reads
   as current, add a `⚠ SUPERSEDED <date>` banner — do not rewrite the body."*
2. **`OWNER_RULINGS_LOCKED.md`** — a one-line supersession note against **ruling 5** and **ruling 7**.
   ⛔ The file states its body is the author's and **has not been edited**; every CLI addition goes under
   its own heading. Follow that convention exactly — add a new dated heading, do not touch rulings 1-20.
3. **`00_MANAGE_REDESIGN_CANON.md`** — `STALE:` flags at `:19`, `:52` and `:61` per §15.
4. Regenerate the board: `python tools/board_build.py`. It must still report `BOARD_CHECK_OK` with
   **0 status contradictions**.

## 5. OPEN FOR THE OWNER — one question, and it is not blocking

With CIVIC gone, **barracks, Store, Echo Hollow and Healing Caravan are reachable only under ALL — the
one filter that scrolls.** `BuildFilter.cs:59-73` re-homed all five CIVIC rows deliberately and names each
mapping, so this is a consequence, not an accident. Is four service structures sharing only the scrolling
filter the intended result, or does one need re-homing? **Banner the files regardless — this question does
not block any of §4.**

## 6. ACCEPTANCE

1. All three tickets carry a banner and a status that no longer certifies an unmet acceptance.
2. The WO-2005 banner explicitly records that its claim was **true at `a6bbc523d` and reversed by
   `32659c0f6`**.
3. Rulings 5 and 7 and the three canon lines carry supersession notes naming the mockup.
4. No ticket body, ruling body or RESULT body is rewritten — banners only.
5. `python tools/board_build.py` reports `BOARD_CHECK_OK`, 0 contradictions.

## 7. WHAT NOT TO TOUCH

- **Any `.cs` file.** The code is CORRECT: five chips and a 10-tile grid are the mockup's spec.
- The 13 ManageRedesign files carrying uncommitted `**Status:**` lines from the **WO-1492** lane. Add your
  banners without reverting or reformatting that lane's edits.
- WO-2001/2002's other uncommitted edits in the shared tree.
