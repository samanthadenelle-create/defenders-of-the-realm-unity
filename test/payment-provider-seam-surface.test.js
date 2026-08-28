const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..');

test('core payment seam is provider-neutral and fails closed', () => {
  const source = fs.readFileSync(
    path.join(root, 'Assets/_Modules/Core/Payments/IPaymentProvider.cs'),
    'utf8'
  );

  assert.match(source, /interface IPaymentProvider/);
  assert.match(source, /DisplayPrice GetDisplayPrice\(string sku\)/);
  assert.match(source, /void Purchase\(string sku, Action<ProviderPurchaseResult> onComplete\)/);
  assert.match(source, /void RestorePurchases\(Action<bool, string> onComplete\)/);
  assert.match(source, /Payment provider channel mismatch/);
  assert.match(source, /Payment provider already registered/);
  assert.doesNotMatch(source, /SolanaWalletProvider|UnityEngine\.Purchasing|Pi\.createPayment/);
});

