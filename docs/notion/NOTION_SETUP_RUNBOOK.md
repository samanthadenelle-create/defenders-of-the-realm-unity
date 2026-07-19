# Notion Setup Runbook - Defenders of the Realm / Echoes of Elarion

Purpose: stand up a BRAND-NEW Notion instance for the project so that the instant the
owner authenticates the Notion MCP server, setup is one import/paste away. Prepared
2026-07-19 while the Notion MCP was DISCONNECTED (auth is an interactive owner-only step).

Companion files in this folder:
- `notion-schema.md` - exact schema for all four databases (properties, options, views).
- `work-orders-seed.csv` - 665 rows, one per `WorkOrders/*.md`, git-reconciled status. Direct Notion import.

Grounded in: `NOTION_SOURCE_OF_TRUTH.md` (retired board ids), `CLI_LANES_WO_NUMBERS.md`
(numbering authority), CLAUDE.md 2 + 13 (WO protocol + QA->CLI->PO pipeline),
`SUNDAY_HOUSEKEEPING.md` (weekly ritual + known dictionaries).

---

## STEP 0 - OWNER ACTION (the ONE thing that unblocks everything)

The Notion MCP server is disconnected; no agent can create Notion content until it is
authenticated. This is interactive and owner-only.

> In Claude Code, run `/mcp`, select the Notion server, and complete the OAuth
> sign-in for the NEW Notion workspace. When it shows "connected", tell the CLI
> "Notion is connected" and it executes STEP 1 onward.

Nothing below can run until this is done. Everything below is prepared and waiting.

Note on the RETIRED board: the old workspace (data-source `5f66b263-c732-4075-b94a-f5f4de9f8087`,
board `f3115f05...`) is history - do NOT reconnect it. This runbook builds a fresh instance.

---

## STEP 1 - Create the home page

Create a top-level page: `Defenders of the Realm / Echoes of Elarion - Pipelines (Source of Truth)`.
All four databases live as sub-pages / inline DBs under it.

Add an intro callout on the page:
- Numbering authority stays in git (`CLI_LANES_WO_NUMBERS.md` banner); Notion is the board VIEW.
- Full WO specs + RESULTs stay in `WorkOrders/` in the repo.
- As of 2026-07-19: banner next-free = 749, but a `WORK_ORDER_749_dungeon_ingredient_sourcing.md`
  file now exists on disk -> effective next-free = 750 (flag for banner reconciliation on the next sweep).

---

## STEP 2 - Create DB 1: Work Orders, then import the seed CSV

1. Create an inline database `Work Orders` under the home page.
2. Add the properties exactly as in `notion-schema.md` DB 1 (Title, WO# number, Status
   select, Type select, Silo/Lane multi-select, Stage select, Owner select, Priority
   select, Spec url, RESULT url, Updated date, Notes text). Create the Select options
   listed there BEFORE import so import maps them cleanly.
3. Import `work-orders-seed.csv` (Notion: `...` menu -> Merge with CSV / Import).
   - Map CSV `WO#`->WO#, `Title`->Title, `Status`->Status, `Type`->Type,
     `Silo/Lane`->Silo/Lane, `Stage`->Stage, `Owner`->Owner, `SpecPath`->Spec,
     `ResultPath`->RESULT, `Notes`->Notes.
   - The CSV Status values already match the schema select options exactly
     (`Spec`, `Ready`, `In Implementation`, `Done`, `RESULT-filed`). `Closed` is set
     later by the PO.
4. Build the five views from `notion-schema.md` DB 1: By Status, By Silo, By Stage
   (pipeline), Live queue, Current arc.

Seed data reality (as enumerated + git-reconciled 2026-07-19):
- 665 rows total (the WorkOrders/ folder is a mostly-frozen archive; many pre-pivot
  WOs read "READY" in their spec header but are historical - see `WorkOrders/README.md`).
- Status distribution: RESULT-filed 65, Done 20, In Implementation 3, Ready 478, Spec 99.
- Current live arc (cite these; the rest of the folder is history):
  - WO-739 Ready (Enhancement Path obsidian upgrade panel; banner 2026-07-17b).
  - WO-740..743 + 745 Done/closed - Room Forge program, commit `f86e7f3f`
    ("Room Forge program RESULT + close (740-745 done)"); WO-744 COMPLETE `b337affe`.
  - WO-745 RESULT-filed (Room Forge regression + FlowTrace).
  - WO-746 RESULT-filed (Build-Mode BM-1/2/3 tickets; committed `9b0f27e0`).
  - WO-747 Done - gear curation -> runtime; committed `7c843ad3`. NOTE: the spec header
    still reads "IN IMPLEMENTATION" - the CSV reconciles it to Done via git. Reconcile the
    spec header on the next sweep.
  - WO-748 Ready - Founding "Default Town vs Build Your Own" choice; awaiting owner go.
  - WO-749 Ready - dungeon ingredient sourcing; awaiting owner go.

The Notion import inherits the CSV's Notes column, which carries each row's status
citation (spec Status line / commit / RESULT-filed), so provenance survives the import.

---

## STEP 3 - Create DB 2: Regression Coverage

1. Create inline DB `Regression Coverage`; add properties per `notion-schema.md` DB 2
   (Finding title, Silo, Severity P1/P2/P3, Covered? Yes/No/Soft, Covering Suite,
   Proposed Regression, Source file:line, Sweep Date, WO relation -> Work Orders).
2. Views: Uncovered (filter Covered? = No), By Silo, Soft coverage.
3. Seed source: the current `docs/reference/REGRESSION_COVERAGE_MATRIX.md` (KNOWN
   DICTIONARY). If that file exists, import/paste its matrix; otherwise leave empty and
   let the first Sunday sweep populate it (SUNDAY_HOUSEKEEPING.md step 4).

---

## STEP 4 - Create DB 3: Known Dictionaries

1. Create inline DB `Known Dictionaries`; properties per `notion-schema.md` DB 3
   (Dictionary title, What it stores, Location, Last refreshed, Owner).
2. Seed the six rows verbatim from `notion-schema.md` DB 3 / `SUNDAY_HOUSEKEEPING.md` 2
   (Hero Animation map, Regression-coverage matrix, WO ledger, Feature-flag registry,
   Save-schema field map, Regression-oracle inventory).
3. View: Stale watch (sort Last refreshed ascending).

---

## STEP 5 - Create DB 4: Sunday Sweep Log

1. Create inline DB `Sunday Sweep Log`; properties per `notion-schema.md` DB 4
   (Date, Findings, New, Uncovered, Anchor minted, Notes, Findings link -> Regression Coverage).
2. View: Timeline (sort Date descending).
3. First row is created by the first live Sunday sweep after Notion is connected.

---

## STEP 6 - Wire relations + verify

1. Confirm the relations: Regression Coverage.WO -> Work Orders; Sunday Sweep Log.Findings
   link -> Regression Coverage.
2. In Known Dictionaries, point the `WO ledger` row Location at DB 1 and the
   `Regression-coverage matrix` row at DB 2 (they are the live mirrors).
3. Smoke check: open the Work Orders "Current arc" view - it should show WO-739..749 with
   the statuses in STEP 2.

---

## STEP 7 - Keep it in sync (standing rules, per CLAUDE.md 2 / 13 / 15)

- Numbering authority stays `CLI_LANES_WO_NUMBERS.md` - mint from the banner, bump in the
  same edit. Notion never mints numbers.
- New WO: create `WorkOrders/WORK_ORDER_NNN_*.md` in git AND add a Work Orders row.
- Status flow: `Ready` when claimed -> `In Implementation` -> `Done` on commit ->
  `RESULT-filed` when `*.RESULT.md` lands -> `Closed` when the PO felt-verifies (PO closes, not CLI).
- Stage flow (ticket pipeline): `Queue` (PO) -> `QA` (read-only triage) -> `CLI`
  (implement + headless-verify) -> `PO` (felt-verify + close). Log every hand-off in Notes.
- Every Sunday: run the SUNDAY_HOUSEKEEPING sweep, then update DB 2 (coverage), DB 3
  (`Last refreshed` bumps), and add a DB 4 row. That is the guarantee "what is done" is
  always answerable from stored state.

---

## Re-running the seed generator (if WorkOrders/ changes)

`work-orders-seed.csv` was generated by enumerating `WorkOrders/*.md`, extracting each
spec's Status line + RESULT-file presence, normalizing to the schema's Status options,
and applying cited git-reconciliation overrides for the live arc (WO-740/747 etc.). To
regenerate after the folder changes, re-run the enumeration (Python 3, from `WorkOrders/`):
extract WO# from the filename, read the `**Status:**` line, mark RESULT-filed when a
matching `*.RESULT.md` exists, and override the current-arc rows against `git log`.
