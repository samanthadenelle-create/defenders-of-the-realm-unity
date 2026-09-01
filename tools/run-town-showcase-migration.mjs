#!/usr/bin/env node
// WO-1276 additive/default-unpublished showcase rollout. Never prints database
// credentials, wallets, public ids, snapshots, or player data.
import { readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { Client } from '@neondatabase/serverless';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const migrationPaths = [
  join(root, 'api/migrations/20260829_0009_public_town_showcases.sql'),
  join(root, 'api/migrations/20260829_0011_public_town_snapshot_profile.sql'),
];
function fail(message) { console.error('TOWN_SHOWCASE_MIGRATION_FAIL: ' + message); process.exit(16); }
function databaseUrl() {
  if (process.env.DATABASE_URL) return process.env.DATABASE_URL;
  try {
    const match = readFileSync(join(root, '.env.local'), 'utf8').match(/^\s*DATABASE_URL\s*=\s*(.*)$/m);
    if (!match) return '';
    let value = match[1].trim();
    if ((value.startsWith('"') && value.endsWith('"')) ||
        (value.startsWith("'") && value.endsWith("'"))) value = value.slice(1, -1);
    return value;
  } catch { return ''; }
}

const url = databaseUrl();
if (!url) fail('DATABASE_URL is unavailable; nothing was changed.');
const migrations = migrationPaths.map(path => readFileSync(path, 'utf8'));
for (const migration of migrations) {
  // Constraint replacement in 0011 widens schema_version from v1 to v1/v2 and
  // is intentionally allowed; destructive data/schema operations remain barred.
  if (/^\s*(DROP\s+(TABLE|SCHEMA|DATABASE)|DELETE|TRUNCATE|ALTER\s+TABLE[^;]+DROP\s+COLUMN)\b/im.test(migration))
    fail('tracked migration contains a destructive data/schema statement; nothing was changed.');
}

const expected = new Map([
  ['public_town_showcases', 7],
  // 0009 creates 7 columns, 0010 adds the contest provenance column, and
  // 0011 adds the ten bounded public-profile fields.
  ['public_town_snapshot_versions', 18],
]);
const client = new Client(url);
client.on('error', error => fail('database connection failed: ' + (error?.message || 'unknown')));
await client.connect();
try {
  for (const migration of migrations) await client.query(migration);
  const proof = await client.query(`
    SELECT table_name, count(*)::int AS column_count
      FROM information_schema.columns
     WHERE table_schema = 'public' AND table_name = ANY($1::text[])
     GROUP BY table_name ORDER BY table_name`, [[...expected.keys()]]);
  const actual = new Map(proof.rows.map(row => [row.table_name, Number(row.column_count)]));
  for (const [table, columns] of expected) {
    if (actual.get(table) !== columns) fail(`post-migration shape proof failed for ${table}.`);
  }
  const state = await client.query(`
    SELECT
      (SELECT count(*)::int FROM public_town_showcases) AS directory_rows,
      (SELECT count(*)::int FROM public_town_showcases WHERE published) AS published_rows,
      (SELECT count(*)::int FROM public_town_snapshot_versions) AS snapshot_rows`);
  const row = state.rows[0];
  console.log(`TOWN_SHOWCASE_MIGRATION_OK tables=2 schema=v2 directory_rows=${row.directory_rows} published_rows=${row.published_rows} snapshot_rows=${row.snapshot_rows}`);
} finally {
  await client.end().catch(() => {});
}
