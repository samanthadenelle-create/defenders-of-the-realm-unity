'use strict';

// =============================================================================
// The session renewal chain must be CAPPED in absolute time (WO-1441).
// -----------------------------------------------------------------------------
// ⛔ WHAT WAS ACTUALLY WRONG, AND IT WAS NOT A MISSING FEATURE.
//
// The ticket asked for a signature-free renewal to be BUILT, because a 15-minute
// SESSION_TTL_SECONDS with no refresh killed cloud save mid-session. Reading the
// code showed renewal ALREADY EXISTED, by accident: `verifyWallet` tries the
// session rail FIRST when an `x-session` header is offered, so a POST to
// /api/auth/session carrying a valid session and NO nonce already authenticated
// and fell straight into `issueSession` — a fresh 15-minute token, no signature.
//
// Which means the file was breaking its own stated rule. wallet-auth's TTL note
// says a session must never "become a permanent login", but an UNCAPPED renewal is
// precisely that: every renewal restarted the clock, so one signature — or one
// LEAKED token — could be walked forward indefinitely. The 15-minute window only
// ever bound someone who did not renew.
//
// So the fix is a ceiling, not a feature: `signed_at` records the ORIGINAL
// signature and is carried forward across rotations, and renewSession refuses past
// SESSION_ABSOLUTE_TTL_SECONDS from it.
//
// ⚠ THESE ARE SOURCE-SHAPE ASSERTIONS, DELIBERATELY WITHOUT A DB. The defect class
// here is "a condition composed wrongly" and "a refusal that falls through to a
// path which would have allowed it anyway" — both hold still in source, and both
// are invisible to a happy-path integration test. The behavioural proof that a
// renewal RENEWS needs a real wallet signature and is named in the WO as a device
// step.
// =============================================================================

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const walletAuthSrc = fs.readFileSync(
    path.join(__dirname, '..', 'api', '_lib', 'wallet-auth.js'), 'utf8');
const sessionSrc = fs.readFileSync(
    path.join(__dirname, '..', 'api', 'auth', 'session.js'), 'utf8');
const schemaSrc = fs.readFileSync(
    path.join(__dirname, '..', 'api', 'schema.sql'), 'utf8');

test('the absolute cap exists and is a DIFFERENT constant from the bearer TTL', () => {
    assert.match(walletAuthSrc, /SESSION_ABSOLUTE_TTL_SECONDS\s*=\s*\d+/,
        'the renewal chain has no absolute ceiling - one signature becomes a permanent login');
    assert.match(walletAuthSrc, /SESSION_TTL_SECONDS\s*=\s*900/,
        'the bearer TTL moved; renewal must not be "fixed" by lengthening the stolen-token window');
});

test('renewSession refuses an EXPIRED session - renewal is not a resurrection', () => {
    // The whole safety of carrying proof forward is that it is carried while it still
    // stands. Renewing an expired token would make a leaked credential immortal.
    assert.match(walletAuthSrc, /row\.expired\s*===\s*true[\s\S]{0,220}SESSION_EXPIRED/,
        'renewSession no longer refuses expired sessions');
});

test('renewSession refuses past the absolute cap, measured from signed_at', () => {
    assert.match(walletAuthSrc, /signed_at\s*\+\s*\(\$\{SESSION_ABSOLUTE_TTL_SECONDS\}/,
        'the cap is no longer measured from the original signature');
    assert.match(walletAuthSrc, /past_absolute\s*===\s*true/,
        'the past-absolute refusal is gone - chains can renew forever again');
});

test('renewal CARRIES signed_at forward instead of restarting it', () => {
    // ⛔ THE SINGLE MOST IMPORTANT LINE IN THE FEATURE. If a renewal stamps signed_at
    // with NOW(), the cap resets on every renewal and is not a cap at all - the code
    // still looks correct and the ceiling silently does nothing.
    assert.match(walletAuthSrc, /issueSession\(sql,\s*wallet,\s*row\.identity_kind,\s*row\.signed_at\)/,
        'renewSession no longer passes the original signed_at into the new row');
    assert.match(walletAuthSrc, /COALESCE\(\$\{signedAt \|\| null\}::timestamptz,\s*NOW\(\)\)/,
        'issueSession no longer honours a caller-supplied signed_at');
});

test('renewal ROTATES: the old token is revoked so one chain means one live token', () => {
    assert.match(walletAuthSrc, /UPDATE auth_sessions SET revoked = TRUE WHERE token = \$\{token\}/,
        'the spent token is no longer revoked - renewal accumulates live bearer tokens');
});

test('the endpoint routes to renewal on the ABSENCE of a VERIFIABLE signature, never on a client flag', () => {
    // A caller must not be able to SELECT the cheaper check for itself; that is the
    // shape of most auth bypasses. It reaches renewal only by having nothing else.
    //
    // ⛔ AMENDED 2026-09-07 (WO-1452). This assertion used to pin the literal
    // `sessionHeader && !nonceHeader`, and that gate WAS the defect: a junk X-Nonce
    // skipped renewal, verifyWallet's session rail then authenticated the bearer token
    // without ever checking the nonce, and the mint reset the chain origin. The
    // PROPERTY this test exists for - renewal is not client-selectable - is unchanged;
    // what changed is that the gate now turns on whether a signature can VERIFY, which
    // takes both halves of the signature material to even attempt.
    assert.match(sessionSrc, /if\s*\(\s*sessionHeader\s*&&\s*!offersSignature\s*\)/,
        'renewal is no longer gated on the absence of verifiable signature material');
    assert.match(sessionSrc, /const offersSignature = !!\(nonceHeader && signatureHeader\)/,
        'a lone X-Nonce counts as signature material again - WO-1452 is re-opened');
    assert.doesNotMatch(sessionSrc, /body\s*\.\s*renew|headers\['x-renew'\]/,
        'renewal became client-selectable - a caller must never choose its own auth path');
});

test('a request offering signature material has its session token WITHHELD from verifyWallet', () => {
    // ⛔ WO-1452, the half a widened condition does not fix. verifyWallet tries the session
    // rail FIRST, so passing both a session token and signature material means the signature
    // is never verified and the nonce never burned - the request authenticates as a bearer
    // token and then MINTS, stamping a new chain origin. Withholding the token is what makes
    // the verification real, and it is what a junk `X-Signature` can no longer dodge.
    assert.match(sessionSrc, /delete verifyHeaders\['x-session'\]/,
        'the session token is passed to verifyWallet alongside signature material again');
    assert.match(sessionSrc, /verifyWallet\(sql,\s*verifyHeaders,\s*null,\s*wallet\)/,
        'verifyWallet is back to reading the raw headers - the session rail short-circuits again');
});

test('nothing that authenticated by BEARER TOKEN may reach the mint', () => {
    // The backstop: a mint stamps a fresh signed_at, so a session-verified request that mints
    // has just reset its own cap. wallet-auth must say WHICH credential verified, positively.
    assert.match(walletAuthSrc, /mode: 'wallet',\s*via: 'signature'/,
        'verifyWallet no longer distinguishes a verified signature from a presented token');
    assert.match(sessionSrc, /auth\.via === 'session'[\s\S]{0,400}tryRenew\(\)/,
        'a session-verified request can reach issueSession again - the WO-1452 bypass returns');
});

test('a CAPPED refusal does NOT fall through to the path that would have allowed it', () => {
    // ⛔ THE SUBTLE FAILURE THIS PINS. Everything after the renewal block hands the
    // request to verifyWallet, whose session rail accepts a STILL-VALID token and
    // mints - which is exactly the uncapped behaviour. So a cap that "refuses" by
    // falling through does nothing at all. It must return.
    const capIndex = sessionSrc.indexOf('absolute_cap === true');
    assert.ok(capIndex > 0, 'the absolute-cap branch is gone from the endpoint');
    const afterCap = sessionSrc.slice(capIndex, capIndex + 400);
    // WO-1452 moved the renewal block into a `tryRenew` helper, so the refusal is now
    // "answer 401 AND tell the caller to stop" rather than a bare `return quietFail`.
    // Both halves are asserted: writing the 401 without returning true would fall through
    // to verifyWallet exactly as before, which is the failure this test names.
    assert.match(afterCap, /quietFail\(res,\s*401/,
        'a chain past its absolute life falls through to verifyWallet, which would renew it anyway');
    assert.match(afterCap, /quietFail\(res,\s*401[\s\S]{0,120}return true;/,
        'the capped refusal no longer stops the handler - control continues to the mint path');
});

test('a SCHEMA-missing refusal DOES fall through, so a lagging DB cannot cause an outage', () => {
    // Renewal works in production TODAY via verifyWallet's session rail. If this code
    // ships before api/schema.sql is applied, the signed_at query throws - and a hard
    // 401 there would REMOVE working renewal and break cloud save. Falling through
    // preserves exactly today's behaviour on a database that is behind.
    assert.match(walletAuthSrc, /query_failed:\s*true,\s*likely_schema:\s*'signed_at'/,
        'the schema-missing case no longer reports itself distinctly');
    assert.match(sessionSrc, /falling through to full verification/,
        'a schema-missing renewal now hard-fails instead of degrading to the existing rail');
});

test('the schema carries signed_at additively, with a safe default', () => {
    assert.match(schemaSrc, /ALTER TABLE auth_sessions ADD COLUMN IF NOT EXISTS signed_at TIMESTAMPTZ NOT NULL DEFAULT NOW\(\)/,
        'signed_at is missing or no longer additive - renewal will throw on a deployed DB');
});
