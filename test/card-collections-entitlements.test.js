'use strict';

const assert = require('assert');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const schema = fs.readFileSync(path.join(root, 'api', 'schema.sql'), 'utf8');
const migration = fs.readFileSync(path.join(
    root, 'api', 'migrations', '20260828_0008_card_collections_reward_entitlements.sql'), 'utf8');

let passed = 0;
function test(name, fn) {
    try {
        fn();
        passed += 1;
        process.stdout.write('PASS ' + name + '\n');
    } catch (err) {
        process.stderr.write('FAIL ' + name + ': ' + err.message + '\n');
        process.exitCode = 1;
    }
}

function requireBoth(pattern, label) {
    assert(pattern.test(schema), 'schema.sql missing ' + label);
    assert(pattern.test(migration), 'migration missing ' + label);
}

test('catalog tables and reward ledger exist in declaration and migration', () => {
    for (const table of ['catalog_items', 'catalog_collections', 'catalog_collection_items', 'sku_entitlements']) {
        requireBoth(new RegExp('CREATE TABLE IF NOT EXISTS\\s+' + table + '\\s*\\(', 'i'), table);
    }
});

test('collection membership is an ordered pointer with no duplicated slot', () => {
    requireBoth(/PRIMARY KEY\s*\(collection_id,\s*sku\)/i, 'collection+sku primary key');
    requireBoth(/UNIQUE\s*\(collection_id,\s*display_order\)/i, 'collection order uniqueness');
    requireBoth(/sku\s+TEXT NOT NULL REFERENCES catalog_items\(sku\) ON DELETE RESTRICT/i,
        'item pointer foreign key');
});

test('remote asset metadata is complete-or-absent and sha256 shaped', () => {
    requireBoth(/asset_sha256\s+TEXT CHECK \(asset_sha256 ~ '\^\[0-9a-f\]\{64\}\$'\)/i,
        'sha256 constraint');
    requireBoth(/asset_url IS NULL AND asset_sha256 IS NULL AND asset_size_bytes IS NULL AND asset_version IS NULL/i,
        'empty asset tuple branch');
    requireBoth(/asset_url IS NOT NULL AND asset_sha256 IS NOT NULL AND asset_size_bytes IS NOT NULL AND asset_version IS NOT NULL/i,
        'complete asset tuple branch');
});

test('reward entitlements are distinct from chain purchase authority', () => {
    const block = schema.slice(schema.indexOf('CREATE TABLE IF NOT EXISTS sku_entitlements'));
    assert(!/tx_signature|expected_lamports|observed_lamports|recipient/.test(block),
        'reward ledger must not impersonate chain settlement');
    assert(/grant_id\s+TEXT NOT NULL UNIQUE/i.test(block), 'grant_id must be unique');
    assert(/source_kind\s+TEXT NOT NULL CHECK/i.test(block), 'source kind must be bounded');
});

test('expiry and revocation are server-row invariants', () => {
    requireBoth(/CHECK \(expires_at IS NULL OR expires_at > granted_at\)/i, 'expiry ordering');
    requireBoth(/state = 'active' AND revoked_at IS NULL AND revoke_reason IS NULL/i,
        'active consistency');
    requireBoth(/state = 'revoked' AND revoked_at IS NOT NULL AND revoke_reason IS NOT NULL/i,
        'revoked consistency');
});

test('migration is transactional and carries no production seed or grant', () => {
    assert(/^BEGIN;/m.test(migration), 'migration must begin transaction');
    assert(/COMMIT;\s*$/.test(migration), 'migration must commit transaction');
    assert(!/INSERT INTO\s+(catalog_items|catalog_collections|catalog_collection_items|sku_entitlements)/i.test(migration),
        'foundation migration must not seed or grant production content');
});

process.stdout.write('CARD_COLLECTION_ENTITLEMENT_SCHEMA_OK ' + passed + '/6\n');
if (passed !== 6) process.exitCode = 1;
