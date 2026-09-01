'use strict';

const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const assert = require('node:assert/strict');

const policy = require('../api/_lib/town-showcase');
const publishModule = require('../api/showcase/publish');
const unpublishModule = require('../api/showcase/unpublish');
const getModule = require('../api/showcase/get');
const topModule = require('../api/showcase/top');

function response() {
    return {
        statusCode: 0, body: null, headers: {},
        setHeader(k, v) { this.headers[k] = v; },
        status(code) { this.statusCode = code; return this; },
        json(body) { this.body = body; return this; },
        end() { return this; },
    };
}

function request(method, body, query = {}) {
    return {
        method, query, headers: { 'x-session': 'session-proof' },
        body: JSON.stringify(body), readableEnded: true, complete: true,
    };
}

function validPublish() {
    return {
        playerId: 'internal-wallet-never-public',
        schemaVersion: 2,
        catalogVersion: 42,
        minimumClientVersion: '2026.08.29',
        publishRequested: true,
        structures: [{
            itemId: 'tower_archer', cellX: 2, cellZ: -3, yawSteps: 1,
            level: 4, yawOffset: 45, worldY: 2.5, wallMounted: true,
        }],
        equippedCosmeticSkus: ['skin_castle_firstwatch'],
        publicHeroLineup: [{ sku: 'hero_knight', level: 7 }],
        publicArmyLineup: [{ sku: 'unit_archer', level: 4, count: 12 }],
        selectedEchoes: [{ sku: 'echo_luma', level: 3 }],
        echoesSaved: 43,
        bannerSku: 'banner_firstwatch',
        titleSku: 'title_watchkeeper',
        townLevel: 8,
        publicAchievementSkus: ['achievement_wave_7'],
    };
}

function publicRow(overrides = {}) {
    return Object.assign({
        showcase_id: 'sh_7Hy3qP9mN2xK4v8Q',
        public_owner_id: 'po_Z4c8V1s6Q0rT5y2M',
        snapshot_version: 3,
        schema_version: 2,
        catalog_version: 42,
        minimum_client_version: '2026.08.29',
        structures: validPublish().structures,
        equipped_cosmetic_skus: validPublish().equippedCosmeticSkus,
        public_hero_lineup: validPublish().publicHeroLineup,
        public_army_lineup: validPublish().publicArmyLineup,
        selected_echoes: validPublish().selectedEchoes,
        echoes_saved: validPublish().echoesSaved,
        banner_sku: validPublish().bannerSku,
        title_sku: validPublish().titleSku,
        town_level: validPublish().townLevel,
        public_achievement_skus: validPublish().publicAchievementSkus,
        leaderboard_rank: 3,
    }, overrides);
}

test('publish validation is strict, bounded, and explicit opt-in only', () => {
    const good = policy.validatePublishBody(validPublish());
    assert.equal(good.ok, true);
    assert.deepEqual(Object.keys(good.value).sort(),
        ['bannerSku', 'catalogVersion', 'echoesSaved', 'equippedCosmeticSkus',
            'minimumClientVersion', 'publicAchievementSkus', 'publicArmyLineup', 'publicHeroLineup',
            'schemaVersion', 'selectedEchoes', 'structures', 'titleSku', 'townLevel'].sort());

    const privateByDefault = validPublish();
    privateByDefault.publishRequested = false;
    assert.equal(policy.validatePublishBody(privateByDefault).code, 'PUBLICATION_OPT_IN_REQUIRED');
    const smuggled = validPublish();
    smuggled.wallet = 'leak';
    assert.equal(policy.validatePublishBody(smuggled).code, 'PAYLOAD_FIELDS_INVALID');
    const forgedRank = validPublish();
    forgedRank.leaderboardRank = 1;
    assert.equal(policy.validatePublishBody(forgedRank).code, 'PAYLOAD_FIELDS_INVALID');
    const privateStructure = validPublish();
    privateStructure.structures[0].resources = { crystals: 999 };
    assert.equal(policy.validatePublishBody(privateStructure).code, 'STRUCTURE_INVALID');
    const oversized = validPublish();
    oversized.structures = Array(policy.MAX_STRUCTURES + 1).fill(validPublish().structures[0]);
    assert.equal(policy.validatePublishBody(oversized).code, 'STRUCTURES_INVALID');
    const duplicatePrivateRoster = validPublish();
    duplicatePrivateRoster.publicHeroLineup.push({ sku: 'hero_knight', level: 99 });
    assert.equal(policy.validatePublishBody(duplicatePrivateRoster).code, 'PUBLIC_PROFILE_INVALID');
    const oversizedArmy = validPublish();
    oversizedArmy.publicArmyLineup = Array(policy.MAX_ARMY_UNITS + 1)
        .fill(null).map((_, i) => ({ sku: `unit_${i}`, level: 1, count: 1 }));
    assert.equal(policy.validatePublishBody(oversizedArmy).code, 'PUBLIC_PROFILE_INVALID');
    const smuggledHero = validPublish();
    smuggledHero.publicHeroLineup[0].inventory = ['private'];
    assert.equal(policy.validatePublishBody(smuggledHero).code, 'PUBLIC_PROFILE_INVALID');
});

test('publish requires an authenticated session bound to the internal owner', async () => {
    let queried = false;
    const sql = async () => { queried = true; return []; };
    const handler = publishModule._test.makeHandler({
        getSql: () => sql,
        verifySession: async (_sql, token, claimed) => {
            assert.equal(token, 'session-proof');
            assert.equal(claimed, validPublish().playerId);
            return { ok: false, code: 'AUTH_SESSION_INVALID' };
        },
    });
    const res = response();
    await handler(request('POST', validPublish()), res);
    assert.equal(res.statusCode, 401);
    assert.equal(res.body.code, 'AUTH_SESSION_INVALID');
    assert.equal(queried, false);
});

test('authenticated publish returns only the sanitized opaque public snapshot', async () => {
    let query = '';
    const sql = async (strings) => {
        const text = strings.join('?'); query += text;
        if (/owned_cosmetics/.test(text)) return [{
            owned_cosmetics: validPublish().equippedCosmeticSkus,
            owned_achievements: validPublish().publicAchievementSkus,
        }];
        return [publicRow()];
    };
    const handler = publishModule._test.makeHandler({
        getSql: () => sql,
        verifySession: async () => ({ ok: true, wallet: validPublish().playerId }),
        opaqueId: prefix => prefix + 'FixedOpaqueValue1234',
    });
    const res = response();
    await handler(request('POST', validPublish()), res);
    assert.equal(res.statusCode, 200);
    assert.equal(res.body.snapshot.snapshotId, publicRow().showcase_id);
    assert.match(query, /published, published_at/);
    assert.match(query, /public_town_snapshot_versions/);
    const json = JSON.stringify(res.body).toLowerCase();
    for (const forbidden of ['wallet', 'playerid', 'inventory', 'balance', 'resources',
        'crystals', 'coins', 'saveblob', 'email', 'session', 'private'])
        assert.doesNotMatch(json, new RegExp(forbidden));
    assert.deepEqual(res.body.snapshot.publicHeroLineup, validPublish().publicHeroLineup);
    assert.deepEqual(res.body.snapshot.publicArmyLineup, validPublish().publicArmyLineup);
    assert.equal(res.body.snapshot.echoesSaved, 43);
    assert.equal(res.body.snapshot.leaderboardRank, 3);
    assert.match(query, /leaderboard_scores/);
    assert.match(query, /highest_wave/);
});

test('publish cannot claim unowned cosmetics or achievements', async () => {
    let writes = 0;
    const sql = async strings => {
        const text = strings.join('?');
        if (/owned_cosmetics/.test(text)) return [{ owned_cosmetics: [], owned_achievements: [] }];
        writes += 1;
        return [publicRow()];
    };
    const handler = publishModule._test.makeHandler({
        getSql: () => sql,
        verifySession: async () => ({ ok: true, wallet: validPublish().playerId }),
    });
    const res = response();
    await handler(request('POST', validPublish()), res);
    assert.equal(res.statusCode, 403);
    assert.equal(res.body.code, 'PUBLIC_PROFILE_NOT_OWNED');
    assert.equal(writes, 0);
});

test('unpublish is authenticated, owner-scoped, and preserves snapshot history', async () => {
    let query = '';
    let boundOwner = null;
    const sql = async (strings, owner) => { query = strings.join('?'); boundOwner = owner; return []; };
    const handler = unpublishModule._test.makeHandler({
        getSql: () => sql,
        verifySession: async () => ({ ok: true }),
    });
    const res = response();
    await handler(request('POST', { playerId: validPublish().playerId }), res);
    assert.equal(res.statusCode, 200);
    assert.deepEqual(res.body, { success: true, published: false });
    assert.equal(boundOwner, validPublish().playerId);
    assert.match(query, /UPDATE public_town_showcases/);
    assert.match(query, /published = FALSE/);
    assert.doesNotMatch(query, /DELETE|public_town_snapshot_versions/i);
});

test('public read resists enumeration and never returns internal identity', async () => {
    let calls = 0;
    const missing = getModule._test.makeHandler(() => async () => { calls += 1; return []; });
    const malformedRes = response();
    await missing({ method: 'GET', query: { id: 'wallet-or-sequential-1' } }, malformedRes);
    const unknownRes = response();
    await missing({ method: 'GET', query: { id: 'sh_7Hy3qP9mN2xK4v8Q' } }, unknownRes);
    assert.deepEqual(malformedRes.body, unknownRes.body);
    assert.equal(malformedRes.statusCode, 404);
    assert.equal(calls, 1, 'malformed ids must be refused without a database probe');

    const found = getModule._test.makeHandler(() => async () => [publicRow()]);
    const foundRes = response();
    await found({ method: 'GET', query: { id: publicRow().showcase_id } }, foundRes);
    assert.equal(foundRes.statusCode, 200);
    assert.doesNotMatch(JSON.stringify(foundRes.body).toLowerCase(), /wallet|playerid|accountid/);

    const poisoned = publicRow();
    poisoned.structures[0] = Object.assign({}, poisoned.structures[0], { balance: 999 });
    const poisonedHandler = getModule._test.makeHandler(() => async () => [poisoned]);
    const poisonedRes = response();
    await poisonedHandler({ method: 'GET', query: { id: poisoned.showcase_id } }, poisonedRes);
    assert.equal(poisonedRes.statusCode, 404, 'unexpected stored JSON must fail closed');
    assert.doesNotMatch(JSON.stringify(poisonedRes.body), /balance/);
});

test('legacy v1 layout snapshots stay readable with empty public profile defaults', () => {
    const legacy = publicRow({ schema_version: 1 });
    delete legacy.equipped_cosmetic_skus;
    delete legacy.public_hero_lineup;
    delete legacy.public_army_lineup;
    delete legacy.selected_echoes;
    delete legacy.echoes_saved;
    delete legacy.banner_sku;
    delete legacy.title_sku;
    delete legacy.town_level;
    delete legacy.public_achievement_skus;
    delete legacy.leaderboard_rank;
    const mapped = policy.mapPublicSnapshot(legacy);
    assert.equal(mapped.schemaVersion, 1);
    assert.deepEqual(mapped.equippedCosmeticSkus, []);
    assert.deepEqual(mapped.publicHeroLineup, []);
    assert.deepEqual(mapped.publicArmyLineup, []);
    assert.deepEqual(mapped.selectedEchoes, []);
    assert.equal(mapped.echoesSaved, 0);
    assert.equal(mapped.townLevel, 1);
});

test('Top 10 maps leaderboard rank to optional showcase id without exposing join key', async () => {
    let query = '';
    const sql = async strings => {
        query = strings.join('?');
        return [
            { rank: 1, username: 'FirstWatch', score: 27, showcase_id: publicRow().showcase_id },
            { rank: 2, username: null, score: 24, showcase_id: null },
        ];
    };
    const handler = topModule._test.makeHandler(() => sql);
    const res = response();
    await handler({ method: 'GET', query: { metric: 'highest_wave', period: 'alltime' } }, res);
    assert.equal(res.statusCode, 200);
    assert.deepEqual(res.body.top, [
        { rank: 1, username: 'FirstWatch', score: 27, showcaseId: publicRow().showcase_id },
        { rank: 2, username: null, score: 24, showcaseId: null },
    ]);
    assert.match(query, /ROW_NUMBER/);
    assert.match(query, /rank <= 10/);
    assert.match(query, /LEFT JOIN public_town_showcases/);
    assert.doesNotMatch(JSON.stringify(res.body).toLowerCase(), /wallet|owner_wallet|publicownerid/);
});

test('migration defaults unpublished and public routes never select identity into output', () => {
    const root = path.resolve(__dirname, '..');
    const migration = fs.readFileSync(path.join(root, 'api/migrations/20260829_0009_public_town_showcases.sql'), 'utf8');
    const profileMigration = fs.readFileSync(path.join(root, 'api/migrations/20260829_0011_public_town_snapshot_profile.sql'), 'utf8');
    const getSource = fs.readFileSync(path.join(root, 'api/showcase/get.js'), 'utf8');
    const topSource = fs.readFileSync(path.join(root, 'api/showcase/top.js'), 'utf8');
    assert.match(migration, /published\s+BOOLEAN\s+NOT NULL DEFAULT FALSE/);
    assert.match(migration, /showcase_id\s+TEXT\s+NOT NULL UNIQUE/);
    assert.match(migration, /PRIMARY KEY \(owner_wallet, snapshot_version\)/);
    assert.match(profileMigration, /schema_version IN \(1, 2\)/);
    assert.match(profileMigration, /jsonb_array_length\(public_army_lineup\) <= 12/);
    assert.match(profileMigration, /leaderboard_rank BETWEEN 1 AND 1000000/);
    assert.doesNotMatch(profileMigration, /save_blob|inventory|balance|private_roster/i);
    assert.match(getSource, /s\.published = TRUE/);
    assert.match(topSource, /CASE WHEN sh\.published THEN sh\.showcase_id ELSE NULL END/);
    assert.doesNotMatch(getSource, /wallet:/);
    assert.doesNotMatch(topSource, /wallet:/);
});
