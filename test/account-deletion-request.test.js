'use strict';

const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const assert = require('node:assert/strict');

const root = path.resolve(__dirname, '..');
const {
    normalizeDeletionRequest,
    createDeletionRequest,
} = require('../api/_lib/account-deletion');

function mockSql(rows) {
    const calls = [];
    const sql = async (strings, ...values) => {
        calls.push({ text: strings.join('?'), values });
        return { rows };
    };
    sql.calls = calls;
    return sql;
}

test('normalizes account deletion without accepting client-invented categories', () => {
    assert.deepEqual(normalizeDeletionRequest({ playerId: ' play-id ', scope: 'account' }), {
        ok: true, playerId: 'play-id', scope: 'account', categories: [],
    });
    assert.equal(normalizeDeletionRequest({
        playerId: 'play-id', scope: 'account', categories: ['cloud_saves'],
    }).code, 'DELETION_CATEGORIES_INVALID');
});

test('partial deletion requires a non-empty allowlisted category set', () => {
    assert.equal(normalizeDeletionRequest({
        playerId: 'play-id', scope: 'associated_data', categories: [],
    }).code, 'DELETION_CATEGORIES_REQUIRED');
    assert.equal(normalizeDeletionRequest({
        playerId: 'play-id', scope: 'associated_data', categories: ['purchase_ledger'],
    }).code, 'DELETION_CATEGORIES_INVALID');
    assert.deepEqual(normalizeDeletionRequest({
        playerId: 'play-id', scope: 'associated_data',
        categories: ['diagnostics', 'cloud_saves', 'diagnostics'],
    }).categories, ['cloud_saves', 'diagnostics']);
});

test('request insert is identity-bound and idempotent while active', async () => {
    const row = { request_id: 'request-1', status: 'requested', requested_at: '2026-08-30T00:00:00Z' };
    const sql = mockSql([row]);
    assert.deepEqual(await createDeletionRequest(sql, {
        playerId: 'play-id', scope: 'associated_data', categories: ['cloud_saves'],
    }, 'google'), row);
    assert.match(sql.calls[0].text, /ON CONFLICT \(player_id\) WHERE status IN/);
    assert.deepEqual(sql.calls[0].values, [
        'play-id', 'google', 'associated_data', ['cloud_saves'],
    ]);
});

test('migration constrains scope, identity kind, categories, and one active request', () => {
    const migration = fs.readFileSync(path.join(root, 'api', 'migrations',
        '20260830_0014_account_deletion_requests.sql'), 'utf8');
    assert.match(migration, /^BEGIN;/m);
    assert.match(migration, /identity_kind IN \('wallet', 'google', 'guest'\)/);
    assert.match(migration, /request_scope IN \('account', 'associated_data'\)/);
    assert.match(migration, /request_categories <@ ARRAY/);
    assert.match(migration, /UNIQUE INDEX[\s\S]*WHERE status IN \('requested', 'in_progress'\)/);
    assert.match(migration, /COMMIT;\s*$/);
});

test('route authenticates the exact request before creating a deletion request', () => {
    const route = fs.readFileSync(path.join(root, 'api', 'account', 'delete-request.js'), 'utf8');
    assert.match(route, /readBodyExact\(req, MAX_BODY_BYTES\)/);
    assert.match(route, /authenticate\(sql, req, rawBody, normalized\.playerId\)/);
    assert.ok(route.indexOf('authenticate(sql') < route.indexOf('createDeletionRequest(sql'));
    assert.match(route, /bodyParser: false/);
    assert.doesNotMatch(route, /DELETE\s+FROM/i);
});

test('Google Play storefront submits the request with its verified cached session', () => {
    const vm = fs.readFileSync(path.join(root, 'Assets', '_Modules', 'GooglePlay',
        'GooglePlayStorefrontVM.cs'), 'utf8');
    const view = fs.readFileSync(path.join(root, 'Assets', '_Modules', 'GooglePlay',
        'GooglePlayStorefront.cs'), 'utf8');
    assert.match(vm, /GooglePlayIdentityClient\.EnsureSignedInAsync\(\)/);
    assert.match(vm, /scope = "account"/);
    assert.match(vm, /BackendRequestSigner\.TryAttachCachedSession\(request, playerId\)/);
    assert.match(vm, /\/api\/account\/delete-request/);
    assert.match(view, /_vm\.RequestDeletion/);
    assert.doesNotMatch(view, /_vm\.OpenDeletionPage/);
});

test('Google Play deletion requires a bounded second confirmation tap', () => {
  const vm = fs.readFileSync(path.resolve(__dirname, '..', 'Assets', '_Modules',
    'GooglePlay', 'GooglePlayStorefrontVM.cs'), 'utf8');
  assert.match(vm, /DeletionConfirmationSeconds\s*=\s*12f/);
  assert.match(vm, /Time\.realtimeSinceStartup\s*>\s*_deletionConfirmUntil/);
  assert.match(vm, /Tap again within 12 seconds to confirm account deletion/);
  assert.match(vm, /_deletionConfirmUntil\s*=\s*0f/);
});
