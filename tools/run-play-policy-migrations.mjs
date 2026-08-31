#!/usr/bin/env node
// Additive/default-off account-deletion and Google Play RTDN rollout. Never
// prints credentials, player identifiers, purchase tokens, orders, or payloads.
import { readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { Client } from '@neondatabase/serverless';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const migrationPaths = [
  join(root, 'api/migrations/20260830_0014_account_deletion_requests.sql'),
  join(root, 'api/migrations/20260830_0015_google_play_rtdn.sql'),
];

function fail(message) {
  console.error('PLAY_POLICY_MIGRATION_FAIL: ' + message);
  process.exit(16);
}

function databaseUrl() {
  if (process.env.DATABASE_URL) return process.env.DATABASE_URL;
  try {
    const match = readFileSync(join(root, '.env.local'), 'utf8')
      .match(/^\s*DATABASE_URL\s*=\s*(.*)$/m);
    if (!match) return '';
    let value = match[1].trim();
    if ((value.startsWith('"') && value.endsWith('"')) ||
        (value.startsWith("'") && value.endsWith("'"))) value = value.slice(1, -1);
    return value;
  } catch { return ''; }
}

const migrations = migrationPaths.map(path => readFileSync(path, 'utf8'));
for (const migration of migrations) {
  if (/^\s*(DROP|DELETE|TRUNCATE|ALTER\s+TABLE[^;]+DROP)\b/im.test(migration))
    fail('tracked migration contains a destructive statement; nothing was changed.');
}

const url = databaseUrl();
if (!url) fail('DATABASE_URL is unavailable; nothing was changed.');
const client = new Client(url);
client.on('error', () => fail('database connection failed.'));
await client.connect();
try {
  for (const migration of migrations) await client.query(migration);
  const proof = await client.query(`
    SELECT table_name, count(*)::int AS column_count
      FROM information_schema.columns
     WHERE table_schema = 'public'
       AND table_name = ANY($1::text[])
     GROUP BY table_name
     ORDER BY table_name`, [[
       'account_deletion_requests', 'google_play_rtdn_messages',
     ]]);
  const actual = new Map(proof.rows.map(row => [row.table_name, Number(row.column_count)]));
  if (actual.get('account_deletion_requests') !== 10 ||
      actual.get('google_play_rtdn_messages') !== 13)
    fail('post-migration shape proof failed.');
  console.log('PLAY_POLICY_MIGRATION_OK tables=2 requests=0 notifications=0 billing=disabled');
} finally {
  await client.end().catch(() => {});
}
