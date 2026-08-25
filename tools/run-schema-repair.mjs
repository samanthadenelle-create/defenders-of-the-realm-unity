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
// ORDER MATTERS, and 0002 exists because 0001 could not do its own job.
// purchase_quotes and purchase_entitlements ALREADY EXISTED, so 0001's
// CREATE TABLE IF NOT EXISTS skipped them whole - and skipped the CHECK
// constraints written inside those CREATEs with them. Re-running 0001 alone will
// never fix that: it is a no-op against an existing table, forever.
const MIGRATIONS = [
  join(repo, 'api', 'migrations', '20260824_0001_repair_schema_parity.sql'),
  join(repo, 'api', 'migrations', '20260825_0002_repair_parity_remainder.sql'),
];

function die(msg) { console.error('\nSCHEMA_REPAIR_FAIL: ' + msg); process.exit(16); }

const url = process.env.DATABASE_URL;
if (!url) {
  die('DATABASE_URL is not set.\n' +
      '  PowerShell:  $env:DATABASE_URL = \'<neon connection string>\'\n' +
      '  Then re-run: node tools/run-schema-repair.mjs\n' +
      '  (Vercel dashboard -> project env vars. It is deliberately not in the repo.)');
}

const loaded = [];
for (const m of MIGRATIONS) {
  try { loaded.push({ path: m, sql: readFileSync(m, 'utf8') }); }
  catch (e) { die('cannot read the migration at ' + m + ' - ' + e.message); }
}

// Refuse to run anything destructive, even though the file was audited clean.
// A guard that only trusts a past audit is not a guard.
for (const { path, sql: body } of loaded) {
  const destructive = body.split('\n').filter(l => /^\s*(DROP|DELETE|TRUNCATE)\b/i.test(l));
  if (destructive.length) {
    die(path + ' contains ' + destructive.length + ' destructive statement(s), and NOTHING was run:\n  ' +
        destructive.join('\n  '));
  }
}

console.log('[repair] migrations: ' + loaded.length + ', applied in order:');
for (const { path } of loaded) console.log('[repair]   - ' + path.split(/[\\/]/).pop());
console.log('[repair] statements: additive only (0 DROP/DELETE/TRUNCATE) - verified just now, not assumed');
console.log('[repair] applying...\n');

// ⛔ NOT `neon()`. That is the HTTP one-shot driver: it returns a tagged-template
// function with no .query(), and it CANNOT run a multi-statement transaction -
// this migration carries its own BEGIN/COMMIT and must arrive as one unit.
// `Client` speaks the real wire protocol over a WebSocket and honours it.
// A connection failure arrives ASYNCHRONOUSLY as an 'error' event on the client,
// not as a rejection from connect(). Without this listener Node treats it as an
// unhandled error and dumps a WebSocket stack trace over the top of any message
// we would have printed. Attach it BEFORE connecting.
const { Client } = await import('@neondatabase/serverless');
const client = new Client(url);
client.on('error', (e) => {
  // An ErrorEvent stringifies to "[object ErrorEvent]", which tells the reader
  // nothing. Dig for the real cause before giving up on it.
  const why = (e && (e.message || (e.error && e.error.message) || e.reason || e.type)) || String(e);
  die('the database connection failed: ' + why + '\n' +
      '  Most common cause: DATABASE_URL is not the real string. Check it does not still contain\n' +
      '  "..." from a redacted example, and that it is wrapped in SINGLE quotes so PowerShell does\n' +
      '  not split it at the & before channel_binding.\n' +
      '  Nothing was applied - the migration is transactional and never began.');
});

try {
  await client.connect();
  // Simple-query protocol: each file, BEGIN/COMMIT included, as one send.
  for (const { path, sql: body } of loaded) {
    await client.query(body);
    console.log('[repair] applied ' + path.split(/[\\/]/).pop());
  }
  console.log('[repair] all migrations executed without error.');
} catch (e) {
  await client.end().catch(() => {});
  die('the migration threw and NOTHING was committed (it is transactional):\n  ' + e.message);
}
await client.end().catch(() => {});

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
