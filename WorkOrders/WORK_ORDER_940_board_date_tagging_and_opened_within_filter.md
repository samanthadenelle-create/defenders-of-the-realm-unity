# WORK ORDER 940 — Board: CREATED-date tag on every ticket + an "opened within" filter

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-09 (number from the `CLI_LANES_WO_NUMBERS.md` banner; banner bumped 940 → 941 in the SAME edit as this mint)
**Lane:** Tooling / board. **Touches exactly one file: `tools/board_build.py`.** No game code, no Unity, no scene, no UI.
**Owner ruling 2026-08-09 (verbatim):** *"i want aged tagged to every ticket"* · *"date tagged"* ·
*"so we can filter opened within and see"* · *"created date"*.
**Suited to an overnight seat:** self-contained, single file, verifiable in ~2 seconds with no editor.

---

## 1. Why

`SUNDAY_HOUSEKEEPING.md` §4 makes age the primary evidence of whether a ticket is still true —
**within 7 days: take at face value; older: verify at source.** That rule is unusable if the board
cannot show how old a ticket is. This WO makes the threshold visible and filterable.

## 2. The defect this carries

**`tools/board_build.py:116`**

```python
age = datetime.datetime.fromtimestamp(r["mtime"]).strftime("%Y-%m-%d")
```

The column is named `age` and is populated from **`os.path.getmtime`** (stored at `:104`) — that is
**LAST MODIFIED, not CREATED.** Any edit — a status fix, a typo, a banner sweep — resets a ticket's
apparent age. An 8-week-old ticket touched this morning reads as brand new, which inverts the exact
signal the owner wants. "Opened within" cannot be answered by the board as it stands.

## 3. What to build

**The date is the CREATED date** (when the ticket was opened), resolved in this priority order:

1. **`**Minted:** YYYY-MM-DD`** parsed from the WO body — the authored creation date, and the most
   trustworthy because a human stated it. (Recent WOs carry it; see WO-939/940.)
2. **git first-add date** — the commit that ADDED the file. Get all of them in **ONE** call, not one
   per file (there are ~880): `git log --reverse --diff-filter=A --date=short --format=%ad --name-only -- WorkOrders/`,
   then map path → first date seen. A per-file `git log` loop is too slow and will make the 2-second
   board build unusable.
3. **mtime** — last resort only, and it must be visibly marked as an ESTIMATE (e.g. `~2026-07-13`) so
   nobody mistakes a guess for a creation date.

Then:

- [ ] Render **created date + age in days** per row (e.g. `2026-07-13 · 27d`).
- [ ] Rows older than **7 days** carry a visible marker — a distinct colour is NOT sufficient on its own
      (the owner is red/green colourblind); use a word or symbol plus colour.
- [ ] Add **"opened within"** filter controls: `7d` / `30d` / `90d` / `all`, composing with the existing
      bucket filters and the search box rather than replacing them. Emit `data-age-days` per row and
      extend the existing `apply()` in the inline `<script>`.
- [ ] Sorting stays as-is by default (bucket, then number); do not reorder the board in this WO.

## 4. Acceptance criteria

- [ ] `python tools/board_build.py` still completes in ~2 seconds over all ~880 rows.
- [ ] Every row shows a created date and an age in days.
- [ ] A ticket whose file was edited today but created weeks ago shows the **OLD** date. State the WO
      number you used to prove it in the RESULT — this is the whole point of the ticket.
- [ ] `python tools/board_build.py --check` still prints its `BOARD_CHECK_OK` / `BOARD_CHECK_FAIL`
      line and its exit code is unchanged. Do not alter the unlabeled-defect reporting.
- [ ] The "opened within 7d" filter shows exactly the tickets inside the owner's threshold.
- [ ] Companion docs (non-`WORK_ORDER_*` files in `WorkOrders/`) still bucket as today — they are not
      work and must not become age defects.

## 5. What NOT to touch

- Anything outside `tools/board_build.py`. Do **not** edit WO markdown files to add dates by hand — a
  stored age is stale the next morning, which is the disease this rule exists to cure. **Age is
  DERIVED, never typed.**
- The status-bucketing vocabulary and the `--check` contract (WO-937/WO-1011 own those).
- `BOARD.html` is a generated artifact — never hand-edit it.
