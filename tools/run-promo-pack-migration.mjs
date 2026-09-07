#!/usr/bin/env node
// =============================================================================
// RETIRED 2026-09-07 (WO-1505). THIS IS A SHIM. IT APPLIES NOTHING.
// -----------------------------------------------------------------------------
// It used to apply a HAND-KEPT list - 20260828_0005_db_promo_packs.sql and
// 20260828_0006_db_promo_pack_fks.sql (WO-1258) - and, between the two, SEED the
// packs table from Assets/Resources/Data/Canonical/packs.json.
//
// The migrations now run through the one derived, ledger-recorded runner:
//     node tools/run-migrations.mjs
//
// SEEDING IS A SEPARATE CONCERN AND HAS A SEPARATE TOOL. tools/seed-promo-packs.mjs
// emits the same INSERT ... ON CONFLICT (sku) DO NOTHING statements as SQL, from the
// same canonical packs.json, and never connects to anything. That is deliberate: a
// migration runner that also writes catalog ROWS is two jobs in one file, and the row
// job is what made this runner's file list feel load-bearing. The dangling-FK
// pre-check this script ran is not lost either - 0006's ADD CONSTRAINT fails on
// dangling rows, and run-migrations.mjs rolls that file back with no ledger row.
// =============================================================================

console.error(
    'RUNNER_RETIRED: tools/run-promo-pack-migration.mjs applies nothing and reads nothing.\n' +
    '  It hardcoded migrations 0005 and 0006. A hand-kept list is the WO-1505 defect:\n' +
    '  a migration on disk that no array names is NEVER applied, and nothing reports it.\n' +
    '\n' +
    '  use run-migrations.mjs - one runner, derived from api/migrations/, ledger-recorded:\n' +
    '      node tools/run-migrations.mjs\n' +
    '  For the packs catalog seed, generate the SQL with:\n' +
    '      node tools/seed-promo-packs.mjs\n' +
    '\n' +
    '  READ THE HEADER OF tools/run-migrations.mjs FIRST. A database with no ledger yet\n' +
    '  needs a --baseline run before the ordinary one, and the header says exactly why.\n' +
    '  Nothing was connected to and nothing was changed by this invocation.');
process.exit(16);
