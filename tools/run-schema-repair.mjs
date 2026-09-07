#!/usr/bin/env node
// =============================================================================
// RETIRED 2026-09-07 (WO-1505). THIS IS A SHIM. IT APPLIES NOTHING.
// -----------------------------------------------------------------------------
// It used to apply a HAND-KEPT list - 20260824_0001_repair_schema_parity.sql and
// 20260825_0002_repair_parity_remainder.sql - and then judge the result by
// tools/schema-parity.mjs / SCHEMA_PARITY_OK.
//
// The list is the defect. api/migrations/ held twenty-one files while this array
// held two, and four of those files (0003, 0004, 0017, 0018) were named by NO
// runner at all: on disk, never applied, and nothing said so. That is the same
// duplicated-state failure CLAUDE.md carries four scars from, and it is how
// auth_sessions.identity_kind sat unapplied for a week while the deployed
// issueSession INSERTed it (WO-1440 RESULT section 7c).
//
// tools/run-migrations.mjs derives the list from the directory, records a
// schema_migrations ledger row per file inside the same transaction as the file,
// and proves the run with a ledger shape query PLUS tools/wo1440-alter-column-sweep.mjs
// (ALTER_COLUMN_SWEEP_OK) - which, unlike schema-parity.mjs, can see an
// ALTER-added column at all.
//
// Kept as a file rather than deleted: several docs still name this path
// (CANON_GROUND_TRUTH_2026-09-06.md, KEY_FACTS.md, docs/GET_WELL_PLAN_2026-09-06.md,
// docs/READY_RCA_2026-09-06.md, WO-1446, WO-1441.RESULT). Whoever follows one of
// those lines must land on this refusal, not on a runner that silently applies a
// two-entry subset.
// =============================================================================

console.error(
    'RUNNER_RETIRED: tools/run-schema-repair.mjs applies nothing and reads nothing.\n' +
    '  It hardcoded migrations 0001 and 0002. A hand-kept list is the WO-1505 defect:\n' +
    '  a migration on disk that no array names is NEVER applied, and nothing reports it.\n' +
    '\n' +
    '  use run-migrations.mjs - one runner, derived from api/migrations/, ledger-recorded:\n' +
    '      node tools/run-migrations.mjs\n' +
    '\n' +
    '  READ THE HEADER OF tools/run-migrations.mjs FIRST. A database with no ledger yet\n' +
    '  needs a --baseline run before the ordinary one, and the header says exactly why\n' +
    '  and which files a re-run cannot survive.\n' +
    '  Nothing was connected to and nothing was changed by this invocation.');
process.exit(16);
