'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const Module = require('node:module');
const { CatalogError, parseVersion, readCollection, safeHttpsUrl, versionAllows } = require('../api/_lib/catalog-read');

function sqlWith(rows) {
    const calls = [];
    const sql = async (strings, ...values) => { calls.push({ text: strings.join('?'), values }); return rows; };
    sql.calls = calls; return sql;
}

function row(overrides) {
    return Object.assign({
        collection_id: 'build.protection', context: 'build', title: 'Protection', subtitle: null,
        icon_key: 'shield', icon_url: null, icon_sha256: null, collection_version: 1,
        active: true, starts_at: null, ends_at: null, min_client_version: '1.0.0', depth: 0,
        display_order: 0, badge: null, visibility_rule: {}, sku: 'gate_stone',
        item_kind: 'building', definition: { title: 'Stone Gate', purpose: 'Protect an opening' },
        item_version: 1, item_min_client_version: null, packaged_fallback_key: 'gate_stone',
        fallback_sku: null, asset_url: null, asset_sha256: null, asset_size_bytes: null,
        asset_version: null, expiry_behavior: 'lock',
    }, overrides || {});
}

test('semantic client version comparison is explicit and bounded', () => {
    assert.deepEqual(parseVersion('1.2.3', true), [1, 2, 3]);
    assert.equal(versionAllows([1, 2, 3], '1.2.3'), true);
    assert.equal(versionAllows([1, 2, 2], '1.2.3'), false);
    assert.throws(() => parseVersion('latest', true), e => e.code === 'CLIENT_VERSION_INVALID');
});

test('remote URLs are credential-free HTTPS only', () => {
    assert.equal(safeHttpsUrl('https://cdn.example/a.bundle'), 'https://cdn.example/a.bundle');
    for (const bad of ['http://cdn.example/a', 'https://user:pass@cdn.example/a', 'javascript:alert(1)']) {
        assert.throws(() => safeHttpsUrl(bad), e => e instanceof CatalogError);
    }
});

test('one bounded recursive server query returns ordered pointers, not browser joins', async () => {
    const sql = sqlWith([row(), row({ sku: 'healing_caravan', display_order: 1,
        definition: { title: 'Healing Caravan' } })]);
    const out = await readCollection(sql, { collectionId: 'build.protection', clientVersion: '1.0.0' });
    assert.equal(out.items.length, 2);
    assert.deepEqual(out.items.map(x => x.sku), ['gate_stone', 'healing_caravan']);
    assert.match(sql.calls[0].text, /WITH RECURSIVE chain/);
    assert.match(sql.calls[0].text, /LIMIT 500/);
    assert.deepEqual(sql.calls[0].values, ['build.protection']);
});

test('inactive or incompatible requested collection resolves to an eligible fallback', async () => {
    const sql = sqlWith([
        row({ active: false, sku: null, display_order: null }),
        row({ collection_id: 'build.fallback', depth: 1, min_client_version: null }),
    ]);
    const out = await readCollection(sql, { collectionId: 'build.protection', clientVersion: '1.0.0' });
    assert.equal(out.collection_id, 'build.fallback');
    assert.equal(out.used_fallback, true);
});

test('unknown definition fields and incomplete asset tuples fail closed', async () => {
    await assert.rejects(() => readCollection(sqlWith([row({ definition: { html: '<script>' } })]),
        { collectionId: 'build.protection', clientVersion: '1.0.0' }), e => e.code === 'CATALOG_INVALID');
    await assert.rejects(() => readCollection(sqlWith([row({ asset_url: 'https://cdn.example/a' })]),
        { collectionId: 'build.protection', clientVersion: '1.0.0' }), e => e.code === 'CATALOG_INVALID');
});

test('invalid collection metadata fails closed even when the collection is empty', async () => {
    await assert.rejects(() => readCollection(sqlWith([row({ context: 'secret', sku: null })]),
        { collectionId: 'build.protection', clientVersion: '1.0.0' }), e => e.code === 'CATALOG_INVALID');
});

test('nested public definition data cannot smuggle identity or secret fields', async () => {
    await assert.rejects(() => readCollection(sqlWith([row({
        definition: { contents: { reward: { sessionToken: 'do-not-return' } } },
    })]), { collectionId: 'build.protection', clientVersion: '1.0.0' }),
    e => e.code === 'CATALOG_INVALID');
});

test('endpoint is public-safe GET-only and does not expose database errors', async () => {
    const originalLoad = Module._load;
    Module._load = function(request, parent, isMain) {
        if (request === '@neondatabase/serverless') return { neon: () => sqlWith([row()]) };
        return originalLoad.call(this, request, parent, isMain);
    };
    delete require.cache[require.resolve('../api/catalog/collection')];
    const handler = require('../api/catalog/collection');
    Module._load = originalLoad;
    const out = { headers: {} };
    const res = { setHeader(k,v){ out.headers[k.toLowerCase()] = v; }, status(c){ out.status=c; return res; },
        json(v){ out.body=v; return res; }, end(){ return res; } };
    await handler({ method:'GET', headers:{}, query:{ collectionId:'build.protection', clientVersion:'1.0.0' } }, res);
    assert.equal(out.status, 200); assert.equal(out.body.success, true);
    assert.equal(Object.prototype.hasOwnProperty.call(out.body, 'wallet'), false);
    assert.match(out.headers['cache-control'], /public/);
});
