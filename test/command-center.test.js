'use strict';

// =============================================================================
// test/command-center.test.js -- the oracle for the Command Center console
// (WO-1244, building the surface WO-1169 specced).
//
// It pins the things the ticket actually turns on, and nothing decorative:
//
//   1. THE READ/WRITE BOUNDARY IS AT THE ENDPOINT. api/admin/db.js and
//      api/admin/stats.js stay SELECT-only -- proven by a source lint that
//      strips comments first and is itself proven able to see a real violation.
//   2. The write endpoint is SEPARATELY GATED (a second secret), POST-only, and
//      refuses in stable machine codes rather than prose.
//   3. Every write is ATTRIBUTABLE AND TIMESTAMPED on the row it changes.
//   4. Nothing renders a wallet, an email or a real name.
//   5. The served page is ASCII, phone-sized, and states its state in WORDS
//      (the owner is red/green colourblind).
//   6. No secret is ever logged.
//
// ⭐ EVERY ASSERTION HERE WAS PROVEN RED FIRST (WO-1138). The mutation list is
// in the WO-1244 handback; a test that has never failed proves nothing.
//
//   node --test test/command-center.test.js
// =============================================================================

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const {
    OPS_ACTIONS,
    OpsError,
    createPromo,
    keyOk,
    normalizeOperator,
    normalizePromoCode,
    optionalCount,
    optionalExpiry,
    setMaintenance,
    setPromoActive,
    validateOpen,
    validatePromoDraft,
    validateSeal,
} = require('../api/_lib/ops');
const { AREAS } = require('../api/_lib/maintenance');
const consoleEndpoint = require('../api/admin/console');

const REPO = path.join(__dirname, '..');
const readSrc = (rel) => fs.readFileSync(path.join(REPO, rel), 'utf8');

// -- harness -----------------------------------------------------------------

// A recording tagged-template sql() that answers from a queued script, so every
// query the code issues is both DRIVEN and INSPECTED.
function mockSql(script) {
    const calls = [];
    const queue = script.slice();
    const sql = async (strings, ...values) => {
        const text = strings.join('?');
        calls.push({ text, values });
        const next = queue.shift();
        if (next && next.throws) throw next.throws;
        return next ? next.rows : [];
    };
    sql.calls = calls;
    return sql;
}

// Minimal req/res doubles. The endpoint only uses status/json/setHeader/send.
function fakeRes() {
    const out = { statusCode: null, body: null, headers: {}, sent: null };
    const res = {
        setHeader(k, v) { out.headers[String(k).toLowerCase()] = v; },
        status(code) { out.statusCode = code; return res; },
        json(obj) { out.body = obj; return res; },
        send(s) { out.sent = s; return res; },
        end() { return res; },
    };
    res.out = out;
    return res;
}

function fakeReq(method, headers, body) {
    return { method: method, headers: headers || {}, body: body, query: {} };
}

// Run the ops endpoint with a controlled environment, then put the environment
// back exactly as it was. No test may leak an admin key into another.
async function runOps(env, req) {
    const keep = {
        ADMIN_DASH_KEY: process.env.ADMIN_DASH_KEY,
        ADMIN_OPS_KEY: process.env.ADMIN_OPS_KEY,
        DATABASE_URL: process.env.DATABASE_URL,
    };
    for (const k of Object.keys(keep)) {
        if (Object.prototype.hasOwnProperty.call(env, k)) {
            if (env[k] === undefined) delete process.env[k];
            else process.env[k] = env[k];
        }
    }
    // Required fresh: the handler reads process.env at call time, not at import.
    delete require.cache[require.resolve('../api/admin/ops')];
    const handler = require('../api/admin/ops');
    const res = fakeRes();
    try {
        await handler(req, res);
    } finally {
        for (const k of Object.keys(keep)) {
            if (keep[k] === undefined) delete process.env[k];
            else process.env[k] = keep[k];
        }
    }
    return res.out;
}

// Strip // and /* */ comments while leaving string literals intact, so a lint
// over CODE cannot be fooled by a comment that happens to contain the pattern --
// and, just as importantly, cannot be DEFEATED by one either.
function stripComments(src) {
    let out = '';
    let i = 0;
    let quote = null;
    while (i < src.length) {
        const c = src[i];
        const d = src[i + 1];
        if (quote) {
            if (c === '\\') { out += '  '; i += 2; continue; }
            if (c === quote) quote = null;
            out += c; i += 1; continue;
        }
        if (c === '"' || c === "'" || c === '`') { quote = c; out += c; i += 1; continue; }
        if (c === '/' && d === '/') {
            while (i < src.length && src[i] !== '\n') { out += ' '; i += 1; }
            continue;
        }
        if (c === '/' && d === '*') {
            while (i < src.length && !(src[i] === '*' && src[i + 1] === '/')) {
                out += src[i] === '\n' ? '\n' : ' '; i += 1;
            }
            out += '  '; i += 2; continue;
        }
        out += c; i += 1;
    }
    return out;
}

function allMatches(text, re) {
    const found = [];
    let m;
    const rx = new RegExp(re.source, re.flags.includes('g') ? re.flags : re.flags + 'g');
    while ((m = rx.exec(text)) !== null) {
        found.push(m[0]);
        if (m.index === rx.lastIndex) rx.lastIndex += 1;
    }
    return found;
}

// The lint itself. Uppercase-only on purpose: every statement in this repo is
// written in uppercase SQL, while English prose in a note string ("writes live
// at ...") is lower case. A case-insensitive rule would fire on the prose and be
// switched off within a week, which is worse than no rule.
const WRITE_VERB = /\b(INSERT\s+INTO|UPDATE\s+[a-z_]+\s+SET|DELETE\s+FROM|TRUNCATE|DROP\s+TABLE|ALTER\s+TABLE|CREATE\s+TABLE)\b/g;

function writeVerbsIn(rel) {
    return allMatches(stripComments(readSrc(rel)), WRITE_VERB);
}

// -- 1. THE READ/WRITE BOUNDARY ----------------------------------------------

test('the lint can actually SEE a write -- proven on synthetic input, both ways', () => {
    // If this ever stops failing, every "no writes" assertion below is vacuous.
    const violation = 'const x = await sql`INSERT INTO promo_codes (code) VALUES (${c})`;';
    assert.deepEqual(allMatches(stripComments(violation), WRITE_VERB), ['INSERT INTO']);

    const update = 'await sql`UPDATE purchase_entitlements SET status = ${s}`;';
    assert.deepEqual(allMatches(stripComments(update), WRITE_VERB), ['UPDATE purchase_entitlements SET']);

    const del = 'await sql`DELETE FROM bug_reports WHERE report_id = ${id}`;';
    assert.deepEqual(allMatches(stripComments(del), WRITE_VERB), ['DELETE FROM']);

    // And a write hidden in a COMMENT is not a write. A lint that cannot tell
    // the difference gets muted by the first honest explanatory comment.
    const commented = '// we do NOT do: INSERT INTO promo_codes\nconst y = 1;';
    assert.deepEqual(allMatches(stripComments(commented), WRITE_VERB), []);
});

test('api/admin/db.js is SELECT-only -- the read contract, not a phase', () => {
    assert.deepEqual(writeVerbsIn('api/admin/db.js'), []);
});

test('api/admin/stats.js is SELECT-only, including the new WO-1244 ops view', () => {
    assert.deepEqual(writeVerbsIn('api/admin/stats.js'), []);
});

test('the ops READ view exists on the read endpoint and is reachable', () => {
    const src = stripComments(readSrc('api/admin/stats.js'));
    assert.match(src, /if \(view === 'ops'\) \{/);
    // A view that is not in the "unknown view" hint is a view nobody finds.
    assert.match(src, /purchases \| ops \| players/);
});

test('the WRITE half is a DIFFERENT FILE, and the read files never reach it', () => {
    assert.ok(fs.existsSync(path.join(REPO, 'api/admin/ops.js')));
    for (const rel of ['api/admin/db.js', 'api/admin/stats.js']) {
        const src = stripComments(readSrc(rel));
        // ⚠ NAMING the write endpoint in a note string is fine and wanted - the
        // read response tells its reader where writes live. REACHING it is what
        // must never happen, so the assertion is about imports and calls, not
        // about prose. (Written this way after the prose form fired on the
        // response note that exists precisely to keep the halves legible.)
        assert.doesNotMatch(src, /require\([^)]*_lib\/ops[^)]*\)/,
            rel + ' must not import the write library');
        assert.doesNotMatch(src, /require\([^)]*admin\/ops[^)]*\)/,
            rel + ' must not import the write endpoint');
        assert.doesNotMatch(src, /fetch\([^)]*admin\/ops/,
            rel + ' must not call the write endpoint');
    }
});

test('the write endpoint writes exactly four things, and money is not one of them', () => {
    assert.deepEqual(OPS_ACTIONS, [
        'maintenance.seal', 'maintenance.open', 'promo.create', 'promo.set_active',
    ]);
    // ⛔ The load-bearing one: no admin surface may write the money tables.
    const src = stripComments(readSrc('api/_lib/ops.js')) + stripComments(readSrc('api/admin/ops.js'));
    assert.doesNotMatch(src, /purchase_entitlements/);
    assert.doesNotMatch(src, /purchase_quotes/);
});

// -- 2. SEPARATE GATING ------------------------------------------------------

test('the write endpoint refuses everything that is not a POST', async () => {
    for (const method of ['GET', 'PUT', 'DELETE', 'HEAD', 'OPTIONS']) {
        const out = await runOps({ ADMIN_DASH_KEY: 'read', ADMIN_OPS_KEY: 'write' },
            fakeReq(method, {}));
        assert.equal(out.statusCode, 400, method + ' must be refused');
        assert.equal(out.body.code, 'METHOD_NOT_ALLOWED');
    }
});

test('the write endpoint needs the READ key AND a SECOND write key', async () => {
    const body = { action: 'maintenance.open', area: 'raiding' };

    // No key at all.
    let out = await runOps({ ADMIN_DASH_KEY: 'read', ADMIN_OPS_KEY: 'write' },
        fakeReq('POST', {}, body));
    assert.equal(out.body.code, 'UNAUTHORIZED');

    // The READ key alone is NOT enough. This is the whole point of the second
    // secret: the read key is typed into a phone in public and ends up in
    // screenshots, and that must never become the power to seal the game.
    out = await runOps({ ADMIN_DASH_KEY: 'read', ADMIN_OPS_KEY: 'write' },
        fakeReq('POST', { 'x-admin-key': 'read' }, body));
    assert.equal(out.body.code, 'OPS_UNAUTHORIZED');

    // Wrong write key.
    out = await runOps({ ADMIN_DASH_KEY: 'read', ADMIN_OPS_KEY: 'write' },
        fakeReq('POST', { 'x-admin-key': 'read', 'x-admin-ops-key': 'nope' }, body));
    assert.equal(out.body.code, 'OPS_UNAUTHORIZED');

    // Wrong read key, right write key. Still refused, and refused on the READ
    // key first -- neither is sufficient alone.
    out = await runOps({ ADMIN_DASH_KEY: 'read', ADMIN_OPS_KEY: 'write' },
        fakeReq('POST', { 'x-admin-key': 'nope', 'x-admin-ops-key': 'write' }, body));
    assert.equal(out.body.code, 'UNAUTHORIZED');
});

test('an unconfigured write key FAILS CLOSED and says so with the remedy', async () => {
    const out = await runOps({ ADMIN_DASH_KEY: 'read', ADMIN_OPS_KEY: undefined },
        fakeReq('POST', { 'x-admin-key': 'read', 'x-admin-ops-key': 'anything' },
            { action: 'maintenance.open', area: 'raiding' }));
    assert.equal(out.statusCode, 400);
    assert.equal(out.body.code, 'OPS_WRITE_NOT_CONFIGURED');
    // ⚠ The remedy must be IN the response. During an incident the owner is on a
    // phone with no log access, and "Unauthorized" would send her hunting a key
    // she typed correctly.
    assert.match(out.body.hint, /ADMIN_OPS_KEY/);

    // ⛔ AND IT IS THE OPPOSITE OF api/_lib/maintenance.js, ON PURPOSE. There an
    // unreadable table leaves the game OPEN (availability). Here a missing key
    // refuses the write (correctness). Never unify the two.
    assert.equal(out.body.ok, false);
});

test('an unconfigured ADMIN_DASH_KEY refuses too -- the admin surface never fails open', async () => {
    const out = await runOps({ ADMIN_DASH_KEY: undefined, ADMIN_OPS_KEY: 'write' },
        fakeReq('POST', { 'x-admin-key': 'read', 'x-admin-ops-key': 'write' },
            { action: 'maintenance.open', area: 'raiding' }));
    assert.equal(out.body.code, 'ADMIN_NOT_CONFIGURED');
});

test('an unknown action is refused BEFORE a database handle is ever made', async () => {
    const out = await runOps({ ADMIN_DASH_KEY: 'r', ADMIN_OPS_KEY: 'w', DATABASE_URL: undefined },
        fakeReq('POST', { 'x-admin-key': 'r', 'x-admin-ops-key': 'w' },
            { action: 'purchase.refund', sku: 'x' }));
    assert.equal(out.body.code, 'UNKNOWN_ACTION');
    assert.match(out.body.hint, /maintenance\.seal/);
});

test('the write endpoint sets NO CORS header -- same-origin only, like cleanup.js', () => {
    const src = stripComments(readSrc('api/admin/ops.js'));
    assert.doesNotMatch(src, /Access-Control-Allow-Origin/);
    assert.doesNotMatch(src, /applyCors/);
});

test('the key comparison is constant time and length-blind', () => {
    assert.equal(keyOk('abc', 'abc'), true);
    assert.equal(keyOk('abc', 'abcd'), false);      // would THROW on a raw timingSafeEqual
    assert.equal(keyOk('', 'abc'), false);
    assert.equal(keyOk(null, 'abc'), false);
    assert.equal(keyOk('abc', undefined), false);   // no key configured is never a pass
    assert.equal(keyOk(undefined, undefined), false);
});

// -- 3. ATTRIBUTION AND TIMESTAMP --------------------------------------------

test('a seal stamps WHO and WHEN on the row, and reads the row back as proof', async () => {
    const sql = mockSql([{ rows: [{
        area_id: 'raiding', closed: true, message: 'Raids are closed while we patch.',
        updated_by: 'console', updated_at: '2026-08-27T10:00:00.000Z',
    }] }]);
    const row = await setMaintenance(sql, 'raiding', true, 'Raids are closed while we patch.', 'console');

    const q = sql.calls[0];
    assert.match(q.text, /INSERT INTO maintenance_toggles/);
    assert.match(q.text, /updated_by/);
    // NOW() from the DATABASE clock, never a timestamp the caller supplied.
    assert.match(q.text, /updated_at = NOW\(\)/);
    // UPSERT, never DO NOTHING: schema.sql seeds with DO NOTHING and does not
    // back-fill an older database. A SEAL that silently did not write would be a
    // disaster, so the write path can never be the no-op branch.
    assert.match(q.text, /ON CONFLICT \(area_id\) DO UPDATE/);
    assert.doesNotMatch(q.text, /DO NOTHING/);
    assert.match(q.text, /RETURNING/);

    assert.deepEqual(q.values, ['raiding', true, 'Raids are closed while we patch.', 'console']);
    assert.equal(row.updated_by, 'console');
});

test('a write that returns no row is a FAILURE, never a silent success', async () => {
    const sql = mockSql([{ rows: [] }]);
    await assert.rejects(() => setMaintenance(sql, 'arena', true, 'closed', 'console'),
        (e) => e instanceof OpsError && e.code === 'WRITE_RETURNED_NO_ROW');
});

test('opening an area CLEARS the banner text with the seal', async () => {
    const sql = mockSql([{ rows: [{ area_id: 'arena', closed: false, message: null,
        updated_by: 'console', updated_at: 'x' }] }]);
    // The endpoint passes null for message on an open; a stale "closed for
    // maintenance" banner over an open area is a lie the players would read.
    await setMaintenance(sql, 'arena', false, null, 'console');
    assert.equal(sql.calls[0].values[2], null);
});

test('the operator label is an operator label, capped and ASCII', () => {
    assert.equal(normalizeOperator(undefined), 'console');
    assert.equal(normalizeOperator(''), 'console');
    assert.equal(normalizeOperator('  night-shift '), 'night-shift');
    assert.equal(normalizeOperator('x'.repeat(200)).length, 64);
    assert.throws(() => normalizeOperator('opérateur'),
        (e) => e.code === 'OPERATOR_NOT_ASCII');
});

test('every write leaves a history row, and it is not billed to a player', () => {
    const src = stripComments(readSrc('api/_lib/ops.js'));
    assert.match(src, /admin_ops_write/);
    // 'anonymous' is the id api/admin/stats.js excludes from every distinct-player
    // metric. An operator row must never surface as a person who plays the game.
    assert.match(src, /logApiEvent\(sql, 'anonymous', OPS_AUDIT_EVENT/);

    // And the console can read that history back -- an audit trail nobody can
    // see from the surface that wrote it is not an audit trail.
    assert.match(stripComments(readSrc('api/admin/stats.js')), /event_name = 'admin_ops_write'/);
});

// -- 4. VALIDATION (the refusals the console must inherit, not re-invent) -----

test('a seal without a message is REFUSED -- the banner has nothing to say', () => {
    assert.throws(() => validateSeal({ area: 'raiding' }),
        (e) => e.code === 'MESSAGE_REQUIRED_TO_SEAL');
    assert.throws(() => validateSeal({ area: 'raiding', message: '   ' }),
        (e) => e.code === 'MESSAGE_REQUIRED_TO_SEAL');
});

test('a seal message is ASCII and bounded -- the in-game banner font and width', () => {
    assert.throws(() => validateSeal({ area: 'raiding', message: 'café closed' }),
        (e) => e.code === 'MESSAGE_NOT_ASCII');
    assert.throws(() => validateSeal({ area: 'raiding', message: 'x'.repeat(201) }),
        (e) => e.code === 'MESSAGE_TOO_LONG');
    const ok = validateSeal({ area: 'raiding', message: 'x'.repeat(200) });
    assert.equal(ok.area, 'raiding');
});

test('there are six areas and there is no seventh', () => {
    assert.deepEqual(AREAS, ['farming', 'raiding', 'arena', 'dungeons', 'store', 'server']);
    for (const a of AREAS) {
        assert.equal(validateOpen({ area: a.toUpperCase() }).area, a);
    }
    for (const bad of ['', 'shop', 'raid', 'everything', null]) {
        assert.throws(() => validateOpen({ area: bad }), (e) => e.code === 'UNKNOWN_AREA');
    }
});

test('a promo code is normalised the way the CLIENT normalises it', () => {
    // PromoCodeService.cs does Trim().ToUpperInvariant() BEFORE sending, and
    // schema.sql says store + compare uppercase. A lower-case code authored here
    // would be unredeemable and would read as a backend bug.
    assert.equal(normalizePromoCode('  launch2026 '), 'LAUNCH2026');
    assert.equal(normalizePromoCode('a-b_c'), 'A-B_C');
    assert.throws(() => normalizePromoCode(''), (e) => e.code === 'PROMO_CODE_REQUIRED');
    assert.throws(() => normalizePromoCode('AB'), (e) => e.code === 'PROMO_CODE_TOO_SHORT');
    assert.throws(() => normalizePromoCode('X'.repeat(33)), (e) => e.code === 'PROMO_CODE_TOO_LONG');
    assert.throws(() => normalizePromoCode('DROP TABLE'), (e) => e.code === 'PROMO_CODE_CHARSET');
    assert.throws(() => normalizePromoCode('CAFÉ'), (e) => e.code === 'PROMO_CODE_CHARSET');
});

test('THE EMPTY-STRING TRAP: a blank cap means UNLIMITED, never zero', () => {
    // An untouched HTML number input POSTs "". Number('') is 0, not NaN -- so a
    // naive read would author max_redemptions = 0, a code nobody can ever
    // redeem, while the schema's own meaning for the field is "NULL = unlimited".
    assert.equal(optionalCount('', 'max', 100), null);
    assert.equal(optionalCount('   ', 'max', 100), null);
    assert.equal(optionalCount(null, 'max', 100), null);
    assert.equal(optionalCount(undefined, 'max', 100), null);
    assert.equal(optionalCount('0', 'max', 100), 0);      // an EXPLICIT zero is still zero
    assert.equal(optionalCount(7, 'max', 100), 7);
    assert.throws(() => optionalCount('12.5', 'max', 100), (e) => e.code === 'NOT_A_WHOLE_NUMBER');
    assert.throws(() => optionalCount('-1', 'max', 100), (e) => e.code === 'NOT_A_WHOLE_NUMBER');
    assert.throws(() => optionalCount('lots', 'max', 100), (e) => e.code === 'NOT_A_WHOLE_NUMBER');
    assert.throws(() => optionalCount('101', 'max', 100), (e) => e.code === 'VALUE_TOO_LARGE');
});

test('a draft that sets BOTH a pack sku and coins is REFUSED, not silently resolved', () => {
    // schema.sql section 3: when reward_pack_sku is set it WINS and the
    // crystal/coin columns are ignored. Picking silently is how an operator finds
    // out from a player that the code granted the wrong thing.
    assert.throws(
        () => validatePromoDraft({ code: 'MIXED1', rewardPackSku: 'hearth-spark', rewardCoins: 100 }),
        (e) => e.code === 'REWARD_AMBIGUOUS');

    assert.throws(() => validatePromoDraft({ code: 'EMPTY1' }),
        (e) => e.code === 'REWARD_EMPTY');
    assert.throws(() => validatePromoDraft({ code: 'EMPTY2', rewardCoins: '0', rewardCrystals: '' }),
        (e) => e.code === 'REWARD_EMPTY');
});

test('a promo draft lands on exactly the columns promo_codes holds', () => {
    const d = validatePromoDraft({
        code: ' launch2026 ', rewardCrystals: '500', rewardCoins: '',
        message: 'Thanks for playing', maxRedemptions: '', perPlayerLimit: '1',
        expiresAt: '',
    });
    assert.deepEqual(d, {
        code: 'LAUNCH2026',
        rewardCrystals: 500,
        rewardCoins: 0,
        rewardPackSku: null,
        message: 'Thanks for playing',
        maxRedemptions: null,
        perPlayerLimit: 1,
        expiresAt: null,
        active: true,
    });
});

test('a code that is born expired is refused at authoring time', () => {
    const now = Date.parse('2026-08-27T12:00:00.000Z');
    assert.throws(() => optionalExpiry('2026-08-26T12:00:00.000Z', now),
        (e) => e.code === 'EXPIRY_IN_THE_PAST');
    assert.throws(() => optionalExpiry('not a date', now), (e) => e.code === 'BAD_EXPIRY');
    assert.equal(optionalExpiry('', now), null);
    assert.equal(optionalExpiry('2026-09-01T00:00:00.000Z', now), '2026-09-01T00:00:00.000Z');
});

test('authoring never OVERWRITES an existing code -- that would be an edit', async () => {
    const sql = mockSql([{ rows: [] }]);   // ON CONFLICT DO NOTHING -> no row back
    const draft = validatePromoDraft({ code: 'TAKEN1', rewardCoins: '10' });
    await assert.rejects(() => createPromo(sql, draft, 'console'),
        (e) => e instanceof OpsError && e.code === 'PROMO_CODE_EXISTS');
});

test('a missing created_by column degrades ONE step and still authors the code', async () => {
    // There is no migration runner in this repo: a migration is a human running a
    // file, and a deploy can beat them to it. The cascade exists for exactly that
    // window -- and it retries ONLY 42703.
    const undefinedColumn = Object.assign(new Error('column "created_by" does not exist'),
        { code: '42703' });
    const sql = mockSql([
        { throws: undefinedColumn },
        { rows: [{ code: 'FALLBK', active: true, created_at: 'now' }] },
    ]);
    const draft = validatePromoDraft({ code: 'FALLBK', rewardCoins: '10' });
    const made = await createPromo(sql, draft, 'console');
    assert.equal(made.shape, 'without_created_by');
    assert.equal(sql.calls.length, 2);
    assert.match(sql.calls[0].text, /created_by/);
    assert.doesNotMatch(sql.calls[1].text, /created_by/);
});

test('ANY OTHER database error rethrows untouched -- the cascade is not a swallow', async () => {
    const boom = Object.assign(new Error('connection reset'), { code: '08006' });
    const sql = mockSql([{ throws: boom }]);
    const draft = validatePromoDraft({ code: 'BOOM01', rewardCoins: '10' });
    await assert.rejects(() => createPromo(sql, draft, 'console'), /connection reset/);
    assert.equal(sql.calls.length, 1, 'a non-42703 error must not be retried');
});

test('disabling a code is an UPDATE of active, never a DELETE', async () => {
    const sql = mockSql([{ rows: [{ code: 'OLD123', active: false }] }]);
    await setPromoActive(sql, 'OLD123', false);
    assert.match(sql.calls[0].text, /UPDATE promo_codes/);
    assert.match(sql.calls[0].text, /SET active = /);
    // promo_redemptions.code is FK'd ON DELETE CASCADE, so deleting the row
    // would take its redemption history with it.
    assert.doesNotMatch(sql.calls[0].text, /DELETE/);

    const missing = mockSql([{ rows: [] }]);
    await assert.rejects(() => setPromoActive(missing, 'NOPE99', false),
        (e) => e.code === 'PROMO_CODE_NOT_FOUND');
});

// -- 5. PRIVACY --------------------------------------------------------------

test('the ops READ view never selects a wallet address', () => {
    const src = stripComments(readSrc('api/admin/stats.js'));
    const block = src.slice(src.indexOf("if (view === 'ops')"), src.indexOf("=== 'players'"));
    assert.ok(block.length > 500, 'the ops block must be found');

    // bound_wallet may be TESTED for null-ness and never SELECTED. Asserted on
    // the SQL shape, not on every mention: the response note explains the rule in
    // prose ("bound_wallet is reported as is_bound only"), and a rule that fires
    // on its own explanation gets deleted rather than obeyed.
    assert.match(block, /\(bound_wallet IS NOT NULL\) AS is_bound/);
    assert.doesNotMatch(block, /AS bound_wallet\b/);
    assert.doesNotMatch(block, /bound_wallet,/);
    assert.doesNotMatch(block, /SELECT[^;]*\bbound_wallet\b(?![^\n]*IS NOT NULL)/);
    // Same for the report wallet: presence, never the address. The column may be
    // TESTED for null-ness, and the resulting BOOLEAN may be read; the address
    // itself may never appear in a select list.
    assert.doesNotMatch(block, /AS wallet\b/, 'no query here may return a raw wallet column');
    for (const hit of allMatches(block, /[^_]wallet[^\n]*/g)) {
        assert.ok(/IS NOT NULL\) AS wallet_verified/.test(hit) || /^\.wallet_verified /.test(hit),
            'a wallet may only be reduced to a boolean: ' + hit);
    }
    // And the player id is masked on the way out.
    assert.match(block, /player_masked: maskId\(r\.player_id\)/);
});

test('the served page cannot render a wallet, an email or a real name', () => {
    const page = consoleEndpoint.PAGE;
    // The ONLY wallet FIELD the page may read is the already-masked one stats.js
    // emits (first4...last4). Prose that mentions wallets is fine and useful; a
    // FIELD ACCESS is what puts an address on a screen.
    for (const hit of allMatches(page, /\.\s*wallet[A-Za-z_]*/g)) {
        assert.equal(hit, '.wallet_masked', 'unexpected wallet field on the page: ' + hit);
    }
    assert.doesNotMatch(page, /bound_wallet/);
    assert.doesNotMatch(page, /\bemail\b/i);
    assert.doesNotMatch(page, /player_id\b/);      // masked ids only
    // Every value the page prints goes through esc() -- a player-authored bug
    // description is untrusted text and must never become markup.
    assert.match(page, /function esc\(v\)/);
    assert.match(page, /esc\(x\.description\)/);
});

test('no secret is ever logged, anywhere in the write half', () => {
    for (const rel of ['api/_lib/ops.js', 'api/admin/ops.js', 'api/admin/console.js']) {
        const src = stripComments(readSrc(rel));
        for (const line of src.split('\n')) {
            if (!/console\.(log|warn|error|info)/.test(line)) continue;
            assert.doesNotMatch(line, /ADMIN_DASH_KEY|ADMIN_OPS_KEY|DATABASE_URL|process\.env/,
                rel + ' logs an environment value: ' + line.trim());
        }
        // And no key is ever hardcoded as a fallback "just while testing".
        assert.doesNotMatch(src, /ADMIN_(DASH|OPS)_KEY\s*\|\|\s*['"]/);
    }
});

test('the console never persists a key', () => {
    const page = consoleEndpoint.PAGE;
    assert.doesNotMatch(page, /localStorage/);
    assert.doesNotMatch(page, /sessionStorage/);
    assert.doesNotMatch(page, /document\.cookie/);
    // The key travels in a HEADER, never in the query string.
    assert.match(page, /'X-Admin-Key': READ_KEY/);
    assert.match(page, /'X-Admin-Ops-Key':OPS_KEY/);
    assert.doesNotMatch(page, /[?&]key=/);
});

// -- 6. THE PAGE ITSELF ------------------------------------------------------

test('the served page is 7-bit ASCII from end to end', () => {
    const page = consoleEndpoint.PAGE;
    const bad = [];
    for (let i = 0; i < page.length; i++) {
        const c = page.charCodeAt(i);
        if (c > 126 || (c < 32 && c !== 10 && c !== 13 && c !== 9)) {
            bad.push(page.charAt(i) + ' (U+' + c.toString(16) + ')');
        }
    }
    assert.deepEqual(bad, []);
});

test('state is carried by WORDS, never by hue -- the owner is red/green colourblind', () => {
    const statsSrc = stripComments(readSrc('api/admin/stats.js'));
    // The READ side emits the word, so the page cannot invent one from a boolean.
    assert.match(statsSrc, /state: closed \? 'CLOSED' : 'open'/);
    assert.match(statsSrc, /'DISABLED' : expired \? 'EXPIRED' : capped \? 'FULLY REDEEMED' : 'ACTIVE'/);

    const opsSrc = stripComments(readSrc('api/admin/ops.js'));
    assert.match(opsSrc, /state: row\.closed \? 'SEALED' : 'open'/);

    const page = consoleEndpoint.PAGE;
    for (const word of ['SERVER IS CLOSED', 'ALERT', 'verified', 'unverified', 'unlimited', 'never']) {
        assert.ok(page.indexOf(word) >= 0, 'the page must say "' + word + '" in words');
    }
    // A state chip prints esc(a.state); it must not be derived from the boolean
    // alone, which is how a colour-only signal creeps back in.
    assert.match(page, /esc\(a\.state\)/);
});

test('the page is built for a phone: one column, big targets, no framework', () => {
    const page = consoleEndpoint.PAGE;
    assert.match(page, /<meta name="viewport" content="width=device-width, initial-scale=1/);
    assert.match(page, /--tap:48px/);
    assert.match(page, /min-height:var\(--tap\)/);
    assert.match(page, /@media \(max-width:520px\)/);
    // Wide content scrolls INSIDE its own container; the page body never does.
    assert.match(page, /\.scroll\{overflow-x:auto/);
    // No build step, no CDN, no remote anything.
    assert.doesNotMatch(page, /<script[^>]+src=/);
    assert.doesNotMatch(page, /https?:\/\//);
});

test('the default command-center view shows real daily active-player telemetry', () => {
    const page = consoleEndpoint.PAGE;
    // The metric already has one authority: stats.js overview. The console must
    // consume it rather than counting rows or inventing a second definition.
    assert.match(page, /state = \{ tab:'players'/);
    assert.match(page, /stats\?view=overview&days=/);
    assert.match(page, /Active players in the last 24 hours/);
    assert.match(page, /Daily active players/);
    assert.match(page, /Identified coverage/);
    assert.match(page, /Anonymous sessions/);
});

test('the page script PARSES -- a syntax error here is a blank screen, silently', () => {
    // The page is one string in a .js file, so nothing else in this repo ever
    // parses it. A stray bracket ships as a completely blank console and the
    // first anyone knows is the owner staring at nothing during an incident.
    const page = consoleEndpoint.PAGE;
    const open = page.indexOf('<script>');
    const close = page.lastIndexOf('</script>');
    assert.ok(open > 0 && close > open, 'the page must carry exactly one inline script');
    const js = page.slice(open + '<script>'.length, close);
    assert.ok(js.length > 2000);
    // Parses without evaluating. Throws SyntaxError on a malformed page.
    // eslint-disable-next-line no-new-func
    assert.doesNotThrow(() => new Function(js));

    // And nothing inside the script can close the tag early.
    assert.equal(js.indexOf('</script'), -1);
});

test('the page is unlisted and uncacheable, and refuses non-GET', async () => {
    const res = fakeRes();
    await consoleEndpoint(fakeReq('GET', {}), res);
    assert.equal(res.out.statusCode, 200);
    assert.equal(res.out.headers['content-type'], 'text/html; charset=utf-8');
    assert.match(res.out.headers['x-robots-tag'], /noindex/);
    assert.match(res.out.headers['cache-control'], /no-store/);
    assert.equal(res.out.sent, consoleEndpoint.PAGE);

    const res2 = fakeRes();
    await consoleEndpoint(fakeReq('POST', {}), res2);
    assert.equal(res2.out.statusCode, 400);
    assert.equal(res2.out.sent, null, 'the page must not be served on a POST');
});

test('the page shows client-reported and server-settled SIDE BY SIDE, never blended', () => {
    const page = consoleEndpoint.PAGE;
    // Two separate tiles reading two separate fields. WO-1169 section 3: a
    // blended figure would hide the disagreement, and the disagreement IS the
    // alert.
    assert.match(page, /Client reported/);
    assert.match(page, /Server settled/);
    assert.match(page, /d\.client_completed_events/);
    assert.match(page, /d\.server_settled_window/);
    // The orphan list is rendered as an ALERT with a count, not a footnote.
    assert.match(page, /ALERT: ' \+ orphans\.length \+ ' client purchase\(s\) with NO server entitlement/);
    // ⛔ And the console offers no way to reconcile it. Re-granting is a write on
    // the money tables and it exists nowhere in this surface.
    //
    // ⚠ ASSERTED ON THE ACTIONS THE PAGE CAN POST, NEVER ON ITS PROSE. The page
    // says the words "cannot re-grant" out loud - which is the OPPOSITE of a
    // violation - so a prose-matching rule fired on the very sentence that makes
    // the boundary legible, and would have been deleted rather than obeyed.
    const posted = allMatches(page, /action:\s*'[a-z._]+'/g)
        .map(s => s.replace(/.*'([a-z._]+)'.*/, '$1'));
    assert.deepEqual(posted.slice().sort(), OPS_ACTIONS.slice().sort());
});

test('the two ticket systems are named and kept apart', () => {
    const page = consoleEndpoint.PAGE;
    // BOARD.html is GENERATED from WorkOrders/*.md, so anything written into it
    // is overwritten on the next run. Player issues are a different board.
    assert.match(page, /BOARD\.html/);
    assert.match(page, /board_build\.py/);
    assert.match(page, /Do not fold them into BOARD\.html/);
});
