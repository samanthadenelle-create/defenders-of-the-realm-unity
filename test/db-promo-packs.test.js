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
  // WO-1440: the single-pack path now joins `packs` against a `claimed` CTE so the
  // global cap is claimed in the SAME statement as the insert (it used to be a bare
  // SELECT … FROM packs WHERE sku, with the cap enforced by a separate count that was
  // measured to over-issue). The pack authority is still the DB row, which is what
  // this test guards.
  assert.match(api, /FROM packs AS p, claimed AS c/);
  assert.match(api, /p\.active = TRUE/);
  assert.match(api, /jsonb_path_exists/);
  // WO-1440 (2026-09-06): every promo_redemptions INSERT now carries ip_hash (a guest
  // grant must be attributable after the fact) AND the single-pack path now claims the
  // ordinal atomically like the tiered one, so BOTH pack inserts write the same column
  // list. The property being guarded is unchanged: the pack snapshot is written in the
  // SAME statement that claims the ordinal.
  // Three grant paths now share it: tiered pack, single pack, and plain currency —
  // the last of which used to be a bare INSERT with no atomic claim at all.
  const atomicInserts = api.match(/pack_sku, contents, redemption_ordinal, ip_hash\)/g) || [];
  assert.equal(atomicInserts.length, 3, 'every grant path snapshots and claims in one statement');
  assert.match(api, /contents: grantedContents/);
  const executable = api.replace(/^\s*\/\/.*$/gm, '');
  assert.doesNotMatch(executable, /packs\.json|PackCatalog\.Find|readFileSync/);
});

test('client advertises inline capability and never falls back to PackCatalog.Find', () => {
  const client = read('Assets/_Modules/Core/Promo/PromoCodeService.cs');
  // Path corrected 2026-09-02: PackStoreVM.cs MOVED to _Modules/Commerce (commit 13770a912,
  // the Google Play storefront rail). This test kept the old _Modules/Wallet path and had been
  // failing ENOENT ever since -- a red suite that reads as "the assertion broke" when in fact
  // the file it asserts over was never being read at all.
  const vm = read('Assets/_Modules/Commerce/PackStoreVM.cs');
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
