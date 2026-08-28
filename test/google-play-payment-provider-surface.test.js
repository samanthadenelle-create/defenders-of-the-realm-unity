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

  const saleProducts = packs.filter((pack) => pack.pricing).map((pack) => pack.sku);
  assert.deepEqual(mapped.sort(), saleProducts.sort());
  assert.equal(new Set(mapped).size, mapped.length);
  assert.match(catalog, /TryGetProductType/);
});

test('client Google product types exactly match the server authority', () => {
  const catalog = read('Assets/_Modules/Core/Payments/Providers/GooglePlay/GooglePlayProductCatalog.cs');
  const server = require('../api/_lib/google-play-purchases');
  const skuBody = catalog.match(/private static readonly string\[\] s_skus =\s*\{([\s\S]*?)\};/)[1];
  const clientSkus = [...skuBody.matchAll(/"([a-z0-9-]+)"/g)].map(m => m[1]);
  const durableBody = catalog.match(/s_nonConsumable = new HashSet<string>\([\s\S]*?\)\s*\{([\s\S]*?)\};/)[1];
  const durable = new Set([...durableBody.matchAll(/"([a-z0-9-]+)"/g)].map(m => m[1]));
  const clientTypes = Object.fromEntries(clientSkus.map(sku =>
    [sku, durable.has(sku) ? 'non_consumable' : 'consumable']));
  assert.deepEqual(clientTypes, server.PRODUCT_TYPES);
  assert.match(catalog, /ProductType\.NonConsumable\s*:\s*ProductType\.Consumable/);
  assert.match(catalog, /new ProductDefinition\(s_productBySku\[sku\], productType\)/);

  const packs = JSON.parse(read('Assets/StreamingAssets/Data/Canonical/packs.json')).packs;
  for (const pack of packs.filter(p => p.pricing)) {
    const permanent = (pack.contents?.cosmetics?.length || 0) > 0 ||
      (pack.contents?.convenience || []).some(c => c.kind === 'permanent-builder');
    assert.equal(server.PRODUCT_TYPES[pack.sku], permanent ? 'non_consumable' : 'consumable',
      `${pack.sku}: product type must derive from permanent contents`);
  }
});

test('Google Play rail uses store-localized prices and server-gated confirmation', () => {
  const provider = read('Assets/_Modules/Core/Payments/Providers/GooglePlay/GooglePlayBillingProvider.cs');

  assert.match(provider, /metadata\.localizedPriceString/);
  assert.match(provider, /metadata\.isoCurrencyCode/);
  assert.match(provider, /VerifyAndGrantAsync/);
  assert.match(provider, /if \(!granted\)[\s\S]*AwaitingSettlement/);
  assert.match(provider, /if \(!granted\)[\s\S]*_store\.ConfirmPurchase\(order\)/);
  assert.doesNotMatch(provider, /\$\d|packs\.json|SKR|Solana|WalletService/);
  const settle = provider.indexOf('granted = await VerifyAndGrantAsync');
  const confirm = provider.indexOf('_store.ConfirmPurchase(order)');
  assert.ok(settle >= 0 && confirm > settle, 'Unity order must remain pending through server settlement');
});

test('receipt adapter enforces verify -> exact-once apply -> fulfill before Unity confirmation', () => {
  const adapter = read('Assets/_Modules/Core/Payments/Providers/GooglePlay/GooglePlayReceiptSettlement.cs');
  const verify = adapter.indexOf('_transport.VerifyAsync');
  const applied = adapter.indexOf('_grantApplier.IsApplied');
  const apply = adapter.indexOf('_grantApplier.ApplyExactlyOnceAsync');
  const fulfill = adapter.indexOf('_transport.FulfillAsync');
  assert.ok(verify >= 0 && applied > verify && apply > applied && fulfill > apply);
  assert.match(adapter, /TryExtractPurchaseToken\(receipt, productId, out var purchaseToken\)/);
  assert.match(adapter, /string\.Equals\(purchaseToken, transactionId/);
  for (const state of ['verified', 'granted', 'consumed', 'acknowledged'])
    assert.match(adapter, new RegExp(`string\\.Equals\\(state, "${state}"`));
  assert.match(adapter, /string\.Equals\(purchase\.productId, expectedProductId/);
  assert.match(adapter, /_attachSession\(request, _playerId\)/);
  assert.doesNotMatch(adapter, /ConfirmPurchase/);
  assert.doesNotMatch(adapter, /Debug\.Log[\s\S]*(receipt|purchaseToken)/);
});

test('authenticated server binding is attached to Google before purchase starts', () => {
  const provider = read('Assets/_Modules/Core/Payments/Providers/GooglePlay/GooglePlayBillingProvider.cs');
  const fetchBinding = provider.indexOf('FetchAccountBindingAsync');
  const setBinding = provider.indexOf('SetObfuscatedAccountId(binding)');
  const purchase = provider.indexOf('_store.PurchaseProduct(productId)', setBinding);
  assert.ok(fetchBinding >= 0 && setBinding > fetchBinding && purchase > setBinding);
  assert.match(provider, /VerifyAndGrantAsync == null \|\| _bindingSource == null/);
  const adapter = read('Assets/_Modules/Core/Payments/Providers/GooglePlay/GooglePlayReceiptSettlement.cs');
  assert.match(adapter, /\/api\/purchases\/google-play-binding/);
  assert.match(adapter, /_attachSession\(request, _playerId\)/);
});

test('Play bootstrap leaves settlement dormant until identity and durable grant composition exist', () => {
  const bootstrap = read('Assets/_Modules/Core/Payments/Providers/GooglePlay/GooglePlayPaymentBootstrap.cs');
  assert.doesNotMatch(bootstrap, /ConfigureSettlement|VerifyAndGrantAsync\s*=/);
  const provider = read('Assets/_Modules/Core/Payments/Providers/GooglePlay/GooglePlayBillingProvider.cs');
  assert.match(provider, /if \(VerifyAndGrantAsync == null \|\| _bindingSource == null\)[\s\S]*Secure Google Play receipt verification is unavailable/);
});

test('Google Play implementation remains isolated in an optional provider assembly', () => {
  const asmdef = JSON.parse(read('Assets/_Modules/Core/Payments/Providers/GooglePlay/DeNelle.PaymentProviders.GooglePlay.asmdef'));
  assert.ok(asmdef.references.includes('DeNelle.Core'));
  assert.ok(asmdef.references.includes('Unity.Purchasing'));
  assert.ok(asmdef.defineConstraints.includes('IAP_PRESENT'));
  assert.equal(asmdef.versionDefines[0].name, 'com.unity.purchasing');
});
