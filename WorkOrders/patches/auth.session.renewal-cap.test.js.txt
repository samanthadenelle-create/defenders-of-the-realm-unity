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

test('the endpoint routes to renewal on the ABSENCE of a nonce, never on a client flag', () => {
    // A caller must not be able to SELECT the cheaper check for itself; that is the
    // shape of most auth bypasses. It reaches renewal only by having nothing else.
    assert.match(sessionSrc, /if\s*\(\s*sessionHeader\s*&&\s*!nonceHeader\s*\)/,
        'renewal is no longer gated on the absence of signature material');
    assert.doesNotMatch(sessionSrc, /body\s*\.\s*renew|headers\['x-renew'\]/,
        'renewal became client-selectable - a caller must never choose its own auth path');
});

test('a CAPPED refusal does NOT fall through to the path that would have allowed it', () => {
    // ⛔ THE SUBTLE FAILURE THIS PINS. Everything after the renewal block hands the
    // request to verifyWallet, whose session rail accepts a STILL-VALID token and
    // mints - which is exactly the uncapped behaviour. So a cap that "refuses" by
    // falling through does nothing at all. It must return.
    const capIndex = sessionSrc.indexOf('absolute_cap === true');
    assert.ok(capIndex > 0, 'the absolute-cap branch is gone from the endpoint');
    const afterCap = sessionSrc.slice(capIndex, capIndex + 400);
    assert.match(afterCap, /return quietFail\(res,\s*401/,
        'a chain past its absolute life falls through to verifyWallet, which would renew it anyway');
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
