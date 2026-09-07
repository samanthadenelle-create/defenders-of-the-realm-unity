'use strict';

// =============================================================================
// WO-1452 — the absolute session cap was defeated by sending ANY junk X-Nonce.
// -----------------------------------------------------------------------------
// ⛔ THE DEFECT, EXACTLY. WO-1441 capped the renewal chain in absolute time from the
// original signature (`signed_at`, carried forward across rotations). But the endpoint
// routed to that capped path on the ABSENCE of a nonce header:
//
//     if (sessionHeader && !nonceHeader) { ...renewal, carries signed_at forward... }
//
// A request presenting a VALID session token plus any arbitrary `X-Nonce` value skipped
// that block — and the code below it does NOT then verify a signature, because
// _lib/wallet-auth.verifyWallet tries the SESSION rail FIRST and returns ok before the
// nonce is ever looked at. Control reached `issueSession(sql, auth.wallet)` with
// `signedAt` undefined, the INSERT resolved `COALESCE(NULL, NOW())`, and the chain
// origin RESET. One junk header renewed forever, and the spent token was not even
// revoked. The cap the whole feature exists to enforce never fired.
//
// ⚠ AND THE OBVIOUS PATCH IS NOT ENOUGH, which is why these tests are BEHAVIOURAL and
// not source-shape greps. Requiring both a nonce AND a signature before skipping renewal
// still loses to `X-Nonce: junk` + `X-Signature: junk`: the junk signature is never
// verified, the session rail short-circuits it, and the mint resets the origin again. A
// grep for a widened condition would have certified that. So these tests drive the real
// handler against a faked Neon driver and assert THE VALUE THAT LANDS IN THE ROW.
//
// RED PROOF — measured 2026-09-07 by re-running this exact file against the PRE-FIX
// api/ (`git stash push -- api/auth/session.js api/_lib/wallet-auth.js`, run, pop).
// Six of the eight cases below failed, verbatim:
//   ✖ a junk X-Nonce beside a valid session must not reset the chain origin
//        "the chain origin was RESET by a junk X-Nonce ... (signed_at moved 39600s)"
//   ✖ junk nonce AND junk signature cannot mint a fresh chain origin either
//   ✖ a chain past its absolute life is refused even when a junk nonce is attached
//        "a chain past its absolute life was renewed anyway - the cap never fires" (200, not 401)
//   ✖ a capped chain is refused with junk nonce AND junk signature too              (200, not 401)
//   ✖ repeated session+nonce renewals never move the chain origin
//        "renewal 0 moved the chain origin by 43140000ms"  (the full 12-hour ceiling, restored)
//   ✖ a REAL signature still mints a FRESH chain, even beside a capped session
//        burnedNonces [] !== ['nonce-real-1'] — the nonce was NEVER BURNED, because the
//        session rail answered before the signature was ever looked at. That single line is
//        the mechanism of the whole bypass.
// All eight pass on the fixed tree, with the rest of test/ green (448/448).
// =============================================================================

const test = require('node:test');
const assert = require('node:assert/strict');

// ── Stub the driver BEFORE api/auth/session.js is first required ─────────────
// session.js does `const { neon } = require('@neondatabase/serverless')` at module
// load, so the replacement has to be in the cache first. The driver's `neon` export
// is getter-only and cannot be assigned, so the whole cached module is replaced —
// the same technique test/promo.owner-bypass.test.js uses.
let CURRENT_SQL = null;
const neonPath = require.resolve('@neondatabase/serverless');
require.cache[neonPath] = {
    id: neonPath, filename: neonPath, loaded: true, exports: { neon: () => CURRENT_SQL },
};

const audit = require('../api/_lib/audit');
audit.logAuthReject = async () => {};
audit.logApiEvent = async () => {};

const walletAuth = require('../api/_lib/wallet-auth');
const { buildSignedMessage, SESSION_ABSOLUTE_TTL_SECONDS } = walletAuth;

process.env.DATABASE_URL = process.env.DATABASE_URL || 'postgres://fake/fake';

const handler = require('../api/auth/session.js');

// ── A real keypair, so the "she signed again" case is proven, not simulated ───
const nacl = require('tweetnacl');
let bs58 = require('bs58');
if (bs58 && typeof bs58.decode !== 'function' && bs58.default) bs58 = bs58.default;

const KEYPAIR = nacl.sign.keyPair();
const WALLET = bs58.encode(Buffer.from(KEYPAIR.publicKey));

function signFor(nonce) {
    const msg = Buffer.from(buildSignedMessage(WALLET, nonce, null), 'utf8');
    return bs58.encode(Buffer.from(nacl.sign.detached(new Uint8Array(msg), KEYPAIR.secretKey)));
}

const ABS_MS = SESSION_ABSOLUTE_TTL_SECONDS * 1000;

// Session tokens must satisfy wallet-auth's SESSION_RE (40-90 of [A-Za-z0-9_-]) or they are
// refused as MALFORMED long before any cap logic runs -- a short fixture name would have
// made every case below pass for the wrong reason.
const T = (name) => (name + '-').padEnd(44, 'z');

// ── The faked database ───────────────────────────────────────────────────────
/**
 * A tagged-template stand-in for the Neon driver, backed by an in-memory
 * `auth_sessions` table. It evaluates `expired` and `past_absolute` the way Postgres
 * would, and — the point of the whole suite — records the `signed_at` each INSERT
 * actually resolves, honouring `COALESCE(<bound value>, NOW())`.
 */
function makeStore(opts) {
    const store = {
        sessions: new Map(),
        inserts: [],          // every row issueSession created, in order
        revoked: [],          // every token renewal rotated out
        validNonce: (opts && opts.validNonce) || null,
        burnedNonces: [],
    };

    store.seed = (token, signedAtMsAgo, ttlMs) => {
        store.sessions.set(token, {
            token,
            wallet: WALLET,
            identity_kind: 'wallet',
            revoked: false,
            expires_at: new Date(Date.now() + (ttlMs == null ? 10 * 60 * 1000 : ttlMs)),
            signed_at: new Date(Date.now() - signedAtMsAgo),
        });
        return token;
    };

    store.sql = async function sql(strings, ...values) {
        const text = Array.isArray(strings) ? strings.join(' ? ') : String(strings);
        const now = Date.now();

        if (/INSERT INTO auth_sessions/.test(text)) {
            // values: token, wallet, identity_kind, ttlSeconds, signedAt|null
            const [token, wallet, kind, ttlSeconds, signedAt] = values;
            const row = {
                token, wallet, identity_kind: kind, revoked: false,
                expires_at: new Date(now + Number(ttlSeconds) * 1000),
                // ⛔ THE LINE UNDER TEST: COALESCE(NULL, NOW()) is a RESET.
                signed_at: signedAt ? new Date(signedAt) : new Date(now),
            };
            store.sessions.set(token, row);
            store.inserts.push(row);
            return [{ token: row.token, expires_at: row.expires_at, signed_at: row.signed_at }];
        }

        if (/FROM auth_sessions WHERE token/.test(text)) {
            // ⚠ THE BOUND VALUES ARE IN TEMPLATE ORDER, NOT THE ORDER THEY READ. renewSession's
            // statement interpolates SESSION_ABSOLUTE_TTL_SECONDS in the past_absolute
            // expression BEFORE the token in the WHERE clause, so values = [absSeconds, token]
            // there and [token] in verifySession's shorter statement. Reading values[0] as the
            // token in both made every renewal miss the row and decline — which silently sent
            // the test through the fall-through path and hid the very reset it was checking.
            const absolute = /past_absolute/.test(text);
            const row = store.sessions.get(absolute ? values[1] : values[0]);
            if (!row) return [];
            if (absolute) {
                const absSeconds = Number(values[0]);
                return [{
                    wallet: row.wallet,
                    identity_kind: row.identity_kind,
                    revoked: row.revoked,
                    expired: row.expires_at.getTime() < now,
                    signed_at: row.signed_at,
                    past_absolute: row.signed_at.getTime() + absSeconds * 1000 < now,
                }];
            }
            return [{
                wallet: row.wallet,
                revoked: row.revoked,
                expired: row.expires_at.getTime() < now,
            }];
        }

        if (/UPDATE auth_sessions SET revoked/.test(text)) {
            const row = store.sessions.get(values[0]);
            if (row) row.revoked = true;
            store.revoked.push(values[0]);
            return [];
        }

        if (/UPDATE auth_nonces/.test(text)) {
            const nonce = values[0];
            if (store.validNonce && nonce === store.validNonce && !store.burnedNonces.includes(nonce)) {
                store.burnedNonces.push(nonce);
                return [{ nonce }];
            }
            return [];
        }

        // DELETE housekeeping, auth_nonces classification, audit inserts — all inert here.
        return [];
    };

    return store;
}

function makeRes() {
    const res = { statusCode: 0, body: null, headers: {} };
    res.setHeader = (k, v) => { res.headers[k] = v; };
    res.status = (code) => { res.statusCode = code; return res; };
    res.json = (body) => { res.body = body; return res; };
    res.end = () => res;
    return res;
}

async function post(store, headers) {
    CURRENT_SQL = store.sql;
    const res = makeRes();
    await handler({ method: 'POST', url: '/api/auth/session', headers }, res);
    return res;
}

// ── The bypass itself ────────────────────────────────────────────────────────

test('a junk X-Nonce beside a valid session must not reset the chain origin', async () => {
    const store = makeStore();
    const origin = Date.now() - 11 * 60 * 60 * 1000;   // signed 11h ago, inside the 12h cap
    store.seed(T('tok-A'), Date.now() - origin);

    const res = await post(store, {
        'x-wallet': WALLET,
        'x-session': T('tok-A'),
        'x-nonce': 'not-a-real-nonce-just-noise',
    });

    assert.equal(res.statusCode, 200, 'a valid session inside the cap must still be renewable');
    assert.equal(store.inserts.length, 1, 'exactly one new session row should have been written');

    const drift = Math.abs(store.inserts[0].signed_at.getTime() - origin);
    assert.ok(drift < 5000,
        'the chain origin was RESET by a junk X-Nonce - the absolute cap is defeated ' +
        `(signed_at moved ${Math.round(drift / 1000)}s)`);
});

test('junk nonce AND junk signature cannot mint a fresh chain origin either', async () => {
    // ⛔ THE VARIANT THAT DEFEATS THE OBVIOUS PATCH. Requiring "a nonce AND a signature"
    // before skipping renewal is not enough on its own: the junk signature is never
    // verified, because verifyWallet's session rail answers first. The fix must withhold
    // the session token from verifyWallet whenever it is asked to check a signature.
    const store = makeStore();
    const origin = Date.now() - 11 * 60 * 60 * 1000;
    store.seed(T('tok-B'), Date.now() - origin);

    const res = await post(store, {
        'x-wallet': WALLET,
        'x-session': T('tok-B'),
        'x-nonce': 'noise',
        'x-signature': 'alsonoise',
    });

    assert.equal(res.statusCode, 200, 'stale signature headers must not lock out a good session');
    assert.equal(store.inserts.length, 1);
    const drift = Math.abs(store.inserts[0].signed_at.getTime() - origin);
    assert.ok(drift < 5000,
        'a junk nonce+signature pair still resets the chain origin - the cap is bypassable');
});

test('a chain past its absolute life is refused even when a junk nonce is attached', async () => {
    const store = makeStore();
    store.seed(T('tok-C'), ABS_MS + 60 * 1000);   // signed 12h+1m ago: past the cap

    const res = await post(store, {
        'x-wallet': WALLET,
        'x-session': T('tok-C'),
        'x-nonce': 'noise',
    });

    assert.equal(res.statusCode, 401,
        'a chain past its absolute life was renewed anyway - the cap never fires');
    assert.equal(res.body.code, walletAuth.AuthCode.SESSION_EXPIRED);
    assert.equal(store.inserts.length, 0, 'nothing may be minted for a capped chain');
});

test('a capped chain is refused with junk nonce AND junk signature too', async () => {
    const store = makeStore();
    store.seed(T('tok-D'), ABS_MS + 60 * 1000);

    const res = await post(store, {
        'x-wallet': WALLET,
        'x-session': T('tok-D'),
        'x-nonce': 'noise',
        'x-signature': 'alsonoise',
    });

    assert.equal(res.statusCode, 401, 'the cap must not depend on which junk headers arrive');
    assert.equal(store.inserts.length, 0);
});

test('repeated session+nonce renewals never move the chain origin', async () => {
    // ACCEPTANCE §4 bullet 2: the cap must survive a renewal LOOP, which is the only way
    // the bypass was ever exploited. Each pass rotates the token; none may re-stamp the
    // origin, and the last one is still measured from the very first signature.
    const store = makeStore();
    const origin = Date.now() - (ABS_MS - 60 * 1000);   // one minute of chain life left
    store.seed(T('tok-E'), Date.now() - origin);

    let token = T('tok-E');
    for (let i = 0; i < 5; i++) {
        const res = await post(store, {
            'x-wallet': WALLET,
            'x-session': token,
            'x-nonce': 'noise-' + i,
        });
        assert.equal(res.statusCode, 200, `renewal ${i} should still be inside the cap`);
        token = res.body.token;
        const drift = Math.abs(store.inserts[i].signed_at.getTime() - origin);
        assert.ok(drift < 5000, `renewal ${i} moved the chain origin by ${drift}ms`);
        assert.ok(store.revoked.length === i + 1, `renewal ${i} did not rotate the old token out`);
    }

    // The chain now ages past its ceiling — the next attempt must be refused, not renewed.
    store.sessions.get(token).signed_at = new Date(Date.now() - (ABS_MS + 1000));
    const after = await post(store, {
        'x-wallet': WALLET, 'x-session': token, 'x-nonce': 'noise-last',
    });
    assert.equal(after.statusCode, 401, 'the chain outlived its absolute cap and was renewed anyway');
});

// ── The properties that must NOT regress ─────────────────────────────────────

test('a REAL signature still mints a FRESH chain, even beside a capped session', async () => {
    // ⛔ THE LOCKOUT THE FIX MUST NOT CAUSE. At the cap boundary the client signs again and
    // sends the new nonce+signature WITH its now-dead session token still attached. If the
    // handler routed that to renewal it would 401 forever and cloud save would die for good.
    // A verified signature is a NEW chain origin and must be stamped NOW.
    const store = makeStore({ validNonce: 'nonce-real-1' });
    store.seed(T('tok-F'), ABS_MS + 60 * 1000);

    const res = await post(store, {
        'x-wallet': WALLET,
        'x-session': T('tok-F'),
        'x-nonce': 'nonce-real-1',
        'x-signature': signFor('nonce-real-1'),
    });

    assert.equal(res.statusCode, 200, 'a genuine re-signature was refused - the player is locked out');
    assert.equal(res.body.renewed, undefined, 'a fresh signature is a MINT, not a renewal');
    assert.equal(store.inserts.length, 1);
    assert.ok(Math.abs(store.inserts[0].signed_at.getTime() - Date.now()) < 5000,
        'a freshly signed session must start its own chain at NOW');
    assert.deepEqual(store.burnedNonces, ['nonce-real-1'],
        'the nonce backing a minted session must still be burned exactly once');
});

test('the nonce is still burned on the signature path, so it stays single-use', async () => {
    const store = makeStore({ validNonce: 'nonce-real-2' });
    const sig = signFor('nonce-real-2');
    const headers = {
        'x-wallet': WALLET, 'x-nonce': 'nonce-real-2', 'x-signature': sig,
    };

    const first = await post(store, headers);
    assert.equal(first.statusCode, 200);

    const replay = await post(store, headers);
    assert.equal(replay.statusCode, 401, 'a spent nonce was accepted twice - replay protection is gone');
});

test('a session issued to a DIFFERENT wallet is refused, junk nonce or not', async () => {
    const store = makeStore();
    store.seed(T('tok-G'), 60 * 1000);
    store.sessions.get(T('tok-G')).wallet = '7xKXtg2CW87d97TXJSDpbD5jBkheTqA83TZRuJosgAsU';

    const res = await post(store, {
        'x-wallet': WALLET, 'x-session': T('tok-G'), 'x-nonce': 'noise',
    });
    assert.equal(res.statusCode, 401, 'a token was accepted for a wallet it was never issued to');
    assert.equal(res.body.code, walletAuth.AuthCode.SESSION_WRONG_WALLET);
    assert.equal(res.body.token, undefined, 'a mismatched wallet must never be handed a token');
    // ⚠ NOT ASSERTING zero inserts, and the reason is a real (pre-existing, unchanged by
    // WO-1452) wrinkle worth writing down: renewSession issues the replacement row BEFORE
    // session.js compares the proven wallet to the header, so a rejected request does leave an
    // orphan row behind. It is never DISCLOSED — the response above carries no token — and it
    // dies on its own 15-minute clock, so it is untidiness, not a grant. Asserting 0 here
    // would pin behaviour the fix does not own.
});
