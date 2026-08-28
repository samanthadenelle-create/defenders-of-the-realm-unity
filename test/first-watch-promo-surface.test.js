const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const assert = require('node:assert/strict');

const root = path.resolve(__dirname, '..');
const read = (file) => fs.readFileSync(path.join(root, file), 'utf8');

test('First Watch packs are hidden and carry the owner-ruled baskets', () => {
  const streaming = JSON.parse(read('Assets/StreamingAssets/Data/Canonical/packs.json'));
  const resources = JSON.parse(read('Assets/Resources/Data/Canonical/packs.json'));
  assert.deepEqual(streaming, resources, 'both runtime catalog mirrors must be identical');

  const expected = {
    'welcome-500': { wood: 500, iron: 500, stone: 500, crystals: 500, coins: 500 },
    'welcome-100': { wood: 100, iron: 100, stone: 100, crystals: 100, coins: 100 },
  };
  for (const [sku, economy] of Object.entries(expected)) {
    const pack = streaming.packs.find((row) => row.sku === sku);
    assert.ok(pack, `${sku} missing`);
    assert.equal(pack.storeVisible, false);
    assert.deepEqual(pack.contents.economy, economy);
    assert.deepEqual(pack.contents.cosmetics, []);
    assert.deepEqual(pack.contents.convenience, []);
  }
});

test('FIRSTWATCH live tier is pack-free while pack tiers remain supported for the next APK', () => {
  const api = read('api/promo/redeem.js');
  const migration = read('api/migrations/20260828_0004_promo_reward_tiers.sql');
  assert.match(api, /SET redemption_count = redemption_count \+ 1/);
  assert.match(api, /CASE WHEN redemption_count <= tier1_limit/);
  assert.match(api, /INSERT INTO promo_redemptions[\s\S]*pack_sku/);
  assert.match(api, /supportsPackRewards/);
  assert.match(api, /tier2_reward_crystals/);
  assert.match(migration, /'2026-08-31T04:59:00Z'/);
  assert.match(migration, /'FIRSTWATCH', 500, 500/);
  assert.match(migration, /NULL, 500, NULL, 100, 100/);
  assert.match(migration, /max_redemptions,[\s\S]*NULL,/);
  assert.match(migration, /SET active = FALSE[\s\S]*code = 'TEST10'/);
});

test('welcome letter is one-shot, waits for a safe gameplay moment, and never exposes the code', () => {
  const letter = read('Assets/_Modules/Onboarding/FirstWatchWelcomeLetter.cs');
  assert.match(letter, /state\.Onboarded/);
  assert.match(letter, /!PanelManager\.AnyOpen/);
  assert.match(letter, /!BattleLock\.IsInBattle\(\)/);
  assert.match(letter, /PlayerPrefs\.SetInt\(SeenKey, 1\)/);
  assert.doesNotMatch(letter, /FIRSTWATCH/);
});
