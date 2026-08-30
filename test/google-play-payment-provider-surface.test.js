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

// ⚠ THIS TEST REPLACES 'Play bootstrap leaves settlement dormant until identity and durable
// grant composition exist' (WO-1255), which asserted the bootstrap must NOT contain
// ConfigureSettlement. That assertion pinned the very defect WO-1282 closed: the method had NO
// CALLER in the whole tree, so VerifyAndGrantAsync stayed null and a Play purchase would have
// been taken and never granted. The invariant it was protecting — never sell what cannot be
// settled — is unchanged and is now asserted where it actually lives: composition happens
// BEFORE the store connects, and a chain that cannot be built leaves the provider unconfigured
// so CanBuy refuses every SKU.
test('Play bootstrap composes settlement before connecting, and fails closed if it cannot', () => {
  const bootstrap = read('Assets/_Modules/Core/Payments/Providers/GooglePlay/GooglePlayPaymentBootstrap.cs');

  // Still channel-gated: nothing on this rail runs on a Seeker/dApp-Store artifact.
  assert.match(bootstrap, /PaymentChannelResolver\.Current != PaymentChannel\.GooglePlay\) return;/);

  const compose = bootstrap.indexOf('GooglePlaySettlementComposer.TryConfigure(provider)');
  const initialize = bootstrap.indexOf('provider.Initialize()');
  assert.ok(compose >= 0, 'the bootstrap must be the composition root for ConfigureSettlement');
  assert.ok(initialize > compose,
    'settlement must be configured BEFORE the store connects, so no PendingOrder can arrive without it');
  assert.match(bootstrap, /if \(!GooglePlaySettlementComposer\.TryConfigure\(provider\)\)[\s\S]*FlowTrace\.Fail/);

  const composer = read('Assets/_Modules/Core/Payments/Providers/GooglePlay/GooglePlaySettlementComposer.cs');
  assert.match(composer, /new GooglePlayReceiptSettlement\(transport, new GooglePlayGrantApplier\(\)\)/);
  // A half-built chain must NOT be handed to the provider.
  const guardFail = composer.indexOf('if (!built || settlement == null || transport == null)');
  const configure = composer.indexOf('provider.ConfigureSettlement(settlement, transport)');
  assert.ok(guardFail >= 0 && configure > guardFail,
    'ConfigureSettlement must be unreachable when the chain failed to build');
  // Identity is resolved per call, never captured at boot (WO-1282 PIN-1b: no re-keying).
  assert.match(composer, /BackendRequestSigner\.CurrentPlayerId\(\)/);
  assert.match(composer, /BackendRequestSigner\.TryAttachCachedSession\(request, playerId\)/);

  const provider = read('Assets/_Modules/Core/Payments/Providers/GooglePlay/GooglePlayBillingProvider.cs');
  assert.match(provider, /if \(VerifyAndGrantAsync == null \|\| _bindingSource == null\)[\s\S]*Secure Google Play receipt verification is unavailable/);
});

test('durable grant applier is idempotent by purchase token and never fails open', () => {
  const applier = read('Assets/_Modules/Core/Payments/Providers/GooglePlay/GooglePlayGrantApplier.cs');

  // The write-ahead ordering IS the exactly-once property: journal the intent, then mutate,
  // then mark applied. Reversing these grants twice after a crash.
  const preOwned = applier.indexOf('PackGrantBridge.TryIsOwned(sku, out bool preOwned)');
  const writeAhead = applier.indexOf('WriteJournal(key, StatePending');
  // from writeAhead: the header comment names the same call while describing the ordering.
  const apply = applier.indexOf('PackGrantBridge.TryApply(sku)', writeAhead);
  const markApplied = applier.indexOf('WriteJournal(key, StateApplied + "|" + sku);', apply);
  assert.ok(preOwned >= 0 && writeAhead > preOwned && apply > writeAhead && markApplied > apply,
    'probe ownership -> journal pending -> apply -> mark applied');

  // A failed grant must leave the entry PENDING so Google re-delivers.
  assert.match(applier, /if \(!granted\)[\s\S]*return Task\.FromResult\(false\);/);
  // No local entitlement writer => refuse, never confirm.
  assert.match(applier, /if \(!PackGrantBridge\.HasApplier\)[\s\S]*return Task\.FromResult\(false\);/);
  // The token itself is never persisted — the journal key is its SHA-256.
  assert.match(applier, /SHA256\.Create\(\)/);
  assert.match(applier, /JournalPrefix\s*=\s*"gp\.settle\."/);
  assert.doesNotMatch(applier, /PlayerPrefs\.SetString\(\s*purchaseToken/);
  // Markers must be flushed; an unflushed journal is lost in the exact crash it guards.
  assert.match(applier, /PlayerPrefs\.SetString\(key, entry\);\s*\n\s*PlayerPrefs\.Save\(\);/);
});

test('the rail-neutral pack grant bridge exists and defaults to refusing', () => {
  const bridge = read('Assets/_Modules/Commerce/PackGrantBridge.cs');
  assert.match(bridge, /public static bool HasApplier => _apply != null && _isOwned != null;/);
  // Every accessor returns false when nothing is registered — no "assume granted" default.
  assert.match(bridge, /if \(!HasApplier\)\s*\n\s*\{[\s\S]*?return false;/);
  assert.doesNotMatch(bridge, /Solana|WalletService|CurrencyKind/);

  // The one registrar is the assembly that owns the single entitlement writer.
  const packBootstrap = read('Assets/_Modules/Wallet/PackStoreBootstrap.cs');
  assert.match(packBootstrap, /PackGrantBridge\.RegisterApplier\(ApplyPackBySku, IsPackOwned\)/);
  // The applier reports the OWNERSHIP PROBE, not "ApplyPackContents did not throw".
  assert.match(packBootstrap, /vm\.ApplyPackContents\(pack\);\s*\n\s*return vm\.IsOwned\(sku\);/);
});

test('Google Play implementation remains isolated in an optional provider assembly', () => {
  const asmdef = JSON.parse(read('Assets/_Modules/Core/Payments/Providers/GooglePlay/DeNelle.PaymentProviders.GooglePlay.asmdef'));
  assert.ok(asmdef.references.includes('DeNelle.Core'));
  assert.ok(asmdef.references.includes('Unity.Purchasing'));
  assert.ok(asmdef.defineConstraints.includes('IAP_PRESENT'));
  assert.equal(asmdef.versionDefines[0].name, 'com.unity.purchasing');
});
