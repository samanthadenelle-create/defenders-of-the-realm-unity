// =============================================================================
// run-schema-repair.mjs - apply the tracked schema-parity repair, then PROVE it.
// -----------------------------------------------------------------------------
// Authorized by the owner, 2026-08-25 (FOUNDATIONAL_RULINGS.md section 9).
//
//   node tools/run-schema-repair.mjs
//
// DATABASE_URL must be in the environment. It is redacted for every agent seat,
// so this script is written to be run BY THE OWNER and by nobody else.
//
//   PowerShell:  $env:DATABASE_URL = '<the neon connection string>'
//                node tools/run-schema-repair.mjs
//
// ---------------------------------------------------------------------------
// WHY THIS EXISTS RATHER THAN A psql ONE-LINER: psql is not on this machine.
// node and @neondatabase/serverless already are, so this uses what is here.
//
// STOP THE EXIT CODE IS NOT THE PROOF, AND THIS DATABASE HAS ALREADY TAUGHT US WHY.
// `CREATE TABLE IF NOT EXISTS` against a table that exists with the WRONG SHAPE
// reports success and changes NOTHING. The bug_reports repair reported success
// three times while doing nothing at all. So this script does not stop at
// "migration ran" - it runs the shape query afterwards and judges by the
// SCHEMA_PARITY_OK marker, exactly as every gate in this repo is judged.
//
// It is additive-only by audit: zero DROP, DELETE or TRUNCATE, wrapped in
// BEGIN/COMMIT. It never deletes application rows.
// =============================================================================

import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
const repo = join(here, '..');
const MIGRATION = join(repo, 'api', 'migrations', '20260824_0001_repair_schema_parity.sql');

function die(msg) { console.error('\nSCHEMA_REPAIR_FAIL: ' + msg); process.exit(16); }

const url = process.env.DATABASE_URL;
if (!url) {
  die('DATABASE_URL is not set.\n' +
      '  PowerShell:  $env:DATABASE_URL = \'<neon connection string>\'\n' +
      '  Then re-run: node tools/run-schema-repair.mjs\n' +
      '  (Vercel dashboard -> project env vars. It is deliberately not in the repo.)');
}

let sql;
try { sql = readFileSync(MIGRATION, 'utf8'); }
catch (e) { die('cannot read the migration at ' + MIGRATION + ' - ' + e.message); }

// Refuse to run anything destructive, even though the file was audited clean.
// A guard that only trusts a past audit is not a guard.
const destructive = sql.split('\n').filter(l => /^\s*(DROP|DELETE|TRUNCATE)\b/i.test(l));
if (destructive.length) {
  die('the migration contains ' + destructive.length + ' destructive statement(s) and was NOT run:\n  ' +
      destructive.join('\n  '));
}

console.log('[repair] migration : ' + MIGRATION);
console.log('[repair] statements: additive only (0 DROP/DELETE/TRUNCATE) - verified just now, not assumed');
console.log('[repair] applying...\n');

const { neon } = await import('@neondatabase/serverless');
const db = neon(url);

try {
  // The file carries its own BEGIN/COMMIT, so hand it over whole.
  await db.query(sql);
  console.log('[repair] migration executed without error.');
} catch (e) {
  die('the migration threw and NOTHING was committed (it is transactional):\n  ' + e.message);
}

console.log('\n[repair] ---------------------------------------------------------');
console.log('[repair] THE MIGRATION RUNNING IS NOT THE PROOF. Running the shape query.');
console.log('[repair] ---------------------------------------------------------\n');

const parity = spawnSync(process.execPath, [join(repo, 'tools', 'schema-parity.mjs')], {
  cwd: repo, env: process.env, encoding: 'utf8',
});

const out = (parity.stdout || '') + (parity.stderr || '');
process.stdout.write(out);

if (/^SCHEMA_PARITY_OK/m.test(out)) {
  console.log('\nSCHEMA_REPAIR_OK - the migration applied AND the shape query agrees.');
  process.exit(0);
}

die('the migration ran but SCHEMA_PARITY_OK did NOT appear.\n' +
    '  That is the wrong-shape case: an IF NOT EXISTS statement reported success and changed nothing,\n' +
    '  OR a drift remains that this migration does not cover. Read the parity output above - it names\n' +
    '  the table and column. Do NOT re-run this script hoping for a different answer.');
