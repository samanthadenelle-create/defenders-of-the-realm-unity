'use strict';

// =============================================================================
// The raw-body guard must not fire on the SESSION rail.
// -----------------------------------------------------------------------------
// ⛔ THE DEFECT THIS PINS (found in production 2026-08-24): every wallet-authed
// /api/game/save returned 500 with reason `raw_body_unavailable_bodyparser_active`,
// and had done for an unknown length of time. `player_data` held 21 rows and EVERY
// ONE was `guest-local-*` — not a single save had ever been written under a wallet
// identity.
//
// The cause was two correct rails that never met:
//   1. The guard (predates WO-1157): a wallet SIGNATURE is computed over the exact
//      raw bytes, so if the runtime already parsed the body, verification is
//      genuinely impossible. Refusing precisely beats emitting a lying
//      AUTH_BAD_SIGNATURE. Still true, still kept.
//   2. The session rail (WO-1157): `verifyWallet` accepts an `x-session` bearer and
//      returns `via:'session'` WITHOUT EVER READING `payload` — its own comment says
//      "A valid session is proof of the same fact the signature proves."
//
// The guard ran BEFORE authenticate() and did not know rail 2 existed, so a
// session-authed request was refused for lacking bytes it never needed.
//
// ⚠ WHY IT HID SO WELL: the failure is invisible from inside the game. The guest id
// is derived from the device, so the player's town kept persisting under the guest
// key while the identity their PURCHASES bind to had nothing behind it. Nothing on
// screen said "your cloud save is not being written".
//
// These cases assert the SHAPE of the precondition, deliberately without a DB or a
// live request: the bug was a boolean composed wrongly, and that is exactly what a
// unit test can hold still. The three call sites are asserted from source so a
// fourth endpoint copying the old shape is caught too.
// =============================================================================

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

/** The precondition as it now reads at every call site. */
function rejectsForRawBody({ exactBytes, isGuest, hasSessionHeader }) {
    return !exactBytes && !isGuest && !hasSessionHeader;
}

test('a SESSION-authed request is NOT rejected when the runtime parsed the body', () => {
    // The production case: Vercel parsed the JSON, so exact bytes are gone — but the
    // caller holds a session bearer, which does not sign the body at all.
    assert.equal(
        rejectsForRawBody({ exactBytes: false, isGuest: false, hasSessionHeader: true }),
        false,
        'a session bearer needs no raw bytes; refusing it 500s every wallet save',
    );
});

test('a SIGNATURE-authed request IS still rejected when the bytes are inexact', () => {
    // The guard's original purpose, unchanged. A signature over re-serialised JSON
    // cannot verify, and saying so precisely beats a lying AUTH_BAD_SIGNATURE.
    assert.equal(
        rejectsForRawBody({ exactBytes: false, isGuest: false, hasSessionHeader: false }),
        true,
    );
});

test('exact bytes always pass, with or without a session', () => {
    for (const hasSessionHeader of [true, false]) {
        assert.equal(rejectsForRawBody({ exactBytes: true, isGuest: false, hasSessionHeader }), false);
    }
});

test('guests are exempt regardless — they do not sign', () => {
    for (const hasSessionHeader of [true, false]) {
        assert.equal(rejectsForRawBody({ exactBytes: false, isGuest: true, hasSessionHeader }), false);
    }
});

// ── The ratchet ─────────────────────────────────────────────────────────────
// ⛔ THE SAME GUARD IS COPY-PASTED INTO THREE ENDPOINTS, which is how one of them
// could have been fixed and the others left broken. Assert the shape at SOURCE so a
// partial fix — or a fourth endpoint pasting the old form — fails here.
const GUARDED = [
    'api/game/save.js',
    'api/promo/redeem.js',
    'api/referral/claim.js',
];

test('every endpoint carrying the raw-body guard also consults the session header', () => {
    const repoRoot = path.join(__dirname, '..');
    for (const rel of GUARDED) {
        const src = fs.readFileSync(path.join(repoRoot, rel), 'utf8');

        assert.ok(
            src.includes('!exactBytes'),
            `${rel}: expected the raw-body guard to still exist — it protects the signature path`,
        );
        assert.ok(
            /const hasSessionHeader\s*=/.test(src),
            `${rel}: raw-body guard present but hasSessionHeader is not computed — ` +
            `a session-authed call will be refused for bytes it never needed (the 2026-08-24 defect)`,
        );
        assert.ok(
            /!exactBytes[^\n]*&&[^\n]*!hasSessionHeader/.test(src),
            `${rel}: hasSessionHeader is computed but NOT part of the rejection condition — ` +
            `computing it and not using it is the same bug with extra steps`,
        );
    }
});

test('the session rail really does authenticate without reading the payload', () => {
    // Sourced, not assumed: if verifyWallet ever starts folding `payload` into the
    // session branch, the exemption above becomes unsafe and this must fail loudly.
    const src = fs.readFileSync(
        path.join(__dirname, '..', 'api', '_lib', 'wallet-auth.js'), 'utf8');
    const idx = src.indexOf("headers['x-session']");
    assert.ok(idx > 0, 'wallet-auth.js no longer reads an x-session header — re-check the exemption');

    const branch = src.slice(idx, idx + 600);
    assert.ok(
        /verifySession\(\s*sql\s*,\s*sessionToken\s*,\s*claimedPlayerId\s*\)/.test(branch),
        'the session branch should verify (token, claimedPlayerId) only',
    );
    assert.ok(
        !/verifySession\([^)]*payload/.test(branch),
        'verifySession must NOT take the payload — if it does, a session DOES depend on ' +
        'the raw body and the guard exemption is wrong',
    );
});
