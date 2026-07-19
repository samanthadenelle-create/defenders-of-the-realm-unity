# Notion Workspace Schema - Defenders of the Realm / Echoes of Elarion

Exact database schemas for the BRAND-NEW Notion instance. Grounded in the retired
board's schema (`NOTION_SOURCE_OF_TRUTH.md`), CLAUDE.md 13 (ticket pipeline) and 2
(WO protocol), `CLI_LANES_WO_NUMBERS.md` (numbering authority), and
`SUNDAY_HOUSEKEEPING.md` 2 (known dictionaries).

ASCII only. Property types use Notion's names (Title, Text/Rich text, Number, Select,
Multi-select, URL, Date, Checkbox, Relation).

Prior board (retired, for reference only - do NOT reconnect):
- Home page: https://app.notion.com/p/378bf190c68981d0b63fe44b5661fa8f
- Work Orders DB: https://app.notion.com/p/f3115f05ecf940cf8968bd82bbbdff9f
- Old data-source id (API/MCP): `5f66b263-c732-4075-b94a-f5f4de9f8087`
- We migrated off Linear (250-issue free cap) onto Notion; now standing up a fresh instance.

Numbering authority is unchanged and stays in git: `MASTER_PIPELINES_BACKLOG_2026-06-06.md`
+ `CLI_LANES_WO_NUMBERS.md` banner. Notion is the board VIEW, never the numbering source.
Full WO spec + `.RESULT.md` files stay in the git repo under `WorkOrders/`.

---

## DB 1 - Work Orders (the board; mirror of the retired DB)

The primary kanban. One row per work order. Seed from `work-orders-seed.csv`
(665 rows enumerated from `WorkOrders/*.md` with real, git-reconciled status).

| Property | Type | Options / notes |
|---|---|---|
| Title | Title | `WO-NNN - short name` (Notion requires the Title property; put the WO# inside it too for readability) |
| WO# | Number | numeric, for sort/filter (collisions exist in the archive - many numbers repeat; see Notes) |
| Status | Select | `Spec`, `Ready`, `In Implementation`, `Done`, `RESULT-filed`, `Closed` |
| Type | Select | `feature`, `fix`, `docs` |
| Silo/Lane | Multi-select | `0 Verify`, `1 World/Env`, `2 Combat/AI`, `3 Combat Feel`, `4 UI/HUD`, `5 World/Exploration`, `6 Economy/Progression`, `7 Persistence/Backend`, `8 Monetization/Store`, `9 VFX/Audio`, `10 Build/Deploy/Perf`, `11 Build Mode/Player Base`, `12 Narrative/Onboarding/Quests` (matches `CLI_LANES_WO_NUMBERS.md` lanes) |
| Stage | Select | `Queue`, `QA`, `CLI`, `PO` (the QA -> CLI -> PO pipeline from CLAUDE.md 13 / `docs/TICKET_PIPELINE.md`) |
| Owner | Select | `PO (Samantha)`, `CLI`, `UI (Claude)`, `Grok` (per CLAUDE.md 2 role split; Grok authors WOs from 723 up) |
| Priority | Select | `P0`, `P1`, `P2`, `P3` |
| Spec | URL | link/path to `WorkOrders/WORK_ORDER_NNN_*.md` in git |
| RESULT | URL | link/path to `WorkOrders/WORK_ORDER_NNN_*.RESULT.md` when filed |
| Updated | Date | last status change |
| Notes | Rich text | status source citation (spec Status line / commit / RESULT) |

Status meanings (reconciled to git reality, not just the spec header text):
- `Spec` - drafted, not yet marked ready / awaiting owner pins.
- `Ready` - spec says READY TO IMPLEMENT, not yet claimed.
- `In Implementation` - claimed, code in flight, not yet committed.
- `Done` - committed to git (cite the commit) but no `.RESULT.md` yet.
- `RESULT-filed` - a `*.RESULT.md` exists (CLI verified + wrote the result).
- `Closed` - PO felt-verified and closed (CLAUDE.md 13: PO closes, not CLI).

Views (create all five):
1. `By Status` - board grouped by Status (Spec | Ready | In Implementation | Done | RESULT-filed | Closed).
2. `By Silo` - board grouped by Silo/Lane (the 13-lane kanban; retired board grouped by Lane).
3. `By Stage (pipeline)` - board grouped by Stage: Queue | QA | CLI | PO. This is the
   QA -> CLI -> PO hand-off board from the ticket pipeline.
4. `Live queue` - table filtered `Status is not Done AND Status is not RESULT-filed AND Status is not Closed`,
   sorted by WO# desc. The retired board's default (`Status != Done`).
5. `Current arc` - table filtered `WO# >= 739`, sorted WO# asc. The live 739-749 work.

---

## DB 2 - Regression Coverage (the weekly silo-audit coverage matrix)

Home for `SUNDAY_HOUSEKEEPING.md` step 4 ("we must KNOW our regressions cover everything
found") and the KNOWN DICTIONARY `docs/reference/REGRESSION_COVERAGE_MATRIX.md`.
One row per audit finding.

| Property | Type | Options / notes |
|---|---|---|
| Finding | Title | the audited gap/finding (one line) |
| Silo | Select | same 13-lane options as Work Orders Silo/Lane |
| Severity | Select | `P1`, `P2`, `P3` |
| Covered? | Select | `Yes`, `No`, `Soft` (Soft = partially / indirectly covered) |
| Covering Suite | Rich text | regression oracle `[tag]` that locks it (e.g. `[ui-mvvm]`, `DataWebRegression`, `GEAR_CURATION_OK`) |
| Proposed Regression | Rich text | if uncovered, the new suite to author |
| Source | Rich text | `file:line` / asset path proving the finding (every dictionary entry carries its citation) |
| Sweep Date | Date | which Sunday sweep produced/last-touched this row |
| WO | Relation | -> Work Orders (link the queued regression WO, if any) |

Views:
1. `Uncovered` - filter `Covered? is No`, sort Severity. The must-fix queue.
2. `By Silo` - grouped by Silo.
3. `Soft coverage` - filter `Covered? is Soft` (the audit's watch-list).

---

## DB 3 - Known Dictionaries (the registry index)

Mirror of `SUNDAY_HOUSEKEEPING.md` 2. One row per durable, canonical registry so state
stays KNOWN and any single fact is re-verifiable at a glance. This is an INDEX - the
registries themselves live in git; this DB tracks their freshness.

| Property | Type | Options / notes |
|---|---|---|
| Dictionary | Title | registry name |
| What it stores | Rich text | one-line description |
| Location | Rich text | git path / source-of-truth file |
| Last refreshed | Date | last Sunday-sweep refresh |
| Owner | Select | `CLI`, `PO`, `UI` |

Seed rows (verbatim from `SUNDAY_HOUSEKEEPING.md` 2):

| Dictionary | What it stores | Location |
|---|---|---|
| Hero Animation / Action map | every hero animation -> action; Right ActionBar (Attack + 3 named skills + clips); Hot-Swap bar actions + mappings | `docs/reference/HERO_ANIMATION_DICTIONARY.md` (+ dual-copy JSON if runtime-read) |
| Regression-coverage matrix | every audit finding -> covering regression suite, or "needs a new one" | `docs/reference/REGRESSION_COVERAGE_MATRIX.md` (= DB 2) |
| WO ledger | next-free number + every WO's real status (spec/READY/done/RESULT) | `CLI_LANES_WO_NUMBERS.md` banner (authority) (= DB 1) |
| Feature-flag registry | every `ff.*` flag + default + meaning | `Assets/_Modules/Core/FeatureFlags.cs` (source of truth) |
| Save-schema field map | every persisted field -> version added -> migrator step | `SaveSchema.cs` / `SaveMigrator.cs` |
| Regression-oracle inventory | every suite `[tag]` + what it locks + current known-red baseline | `SUNDAY_HOUSEKEEPING.md` / newest `CANON_GROUND_TRUTH` |

View: `Stale watch` - table sorted `Last refreshed` ascending (oldest = refresh first).

---

## DB 4 - Sunday Sweep Log (weekly ritual record)

One row per Sunday sweep (`SUNDAY_HOUSEKEEPING.md` 1). Makes each week's sweep a known,
confirmable record instead of a memory.

| Property | Type | Options / notes |
|---|---|---|
| Date | Title (or Date as Title) | the Sunday's date, `YYYY-MM-DD` |
| Findings | Number | total findings the fleet surfaced |
| New (missed-by-prior-audit) | Number | findings a prior audit should have caught |
| Uncovered | Number | findings with no covering regression (feeds DB 2 `Uncovered`) |
| Anchor minted | Rich text | the `CANON_GROUND_TRUTH_<date>.md` minted that sweep |
| Notes | Rich text | what's DONE / open / newly known; the Sunday report link |
| Findings link | Relation | -> Regression Coverage (rows created this sweep) |

View: `Timeline` - table sorted `Date` descending (latest sweep on top).

---

## Relations summary

- Work Orders <- Regression Coverage (`WO`): a finding can point at the WO that fixes it.
- Regression Coverage <- Sunday Sweep Log (`Findings link`): a sweep owns the rows it created.
- Known Dictionaries `Regression-coverage matrix` row and `WO ledger` row are the live
  mirrors of DB 2 and DB 1 respectively - keep their `Last refreshed` bumped each Sunday.
