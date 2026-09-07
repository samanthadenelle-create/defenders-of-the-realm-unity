'use strict';

// =============================================================================
// test/command-center.refusal-logging.test.js
//   -- the oracle for WO-1244's REOPENED bounce (owner felt-test 2026-09-03,
//      verdict "Fail", note EMPTY).
//
// WHY THIS FILE EXISTS, said plainly.
// -----------------------------------------------------------------------------
// The bounce carries no note, no screenshot and no response code
// (proof/owner-validations.json:130 -- {"note": "", "verdict": "Fail"}), so the
// failing pillar is unknown. That is not only a missing note: at the moment she
// felt-tested, THE SERVER RECORDED NOTHING EITHER. api/admin/ops.js refused in
// six distinct places and every one of them returned an HTTP 400 in silence, and
// api/admin/stats.js refused the READ gate in silence too. A successful write
// lands in the ops history table (recordOpsWrite); a REFUSED one left no trace
// anywhere. So after the fact there was no way -- from a runtime log, from the
// database, from anything -- to tell these four apart:
//
//   * the deployment is missing ADMIN_OPS_KEY      -> OPS_WRITE_NOT_CONFIGURED
//   * she typed the write key wrong                -> OPS_UNAUTHORIZED
//   * she typed the READ key wrong                 -> UNAUTHORIZED
//   * the page never reached this deployment at all -> NO LINE AT ALL
//
// The last one is the diagnosis the absence of a line gives you for free, and it
// is the one this repo's own memory flags as live: the game site and these
// functions are on DIFFERENT Vercel projects, so a console opened on the wrong
// host answers 404 and no function here ever runs.
//
// ⛔ WHAT THIS DOES NOT DO: it does not guess which one it was. It makes the NEXT
// attempt self-diagnosing. Per CLAUDE.md 11B, an unproven cause is named unproven
// rather than shipped as a fix.
//
// ⛔ AND IT MUST NEVER BECOME A LEAK. The whole reason the write half has a second
// secret is that the read key ends up in phone screenshots. A diagnostic line that
// printed a supplied key would be strictly worse than no line. So every field here
// is a BOOLEAN or a stable machine code -- never a value, never a length.
//
// Proven RED first (WO-1138): before api/admin/ops.js and api/admin/stats.js grew
// logRefusal(), every assertion below failed on "expected 1 refusal line, got 0".
//
//   node --test test/command-center.refusal-logging.test.js
// =============================================================================

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const REPO = path.join(__dirname, '..');
const readSrc = (rel) => fs.readFileSync(path.join(REPO, rel), 'utf8');

// -- harness -----------------------------------------------------------------

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

function fakeReq(method, headers, body, query) {
    return { method: method, headers: headers || {}, body: body, query: query || {} };
}

// Capture everything the handler writes to the runtime log, so the test can
// assert on the exact bytes an operator would later read in Vercel.
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

// Run an endpoint under a controlled environment and put the environment back
// exactly as it was. No test may leak an admin key into another.
const ENV_KEYS = ['ADMIN_DASH_KEY', 'ADMIN_OPS_KEY', 'DATABASE_URL'];

async function runEndpoint(modulePath, env, req) {
    const keep = {};
    for (const k of ENV_KEYS) keep[k] = process.env[k];
    for (const k of ENV_KEYS) {
        if (Object.prototype.hasOwnProperty.call(env, k)) {
            if (env[k] === undefined) delete process.env[k];
            else process.env[k] = env[k];
        }
    }
    delete require.cache[require.resolve(modulePath)];
    const handler = require(modulePath);
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

const runOps = (env, req) => runEndpoint('../api/admin/ops', env, req);
const runStats = (env, req) => runEndpoint('../api/admin/stats', env, req);

const SEAL = { action: 'maintenance.seal', area: 'raiding', message: 'back soon', by: 'console' };

// -- 1. EVERY WRITE REFUSAL LEAVES EXACTLY ONE LINE ---------------------------

test('every ops refusal writes exactly one machine-readable line carrying its code', async () => {
    const cases = [
        {
            what: 'a GET (a write reachable by a link)',
            env: { ADMIN_DASH_KEY: 'read', ADMIN_OPS_KEY: 'write' },
            req: fakeReq('GET', { 'x-admin-key': 'read' }, SEAL),
            code: 'METHOD_NOT_ALLOWED',
        },
        {
            what: 'no read key on the deployment',
            env: { ADMIN_DASH_KEY: undefined, ADMIN_OPS_KEY: 'write' },
            req: fakeReq('POST', { 'x-admin-key': 'read', 'x-admin-ops-key': 'write' }, SEAL),
            code: 'ADMIN_NOT_CONFIGURED',
        },
        {
            what: 'the read key typed wrong',
            env: { ADMIN_DASH_KEY: 'read', ADMIN_OPS_KEY: 'write' },
            req: fakeReq('POST', { 'x-admin-key': 'nope', 'x-admin-ops-key': 'write' }, SEAL),
            code: 'UNAUTHORIZED',
        },
        {
            // ⭐ THE ONE THE WO's OWN BANNER IS ARGUING ABOUT. The ticket says the
            // key is unset on the deployment; docs/ACCESS_AND_SECRETS.md says it
            // was set on 2026-08-28. This line settles it from the runtime log
            // instead of from two documents that disagree.
            what: 'no write key on the deployment',
            env: { ADMIN_DASH_KEY: 'read', ADMIN_OPS_KEY: undefined },
            req: fakeReq('POST', { 'x-admin-key': 'read', 'x-admin-ops-key': 'write' }, SEAL),
            code: 'OPS_WRITE_NOT_CONFIGURED',
        },
        {
            what: 'the write key typed wrong',
            env: { ADMIN_DASH_KEY: 'read', ADMIN_OPS_KEY: 'write' },
            req: fakeReq('POST', { 'x-admin-key': 'read', 'x-admin-ops-key': 'nope' }, SEAL),
            code: 'OPS_UNAUTHORIZED',
        },
        {
            what: 'a body that is not JSON',
            env: { ADMIN_DASH_KEY: 'read', ADMIN_OPS_KEY: 'write' },
            req: fakeReq('POST', { 'x-admin-key': 'read', 'x-admin-ops-key': 'write' }, '{not json'),
            code: 'BAD_BODY',
        },
        {
            what: 'an action that does not exist',
            env: { ADMIN_DASH_KEY: 'read', ADMIN_OPS_KEY: 'write', DATABASE_URL: undefined },
            req: fakeReq('POST', { 'x-admin-key': 'read', 'x-admin-ops-key': 'write' },
                { action: 'purchase.refund', by: 'console' }),
            code: 'UNKNOWN_ACTION',
        },
    ];

    for (const c of cases) {
        const r = await runOps(c.env, c.req);
        assert.equal(r.out.body.code, c.code, c.what + ': wrong response code');
        const recs = refusalRecords(r.lines);
        assert.equal(recs.length, 1,
            c.what + ': expected exactly 1 ' + REFUSAL_TAG + ' line, got ' + recs.length);
        assert.equal(recs[0].code, c.code, c.what + ': the logged code must equal the answered code');
    }
});

// -- 2. THE LINE SEPARATES "NOT CONFIGURED" FROM "TYPED WRONG" ----------------

test('the line says whether each key was CONFIGURED and whether one was SUPPLIED', async () => {
    // Missing on the deployment: configured=false, supplied=true. This is the
    // shape that means "go set the env var", and nothing else produces it.
    let r = await runOps({ ADMIN_DASH_KEY: 'read', ADMIN_OPS_KEY: undefined },
        fakeReq('POST', { 'x-admin-key': 'read', 'x-admin-ops-key': 'write' }, SEAL));
    let rec = refusalRecords(r.lines)[0];
    assert.equal(rec.readKeyConfigured, true);
    assert.equal(rec.opsKeyConfigured, false);
    assert.equal(rec.opsKeySupplied, true);

    // Typed wrong: configured=true, supplied=true. Same 400 to the caller, a
    // completely different remedy, and now they are distinguishable after the fact.
    r = await runOps({ ADMIN_DASH_KEY: 'read', ADMIN_OPS_KEY: 'write' },
        fakeReq('POST', { 'x-admin-key': 'read', 'x-admin-ops-key': 'nope' }, SEAL));
    rec = refusalRecords(r.lines)[0];
    assert.equal(rec.opsKeyConfigured, true);
    assert.equal(rec.opsKeySupplied, true);

    // Header absent entirely (an old cached page, or a client that never prompted).
    r = await runOps({ ADMIN_DASH_KEY: 'read', ADMIN_OPS_KEY: 'write' },
        fakeReq('POST', { 'x-admin-key': 'read' }, SEAL));
    rec = refusalRecords(r.lines)[0];
    assert.equal(rec.opsKeySupplied, false);
});

test('the line carries the ACTION and a timestamp, so a bounce can be dated', async () => {
    const r = await runOps({ ADMIN_DASH_KEY: 'read', ADMIN_OPS_KEY: undefined },
        fakeReq('POST', { 'x-admin-key': 'read', 'x-admin-ops-key': 'write' }, SEAL));
    const rec = refusalRecords(r.lines)[0];
    assert.equal(rec.action, 'maintenance.seal');
    assert.match(rec.at, /^\d{4}-\d{2}-\d{2}T/);
});

// -- 3. A REFUSAL LINE MUST NEVER BE A LEAK ----------------------------------

test('no supplied key, and no environment value, ever reaches the log line', async () => {
    const SECRET_READ = 'read-secret-do-not-print';
    const SECRET_OPS = 'ops-secret-do-not-print';
    const TYPED = 'typed-wrong-value';

    const r = await runOps({ ADMIN_DASH_KEY: SECRET_READ, ADMIN_OPS_KEY: SECRET_OPS },
        fakeReq('POST', { 'x-admin-key': SECRET_READ, 'x-admin-ops-key': TYPED }, SEAL));

    const all = r.lines.join('\n');
    for (const forbidden of [SECRET_READ, SECRET_OPS, TYPED]) {
        assert.equal(all.indexOf(forbidden), -1, 'a key value reached the log: ' + forbidden);
    }
    // Not even a LENGTH: a length narrows a brute force and buys no diagnosis a
    // boolean does not already give.
    const rec = refusalRecords(r.lines)[0];
    for (const [k, v] of Object.entries(rec)) {
        assert.notEqual(typeof v, 'number', 'refusal field "' + k + '" is a number; lengths are not diagnostics');
    }
});

test('the source-level no-secret-logged lint still holds over the new logging', () => {
    // Same rule test/command-center.test.js pins, re-asserted here because THIS
    // ticket is the one that added logging to these files.
    for (const rel of ['api/_lib/ops.js', 'api/admin/ops.js', 'api/admin/stats.js']) {
        const src = readSrc(rel);
        for (const line of src.split('\n')) {
            if (/^\s*(\/\/|\*)/.test(line)) continue;          // comments
            if (!/console\.(log|warn|error|info)/.test(line)) continue;
            assert.doesNotMatch(line, /ADMIN_DASH_KEY|ADMIN_OPS_KEY|DATABASE_URL|process\.env/,
                rel + ' logs an environment value: ' + line.trim());
            assert.doesNotMatch(line, /headers\[/,
                rel + ' logs a request header value: ' + line.trim());
        }
    }
});

// -- 4. THE READ HALF REFUSES OUT LOUD TOO -----------------------------------

test('a refused READ leaves a line, so "the key is wrong" and "the page never arrived" differ', async () => {
    let r = await runStats({ ADMIN_DASH_KEY: 'read' },
        fakeReq('GET', { 'x-admin-key': 'nope' }, null, { view: 'ops' }));
    assert.equal(r.out.statusCode, 400);
    let recs = refusalRecords(r.lines);
    assert.equal(recs.length, 1, 'expected 1 refusal line from the read half');
    assert.equal(recs[0].code, 'UNAUTHORIZED');
    assert.equal(recs[0].endpoint, 'admin/stats');
    assert.equal(recs[0].view, 'ops');

    r = await runStats({ ADMIN_DASH_KEY: undefined },
        fakeReq('GET', { 'x-admin-key': 'read' }, null, { view: 'ops' }));
    recs = refusalRecords(r.lines);
    assert.equal(recs.length, 1);
    assert.equal(recs[0].code, 'ADMIN_NOT_CONFIGURED');
});

test('an ACCEPTED read is silent -- the log is a refusal record, not a traffic log', async () => {
    // A CORS preflight is the cheapest accepted path that never touches the
    // database, so it proves "no refusal, no line" without a live connection.
    const r = await runStats({ ADMIN_DASH_KEY: 'read' },
        fakeReq('OPTIONS', { 'x-admin-key': 'read' }, null, {}));
    assert.equal(refusalRecords(r.lines).length, 0);
});

test('the write half stays a WRITE endpoint -- logging added no read of the money tables', () => {
    const src = readSrc('api/admin/ops.js');
    assert.doesNotMatch(src, /purchase_entitlements/);
    assert.doesNotMatch(src, /purchase_quotes/);
});
