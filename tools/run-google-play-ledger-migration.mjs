#!/usr/bin/env node
// =============================================================================
// RETIRED 2026-09-07 (WO-1505). THIS IS A SHIM. IT APPLIES NOTHING.
// -----------------------------------------------------------------------------
// It used to apply one hardcoded file - 20260828_0007_google_play_purchase_state.sql
// (WO-1255) - and prove it with a column/index count against google_play_purchases.
//
// The apply now runs through the one derived, ledger-recorded runner. The shape
// proof it did by hand is not the loss it looks like: run-migrations.mjs proves
// every run with a ledger shape query plus tools/wo1440-alter-column-sweep.mjs,
// which checks EVERY column named by api/schema.sql and api/migrations/ against the
// live database rather than the four tables one feature happened to care about.
// =============================================================================

console.error(
    'RUNNER_RETIRED: tools/run-google-play-ledger-migration.mjs applies nothing and reads nothing.\n' +
    '  It hardcoded migration 0007. A hand-kept list is the WO-1505 defect:\n' +
    '  a migration on disk that no array names is NEVER applied, and nothing reports it.\n' +
    '\n' +
    '  use run-migrations.mjs - one runner, derived from api/migrations/, ledger-recorded:\n' +
    '      node tools/run-migrations.mjs\n' +
    '\n' +
    '  READ THE HEADER OF tools/run-migrations.mjs FIRST. A database with no ledger yet\n' +
    '  needs a --baseline run before the ordinary one, and the header says exactly why.\n' +
    '  Nothing was connected to and nothing was changed by this invocation.');
process.exit(16);
