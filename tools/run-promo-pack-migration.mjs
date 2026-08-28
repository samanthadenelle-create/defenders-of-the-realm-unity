#!/usr/bin/env node
// WO-1258 additive Neon rollout: table/column -> seed -> foreign keys -> prove shape.
// Never logs DATABASE_URL, promo codes, player ids, or pack contents.
import { readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { Client } from '@neondatabase/serverless';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
function fail(message) { console.error('PROMO_PACK_MIGRATION_FAIL: ' + message); process.exit(16); }

function resolveDatabaseUrl() {
  if (process.env.DATABASE_URL) return process.env.DATABASE_URL;
  try {
    const env = readFileSync(join(root, '.env.local'), 'utf8');
    const match = env.match(/^\s*DATABASE_URL\s*=\s*(.*)$/m);
    if (!match) return '';
    let value = match[1].trim();
    if ((value.startsWith('"') && value.endsWith('"')) ||
        (value.startsWith("'") && value.endsWith("'"))) value = value.slice(1, -1);
    return value;
  } catch { return ''; }
}

const url = resolveDatabaseUrl();
if (!url) fail('DATABASE_URL is unavailable; nothing was changed.');

const migration1 = readFileSync(join(root, 'api/migrations/20260828_0005_db_promo_packs.sql'), 'utf8');
const migration2 = readFileSync(join(root, 'api/migrations/20260828_0006_db_promo_pack_fks.sql'), 'utf8');
for (const body of [migration1, migration2]) {
  if (/^\s*(DROP|DELETE|TRUNCATE)\b/im.test(body)) fail('tracked migration contains a destructive statement; nothing was changed.');
}
const catalog = JSON.parse(readFileSync(join(root, 'Assets/Resources/Data/Canonical/packs.json'), 'utf8'));
if (!Array.isArray(catalog.packs) || catalog.packs.length === 0) fail('canonical pack seed is empty; nothing was changed.');

const client = new Client(url);
client.on('error', e => fail('database connection failed: ' + (e?.message || e?.type || 'unknown')));
await client.connect();
try {
  await client.query(migration1);
  console.log('PROMO_PACK_STAGE_1_OK table + snapshot column present');

  await client.query('BEGIN');
  try {
    for (const pack of catalog.packs) {
      if (!pack?.sku || !pack?.name || !pack?.contents) throw new Error('canonical pack is missing sku/name/contents');
      await client.query(
        `INSERT INTO packs (sku,name,contents,active,store_visible)
         VALUES ($1,$2,$3::jsonb,TRUE,$4)
         ON CONFLICT (sku) DO NOTHING`,
        [String(pack.sku), String(pack.name), JSON.stringify(pack.contents), pack.storeVisible === true]);
    }
    await client.query('COMMIT');
  } catch (error) {
    await client.query('ROLLBACK');
    throw error;
  }

  const dangling = await client.query(`
    SELECT count(*)::int AS n
      FROM promo_codes pc
     WHERE (pc.reward_pack_sku IS NOT NULL AND NOT EXISTS (SELECT 1 FROM packs p WHERE p.sku=pc.reward_pack_sku))
        OR (pc.tier1_pack_sku IS NOT NULL AND NOT EXISTS (SELECT 1 FROM packs p WHERE p.sku=pc.tier1_pack_sku))
        OR (pc.tier2_pack_sku IS NOT NULL AND NOT EXISTS (SELECT 1 FROM packs p WHERE p.sku=pc.tier2_pack_sku))`);
  if (Number(dangling.rows[0]?.n) !== 0) fail('existing promo rows reference unknown pack SKUs; FK stage was not applied.');

  await client.query(migration2);
  const shape = await client.query(`
    SELECT
      to_regclass('public.packs') IS NOT NULL AS has_packs,
      EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='promo_redemptions' AND column_name='contents') AS has_contents,
      (SELECT count(*)::int FROM packs) AS pack_count,
      (SELECT count(*)::int FROM pg_constraint WHERE conname IN
        ('promo_codes_reward_pack_sku_fk','promo_codes_tier1_pack_sku_fk','promo_codes_tier2_pack_sku_fk')) AS fk_count`);
  const row = shape.rows[0];
  if (!row?.has_packs || !row?.has_contents || Number(row?.pack_count) < catalog.packs.length || Number(row?.fk_count) !== 3)
    fail('post-migration shape proof failed.');
  console.log(`PROMO_PACK_MIGRATION_OK packs=${row.pack_count} foreignKeys=${row.fk_count} (promo rows unchanged)`);
} finally {
  await client.end().catch(() => {});
}
