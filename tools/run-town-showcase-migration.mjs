#!/usr/bin/env node
// =============================================================================
// RETIRED 2026-09-07 (WO-1505). THIS IS A SHIM. IT APPLIES NOTHING.
// -----------------------------------------------------------------------------
// It used to apply a HAND-KEPT pair - 20260829_0009_public_town_showcases.sql and
// 20260829_0011_public_town_snapshot_profile.sql (WO-1276) - SKIPPING 0010, which
// sits between them and which a different bespoke runner owned. That interleaving
// is the clearest picture of why nine runners could not be trusted between them to
// cover the directory: neither one applied the whole range it spanned.
//
// api/migrations/ is applied in filename order, whole, by:
//     node tools/run-migrations.mjs
// =============================================================================

console.error(
    'RUNNER_RETIRED: tools/run-town-showcase-migration.mjs applies nothing and reads nothing.\n' +
    '  It hardcoded migrations 0009 and 0011, skipping 0010 between them. A hand-kept\n' +
    '  list is the WO-1505 defect: a migration on disk that no array names is NEVER\n' +
    '  applied, and nothing reports it.\n' +
    '\n' +
    '  use run-migrations.mjs - one runner, derived from api/migrations/, ledger-recorded:\n' +
    '      node tools/run-migrations.mjs\n' +
    '\n' +
    '  READ THE HEADER OF tools/run-migrations.mjs FIRST. A database with no ledger yet\n' +
    '  needs a --baseline run before the ordinary one, and the header says exactly why.\n' +
    '  Nothing was connected to and nothing was changed by this invocation.');
process.exit(16);
