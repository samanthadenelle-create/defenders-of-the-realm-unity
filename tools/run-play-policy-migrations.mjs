#!/usr/bin/env node
// =============================================================================
// RETIRED 2026-09-07 (WO-1505). THIS IS A SHIM. IT APPLIES NOTHING.
// -----------------------------------------------------------------------------
// It used to apply a HAND-KEPT run of three - 20260830_0014_account_deletion_requests.sql,
// 20260830_0015_google_play_rtdn.sql and 20260830_0016_google_play_voided_reconciliation.sql -
// and prove them with per-table column counts.
//
// It also stopped exactly one file short of 20260830_0013_auth_sessions_identity_kind.sql,
// which no run-* runner ever named, and which therefore never reached the live
// database while the deployed issueSession INSERTed that column - 42703 on EVERY
// wallet session for a week (WO-1440 RESULT section 7c). That is this ticket in one
// sentence.
//
// api/migrations/ is applied in filename order, whole, by:
//     node tools/run-migrations.mjs
// =============================================================================

console.error(
    'RUNNER_RETIRED: tools/run-play-policy-migrations.mjs applies nothing and reads nothing.\n' +
    '  It hardcoded migrations 0014, 0015 and 0016 - and not 0013, which no runner named\n' +
    '  and which 500d every wallet session for a week. A hand-kept list is the WO-1505\n' +
    '  defect: a migration on disk that no array names is NEVER applied, and nothing\n' +
    '  reports it.\n' +
    '\n' +
    '  use run-migrations.mjs - one runner, derived from api/migrations/, ledger-recorded:\n' +
    '      node tools/run-migrations.mjs\n' +
    '\n' +
    '  READ THE HEADER OF tools/run-migrations.mjs FIRST. A database with no ledger yet\n' +
    '  needs a --baseline run before the ordinary one, and the header says exactly why.\n' +
    '  Nothing was connected to and nothing was changed by this invocation.');
process.exit(16);
