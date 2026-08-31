const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const assert = require('node:assert/strict');

const pagePath = path.resolve(__dirname, '..', 'site', 'delete-account.html');
const page = fs.readFileSync(pagePath, 'utf8');

test('deletion page pins the Play-facing route, title, and identities', () => {
  assert.ok(fs.existsSync(pagePath));
  assert.match(page, /<title>Request Account and Data Deletion[^<]*Echoes of Elarion[^<]*DeNelle Studios<\/title>/);
  assert.match(page, /<h1>Request account and data deletion<\/h1>/);
  assert.match(page, /<strong>App:<\/strong> Echoes of Elarion/);
  assert.match(page, /<strong>Developer:<\/strong> DeNelle Studios/);
  assert.match(page, /support\.eoa@icloud\.com/);
});

test('deletion page accurately distinguishes current Play, guest, and wallet identities', () => {
  assert.match(page, /Google\s+sign-in is available in the Google Play version/);
  assert.match(page, /guest play and wallet-linked versions use\s+different identifiers/);
  assert.match(page, /deletion-request reference returned\s+by the app/);
  assert.match(page, /public wallet address/);
  assert.match(page, /guest, report,\s+or session reference/);
  assert.match(page, /Realm Store while signed in/);
  assert.doesNotMatch(page, /Google sign-in (?:is|are) planned/i);
});

test('deletion page pins deletion scope, limited retention, and local-data instructions', () => {
  for (const scope of ['account or binding record', 'cloud saves', 'gameplay analytics', 'diagnostics', 'bug reports']) {
    assert.match(page, new RegExp(scope));
  }
  for (const reason of ['legal', 'fraud-prevention', 'chargeback', 'purchase and entitlement', 'security', 'audit']) {
    assert.match(page, new RegExp(reason));
  }
  assert.match(page, /only for as long\s+as the applicable obligation or legitimate security need requires/);
  assert.match(page, /clear Echoes of Elarion's storage in Android settings or uninstall the app/);
});

test('deletion page supports partial deletion and never asks for secrets', () => {
  assert.match(page, /You do not have to delete an account to request deletion of associated data/);
  assert.match(page, /only specific\s+categories of associated data deleted/);
  for (const secret of ['password', 'wallet signature', 'seed phrase', 'private key', 'purchase token', 'full payment details']) {
    assert.match(page, new RegExp(secret));
  }
  assert.match(page, /Never send us/);
  assert.match(page, /href="\/privacy"/);
});

test('deletion instructions remain readable without JavaScript', () => {
  assert.doesNotMatch(page, /<script\b/i);
  assert.match(page, /<meta name="viewport" content="width=device-width, initial-scale=1">/);
  assert.match(page, /<link rel="stylesheet" href="\/styles\.css">/);
});
