#!/usr/bin/env node
// WO-1277 additive/default-off contest schema rollout. Prints shape/counts only.
import { readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { Client } from '@neondatabase/serverless';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const migrationPaths = [
  join(root, 'api/migrations/20260829_0010_showcase_contests_votes_rewards.sql'),
  join(root, 'api/migrations/20260829_0012_showcase_category_voting_audit.sql'),
];
function fail(message) { console.error('SHOWCASE_CONTEST_MIGRATION_FAIL: ' + message); process.exit(16); }
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
  if (/^\s*(DROP\s+(TABLE|SCHEMA|DATABASE)|DELETE|TRUNCATE|ALTER\s+TABLE[^;]+DROP\s+(COLUMN|CONSTRAINT))\b/im.test(migration))
    fail('tracked migration contains a destructive data/schema statement; nothing was changed.');
}

const expected = new Map([
  ['public_town_snapshot_versions', 8],
  ['showcase_contests', 7],
  ['showcase_contest_candidates', 5],
  ['showcase_contest_votes', 4],
  ['showcase_contest_reward_tiers', 6],
  ['showcase_contest_categories', 9],
  ['showcase_contest_category_candidates', 8],
  ['showcase_contest_category_votes', 5],
  ['showcase_contest_category_reward_tiers', 9],
  ['showcase_contest_result_runs', 6],
  ['showcase_contest_result_rows', 5],
  ['showcase_contest_result_reversals', 4],
]);
const client = new Client(url);
client.on('error', error => fail('database connection failed: ' + (error?.message || 'unknown')));
await client.connect();
try {
  for (const migration of migrations) await client.query(migration);
  const proof = await client.query(`
    SELECT table_name, count(*)::int AS column_count
      FROM information_schema.columns
     WHERE table_schema='public' AND table_name = ANY($1::text[])
     GROUP BY table_name ORDER BY table_name`, [[...expected.keys()]]);
  const actual = new Map(proof.rows.map(row => [row.table_name, Number(row.column_count)]));
  for (const [table, columns] of expected) {
    if (actual.get(table) !== columns) fail(`post-migration shape proof failed for ${table}.`);
  }
  const state = await client.query(`
    SELECT
      (SELECT count(*)::int FROM showcase_contests) AS contests,
      (SELECT count(*)::int FROM showcase_contest_candidates) AS candidates,
      (SELECT count(*)::int FROM showcase_contest_votes) AS votes,
      (SELECT count(*)::int FROM showcase_contest_reward_tiers) AS tiers,
      (SELECT count(*)::int FROM showcase_contest_categories) AS categories,
      (SELECT count(*)::int FROM showcase_contest_category_votes) AS category_votes,
      (SELECT count(*)::int FROM showcase_contest_result_runs) AS results,
      (SELECT count(*)::int FROM showcase_contest_result_reversals) AS reversals`);
  const row = state.rows[0];
  console.log(`SHOWCASE_CONTEST_MIGRATION_OK tables=11 contests=${row.contests} candidates=${row.candidates} votes=${row.votes} tiers=${row.tiers} categories=${row.categories} category_votes=${row.category_votes} results=${row.results} reversals=${row.reversals} runtime=disabled`);
} finally { await client.end().catch(() => {}); }
