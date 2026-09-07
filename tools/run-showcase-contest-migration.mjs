#!/usr/bin/env node
// =============================================================================
// RETIRED 2026-09-07 (WO-1505). THIS IS A SHIM. IT APPLIES NOTHING.
// -----------------------------------------------------------------------------
// It used to apply a HAND-KEPT pair - 20260829_0010_showcase_contests_votes_rewards.sql
// and 20260829_0012_showcase_category_voting_audit.sql (WO-1277) - interleaved with
// tools/run-town-showcase-migration.mjs, which owned 0009 and 0011. Two runners,
// four files, one directory, and no single thing that knew the whole order.
//
// api/migrations/ is applied in filename order, whole, by:
//     node tools/run-migrations.mjs
// =============================================================================

console.error(
    'RUNNER_RETIRED: tools/run-showcase-contest-migration.mjs applies nothing and reads nothing.\n' +
    '  It hardcoded migrations 0010 and 0012, interleaved with another runner owning\n' +
    '  0009 and 0011. A hand-kept list is the WO-1505 defect: a migration on disk that\n' +
    '  no array names is NEVER applied, and nothing reports it.\n' +
    '\n' +
    '  use run-migrations.mjs - one runner, derived from api/migrations/, ledger-recorded:\n' +
    '      node tools/run-migrations.mjs\n' +
    '\n' +
    '  READ THE HEADER OF tools/run-migrations.mjs FIRST. A database with no ledger yet\n' +
    '  needs a --baseline run before the ordinary one, and the header says exactly why.\n' +
    '  Nothing was connected to and nothing was changed by this invocation.');
process.exit(16);
