'use strict';

// =============================================================================
// test/admin.skus.view.test.js - WO-1532.
//   the oracle for GET /api/admin/stats?view=skus and the SKUs tab it feeds.
//
// Owner ask 2026-09-06 20:52, verbatim:
//   "can we add a list in command center of All SKU's and contents"
//
// WHAT IS ACTUALLY BEING PROVEN, and why each case is here.
// -----------------------------------------------------------------------------
//  1 THE GATE. The view sits behind ADMIN_DASH_KEY like every other read on this
//    endpoint. ⚠ THE REFUSAL STATUS IS 400, NOT 401 - api/admin/stats.js returns
//    400 for 'Unauthorized', copied verbatim from api/admin/db.js so the admin
//    surface has ONE auth scheme rather than two that drift. The ticket asked for
//    401; the code is the authority and the ticket is corrected, not the code.
//    A refusal must also LOG one machine-readable line (WO-1244 reopened) naming
//    this view, or an owner bounce with no note is undiagnosable again.
//
//  2 IT OPENS NO DATABASE CONNECTION. Proven structurally: the whole view runs
//    with DATABASE_URL UNSET. If a statement were ever added it would throw here
//    rather than pass a lint that only reads source text.
//
//  3 THE PARITY COLUMNS, INCLUDING A GAP NOBODY HAS YET. Every real pack today
//    has an anchor or is promo-only, so the MISSING path is proven on a SYNTHETIC
//    pack driven through the PURE builder - no HTTP, no canonical file, no env.
//    A column whose failure state has never been executed is decoration.
//
//  4 THE COPY MATCHES ITS SOURCE. api/_lib/sku-catalog.generated.json is a copy
//    of the canonical packs.json, and a copy without an oracle is the duplicated
//    state this repo keeps paying for (CLAUDE.md s2/s5/s16). Drift goes RED.
//
//  5 THE SELECT-ONLY CONTRACT STILL HOLDS on api/admin/stats.js, re-asserted here
//    with the same lint test/command-center.test.js uses, because this ticket
//    edits that file.
//
// PROVEN RED FIRST: before the view existed, cases 1-3 and 6-9 failed on
// 'HTTP 400 / view unknown' and the module resolution of api/_lib/sku-catalog.
//
//   node --test test/admin.skus.view.test.js
// =============================================================================

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const REPO = path.join(__dirname, '..');
const readSrc = (rel) => fs.readFileSync(path.join(REPO, rel), 'utf8');

const STATS = '../api/admin/stats.js';
const KEY = 'sku-view-test-key';

// -- harness (shape borrowed verbatim from test/command-center.refusal-logging.test.js
//    so the admin endpoints are exercised ONE way, not two) --------------------

function fakeRes() {
    const out = { statusCode: null, body: null, headers: {}, sent: null };
    const res = {
        setHeader(k, v) { out.headers[String(k).toLowerCase()] = v; return res; },
        status(code) { out.statusCode = code; return res; },
        json(obj) { out.body = obj; return res; },
        send(s) { out.sent = s; return res; },
        end() { return res; },
    };
    res.out = out;
    return res;
}

function fakeReq(method, headers, query) {
    return { method: method, headers: headers || {}, query: query || {} };
}

function captureLog(fn) {
    const lines = [];
    const keep = { log: console.log, warn: console.warn, error: console.error, info: console.info };
    const grab = (...args) => { lines.push(args.map((a) => (typeof a === 'string' ? a : String(a))).join(' ')); };
    console.log = grab; console.warn = grab; console.error = grab; console.info = grab;
    return Promise.resolve()
        .then(fn)
        .then(
            (value) => { Object.assign(console, keep); return { value: value, lines: lines }; },
            (err) => { Object.assign(console, keep); throw err; },
        );
}

const REFUSAL_TAG = '[ops-refusal]';
function refusalRecords(lines) {
    return lines
        .filter((l) => l.indexOf(REFUSAL_TAG) >= 0)
        .map((l) => JSON.parse(l.slice(l.indexOf(REFUSAL_TAG) + REFUSAL_TAG.length).trim()));
}

const ENV_KEYS = ['ADMIN_DASH_KEY', 'ADMIN_OPS_KEY', 'DATABASE_URL'];

async function runStats(env, req) {
    const keep = {};
    for (const k of ENV_KEYS) keep[k] = process.env[k];
    for (const k of ENV_KEYS) {
        if (Object.prototype.hasOwnProperty.call(env, k)) {
            if (env[k] === undefined) delete process.env[k];
            else process.env[k] = env[k];
        }
    }
    delete require.cache[require.resolve(STATS)];
    const handler = require(STATS);
    const res = fakeRes();
    try {
        const captured = await captureLog(() => handler(req, res));
        return { out: res.out, lines: captured.lines };
    } finally {
        for (const k of ENV_KEYS) {
            if (keep[k] === undefined) delete process.env[k];
            else process.env[k] = keep[k];
        }
    }
}

// DATABASE_URL is UNSET on purpose in every success case below. It is the whole
// point of case 2: a view that needs no database must be unable to want one.
const NO_DB_ENV = { ADMIN_DASH_KEY: KEY, DATABASE_URL: undefined };

const skuRequest = (headers) => fakeReq('GET', headers, { view: 'skus' });

// =============================================================================
// 1. THE GATE
// =============================================================================

test('?view=skus with NO key is refused, and the refusal is LOGGED naming the view', async () => {
    const r = await runStats(NO_DB_ENV, skuRequest({}));

    // 400 is the real contract on this endpoint - see the header note above.
    assert.equal(r.out.statusCode, 400);
    assert.equal(r.out.body.error, 'Unauthorized');
    assert.equal(r.out.body.packs, undefined, 'a refused read must return no catalog');

    const records = refusalRecords(r.lines);
    assert.equal(records.length, 1, 'exactly one refusal line');
    assert.equal(records[0].endpoint, 'admin/stats');
    assert.equal(records[0].code, 'UNAUTHORIZED');
    assert.equal(records[0].view, 'skus', 'the line must name the view or it cannot be triaged');
    assert.equal(records[0].readKeySupplied, false);
    // The line is booleans and machine codes only. It ends up in a runtime log an
    // operator reads on a phone; a key or even a key LENGTH there would be worse
    // than no line at all.
    const encoded = JSON.stringify(records[0]);
    assert.ok(encoded.indexOf(KEY) < 0, 'the refusal line must never carry the key');
});

test('?view=skus with the WRONG key is refused', async () => {
    const r = await runStats(NO_DB_ENV, skuRequest({ 'x-admin-key': KEY + '-nope' }));
    assert.equal(r.out.statusCode, 400);
    assert.equal(r.out.body.error, 'Unauthorized');
    const records = refusalRecords(r.lines);
    assert.equal(records[0].readKeySupplied, true, 'a supplied-but-wrong key is a different diagnosis');
});

test('?view=skus is refused when no admin key is configured at all - never fail open', async () => {
    const r = await runStats({ ADMIN_DASH_KEY: undefined, DATABASE_URL: undefined },
                             skuRequest({ 'x-admin-key': KEY }));
    assert.equal(r.out.statusCode, 400);
    assert.equal(refusalRecords(r.lines)[0].code, 'ADMIN_NOT_CONFIGURED');
});

// =============================================================================
// 2 + 3. THE SHAPE, SERVED WITH NO DATABASE
// =============================================================================

test('?view=skus answers the whole catalog with DATABASE_URL unset', async () => {
    const r = await runStats(NO_DB_ENV, skuRequest({ 'x-admin-key': KEY }));

    assert.equal(r.out.statusCode, 200);
    const body = r.out.body;
    assert.equal(body.view, 'skus');
    assert.ok(typeof body.generated_at === 'string');

    const canonical = JSON.parse(readSrc('Assets/Resources/Data/Canonical/packs.json'));
    assert.equal(body.packs.length, canonical.packs.length,
                 'one row per authored pack - "All SKUs" means all of them');
    assert.deepEqual(body.packs.map((p) => p.sku), canonical.packs.map((p) => p.sku),
                     'authored order is preserved; a re-sort hides where a row was added');

    // No refusal line on a successful read: the log is for refusals only.
    assert.equal(refusalRecords(r.lines).length, 0);
});

test('every row carries the descriptive fields the owner asked for', async () => {
    const r = await runStats(NO_DB_ENV, skuRequest({ 'x-admin-key': KEY }));
    for (const row of r.out.body.packs) {
        for (const field of ['sku', 'name', 'tagline', 'tier', 'section', 'band',
                             'store_visible', 'founder_only', 'promo_grant_only',
                             'pricing', 'contents']) {
            assert.ok(Object.prototype.hasOwnProperty.call(row, field),
                      row.sku + ' is missing ' + field);
        }
        for (const c of ['usd', 'usdc', 'sol', 'skr']) {
            assert.ok(Object.prototype.hasOwnProperty.call(row.pricing, c),
                      row.sku + ' pricing is missing ' + c);
        }
        assert.equal(typeof row.store_visible, 'boolean');
        assert.ok(Array.isArray(row.contents.cosmetics));
        assert.ok(Array.isArray(row.contents.economy));
        assert.ok(Array.isArray(row.contents.convenience));
    }
});

test('contents are read from the canonical file, not summarised away', async () => {
    const r = await runStats(NO_DB_ENV, skuRequest({ 'x-admin-key': KEY }));
    const byId = new Map(r.out.body.packs.map((p) => [p.sku, p]));

    // A pack with all three content shapes, checked against the authored file so
    // this case fails if the reader ever starts reshaping the catalog.
    const canonical = JSON.parse(readSrc('Assets/Resources/Data/Canonical/packs.json'));
    const src = canonical.packs.find((p) => (p.contents.convenience || []).length > 0);
    assert.ok(src, 'the canonical file should still author a convenience grant');

    const row = byId.get(src.sku);
    assert.deepEqual(row.contents.cosmetics, src.contents.cosmetics);
    assert.equal(row.contents.economy.length, Object.keys(src.contents.economy || {}).length);
    for (const e of row.contents.economy) {
        assert.equal(e.amount, src.contents.economy[e.resource],
                     src.sku + ' ' + e.resource + ' amount must match the authored figure');
    }
    assert.equal(row.contents.convenience.length, src.contents.convenience.length);
    assert.equal(row.contents.convenience[0].kind, src.contents.convenience[0].kind);
    assert.equal(row.contents.convenience[0].count, src.contents.convenience[0].count);
    assert.equal(row.contents.convenience[0].description, src.contents.convenience[0].description,
                 'the human description is what makes the list readable - it must survive');
});

test('the parity columns are computed on the SERVER, against the real tables', async () => {
    const { USD_ANCHORS } = require('../api/_lib/purchase-catalog');
    const { PRODUCT_TYPES } = require('../api/_lib/google-play-purchases');

    const r = await runStats(NO_DB_ENV, skuRequest({ 'x-admin-key': KEY }));
    for (const row of r.out.body.packs) {
        const hasAnchor = Object.prototype.hasOwnProperty.call(USD_ANCHORS, row.sku);
        assert.equal(row.usd_anchor_present, hasAnchor, row.sku + ' anchor presence');
        assert.equal(row.usd_anchor, hasAnchor ? USD_ANCHORS[row.sku] : null,
                     row.sku + ' anchor value');

        const hasType = Object.prototype.hasOwnProperty.call(PRODUCT_TYPES, row.sku);
        assert.equal(row.play_product_type_present, hasType, row.sku + ' product type presence');
        assert.equal(row.play_product_type, hasType ? PRODUCT_TYPES[row.sku] : null);

        // "Could the store build sell it" is the AND, said out loud.
        assert.equal(row.sellable, hasAnchor && row.store_visible === true, row.sku + ' sellable');
        assert.ok(Array.isArray(row.parity_gaps));
    }
});

test('the two promo-grant-only packs are LISTED and are not counted as a gap', async () => {
    const r = await runStats(NO_DB_ENV, skuRequest({ 'x-admin-key': KEY }));
    const promo = r.out.body.packs.filter((p) => p.promo_grant_only);
    assert.ok(promo.length >= 1, 'packs.json still authors promo-only welcome packs');
    for (const p of promo) {
        assert.equal(p.sellable, false, 'a promo-only pack is never sellable');
        assert.deepEqual(p.parity_gaps, [],
                         p.sku + ' is never offered for sale, so a missing anchor is the design');
    }
});

test('anchors_without_pack names the Monthly Ledger cards instead of vanishing them', async () => {
    const r = await runStats(NO_DB_ENV, skuRequest({ 'x-admin-key': KEY }));
    const orphans = r.out.body.anchors_without_pack.map((o) => o.sku);
    // These two are authored in battle_monthly.json monthlyCards[], NOT packs.json.
    // A list titled "All SKUs" that silently omitted them would be the WO-1165 s2
    // defect in reverse, which is the whole reason the reverse column exists.
    assert.ok(orphans.indexOf('monthly-wayfarer') >= 0, 'monthly-wayfarer must be named');
    assert.ok(orphans.indexOf('monthly-keeper') >= 0, 'monthly-keeper must be named');
    assert.equal(r.out.body.counts.anchors_without_pack, orphans.length);
});

// =============================================================================
// 3b. THE MISSING PATH, ON A SYNTHETIC PACK - proven through the PURE builder
// =============================================================================

test('a synthetic pack with NO anchor and NO product type reports both gaps', () => {
    const catalog = require('../api/_lib/sku-catalog');

    const synthetic = {
        sku: 'ghost-of-elarion',
        name: 'Ghost of Elarion',
        tagline: 'A pack nobody wired up.',
        tier: 999,
        storeSection: 'featured',
        storeVisible: true,
        pricing: { usd: 9.99, usdc: 9.99, sol: 0.09, skr: 120 },
        contents: { cosmetics: [], economy: { wood: 10 }, convenience: [] },
    };

    const row = catalog.parityRow(synthetic, {}, {});

    assert.equal(row.usd_anchor_present, false);
    assert.equal(row.usd_anchor, null);
    assert.equal(row.play_product_type_present, false);
    assert.equal(row.play_product_type, null);
    assert.equal(row.sellable, false,
                 'storeVisible:true with no anchor is exactly the card that reads "Price unavailable"');
    assert.equal(row.parity_gaps.length, 2, 'both rails must be named, not just the first');
    assert.ok(row.parity_gaps.some((g) => g.indexOf('purchase-catalog') >= 0));
    assert.ok(row.parity_gaps.some((g) => g.indexOf('google-play-purchases') >= 0));
    // The row is still fully described. A SKU with a gap must not be reduced to
    // its gap - the operator needs to see what it WOULD have granted.
    assert.equal(row.name, 'Ghost of Elarion');
    assert.equal(row.contents.economy[0].resource, 'wood');
    assert.equal(row.contents.economy[0].amount, 10);
});

test('a synthetic pack that IS wired up carries no gap', () => {
    const catalog = require('../api/_lib/sku-catalog');
    const row = catalog.parityRow(
        { sku: 'ok-pack', storeVisible: true, pricing: { usd: 4.99 },
          contents: { cosmetics: [], economy: {}, convenience: [] } },
        { 'ok-pack': 4.99 },
        { 'ok-pack': 'consumable' });
    assert.deepEqual(row.parity_gaps, []);
    assert.equal(row.sellable, true);
    assert.equal(row.play_product_type, 'consumable');
});

test('an authored price that disagrees with the server anchor is reported, not averaged', () => {
    const catalog = require('../api/_lib/sku-catalog');
    const row = catalog.parityRow(
        { sku: 'drifted', storeVisible: true, pricing: { usd: 2.99 },
          contents: { cosmetics: [], economy: {}, convenience: [] } },
        { 'drifted': 4.99 },
        { 'drifted': 'consumable' });
    assert.equal(row.usd_anchor, 4.99);
    assert.equal(row.pricing.usd, 2.99);
    assert.equal(row.parity_gaps.length, 1);
    assert.ok(row.parity_gaps[0].indexOf('server figure is what the player is charged') >= 0,
              'the page must say WHICH number wins, not merely that they differ');
});

// =============================================================================
// 4. THE GENERATED COPY MATCHES ITS SOURCE
// =============================================================================

test('api/_lib/sku-catalog.generated.json equals the canonical packs.json', () => {
    const canonical = JSON.parse(readSrc('Assets/Resources/Data/Canonical/packs.json'));
    const generated = JSON.parse(readSrc('api/_lib/sku-catalog.generated.json'));
    assert.deepEqual(generated, canonical,
        'the copy has drifted. Re-run `node tools/gen-sku-catalog.mjs` - and never hand-edit ' +
        'the generated file: a copy with no oracle is the duplicated state this repo keeps ' +
        'paying for (CLAUDE.md s2/s5/s16).');
});

test('the generated copy is LF with a trailing newline and no BOM', () => {
    const raw = fs.readFileSync(path.join(REPO, 'api', '_lib', 'sku-catalog.generated.json'));
    const text = raw.toString('utf8');
    assert.ok(!text.startsWith('﻿'), 'no BOM');
    assert.ok(text.indexOf('\r') < 0, 'LF only - a CRLF rewrite is how canonical JSON gets mangled');
    assert.ok(text.endsWith('\n'), 'trailing newline');
});

// =============================================================================
// 5. THE READ-ONLY CONTRACT SURVIVES THIS TICKET
// =============================================================================

// Same lint as test/command-center.test.js: comments stripped so a write named in
// prose is not mistaken for a write, string literals left intact so a real one
// cannot hide in one.
function stripComments(src) {
    return src.replace(/\/\*[\s\S]*?\*\//g, ' ').replace(/(^|[^:])\/\/[^\n]*/g, '$1 ');
}
const WRITE_VERB = /\b(INSERT\s+INTO|UPDATE\s+[a-z_]+\s+SET|DELETE\s+FROM|TRUNCATE|DROP\s+TABLE|ALTER\s+TABLE|CREATE\s+TABLE)\b/g;

test('the lint can see a write - proven on synthetic input before it is trusted', () => {
    const violation = 'const x = await sql`INSERT INTO promo_codes (code) VALUES (${c})`;';
    assert.deepEqual(stripComments(violation).match(WRITE_VERB), ['INSERT INTO']);
    assert.equal(stripComments('// INSERT INTO promo_codes\nconst y = 1;').match(WRITE_VERB), null);
});

test('api/admin/stats.js is still SELECT-only after the skus view was added', () => {
    assert.equal(stripComments(readSrc('api/admin/stats.js')).match(WRITE_VERB), null);
});

test('the skus library and its generator touch no database at all', () => {
    for (const rel of ['api/_lib/sku-catalog.js', 'tools/gen-sku-catalog.mjs']) {
        const src = stripComments(readSrc(rel));
        assert.equal(src.match(WRITE_VERB), null, rel + ' must contain no write statement');
        assert.ok(src.indexOf('neon(') < 0, rel + ' must not open a database connection');
        assert.ok(src.indexOf('DATABASE_URL') < 0, rel + ' has no business knowing the DSN');
    }
});

// =============================================================================
// 6. THE CONSOLE TAB
// =============================================================================

test('the console renders a SKUs tab and fetches it through the read gate', () => {
    const page = require('../api/admin/console.js').PAGE;
    assert.ok(page.indexOf('data-tab="skus"') > 0, 'the tab button must exist');
    assert.ok(page.indexOf('view=skus') > 0, 'the page must read the new view');
    assert.ok(page.indexOf('renderSkus') > 0, 'the tab must have a renderer');
    // The word carries the state. The owner is red/green colourblind, so a gap
    // that existed only as a colour would be invisible to her.
    assert.ok(page.indexOf('MISSING') > 0, 'a parity gap is stated as a WORD');
});

test('the console still holds its keys in memory only - this ticket changed nothing there', () => {
    // Comments are stripped: the file HEADER says "Not localStorage, not
    // sessionStorage, not a cookie" in prose, and a lint that cannot tell a rule
    // from its violation is worse than none.
    const src = stripComments(readSrc('api/admin/console.js'));
    assert.ok(src.indexOf('localStorage') < 0, 'no localStorage');
    assert.ok(src.indexOf('sessionStorage') < 0, 'no sessionStorage');
    assert.ok(src.indexOf('document.cookie') < 0, 'no cookie');
    assert.ok(src.indexOf('READ_KEY = null') > 0, 'the key still lives in one in-memory variable');
});

// -- 6b. THE HTML THE OWNER ACTUALLY SEES ------------------------------------
// A regex over the page proves the code CONTAINS a sentence; only rendering
// proves she is SHOWN it. The page script is an IIFE, so the harness re-opens it
// just far enough to reach renderSkus - it changes not one byte of what is
// served, and it throws if the page's shape moves rather than silently testing
// nothing. (Harness shape borrowed from test/tunables-manifest.test.js.)

const vm = require('node:vm');

function renderSkusHtml(mutateState) {
    const page = require('../api/admin/console.js').PAGE;
    const i = page.indexOf('<script>');
    const j = page.lastIndexOf('</script>');
    let js = page.slice(i + 8, j).trim();
    const tail = '})();';
    assert.ok(js.endsWith(tail), 'the page script is no longer a plain IIFE - fix this harness');
    js = js.slice(0, -tail.length) +
        '\n  globalThis.__probe = { state: state, render: renderSkus };\n})();';

    const el = () => ({
        textContent: '', value: '', innerHTML: '', hidden: false,
        addEventListener() {}, setAttribute() {}, getAttribute() { return null; },
        querySelectorAll() { return []; }, querySelector() { return null; },
    });
    const ctx = {
        document: { getElementById: el, addEventListener() {} },
        window: { prompt() { return null; }, confirm() { return false; } },
        fetch: () => new Promise(() => {}),
        Date, Math, JSON, Number, String, Array, Object, isFinite, parseInt, Promise, console,
    };
    ctx.globalThis = ctx;
    vm.createContext(ctx);
    vm.runInContext(js, ctx);
    mutateState(ctx.__probe.state);
    return ctx.__probe.render();
}

test('RENDERED: every SKU and its contents actually reach the page', async () => {
    const r = await runStats(NO_DB_ENV, skuRequest({ 'x-admin-key': KEY }));
    const body = r.out.body;
    const html = renderSkusHtml((s) => { s.skus = body; s.skusErr = null; });

    for (const p of body.packs) {
        assert.ok(html.indexOf(p.sku) >= 0, p.sku + ' is missing from the rendered table');
    }
    // Contents, not just names: a pack's grant is the half the owner asked for.
    const withEconomy = body.packs.find((p) => p.contents.economy.length > 0);
    assert.ok(html.indexOf('>' + withEconomy.contents.economy[0].resource + '<') >= 0 ||
              html.indexOf(withEconomy.contents.economy[0].resource) >= 0,
              'an economy resource name must appear');
    const withConv = body.packs.find((p) => p.contents.convenience.length > 0);
    assert.ok(html.indexOf(withConv.contents.convenience[0].kind) >= 0,
              'a convenience kind must appear');
    // The reverse column is on the page too, not only in the JSON.
    assert.ok(html.indexOf('monthly-wayfarer') >= 0,
              'a priced SKU that is not a pack must still be named on screen');
});

test('RENDERED: a parity gap says the WORD MISSING, and says which rail', async () => {
    const r = await runStats(NO_DB_ENV, skuRequest({ 'x-admin-key': KEY }));
    const body = JSON.parse(JSON.stringify(r.out.body));

    // Inject the gap rather than wait for one to ship. Today every real pack is
    // either anchored or promo-only, so the failure column would otherwise never
    // be executed - and an unexecuted failure column is decoration.
    body.packs[0].usd_anchor_present = false;
    body.packs[0].usd_anchor = null;
    body.packs[0].sellable = false;
    body.packs[0].sellable_reason = 'NO ANCHOR - cannot be quoted, so it cannot be sold';
    body.packs[0].parity_gaps = ['no USD anchor in api/_lib/purchase-catalog.js - this SKU ' +
        'cannot be quoted, so the wallet rail cannot sell it'];

    const html = renderSkusHtml((s) => { s.skus = body; s.skusErr = null; });
    assert.ok(html.indexOf('MISSING') >= 0, 'the state must be a WORD, not a colour');
    assert.ok(html.indexOf('purchase-catalog.js') >= 0,
              'the gap must name the file it is missing from, or it just sends someone grepping');
    assert.ok(html.indexOf('cannot be quoted') >= 0,
              'and what the player experiences because of it');
});

test('RENDERED: a failed catalog read shows no table at all', () => {
    const html = renderSkusHtml((s) => { s.skus = null; s.skusErr = 'HTTP 500'; });
    assert.ok(html.indexOf('COULD NOT READ') >= 0);
    assert.ok(html.indexOf('<table') < 0,
              'an empty table here would read as "we sell nothing" - a confident lie');
});

test('the served page is still 7-bit ASCII from end to end', () => {
    // packs.json carries non-ASCII in its authoring notes, which is precisely why
    // the catalog is FETCHED at runtime and never inlined into this page the way
    // the tunable manifest is.
    const page = require('../api/admin/console.js').PAGE;
    for (let i = 0; i < page.length; i++) {
        const c = page.charCodeAt(i);
        if (c > 126) {
            assert.fail('non-ASCII at ' + i + ': ' + JSON.stringify(page.slice(i - 40, i + 40)));
        }
    }
});
