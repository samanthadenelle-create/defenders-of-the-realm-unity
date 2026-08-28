const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const assert = require('node:assert/strict');

const root = path.resolve(__dirname, '..');
const read = (relative) => fs.readFileSync(path.join(root, relative), 'utf8');

test('Google Play catalog maps every canonical pack exactly once', () => {
  const packs = JSON.parse(read('Assets/StreamingAssets/Data/Canonical/packs.json')).packs;
  const catalog = read('Assets/_Modules/Core/Payments/Providers/GooglePlay/GooglePlayProductCatalog.cs');
  const arrayBody = catalog.match(/private static readonly string\[\] s_skus =\s*\{([\s\S]*?)\};/)[1];
  const mapped = [...arrayBody.matchAll(/"([a-z0-9-]+)"/g)].map((match) => match[1]);

  assert.deepEqual(mapped.sort(), packs.map((pack) => pack.sku).sort());
  assert.equal(new Set(mapped).size, mapped.length);
  assert.match(catalog, /ProductType\.Consumable/);
});

test('Google Play rail uses store-localized prices and server-gated confirmation', () => {
  const provider = read('Assets/_Modules/Core/Payments/Providers/GooglePlay/GooglePlayBillingProvider.cs');

  assert.match(provider, /metadata\.localizedPriceString/);
  assert.match(provider, /metadata\.isoCurrencyCode/);
  assert.match(provider, /VerifyAndGrantAsync/);
  assert.match(provider, /if \(!granted\)[\s\S]*AwaitingSettlement/);
  assert.match(provider, /if \(!granted\)[\s\S]*_store\.ConfirmPurchase\(order\)/);
  assert.doesNotMatch(provider, /\$\d|packs\.json|SKR|Solana|WalletService/);
});

test('Google Play implementation remains isolated in an optional provider assembly', () => {
  const asmdef = JSON.parse(read('Assets/_Modules/Core/Payments/Providers/GooglePlay/DeNelle.PaymentProviders.GooglePlay.asmdef'));
  assert.ok(asmdef.references.includes('DeNelle.Core'));
  assert.ok(asmdef.references.includes('Unity.Purchasing'));
  assert.ok(asmdef.defineConstraints.includes('IAP_PRESENT'));
  assert.equal(asmdef.versionDefines[0].name, 'com.unity.purchasing');
});
