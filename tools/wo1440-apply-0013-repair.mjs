#!/usr/bin/env node
// =============================================================================
// RETIRED 2026-09-07 (WO-1505, second sweep). THIS IS A SHIM. IT APPLIES NOTHING.
// -----------------------------------------------------------------------------
// It used to read .env.local for DATABASE_URL and fire ONE hand-typed statement at
// live Neon -
//
//     ALTER TABLE auth_sessions ADD COLUMN IF NOT EXISTS identity_kind TEXT
//         NOT NULL DEFAULT 'wallet'
//
// - then verify by shape query AND by running issueSession's exact INSERT shape
// against a probe row it deleted afterwards. That success-path proof was the right
// instinct and it is NOT what is being retired here (memory:
// prove-the-success-path-not-just-the-refusal); tools/wo1440-alter-column-sweep.mjs
// now carries that role for every ALTER-added column at once.
//
// WHAT IS WRONG IS THE STATEMENT BEING TYPED HERE AT ALL. The authored copy lives in
// api/migrations/20260830_0013_auth_sessions_identity_kind.sql, and a hand-retyped
// second copy of a DDL statement is duplicated state: the two can drift, and only the
// file under api/migrations/ is the one a ledger can record. This applier wrote no
// ledger row, so after it ran, tools/run-migrations.mjs still could not tell whether
// 0013 was on the database. Same defect as the seven run-*.mjs runners retired on
// 2026-09-07 - this file just was not spelled `run-`, so that sweep's glob walked
// past it.
//
// It is also finished on its own terms: the emergency it existed for is over. 0013 was
// applied to production on 2026-09-06 and the wallet rail was proven end to end
// (WO-1440 RESULT section 7c). Re-running this would be a no-op that reports success.
//
// tools/run-migrations.mjs derives the list from api/migrations/, audits it for
// destructive statements before anything applies, records a schema_migrations ledger
// row per file inside the same transaction as the file, and proves the run with a
// ledger shape query PLUS tools/wo1440-alter-column-sweep.mjs (ALTER_COLUMN_SWEEP_OK)
// - which, over every migration at once, is the generalisation of the probe this file
// did for one column.
//
// Kept as a file rather than deleted: WO-1440's RESULT and proof notes name this path,
// and whoever follows one of those lines must land on this refusal rather than on an
// applier that writes DDL no ledger will ever record.
// =============================================================================

console.error(
    'RUNNER_RETIRED: tools/wo1440-apply-0013-repair.mjs applies nothing and reads nothing.\n' +
    '  It hand-retyped the ALTER from 20260830_0013 and took the connection string from a\n' +
    '  local env file.\n' +
    '  A second copy of a DDL statement is the WO-1505 defect: it can drift from the file,\n' +
    '  and it writes no ledger row, so the one runner cannot tell 0013 was ever applied.\n' +
    '  The emergency is also over - 0013 landed on prod 2026-09-06 (WO-1440 RESULT 7c).\n' +
    '\n' +
    '  use run-migrations.mjs - one runner, derived from api/migrations/, ledger-recorded:\n' +
    '      node tools/run-migrations.mjs\n' +
    '\n' +
    '  To prove a column is really on the live database, run the sweep, which covers\n' +
    '  every ALTER-added column instead of one:\n' +
    '      node tools/wo1440-alter-column-sweep.mjs      (ALTER_COLUMN_SWEEP_OK)\n' +
    '\n' +
    '  READ THE HEADER OF tools/run-migrations.mjs FIRST. A database with no ledger yet\n' +
    '  needs a --baseline run before the ordinary one.\n' +
    '  Nothing was connected to and nothing was changed by this invocation.');
process.exit(16);
