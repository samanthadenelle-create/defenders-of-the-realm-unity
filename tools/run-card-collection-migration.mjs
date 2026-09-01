#!/usr/bin/env node
// WO-1272 additive catalog/collection/reward-entitlement rollout. Never prints
// database credentials, player identifiers, SKU values, or entitlement data.
import { readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { Client } from '@neondatabase/serverless';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const migrationPath = join(root, 'api/migrations/20260828_0008_card_collections_reward_entitlements.sql');
function fail(message) { console.error('CARD_COLLECTION_MIGRATION_FAIL: ' + message); process.exit(16); }
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

const expectedColumns = new Map([
  ['catalog_items', 15],
  ['catalog_collections', 15],
  ['catalog_collection_items', 5],
  ['sku_entitlements', 14],
]);

const client = new Client(url);
client.on('error', error => fail('database connection failed: ' + (error?.message || 'unknown')));
await client.connect();
try {
  await client.query(migration);
  const proof = await client.query(`
    SELECT table_name, count(*)::int AS column_count
      FROM information_schema.columns
     WHERE table_schema = 'public'
       AND table_name = ANY($1::text[])
     GROUP BY table_name
     ORDER BY table_name`, [[...expectedColumns.keys()]]);
  const actual = new Map(proof.rows.map(row => [row.table_name, Number(row.column_count)]));
  for (const [table, columns] of expectedColumns) {
    if (actual.get(table) !== columns) fail(`post-migration shape proof failed for ${table}.`);
  }
  console.log('CARD_COLLECTION_MIGRATION_OK tables=4 seeds=0 grants=0');
} finally {
  await client.end().catch(() => {});
}
