# WORK ORDER 940 — RESULT

**Date:** 2026-08-16  **Seat:** edit-only tooling agent (python/tools lane; no Unity, no commit — CLI reconciles + commits)
**Status:** DONE (tool-verified headless: regenerated board inspected programmatically)

## What was built (all in `tools/board_build.py`)

- **The date column is now the CREATED date, never mtime.** Resolution priority exactly per spec:
  1. `**Minted:** YYYY-MM-DD` parsed from the WO body (`_MINTED` regex);
  2. git first-add date via **ONE** call for all files
     (`git log --reverse --no-renames --diff-filter=A --date=short --format=%x01%ad --name-only -- WorkOrders/`;
     `--no-renames` so a file MOVED into WorkOrders/ still registers an Add there);
  3. mtime last resort, rendered with a leading `~` so an estimate can never be mistaken for a
     creation date. On this repo the fallback fired **0 times** — every one of the 958 rows resolved
     from Minted or git.
- **Rendered per row:** `YYYY-MM-DD &middot; <age>d` (e.g. `2026-07-13 · 27d`), plus
  `data-age-days="N"` on every `<tr>`.
- **7d+ marker:** rows older than 7 days carry a literal `7d+` badge — word + colour, never colour
  alone (owner is red/green colourblind). 871 of 958 rows currently carry it.
- **"opened within" filter:** `7d / 30d / 90d / all` buttons with live counts, ANDed with the
  existing bucket chips and the search box in `apply()` — composing, not replacing. Default `all`.
- **Sorting unchanged** (bucket, then number). `--check` contract untouched — still prints
  `BOARD_CHECK_OK / BOARD_CHECK_FAIL` on the unlabeled count only.

## Acceptance proof

- Runtime: **0.74 s** for the full 958-row build including the single git call (was ~2 s budget).
- **The whole-point proof — edited recently, shows the OLD date:**
  `WORK_ORDER_53_animator_culling.md` has file mtime **2026-08-15** (touched in last week's status
  sweep) but the board renders **`2026-06-22 · 54d 7d+`** — the git first-add date, 54 days older
  than the mtime the old column would have shown. 831 of 958 rows show a created date older than
  their mtime; the status-hygiene sweep no longer makes 8-week tickets read as new.
- Emitted-board greps: 958 rows carry `data-age-days`; `opened within:` control present with 4
  `.abtn` buttons; `.oldm` badge CSS present; "opened within 7d" = 87 rows.
- Companion docs unchanged: still 18 rows bucketed `Doc`, no age defect semantics attached.
- No WO markdown was hand-edited to add dates — age is derived at generation time.

## Conflict check vs WO-937

No conflict. WO-937 owns the status vocabulary + `--check` contract; this WO left both untouched
(the duplicate-number report added under WO-937 is report-only and orthogonal).

## Files touched

`tools/board_build.py` (the only file this WO permits), `docs/BOARD.md` §5 (created-date +
opened-within documented alongside the WO-937 gate note), this WO's status line, derived `BOARD.html`.
