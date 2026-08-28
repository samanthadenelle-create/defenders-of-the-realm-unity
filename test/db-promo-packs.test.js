const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const assert = require('node:assert/strict');

const root = path.resolve(__dirname, '..');
const read = (p) => fs.readFileSync(path.join(root, p), 'utf8');

test('promo packs are loaded and atomically snapshotted from Neon', () => {
  const api = read('api/promo/redeem.js');
  assert.match(api, /supportsInlinePackRewards/);
  assert.match(api, /FROM packs AS p/);
  assert.match(api, /FROM packs\s+WHERE sku/);
  assert.match(api, /p\.active = TRUE/);
  assert.match(api, /jsonb_path_exists/);
  assert.match(api, /pack_sku, contents, redemption_ordinal/);
  assert.match(api, /pack_sku, contents\)/);
  assert.match(api, /contents: grantedContents/);
  const executable = api.replace(/^\s*\/\/.*$/gm, '');
  assert.doesNotMatch(executable, /packs\.json|PackCatalog\.Find|readFileSync/);
});

test('client advertises inline capability and never falls back to PackCatalog.Find', () => {
  const client = read('Assets/_Modules/Core/Promo/PromoCodeService.cs');
  const vm = read('Assets/_Modules/Wallet/PackStoreVM.cs');
  assert.match(client, /supportsInlinePackRewards = true/);
  assert.match(client, /JObject Contents/);
  assert.match(client, /TryApplyInlinePack/);
  assert.doesNotMatch(client, /GetMethod\("Find"/);
  assert.match(vm, /ApplyPackContents\(string sku, PackContents contents\)/);
  assert.match(vm, /Contents = contents/);
});

test('schema, staged migrations, and one-shot seed preserve DB authority', () => {
  const schema = read('api/schema.sql');
  const create = read('api/migrations/20260828_0005_db_promo_packs.sql');
  const fks = read('api/migrations/20260828_0006_db_promo_pack_fks.sql');
  const seed = read('tools/seed-promo-packs.mjs');
  assert.match(schema, /CREATE TABLE IF NOT EXISTS packs/);
  assert.match(schema, /contents\s+JSONB/);
  assert.match(create, /ALTER TABLE promo_redemptions ADD COLUMN IF NOT EXISTS contents JSONB/);
  assert.match(fks, /ON DELETE RESTRICT/g);
  assert.match(seed, /ON CONFLICT \(sku\) DO NOTHING/);
  assert.doesNotMatch(seed, /DATABASE_URL|neon\(/);
});
