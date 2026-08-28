const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const assert = require('node:assert/strict');

const root = path.resolve(__dirname, '..');
const read = (file) => fs.readFileSync(path.join(root, file), 'utf8');

test('catalog supports optional Pi without inventing prices', () => {
  const catalog = read('Assets/_Modules/Wallet/PackCatalog.cs');
  assert.match(catalog, /JsonProperty\("pi"\).*double\? Pi/);
  assert.match(catalog, /PiAmountLabel/);
  assert.match(catalog, /"Price unavailable"/);
  assert.doesNotMatch(catalog, /PiForUsd|UsdForPi|SkrForPi/);
});

test('store routes external channels without SKR fallthrough', () => {
  const store = read('Assets/_Modules/Wallet/PackStore.cs');
  assert.match(store, /PaymentChannel\.GooglePlay \|\| channel == PaymentChannel\.PiBrowser/);
  assert.match(store, /provider\.Channel != channel/);
  assert.match(store, /channel == PaymentChannel\.PiBrowser[\s\S]*pack\.PiAmountLabel/);
  assert.match(store, /if \(pack\.PromoGrantOnly\)/);
});

test('Welcome Packs remain hidden free grants in both mirrors', () => {
  const a = JSON.parse(read('Assets/StreamingAssets/Data/Canonical/packs.json'));
  const b = JSON.parse(read('Assets/Resources/Data/Canonical/packs.json'));
  assert.deepEqual(a, b);
  for (const sku of ['welcome-500', 'welcome-100']) {
    const pack = a.packs.find((row) => row.sku === sku);
    assert.ok(pack, `${sku} missing`);
    assert.equal(pack.storeVisible, false);
    assert.equal(pack.promoGrantOnly, true);
    assert.equal(pack.pricing, undefined);
  }
});
