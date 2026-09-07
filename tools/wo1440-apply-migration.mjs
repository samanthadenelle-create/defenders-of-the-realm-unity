#!/usr/bin/env node
// =============================================================================
// RETIRED 2026-09-07 (WO-1505, second sweep). THIS IS A SHIM. IT APPLIES NOTHING.
// -----------------------------------------------------------------------------
// It used to read .env.local for DATABASE_URL, then apply ONE hardcoded file -
// api/migrations/20260906_0019_promo_guest_redeem_ip_budget.sql - by stripping the
// comments, naive-splitting on ';', and firing each fragment at live Neon. It then
// printed MIGRATION_0019_OK / MIGRATION_0019_FAILED off a shape query.
//
// The hardcoded filename is the defect, and it is the SAME defect the seven
// run-*.mjs runners were retired for on 2026-09-07 - this file simply was not
// spelled `run-` so the sweep's glob walked past it. A per-file applier records
// NOTHING in the ledger, so tools/run-migrations.mjs cannot tell that 0019 was
// applied and the next owner must remember it by hand; and a migration on disk that
// no path names is NEVER applied while nothing reports it. That is how
// auth_sessions.identity_kind sat unapplied for a week while the deployed
// issueSession INSERTed it (WO-1440 RESULT section 7c).
//
// The naive `;` split was a second hazard on its own terms: it is only safe while the
// target file happens to contain no dollar-quoted body, which is a property of ONE
// migration that the script asserted in a comment and could not enforce.
//
// tools/run-migrations.mjs derives the list from api/migrations/ itself, runs the
// additive-only audit before anything applies, records a schema_migrations ledger row
// per file INSIDE the same transaction as the file, and proves the run with a ledger
// shape query PLUS tools/wo1440-alter-column-sweep.mjs (ALTER_COLUMN_SWEEP_OK).
//
// Kept as a file rather than deleted: WO-1440's RESULT and proof notes name this path,
// and whoever follows one of those lines must land on this refusal rather than on an
// applier that silently re-runs a single 2026-09-06 file outside the ledger.
// =============================================================================

console.error(
    'RUNNER_RETIRED: tools/wo1440-apply-migration.mjs applies nothing and reads nothing.\n' +
    '  It hardcoded ONE migration (20260906_0019) and took the connection string from a\n' +
    '  local env file.\n' +
    '  A hand-named file is the WO-1505 defect: it writes no ledger row, so the one runner\n' +
    '  cannot tell the file was applied, and any migration no path names is never applied\n' +
    '  at all while nothing reports it.\n' +
    '\n' +
    '  use run-migrations.mjs - one runner, derived from api/migrations/, ledger-recorded:\n' +
    '      node tools/run-migrations.mjs\n' +
    '\n' +
    '  READ THE HEADER OF tools/run-migrations.mjs FIRST. A database with no ledger yet\n' +
    '  needs a --baseline run before the ordinary one, and the header says exactly why\n' +
    '  and which files a re-run cannot survive.\n' +
    '  Nothing was connected to and nothing was changed by this invocation.');
process.exit(16);
