# Source of Truth → Notion (switchover note for CLI) — 2026-06-06

We moved the live backlog/board off **Linear** (its free tier caps at 250 non-archived issues + 2 teams,
and we're well past DEF-270) onto **Notion** (free, connected). The board mirrors
`MASTER_PIPELINES_BACKLOG_2026-06-06.md`.

## Where it lives
- **Home page:** *Defenders of the Realm — Pipelines (Source of Truth)*
  https://app.notion.com/p/378bf190c68981d0b63fe44b5661fa8f
- **Work Orders database (the board):**
  https://app.notion.com/p/f3115f05ecf940cf8968bd82bbbdff9f
  - Notion data source id (for API/MCP writes): `5f66b263-c732-4075-b94a-f5f4de9f8087`
- **106 WO rows** loaded across all 13 lanes.

## Database schema (properties)
- **Title** — `WO-NNN — short name`
- **WO** (number) — for sorting/filtering
- **Lane** (select) — `0 Verify … 12 Narrative/Quests` (matches the master doc lanes)
- **Status** (select) — Done · In progress · Ready · Held · Blocked · Spec
- **Depends On** (text) — blocking WOs/conditions
- **Source** (select) — Backlog · Vendor saga · Forgemasters · Lore · Pets · This session
- **Notes** (text)

## How we work now (keep these in sync)
1. **Numbering authority is unchanged:** `MASTER_PIPELINES_BACKLOG_2026-06-06.md` + `CLI_LANES_WO_NUMBERS.md`.
   Next free WO = **688** (refreshed 2026-07-12; authority = `CLI_LANES_WO_NUMBERS.md` banner, NOT this line — 685/686/687 minted, 677/678 have disk collisions). The old "430" figure below is FROZEN HISTORY.
   ~~Next free WO = **430** (through 429 used; 344–351 skipped, do not mint — reconciled 2026-06-12.~~
   412–428 minted on-board 06-11/12; 429 = repo "store stock from DB" spec renumbered from a colliding WO-414).
   Notion is the *board view*, not the numbering source.
2. **Full WO specs stay in the git repo** as `WORK_ORDER_NNN_*.md`. Notion rows are the status/lane/deps index.
3. **Status flow:** set a row to *In progress* when a WO is claimed, *Done* when its `*.RESULT.md` lands.
4. **New WO:** create the `WORK_ORDER_NNN_*.md` file + add a row here + slot it into a lane in the master doc
   (the nightly `keep-pipelines-full` task does this automatically when a lane runs thin).
5. **Group/sort** the Notion DB by **Lane** for the kanban view; filter `Status != Done` for the live queue.

## Known cleanups visible in the board
- Number collisions flagged in Notes: two **WO-106**, two **WO-108**, two **WO-282** (and a duplicate WO-110 spec).
  Renumber the newer of each from 307+ when convenient.

## Linear
Old Linear DEF-* issues are left as-is (read-only history). New tracking happens in Notion. If you want, we can
bulk-export/close the Linear issues later — not required.
