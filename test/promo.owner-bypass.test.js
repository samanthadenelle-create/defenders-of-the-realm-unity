'use strict';

// =============================================================================
// WO-1533 - the OWNER account bypasses the promo anti-abuse guards.
// -----------------------------------------------------------------------------
// Owner ruling 2026-09-06 20:45, verbatim: "im the one account that should have no
// guards" - after her own account was refused LINK01 on device with "You have
// reached the promo code limit for this account" (PLAYER_LIMIT_REACHED, step 5).
//
// These are BEHAVIOURAL tests: they drive the real api/promo/redeem.js handler with
// a faked Neon driver and a stubbed auth result, rather than grepping the source.
// A source-grep would have passed for a bypass wired into step 4 alone - and step 4
// is NOT the cap (WO-1440 moved the cap into the claiming UPDATE's predicate), so
// the grep would have certified a fix that still refused her at max_redemptions.
//
// The fake sql therefore EVALUATES the claim predicate the way Postgres would, using
// the bypass boolean the handler actually binds into the statement.
// =============================================================================

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..');

// ── The owner identity comes from the ONE authority, never re-typed here ──────
const { OWNER_IDENTITY, isOwnerIdentity } = require('../api/_lib/owner-identity');
const OWNER = OWNER_IDENTITY;
const OTHER_WALLET = '7xKXtg2CW87d97TXJSDpbD5jBkheTqA83TZRuJosgAsU';
const GUEST = 'guest-local-' + 'a'.repeat(64);

// ── Stub the modules redeem.js DESTRUCTURES AT REQUIRE TIME ──────────────────
// It does `const { neon } = require(...)` etc., so every stub must be installed
// on the module object BEFORE api/promo/redeem.js is first required.
const walletAuth = require('../api/_lib/wallet-auth');
const audit = require('../api/_lib/audit');

let CURRENT_SQL = null;
let CURRENT_AUTH = null;
const AUDIT_EVENTS = [];

// The driver's `neon` export is a GETTER-ONLY property, so it cannot be assigned.
// Replace the whole cached module instead - it is only ever used to hand back a sql
// tagged template, and every statement this suite cares about is asserted directly.
const neonPath = require.resolve('@neondatabase/serverless');
require.cache[neonPath] = {
    id: neonPath, filename: neonPath, loaded: true, exports: { neon: () => CURRENT_SQL },
};

walletAuth.authenticatePromoRedeem = async () => CURRENT_AUTH;
audit.hashIp = () => 'deadbeefcafe';
audit.logAuthReject = async () => {};
audit.logApiEvent = async (sql, identity, eventName, properties) => {
    AUDIT_EVENTS.push({ identity, eventName, properties });
};

process.env.DATABASE_URL = process.env.DATABASE_URL || 'postgres://fake/fake';

const handler = require('../api/promo/redeem.js');

// ── Fixtures ─────────────────────────────────────────────────────────────────

/** A plain currency code, no tiers, no pack sku - the LINK01 shape. */
function promoRow(overrides) {
    return Object.assign({
        code: 'LINK01',
        reward_crystals: 500,
        reward_coins: 0,
        message: 'Welcome',
        active: true,
        max_redemptions: null,
        per_player_limit: 1,
        expires_at: null,
        bound_wallet: null,
        reward_pack_sku: null,
        tier1_pack_sku: null,
        tier1_limit: null,
        tier2_pack_sku: null,
        tier2_reward_crystals: null,
        tier2_reward_coins: null,
        redemption_count: 0,
    }, overrides || {});
}

/**
 * A tagged-template stand-in for the Neon driver.
 *
 * The `WITH claimed` branch is the important one: it honours the cap predicate the
 * SAME way the database would, reading the bypass boolean out of the bound values.
 */
function makeSql(state) {
    const calls = [];
    const fn = (strings, ...values) => {
        const text = strings.join(' ? ');
        calls.push({ text, values });

        if (/FROM promo_codes\s+WHERE code/.test(text) && /SELECT code, reward_crystals/.test(text)) {
            return Promise.resolve(state.promo ? [state.promo] : []);
        }
        if (/SELECT 1 FROM promo_redemptions/.test(text)) {
            return Promise.resolve(state.alreadyRedeemedThisCode ? [{ '?column?': 1 }] : []);
        }
        if (/COUNT\(\*\)::int AS n FROM promo_redemptions/.test(text)) {
            return Promise.resolve([{ n: state.globalRedemptions }]);
        }
        if (/COUNT\(DISTINCT code\)::int/.test(text)) {
            return Promise.resolve([{ n: state.distinctCodesForPlayer }]);
        }
        if (/SELECT redemption_count FROM promo_codes/.test(text)) {
            return Promise.resolve([{ redemption_count: state.globalRedemptions }]);
        }
        if (/WITH claimed/.test(text)) {
            const bypass = values.find((v) => typeof v === 'boolean');
            calls[calls.length - 1].bypass = bypass;
            const max = state.promo.max_redemptions;
            const capBlocks = max != null && Number(state.globalRedemptions) >= Number(max);
            if (capBlocks && bypass !== true) return Promise.resolve([]);   // predicate refused
            state.globalRedemptions += 1;
            return Promise.resolve([{
                crystals: state.promo.reward_crystals,
                coins: state.promo.reward_coins,
            }]);
        }
        return Promise.resolve([]);
    };
    fn.calls = calls;
    return fn;
}

function makeRes() {
    const res = {
        statusCode: 0,
        body: null,
        headers: {},
        setHeader(k, v) { this.headers[k] = v; },
        status(code) { this.statusCode = code; return this; },
        json(payload) { this.body = payload; return this; },
        end() { return this; },
    };
    return res;
}

/**
 * Drive the real handler once.
 * @param {object} opts { playerId, unproven, state }
 */
async function redeem(opts) {
    const body = JSON.stringify({ playerId: opts.playerId, code: 'LINK01' });
    const req = {
        method: 'POST',
        headers: {},
        body: body,               // Vercel's runtime parses it regardless of config
        readableEnded: true,
        complete: true,
    };
    const sql = makeSql(opts.state);
    CURRENT_SQL = sql;
    CURRENT_AUTH = {
        ok: true,
        mode: opts.unproven ? 'guest' : 'wallet',
        identity: opts.playerId,
        unproven: opts.unproven === true,
    };
    const res = makeRes();
    await handler(req, res);
    return { res, sql };
}

// ── 1. The helper is a single authority ──────────────────────────────────────

test('the owner identity has ONE authority and is not re-typed in the helper', () => {
    const helper = fs.readFileSync(path.join(root, 'api/_lib/owner-identity.js'), 'utf8');
    const executable = helper.replace(/^\s*\/\/.*$/gm, '');
    assert.ok(OWNER.length > 30, 'no owner identity resolved at all');
    assert.doesNotMatch(executable, new RegExp(OWNER),
        'owner-identity.js carries its own copy of the address - that is a second list');
    assert.match(executable, /require\(['"]\.\/purchase-catalog['"]\)/,
        'the helper must import the existing owner-wallet authority');

    assert.equal(isOwnerIdentity(OWNER), true);
    assert.equal(isOwnerIdentity(' ' + OWNER + ' '), true, 'a trimmed id must still match');
    assert.equal(isOwnerIdentity(OTHER_WALLET), false);
    assert.equal(isOwnerIdentity(GUEST), false);
    assert.equal(isOwnerIdentity(null), false);
    assert.equal(isOwnerIdentity(''), false);
});

// ── 2. The gate that actually refused her: per_player_limit ──────────────────

test('the OWNER redeems past per_player_limit', async () => {
    const state = {
        promo: promoRow({ per_player_limit: 1 }),
        globalRedemptions: 0,
        distinctCodesForPlayer: 7,      // long past the limit of 1
        alreadyRedeemedThisCode: false,
    };
    const { res } = await redeem({ playerId: OWNER, state: state });
    assert.equal(res.statusCode, 200);
    assert.equal(res.body.success, true,
        `the owner was refused with ${res.body && res.body.error} - the ruling is not implemented`);
    assert.equal(res.body.reward.crystals, 500);
});

test('a NON-owner wallet still gets PLAYER_LIMIT_REACHED', async () => {
    const state = {
        promo: promoRow({ per_player_limit: 1 }),
        globalRedemptions: 0,
        distinctCodesForPlayer: 7,
        alreadyRedeemedThisCode: false,
    };
    const { res } = await redeem({ playerId: OTHER_WALLET, state: state });
    assert.equal(res.statusCode, 200);
    assert.equal(res.body.success, false);
    assert.equal(res.body.error, 'PLAYER_LIMIT_REACHED',
        'the bypass leaked to a wallet that is not the owner');
});

// ── 3. The cap - and it does NOT live in step 4 ──────────────────────────────

test('the OWNER redeems past max_redemptions, including the ATOMIC claim predicate', async () => {
    const state = {
        promo: promoRow({ max_redemptions: 20, per_player_limit: null, redemption_count: 20 }),
        globalRedemptions: 20,          // the campaign is finished for everyone else
        distinctCodesForPlayer: 0,
        alreadyRedeemedThisCode: false,
    };
    const { res, sql } = await redeem({ playerId: OWNER, state: state });
    assert.equal(res.body.success, true,
        `the owner was refused with ${res.body && res.body.error} - a bypass wired only into ` +
        'step 4 cannot pass this: the cap lives in the claiming UPDATE predicate (WO-1440)');

    const claim = sql.calls.find((c) => /WITH claimed/.test(c.text));
    assert.ok(claim, 'no claim statement ran');
    assert.equal(claim.bypass, true, 'the claim statement did not carry the bypass boolean');
    assert.match(claim.text, /::boolean/,
        'the bypass must be cast explicitly inside the predicate, not left to type inference');
});

test('a NON-owner wallet is still stopped by max_redemptions', async () => {
    const state = {
        promo: promoRow({ max_redemptions: 20, per_player_limit: null, redemption_count: 20 }),
        globalRedemptions: 20,
        distinctCodesForPlayer: 0,
        alreadyRedeemedThisCode: false,
    };
    const { res, sql } = await redeem({ playerId: OTHER_WALLET, state: state });
    assert.equal(res.body.success, false);
    assert.equal(res.body.error, 'ALREADY_REDEEMED');
    const claim = sql.calls.find((c) => /WITH claimed/.test(c.text));
    if (claim) assert.equal(claim.bypass, false, 'a non-owner claim carried a true bypass');
});

test('an ORDINARY wallet under every limit still redeems normally', async () => {
    // memory `prove-the-success-path-not-just-the-refusal`: every other non-owner case
    // here is a REFUSAL, and a refusal-only suite would pass with `!ownerBypass`
    // inverted into a guard that refuses everyone. This is the success path.
    AUDIT_EVENTS.length = 0;
    const state = {
        promo: promoRow({ per_player_limit: 3, max_redemptions: 20 }),
        globalRedemptions: 0,
        distinctCodesForPlayer: 0,
        alreadyRedeemedThisCode: false,
    };
    const { res, sql } = await redeem({ playerId: OTHER_WALLET, state: state });
    assert.equal(res.body.success, true,
        `an ordinary wallet well inside every limit was refused with ${res.body && res.body.error}`);
    assert.equal(res.body.reward.crystals, 500);
    const claim = sql.calls.find((c) => /WITH claimed/.test(c.text));
    assert.ok(claim, 'no claim statement ran for an ordinary wallet');
    assert.equal(claim.bypass, false, 'an ordinary wallet claimed with the bypass set');
    assert.equal(AUDIT_EVENTS.some((e) => e.properties && e.properties.mode === 'owner-bypass'), false,
        'an ordinary grant was audited as an owner bypass');
});

// ── 4. A guest NEVER bypasses, even claiming the owner's id ──────────────────

test('a GUEST presenting the owner id does NOT bypass', async () => {
    const state = {
        promo: promoRow({ per_player_limit: 1 }),
        globalRedemptions: 0,
        distinctCodesForPlayer: 7,
        alreadyRedeemedThisCode: false,
    };
    const { res } = await redeem({ playerId: OWNER, unproven: true, state: state });
    assert.equal(res.body.success, false,
        'an UNPROVEN caller reached the owner bypass - the body can claim any id');
    assert.equal(res.body.error, 'PLAYER_LIMIT_REACHED');
});

test('an ordinary GUEST is unchanged', async () => {
    const state = {
        promo: promoRow({ per_player_limit: 1 }),
        globalRedemptions: 0,
        distinctCodesForPlayer: 7,
        alreadyRedeemedThisCode: false,
    };
    const { res } = await redeem({ playerId: GUEST, unproven: true, state: state });
    assert.equal(res.body.success, false);
    assert.equal(res.body.error, 'PLAYER_LIMIT_REACHED');
});

// ── 5. The grant is recorded and audited ────────────────────────────────────

test('the owner grant still records a redemption row and audits mode: owner-bypass', async () => {
    AUDIT_EVENTS.length = 0;
    const state = {
        promo: promoRow({ per_player_limit: 1 }),
        globalRedemptions: 0,
        distinctCodesForPlayer: 7,
        alreadyRedeemedThisCode: false,
    };
    const { res, sql } = await redeem({ playerId: OWNER, state: state });
    assert.equal(res.body.success, true);

    const claim = sql.calls.find((c) => /WITH claimed/.test(c.text));
    assert.match(claim.text, /INSERT INTO promo_redemptions/,
        'a bypassed grant must STILL write the ledger row');

    const evt = AUDIT_EVENTS.find((e) => e.properties && e.properties.mode === 'owner-bypass');
    assert.ok(evt, 'no audit event carried mode: "owner-bypass" - a bypassed grant must be MORE visible');
    assert.equal(evt.identity, OWNER);
    assert.equal(evt.properties.code, 'LINK01');
});

// ── 6. What is deliberately NOT bypassed ────────────────────────────────────

test('the owner is still refused an EXPIRED code and a code bound to someone else', async () => {
    const expired = {
        promo: promoRow({ expires_at: new Date(Date.now() - 60000).toISOString() }),
        globalRedemptions: 0, distinctCodesForPlayer: 0, alreadyRedeemedThisCode: false,
    };
    const a = await redeem({ playerId: OWNER, state: expired });
    assert.equal(a.res.body.error, 'EXPIRED', 'expiry is not an abuse guard and must still hold');

    const bound = {
        promo: promoRow({ bound_wallet: OTHER_WALLET }),
        globalRedemptions: 0, distinctCodesForPlayer: 0, alreadyRedeemedThisCode: false,
    };
    const b = await redeem({ playerId: OWNER, state: bound });
    assert.equal(b.res.body.error, 'INVALID_CODE', "a code bound to another player stays theirs");
});

test('the owner still redeems each individual code only once', async () => {
    const state = {
        promo: promoRow(),
        globalRedemptions: 0, distinctCodesForPlayer: 0, alreadyRedeemedThisCode: true,
    };
    const { res } = await redeem({ playerId: OWNER, state: state });
    assert.equal(res.body.error, 'ALREADY_REDEEMED',
        'UNIQUE(code, player_id) is the record of the grant, not an abuse guard - it stays');
});
