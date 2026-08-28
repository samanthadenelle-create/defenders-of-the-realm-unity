// =============================================================================
// api/_lib/ops.js - the WRITE HALF of the Command Center (WO-1244).
// -----------------------------------------------------------------------------
// WO-1169 states the rule this file exists to keep, and WO-1244 repeats it as
// the load-bearing architectural constraint:
//
//     READ-ONLY IS THE CONTRACT, NOT A PHASE. api/admin/db.js and
//     api/admin/stats.js are read-only BY CONSTRUCTION - every statement a
//     SELECT with a hard LIMIT. Writes live somewhere else, separately gated,
//     and every one of them is attributable and timestamped.
//
// So: this is the ONLY place in the admin surface that writes.
//
//     maintenance.seal   - close one area (WO-1243 kill switch)
//     maintenance.open   - re-open one area
//     promo.create       - author a promo code
//     promo.set_active   - disable (or re-enable) an existing promo code
//     purchase.alert_acknowledge - mark one reviewed mismatch as no-action
//
// ⛔ WHAT IS DELIBERATELY NOT HERE, AND WHY
// -----------------------------------------------------------------------------
//   * NO refund, NO grant, NO edit of purchase_quotes / purchase_entitlements.
//     The money tables are READ-ONLY from every admin surface. WO-1244: "a
//     console that can both read and write the money tables is one bug away from
//     being the worst thing in the repo." Re-granting an unfulfilled purchase is
//     a separate, separately-audited decision and it is not made from a phone.
//   * NO bound_wallet on promo authoring. promo_codes.bound_wallet exists and is
//     useful (WO-1115), but authoring one means TYPING A WALLET ADDRESS into a
//     page and reading it back out of a list. WO-1244 rule: never render or log
//     a wallet, an email or a real name. Bound codes stay a SQL / operator-CLI
//     job, where no screen renders the address. The console can see THAT a code
//     is bound (a boolean) and never to whom.
//   * NO delete of anything. Disabling a promo is `active = FALSE`, which is
//     what api/schema.sql section 3 already documents as the kill switch -
//     deleting the row would CASCADE its redemption history away.
//
// ⛔ ATTRIBUTION LIVES ON THE ROW, NOT IN A SIDE TABLE.
// -----------------------------------------------------------------------------
// maintenance_toggles already carries updated_by + updated_at (WO-1243 built it
// that way precisely so "when did we seal it, and who flipped it" survives the
// incident). Every write here stamps them. promo_codes gets the same treatment
// through an OPTIONAL created_by column.
//
// ⚠ created_by is written by a TWO-SHAPE cascade, and that is not defensive
// paranoia - it is this repo's measured reality. There is NO MIGRATION RUNNER
// here: a migration is a human running a file, and api/admin/schema-shape.js
// recorded five file-vs-database drifts on 2026-08-24 alone. A deploy reaches
// production before the SQL is run. So the insert names created_by, and on the
// undefined-column error (42703) falls back to the shape without it and SAYS SO
// in its result. It degrades ONE step and never silently fails to author a code.
//
// The HISTORY trail (every write, in order) goes to analytics_events via
// _lib/audit.logApiEvent under event_name 'admin_ops_write'. That table already
// exists, is already read by both admin endpoints, and - checked at source -
// api/admin/cleanup.js only ever purges 'web_trace' and 'api_auth_reject' BY
// NAME, so these rows are not swept. A brand-new audit table would have been the
// tidier home and would ALSO have failed tools/schema-parity.mjs on every deploy
// until a human ran the file, which is a deploy-blocking gate bought for tidiness.
//
// ⛔ NO SECRET IS EVER LOGGED HERE. Not ADMIN_DASH_KEY, not ADMIN_OPS_KEY, not
// DATABASE_URL. Nothing in this file prints an env var.
//
// CommonJS, zero dependencies. Files under api/_lib/ are NOT routed by Vercel
// (leading underscore), so this is a library, never an endpoint.
// =============================================================================

const crypto = require('crypto');
const { AREAS, isKnownArea } = require('./maintenance');
const { logApiEvent } = require('./audit');

/** The allowlisted things this file may do. */
const OPS_ACTIONS = [
    'maintenance.seal',
    'maintenance.open',
    'promo.create',
    'promo.set_active',
    'purchase.alert_acknowledge',
];

/** Event name for the durable history row. */
const OPS_AUDIT_EVENT = 'admin_ops_write';

/** Postgres "column does not exist". The only error the promo cascade retries. */
const PG_UNDEFINED_COLUMN = '42703';

/** Operator-facing prose caps. The banner scrolls past anything longer. */
const MESSAGE_MAX_LEN = 200;
const PROMO_MESSAGE_MAX_LEN = 200;
const OPERATOR_MAX_LEN = 64;
const PROMO_CODE_MIN_LEN = 3;
const PROMO_CODE_MAX_LEN = 32;
const ALERT_REASON_MAX_LEN = 120;

/** A reward figure the owner could plausibly mean. Above this it is a typo. */
const REWARD_MAX = 1000000;

/**
 * A refusal with a stable machine code. The endpoint returns the code; the
 * console prints it verbatim, because "PROMO_CODE_TOO_SHORT" is an answer and
 * "Bad request" is not.
 */
class OpsError extends Error {
    constructor(code, message) {
        super(message || code);
        this.name = 'OpsError';
        this.code = code;
    }
}

/**
 * Constant-time key check. Hashing both sides first makes timingSafeEqual usable
 * on unequal lengths without leaking length information.
 * (Identical scheme to api/admin/db.js and api/admin/stats.js - ONE admin auth
 * scheme across the surface, not three that can drift apart.)
 */
function keyOk(given, expected) {
    if (!given || !expected) return false;
    const a = crypto.createHash('sha256').update(String(given)).digest();
    const b = crypto.createHash('sha256').update(String(expected)).digest();
    return crypto.timingSafeEqual(a, b);
}

/** True when every character is printable 7-bit ASCII. */
function isAscii(s) {
    // eslint-disable-next-line no-control-regex
    return !/[^\x20-\x7E]/.test(String(s));
}

/**
 * The operator label stamped onto the row. An operator label, NEVER a player
 * identity - same rule as tools/maintenance-toggle.mjs --by.
 */
function normalizeOperator(raw) {
    const s = String(raw == null || raw === '' ? 'console' : raw).trim();
    if (!s) return 'console';
    if (!isAscii(s)) throw new OpsError('OPERATOR_NOT_ASCII', 'operator label must be ASCII');
    return s.slice(0, OPERATOR_MAX_LEN);
}

/**
 * Acknowledgements are keyed by the exact client-reported signature, including
 * malformed legacy stub values. Requiring a valid 64-byte Solana signature here
 * would make the false positives this action exists for impossible to clear.
 */
function validatePurchaseAlertAcknowledgement(body) {
    const signature = String(body && body.txSignature || '').trim();
    if (!/^[1-9A-HJ-NP-Za-km-z]{32,128}$/.test(signature))
        throw new OpsError('ALERT_SIGNATURE_INVALID', 'expected a bounded base58-like transaction signature');
    const reason = String(body && body.reason || '').trim();
    if (!reason) throw new OpsError('ALERT_REASON_REQUIRED', 'state why no action is required');
    if (!isAscii(reason)) throw new OpsError('ALERT_REASON_NOT_ASCII', 'reason must be ASCII');
    if (reason.length > ALERT_REASON_MAX_LEN)
        throw new OpsError('ALERT_REASON_TOO_LONG', `max ${ALERT_REASON_MAX_LEN} chars`);
    return { signature, reason };
}

/**
 * Validate a seal. A seal with no message puts an unexplained wall in front of a
 * paying player; and the owner is red/green colourblind, so the banner has to
 * read as maintenance from its WORDS. Both rules are WO-1243's, kept here so the
 * console cannot author something the operator CLI would have refused.
 */
function validateSeal(payload) {
    const area = String((payload && payload.area) || '').toLowerCase();
    if (!isKnownArea(area)) {
        throw new OpsError('UNKNOWN_AREA', 'area must be one of ' + AREAS.join(', '));
    }
    const message = String((payload && payload.message) || '');
    if (!message.trim()) {
        throw new OpsError('MESSAGE_REQUIRED_TO_SEAL',
            'the player banner has nothing to say without it');
    }
    if (!isAscii(message)) {
        throw new OpsError('MESSAGE_NOT_ASCII', 'the in-game banner font is ASCII-only');
    }
    if (message.length > MESSAGE_MAX_LEN) {
        throw new OpsError('MESSAGE_TOO_LONG', 'max ' + MESSAGE_MAX_LEN + ' chars; it scrolls past');
    }
    return { area: area, message: message };
}

/** Validate an open. Opening clears the banner text along with the seal. */
function validateOpen(payload) {
    const area = String((payload && payload.area) || '').toLowerCase();
    if (!isKnownArea(area)) {
        throw new OpsError('UNKNOWN_AREA', 'area must be one of ' + AREAS.join(', '));
    }
    return { area: area };
}

/**
 * Normalize a promo code the way the CLIENT does before sending it.
 * Assets/_Modules/Core/Promo/PromoCodeService.cs does code.Trim().ToUpperInvariant(),
 * and api/schema.sql section 3 says "store + compare uppercase". A code authored
 * in lower case here would be unredeemable and look like a backend bug.
 */
function normalizePromoCode(raw) {
    const s = String(raw == null ? '' : raw).trim().toUpperCase();
    if (!s) throw new OpsError('PROMO_CODE_REQUIRED', 'a code is required');
    if (!/^[A-Z0-9_-]+$/.test(s)) {
        throw new OpsError('PROMO_CODE_CHARSET', 'letters, digits, - and _ only');
    }
    if (s.length < PROMO_CODE_MIN_LEN) {
        throw new OpsError('PROMO_CODE_TOO_SHORT', 'min ' + PROMO_CODE_MIN_LEN + ' chars');
    }
    if (s.length > PROMO_CODE_MAX_LEN) {
        throw new OpsError('PROMO_CODE_TOO_LONG', 'max ' + PROMO_CODE_MAX_LEN + ' chars');
    }
    return s;
}

/**
 * A whole, non-negative count, or null for "not set".
 *
 * ⚠ THE EMPTY-STRING TRAP IS WHY THIS IS NOT parseInt. An untouched HTML number
 * input POSTs "" - and Number('') is 0, not NaN. Read through parseInt/Number
 * naively, "max redemptions: (blank)" would author max_redemptions = 0, i.e. a
 * code nobody can ever redeem, and the schema's own meaning for the field is
 * "NULL = unlimited". Blank must mean NULL.
 */
function optionalCount(raw, field, max) {
    if (raw == null) return null;
    const s = String(raw).trim();
    if (s === '') return null;
    if (!/^\d+$/.test(s)) throw new OpsError('NOT_A_WHOLE_NUMBER', field + ' must be a whole number');
    const n = Number(s);
    if (!Number.isFinite(n)) throw new OpsError('NOT_A_WHOLE_NUMBER', field + ' must be a whole number');
    if (n > (max == null ? REWARD_MAX : max)) {
        throw new OpsError('VALUE_TOO_LARGE', field + ' exceeds ' + (max == null ? REWARD_MAX : max));
    }
    return n;
}

/** A count that defaults to 0 rather than NULL (the reward columns are NOT NULL). */
function rewardCount(raw, field) {
    const n = optionalCount(raw, field, REWARD_MAX);
    return n == null ? 0 : n;
}

/**
 * An ISO expiry, or null for "never expires". Rejects a date already in the
 * past: authoring a code that is born expired is always a mistake, and the
 * redeem path would answer EXPIRED with no way to tell it from a typo.
 */
function optionalExpiry(raw, nowMs) {
    if (raw == null) return null;
    const s = String(raw).trim();
    if (s === '') return null;
    const t = Date.parse(s);
    if (!Number.isFinite(t)) throw new OpsError('BAD_EXPIRY', 'expiry must be an ISO date/time');
    const now = Number.isFinite(nowMs) ? nowMs : Date.now();
    if (t <= now) throw new OpsError('EXPIRY_IN_THE_PAST', 'that expiry has already passed');
    return new Date(t).toISOString();
}

/**
 * Validate a promo draft into exactly the columns promo_codes holds.
 *
 * PRECEDENCE, copied from api/schema.sql section 3 rather than re-invented: when
 * reward_pack_sku is set it WINS and the crystal/coin columns are ignored. So a
 * draft that sets BOTH is a REFUSAL here, not a silent pick - one source of
 * truth per code, and the operator finds out at authoring time instead of when a
 * player reports getting the wrong thing.
 */
function validatePromoDraft(payload, nowMs) {
    const p = payload || {};
    const code = normalizePromoCode(p.code);

    const packSku = p.rewardPackSku == null ? '' : String(p.rewardPackSku).trim();
    if (packSku && !isAscii(packSku)) {
        throw new OpsError('PACK_SKU_NOT_ASCII', 'pack sku must be ASCII');
    }
    if (packSku.length > 64) throw new OpsError('PACK_SKU_TOO_LONG', 'max 64 chars');

    const crystals = rewardCount(p.rewardCrystals, 'reward crystals');
    const coins = rewardCount(p.rewardCoins, 'reward coins');

    if (packSku && (crystals > 0 || coins > 0)) {
        throw new OpsError('REWARD_AMBIGUOUS',
            'set a pack sku OR crystals/coins, never both - the sku would silently win');
    }
    if (!packSku && crystals === 0 && coins === 0) {
        throw new OpsError('REWARD_EMPTY', 'this code would grant nothing');
    }

    const message = p.message == null ? '' : String(p.message).trim();
    if (message && !isAscii(message)) {
        throw new OpsError('MESSAGE_NOT_ASCII', 'the in-game toast font is ASCII-only');
    }
    if (message.length > PROMO_MESSAGE_MAX_LEN) {
        throw new OpsError('MESSAGE_TOO_LONG', 'max ' + PROMO_MESSAGE_MAX_LEN + ' chars');
    }

    return {
        code: code,
        rewardCrystals: crystals,
        rewardCoins: coins,
        rewardPackSku: packSku || null,
        message: message || null,
        maxRedemptions: optionalCount(p.maxRedemptions, 'max redemptions', 10000000),
        perPlayerLimit: optionalCount(p.perPlayerLimit, 'per player limit', 10000),
        expiresAt: optionalExpiry(p.expiresAt, nowMs),
        active: p.active === false ? false : true,
    };
}

/**
 * Record the write in the durable history trail. NEVER throws - a failed audit
 * insert must not undo a seal that already landed.
 *
 * ⚠ identity is the literal 'anonymous' on purpose. api/admin/stats.js excludes
 * exactly that id from every distinct-player metric, so an ops row cannot show up
 * as a player. The operator label lives in properties, where it is still
 * queryable and is not mistaken for a person who plays the game.
 */
async function recordOpsWrite(sql, entry) {
    try {
        await logApiEvent(sql, 'anonymous', OPS_AUDIT_EVENT, {
            action: entry && entry.action,
            operator: entry && entry.operator,
            target: entry && entry.target,
            outcome: entry && entry.outcome,
            detail: (entry && entry.detail) || {},
        });
    } catch (_) {
        // logApiEvent already swallows; this is belt and braces.
    }
}

/**
 * Persist the acknowledgement as an ordinary admin_ops_write audit row. Unlike
 * best-effort operational history, this insert is the state that suppresses the
 * warning, so it MUST throw on failure and the endpoint must fail closed.
 */
async function acknowledgePurchaseAlert(sql, signature, reason, operator) {
    const properties = JSON.stringify({
        action: 'purchase.alert_acknowledge',
        operator,
        target: signature,
        outcome: 'acknowledged_no_action',
        detail: { reason },
    });
    const rows = await sql`
        INSERT INTO analytics_events (player_id, event_name, properties, client_ts)
        SELECT 'anonymous', ${OPS_AUDIT_EVENT}, ${properties}::jsonb, ${Date.now()}
        WHERE NOT EXISTS (
            SELECT 1 FROM analytics_events
            WHERE event_name = ${OPS_AUDIT_EVENT}
              AND properties->>'action' = 'purchase.alert_acknowledge'
              AND properties->>'target' = ${signature}
              AND properties->>'outcome' = 'acknowledged_no_action'
        )
        RETURNING received_at`;
    return { acknowledgedAt: rows && rows.length ? rows[0].received_at : null, alreadyAcknowledged: !rows || !rows.length };
}

/**
 * Seal or open one area.
 *
 * ⚠ UPSERT, NOT "INSERT ... ON CONFLICT DO NOTHING" - the same reasoning
 * tools/maintenance-toggle.mjs spells out. api/schema.sql seeds the six rows with
 * DO NOTHING, which does not back-fill a database provisioned before WO-1243
 * landed. Under the fail-open ruling a MISSING row is harmless; a SEAL that
 * silently did not write would be a disaster. So the write path can never be the
 * no-op branch.
 */
async function setMaintenance(sql, area, closed, message, operator) {
    const rows = await sql`
        INSERT INTO maintenance_toggles (area_id, closed, message, updated_by, updated_at)
        VALUES (${area}, ${closed}, ${message}, ${operator}, NOW())
        ON CONFLICT (area_id) DO UPDATE
        SET closed = EXCLUDED.closed,
            message = EXCLUDED.message,
            updated_by = EXCLUDED.updated_by,
            updated_at = NOW()
        RETURNING area_id, closed, message, updated_by, updated_at`;
    // Read the row BACK rather than trusting the statement. This is the proof the
    // seal actually landed, which is the one thing that must never be assumed.
    if (!rows || !rows.length) throw new OpsError('WRITE_RETURNED_NO_ROW', 'the seal did not land');
    return rows[0];
}

/**
 * Author a promo code. Refuses to overwrite an existing code: ON CONFLICT DO
 * NOTHING here would report success while changing nothing, and re-pointing a
 * code that players may already hold is an EDIT, not a create. The operator gets
 * PROMO_CODE_EXISTS and picks another name.
 */
async function createPromo(sql, draft, operator) {
    let rows = null;
    let shape = 'with_created_by';
    try {
        rows = await sql`
            INSERT INTO promo_codes (code, reward_crystals, reward_coins, reward_pack_sku,
                                     message, active, max_redemptions, per_player_limit,
                                     expires_at, created_by)
            VALUES (${draft.code}, ${draft.rewardCrystals}, ${draft.rewardCoins},
                    ${draft.rewardPackSku}, ${draft.message}, ${draft.active},
                    ${draft.maxRedemptions}, ${draft.perPlayerLimit},
                    ${draft.expiresAt}, ${operator})
            ON CONFLICT (code) DO NOTHING
            RETURNING code, active, created_at`;
    } catch (err) {
        // THE ONE retried error, and it is retried because there is no migration
        // runner in this repo: created_by is an additive ALTER a human has to run,
        // and a deploy can beat them to it. Any OTHER error rethrows untouched.
        if (!err || err.code !== PG_UNDEFINED_COLUMN) throw err;
        shape = 'without_created_by';
        try {
            console.warn('[ops] promo_codes.created_by is missing on the deployed database - ' +
                         'authored without operator attribution on the row. Run: ' +
                         'ALTER TABLE promo_codes ADD COLUMN IF NOT EXISTS created_by TEXT;');
        } catch (_) { /* logging must never break a write */ }
        rows = await sql`
            INSERT INTO promo_codes (code, reward_crystals, reward_coins, reward_pack_sku,
                                     message, active, max_redemptions, per_player_limit,
                                     expires_at)
            VALUES (${draft.code}, ${draft.rewardCrystals}, ${draft.rewardCoins},
                    ${draft.rewardPackSku}, ${draft.message}, ${draft.active},
                    ${draft.maxRedemptions}, ${draft.perPlayerLimit},
                    ${draft.expiresAt})
            ON CONFLICT (code) DO NOTHING
            RETURNING code, active, created_at`;
    }

    if (!rows || !rows.length) {
        throw new OpsError('PROMO_CODE_EXISTS', 'that code already exists - pick another name');
    }
    return { row: rows[0], shape: shape };
}

/**
 * Flip a promo code's kill switch. UPDATE only: the code must already exist, and
 * a no-op result means it did not, which is worth saying out loud rather than
 * reporting a successful write of nothing.
 */
async function setPromoActive(sql, code, active) {
    const rows = await sql`
        UPDATE promo_codes
        SET active = ${active}
        WHERE code = ${code}
        RETURNING code, active`;
    if (!rows || !rows.length) {
        throw new OpsError('PROMO_CODE_NOT_FOUND', 'no such code');
    }
    return rows[0];
}

module.exports = {
    ALERT_REASON_MAX_LEN,
    MESSAGE_MAX_LEN,
    OPERATOR_MAX_LEN,
    OPS_ACTIONS,
    OPS_AUDIT_EVENT,
    OpsError,
    PROMO_CODE_MAX_LEN,
    PROMO_CODE_MIN_LEN,
    createPromo,
    acknowledgePurchaseAlert,
    isAscii,
    keyOk,
    normalizeOperator,
    normalizePromoCode,
    optionalCount,
    optionalExpiry,
    recordOpsWrite,
    setMaintenance,
    setPromoActive,
    validateOpen,
    validatePromoDraft,
    validateSeal,
    validatePurchaseAlertAcknowledgement,
};
