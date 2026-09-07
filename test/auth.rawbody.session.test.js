'use strict';

// =============================================================================
// The raw-body posture on the signature rail.
// -----------------------------------------------------------------------------
// ⛔ DEFECT 1 (production 2026-08-24): every wallet-authed /api/game/save returned
// 500 `raw_body_unavailable_bodyparser_active`. `player_data` held 21 rows and EVERY
// ONE was `guest-local-*`. The guard ran BEFORE authenticate() and did not know
// WO-1157's session rail existed, so a session-authed request was refused for
// lacking bytes it never needed. Scoping the guard fixed that.
//
// ⛔ DEFECT 2 (production 2026-09-06, WO-1453): the *scoped* guard still 500ed the
// SIGNATURE rail. `WORK_ORDER_1440_...RESULT.md:225-252` proves it with a
// cryptographically valid signature over the exact bytes:
//
//     RAIL 1 (signature) -> HTTP 500 {"ok":false,"code":"SERVER_ERROR",...}
//     [auth_reject] detail={"reason":"raw_body_unavailable_bodyparser_active"}
//
// Vercel's Node 24 runtime parses `req.body` regardless of
// `config.api.bodyParser = false`, so `readBodyExact` reports `exact:false` and the
// guard refused BEFORE a signature could be checked. A fresh device — which has no
// session yet — had no working rail at all.
//
// ⛔ THE RULING (WO-1440 §7b, WO-1453 §2): the guard was a DIAGNOSTICS choice, never
// a security one. Attempting verification against the RECONSTRUCTED bytes cannot
// create a false accept — a signature either verifies against those bytes or it does
// not. So the endpoints now PROCEED and TAG the detail, and the rail returns 200 for
// a good signature / 401 for a bad one instead of 500 for both.
//
// These cases hold the shape still without a DB or a live request: the bug was a
// boolean composed wrongly, and that is exactly what a unit test can pin.
// =============================================================================

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const { bodyBytesDetail, readBodyExact } = require('../api/_lib/http');

const repoRoot = path.join(__dirname, '..');
const read = (rel) => fs.readFileSync(path.join(repoRoot, rel), 'utf8');

// ── 1. The helper: one shared tag, not two copies ───────────────────────────

test('exact bytes carry no tag at all', () => {
    assert.deepEqual(bodyBytesDetail(true), {});
});

test('reconstructed bytes are TAGGED so a 401 here is distinguishable', () => {
    // WO-1453 §2: "Tag the failure detail so a 500 here is distinguishable from a
    // genuine bad-signature 401." The tag rides along on the auth-reject row.
    const detail = bodyBytesDetail(false);
    assert.equal(detail.bytes, 'reconstructed',
        'the auth-reject row must say the bytes were rebuilt from the parsed body');
    assert.match(String(detail.reason), /reconstruct/,
        'the reason must name reconstruction, not the retired "unavailable" refusal');
    assert.notEqual(detail.reason, 'raw_body_unavailable_bodyparser_active',
        'that reason meant "we refused with a 500"; nothing refuses on this ground now');
});

test('the tag merges onto an existing auth detail without eating it', () => {
    const merged = Object.assign({ verified: false }, bodyBytesDetail(false));
    assert.equal(merged.verified, false);
    assert.equal(merged.bytes, 'reconstructed');
});

// ── 2. readBodyExact still reconstructs the bytes the client signed ─────────

test('a parsed object body reconstructs to the compact JSON the client signed', async () => {
    // The client signs `JSON.stringify(obj)` (tools/wo1440-wallet-rail-prod-proof.mjs:51)
    // and the runtime hands us the parsed object back. JSON.parse/stringify preserves
    // insertion order for non-numeric keys, so the round trip reproduces those bytes —
    // which is precisely why ATTEMPTING verification is worth doing.
    const original = JSON.stringify({ playerId: 'abc', code: 'FIRSTWATCH', supportsInlinePackRewards: true });
    const req = { body: JSON.parse(original), readableEnded: true, complete: true, headers: {} };

    const out = await readBodyExact(req, 1024 * 1024);
    assert.equal(out.exact, false, 'a re-serialised object is never CLAIMED as exact');
    assert.equal(out.buffer.toString('utf8'), original,
        'the reconstruction must reproduce the signed bytes for the ordinary compact-JSON body');
});

// ── 3. The source ratchet ───────────────────────────────────────────────────
// ⛔ THE SAME GUARD WAS COPY-PASTED INTO THREE ENDPOINTS, which is how one could be
// fixed and the others left broken. WO-1453's first pass covered two and named
// referral/claim.js as the remaining carrier; the follow-up lane closed it, so ALL
// THREE are ratcheted here. Assert the new shape at SOURCE so a partial revert fails.
const FIXED = ['api/game/save.js', 'api/promo/redeem.js', 'api/referral/claim.js'];

test('the fixed endpoints no longer refuse the signature rail on reconstructed bytes', () => {
    for (const rel of FIXED) {
        // Comment lines are EXCLUDED deliberately: both files quote the retired guard
        // verbatim so the next reader can see it was a considered removal and not an
        // oversight (§15 canon discipline). What must not come back is LIVE code.
        const live = read(rel)
            .split('\n')
            .filter((l) => !/^\s*(\/\/|\*|\/\*)/.test(l))
            .join('\n');
        assert.ok(
            !live.includes('raw_body_unavailable_bodyparser_active'),
            `${rel}: the 500 refusal is retired — a valid signature over reconstructed bytes must ` +
            `be allowed to verify (WO-1453; prod proof RAIL 1 -> HTTP 500)`,
        );
        assert.ok(
            !/!exactBytes/.test(live),
            `${rel}: no live branch may turn on !exactBytes any more — in production it is ` +
            `effectively ALWAYS false, so such a branch is a blanket refusal of the signature rail`,
        );
    }
});

test('both fixed endpoints tag the auth-reject detail through the ONE shared helper', () => {
    for (const rel of FIXED) {
        const src = read(rel);
        assert.ok(
            /bodyBytesDetail/.test(src),
            `${rel}: must use _lib/http.bodyBytesDetail — WO-1453 §2 says one helper, not two copies`,
        );
        assert.ok(
            /bodyBytesDetail\(\s*exactBytes\s*\)/.test(src),
            `${rel}: the helper must be fed the ACTUAL exactBytes flag from readBodyExact`,
        );
        assert.ok(
            /detail:\s*Object\.assign\(\{\},\s*auth\.detail[^)]*bodyBytesDetail\(\s*exactBytes\s*\)\s*\)/.test(src),
            `${rel}: the tag must be merged into the logAuthReject detail — computing it and not ` +
            `logging it leaves a 401 indistinguishable from a genuine bad signature`,
        );
    }
});

// ── 4. The session rail's exemption is still sound ──────────────────────────

test('the session rail really does authenticate without reading the payload', () => {
    // Sourced, not assumed: if verifyWallet ever starts folding `payload` into the
    // session branch, a session-authed call would depend on raw bytes after all.
    const src = read(path.join('api', '_lib', 'wallet-auth.js'));
    const idx = src.indexOf("headers['x-session']");
    assert.ok(idx > 0, 'wallet-auth.js no longer reads an x-session header — re-check the rails');

    const branch = src.slice(idx, idx + 600);
    assert.ok(
        /verifySession\(\s*sql\s*,\s*sessionToken\s*,\s*claimedPlayerId\s*\)/.test(branch),
        'the session branch should verify (token, claimedPlayerId) only',
    );
    assert.ok(
        !/verifySession\([^)]*payload/.test(branch),
        'verifySession must NOT take the payload — a session is proof of the same fact the ' +
        'signature proves, and must never depend on the raw body',
    );
});

test('the signature rail still hashes the payload — reconstruction is not a bypass', () => {
    // The bytes we verify against are still bound into the signed message, so a wrong
    // reconstruction fails CLOSED with AUTH_BAD_SIGNATURE. That is what makes
    // "attempt it" safe: there is no path where inexact bytes grant anything.
    const src = read(path.join('api', '_lib', 'wallet-auth.js'));
    assert.match(src, /createHash\('sha256'\)\.update\(payload\)/,
        'buildSignedMessage must still bind a sha256 of the payload bytes');
});
