#!/usr/bin/env node
// =============================================================================
// RETIRED 2026-09-07 (WO-1505). THIS IS A SHIM. IT APPLIES NOTHING.
// -----------------------------------------------------------------------------
// It used to apply one hardcoded file -
// 20260828_0008_card_collections_reward_entitlements.sql (WO-1272) - and prove it
// with per-table column counts for catalog_items, catalog_collections,
// catalog_collection_items and sku_entitlements.
//
// The apply now runs through the one derived, ledger-recorded runner, whose proof
// (ledger shape query + tools/wo1440-alter-column-sweep.mjs) covers every column
// named by api/schema.sql and api/migrations/, not four hand-picked tables.
// =============================================================================

console.error(
    'RUNNER_RETIRED: tools/run-card-collection-migration.mjs applies nothing and reads nothing.\n' +
    '  It hardcoded migration 0008. A hand-kept list is the WO-1505 defect:\n' +
    '  a migration on disk that no array names is NEVER applied, and nothing reports it.\n' +
    '\n' +
    '  use run-migrations.mjs - one runner, derived from api/migrations/, ledger-recorded:\n' +
    '      node tools/run-migrations.mjs\n' +
    '\n' +
    '  READ THE HEADER OF tools/run-migrations.mjs FIRST. A database with no ledger yet\n' +
    '  needs a --baseline run before the ordinary one, and the header says exactly why.\n' +
    '  Nothing was connected to and nothing was changed by this invocation.');
process.exit(16);
