#!/usr/bin/env node
// WO-1255 additive/default-off ledger rollout. Never prints database credentials,
// purchase tokens, player ids, orders, or product data.
import { readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { Client } from '@neondatabase/serverless';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const migrationPath = join(root, 'api/migrations/20260828_0007_google_play_purchase_state.sql');
function fail(message) { console.error('GOOGLE_PLAY_LEDGER_MIGRATION_FAIL: ' + message); process.exit(16); }
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

const url = databaseUrl();
if (!url) fail('DATABASE_URL is unavailable; nothing was changed.');
const migration = readFileSync(migrationPath, 'utf8');
if (/^\s*(DROP|DELETE|TRUNCATE|ALTER\s+TABLE[^;]+DROP)\b/im.test(migration))
  fail('tracked migration contains a destructive statement; nothing was changed.');

const client = new Client(url);
client.on('error', error => fail('database connection failed: ' + (error?.message || 'unknown')));
await client.connect();
try {
  await client.query(migration);
  const proof = await client.query(`
    SELECT
      to_regclass('public.google_play_purchases') IS NOT NULL AS has_table,
      (SELECT count(*)::int FROM information_schema.columns
        WHERE table_schema='public' AND table_name='google_play_purchases') AS column_count,
      (SELECT count(*)::int FROM pg_indexes
        WHERE schemaname='public' AND tablename='google_play_purchases') AS index_count,
      (SELECT count(*)::int FROM google_play_purchases) AS row_count`);
  const row = proof.rows[0];
  if (!row?.has_table || Number(row.column_count) !== 16 || Number(row.index_count) < 3)
    fail('post-migration shape proof failed.');
  console.log(`GOOGLE_PLAY_LEDGER_MIGRATION_OK columns=${row.column_count} indexes=${row.index_count} rows=${row.row_count} rail=disabled`);
} finally {
  await client.end().catch(() => {});
}
