// =============================================================================
// api/admin/ops.js - THE WRITE ENDPOINT for the Command Center (WO-1244).
// -----------------------------------------------------------------------------
// The ONLY admin endpoint in this repo that writes. api/admin/db.js and
// api/admin/stats.js are read-only BY CONSTRUCTION and stay that way; WO-1169
// and WO-1244 both put the read/write boundary AT THE ENDPOINT LEVEL, not in the
// UI, and this file is that boundary.
//
//   POST /api/admin/ops   { action, by, ...payload }
//
//     action = "maintenance.seal"   { area, message }
//            | "maintenance.open"   { area }
//            | "promo.create"       { code, rewardCrystals|rewardPackSku, ... }
//            | "promo.set_active"   { code, active }
//            | "purchase.alert_acknowledge" { txSignature, reason }
//
// ⛔ SEPARATELY GATED - TWO KEYS, AND THE SECOND ONE IS THE POINT.
// -----------------------------------------------------------------------------
//   X-Admin-Key      must match ADMIN_DASH_KEY   (the read key, same as db/stats)
//   X-Admin-Ops-Key  must match ADMIN_OPS_KEY    (a SECOND secret, writes only)
//
// Why not one key: the read key is typed into a phone browser, in a hurry, in
// public, and ends up in screenshots of the dashboard. That is an acceptable
// exposure for a read surface. It is NOT acceptable for a surface that can seal
// the whole game or mint free currency. A second secret means a leaked read key
// buys a reader exactly nothing more than reading.
//
// ⚠ FAIL CLOSED, ALWAYS - and this is the exact OPPOSITE of api/_lib/maintenance.js,
// on purpose. There, an unreadable table must leave the game OPEN, because a
// player must never lose their session to our outage. Here, a missing key must
// refuse the write, because "we could not check who you are" can never resolve to
// "go ahead and change the money tables". Availability there; correctness here.
// If ADMIN_OPS_KEY is unset the endpoint answers OPS_WRITE_NOT_CONFIGURED and
// writes nothing - see the console's own banner, which says so in words.
//
// ⛔ NO CORS HEADERS ARE SET. Deliberate, and the same choice api/admin/cleanup.js
// makes. The Command Center console is served from /api/admin/console on THIS
// deployment, so it is same-origin and needs none. Anything cross-origin - the
// site/ dashboard, a page someone else hosts - is blocked by the browser before
// the function runs. A write endpoint has no business being callable from a page
// we did not serve.
//
// ⛔ NOTHING HERE LOGS A SECRET. Not ADMIN_DASH_KEY, not ADMIN_OPS_KEY, not
// DATABASE_URL, not a wallet, not an email, not a real name. The audit trail
// carries an OPERATOR LABEL and a target id, and that is all it needs.
//
// Status codes stay 200 | 400 | 500 (project constraint, see api/admin/db.js).
// =============================================================================

const { neon } = require('@neondatabase/serverless');
const { readRawBody } = require('../_lib/http');
const {
    OPS_ACTIONS,
    OpsError,
    createPromo,
    acknowledgePurchaseAlert,
    keyOk,
    normalizeOperator,
    normalizePromoCode,
    recordOpsWrite,
    setMaintenance,
    setPromoActive,
    validateOpen,
    validatePromoDraft,
    validateSeal,
    validatePurchaseAlertAcknowledgement,
    validateTunableSet,
    validateTunableClear,
    setTunable,
    clearTunable,
} = require('../_lib/ops');

/** Bodies here are a handful of short fields. Anything larger is not our client. */
const MAX_BODY_BYTES = 16 * 1024;

/**
 * Read the JSON body whether or not the platform already parsed it. Never
 * throws; a body we cannot understand becomes null and the handler refuses.
 */
async function readJsonBody(req) {
    try {
        if (req && req.body != null) {
            if (typeof req.body === 'object' && !Buffer.isBuffer(req.body)) return req.body;
            const s = Buffer.isBuffer(req.body) ? req.body.toString('utf8') : String(req.body);
            return s.trim() ? JSON.parse(s) : {};
        }
        const buf = await readRawBody(req, MAX_BODY_BYTES);
        const s = buf.toString('utf8');
        return s.trim() ? JSON.parse(s) : {};
    } catch (_) {
        return null;
    }
}

module.exports = async (req, res) => {
    // No CORS. See the header - this is same-origin only, by design.
    res.setHeader('Cache-Control', 'no-store');

    // POST only. A write that can be triggered by a GET is a write that can be
    // triggered by a link someone sends you.
    if (req.method !== 'POST') {
        return res.status(400).json({ ok: false, code: 'METHOD_NOT_ALLOWED' });
    }

    const readKey = process.env.ADMIN_DASH_KEY;
    const opsKey = process.env.ADMIN_OPS_KEY;

    if (!readKey) {
        return res.status(400).json({ ok: false, code: 'ADMIN_NOT_CONFIGURED' });
    }
    if (!keyOk(req.headers['x-admin-key'], readKey)) {
        return res.status(400).json({ ok: false, code: 'UNAUTHORIZED' });
    }
    if (!opsKey) {
        // Said in WORDS, with the remedy, because the alternative is the owner
        // tapping "Seal" on a phone during an incident and getting "Unauthorized"
        // with no idea that the deployment is simply missing an env var.
        return res.status(400).json({
            ok: false,
            code: 'OPS_WRITE_NOT_CONFIGURED',
            hint: 'Set ADMIN_OPS_KEY on the deployment. It is a SECOND secret, ' +
                  'separate from ADMIN_DASH_KEY, and it gates every write.',
        });
    }
    if (!keyOk(req.headers['x-admin-ops-key'], opsKey)) {
        return res.status(400).json({ ok: false, code: 'OPS_UNAUTHORIZED' });
    }

    const body = await readJsonBody(req);
    if (!body || typeof body !== 'object') {
        return res.status(400).json({ ok: false, code: 'BAD_BODY' });
    }

    const action = String(body.action || '');
    if (OPS_ACTIONS.indexOf(action) < 0) {
        return res.status(400).json({
            ok: false, code: 'UNKNOWN_ACTION',
            hint: 'one of ' + OPS_ACTIONS.join(', '),
        });
    }

    let sql = null;
    try { sql = neon(process.env.DATABASE_URL); }
    catch (_) { sql = null; }
    if (!sql) {
        return res.status(400).json({ ok: false, code: 'NO_DATABASE' });
    }

    let operator;
    try { operator = normalizeOperator(body.by); }
    catch (err) { return res.status(400).json({ ok: false, code: err.code || 'BAD_OPERATOR' }); }

    const at = new Date().toISOString();

    try {
        if (action === 'maintenance.seal') {
            const v = validateSeal(body);
            const row = await setMaintenance(sql, v.area, true, v.message, operator);
            await recordOpsWrite(sql, {
                action: action, operator: operator, target: v.area, outcome: 'sealed',
                detail: { messageLen: v.message.length },
            });
            return res.status(200).json({
                ok: true, action: action, at: at, by: operator,
                // "SEALED"/"open" in WORDS. The owner is red/green colourblind and
                // no state in this system may live in a colour.
                state: row.closed ? 'SEALED' : 'open',
                area: row.area_id,
                message: row.message || null,
                updated_by: row.updated_by,
                updated_at: row.updated_at,
                note: 'Server-side enforcement takes effect within ~5s (lambda memo). ' +
                      'The player banner catches up within ~40s (10s edge + 30s client poll).',
            });
        }

        if (action === 'maintenance.open') {
            const v = validateOpen(body);
            // Opening clears the banner text along with the seal - a stale
            // "closed for maintenance" message on an open area is a lie.
            const row = await setMaintenance(sql, v.area, false, null, operator);
            await recordOpsWrite(sql, {
                action: action, operator: operator, target: v.area, outcome: 'opened', detail: {},
            });
            return res.status(200).json({
                ok: true, action: action, at: at, by: operator,
                state: row.closed ? 'SEALED' : 'open',
                area: row.area_id,
                message: row.message || null,
                updated_by: row.updated_by,
                updated_at: row.updated_at,
            });
        }

        // ---------------------------------------------------------------------
        // PROD-022 - the remote knobs. Flipping one changes CLIENT behaviour on the
        // next poll (about 40 s: 10 s edge cache + 30 s client poll) with no rebuild
        // and no deploy. Every knob's default lives in the BUILD, so clearing a row
        // is the one-word way back to today's behaviour.
        // ---------------------------------------------------------------------
        if (action === 'tunable.set') {
            const v = validateTunableSet(body);
            const row = await setTunable(sql, v.key, v.value, operator);
            await recordOpsWrite(sql, {
                action: action, operator: operator, target: v.key, outcome: 'set',
                detail: { value: v.value },
            });
            return res.status(200).json({
                ok: true, action: action, at: at, by: operator,
                key: row.key,
                value: row.value,
                updated_by: row.updated_by,
                updated_at: row.updated_at,
                note: 'Clients pick this up within about 40s (10s edge cache + 30s poll). ' +
                      'Clear the row to return this knob to the value the BUILD hardcodes - ' +
                      'setting it to 0 is not the same thing.',
            });
        }

        if (action === 'tunable.clear') {
            const v = validateTunableClear(body);
            const result = await clearTunable(sql, v.key);
            await recordOpsWrite(sql, {
                action: action, operator: operator, target: v.key,
                outcome: result.existed ? 'cleared' : 'already_default', detail: {},
            });
            return res.status(200).json({
                ok: true, action: action, at: at, by: operator,
                key: result.key,
                // In WORDS. The owner is red/green colourblind and no state in this
                // system may live in a colour.
                state: 'DEFAULT (the value this build hardcodes)',
                had_override: result.existed,
            });
        }

        if (action === 'promo.create') {
            const draft = validatePromoDraft(body, Date.now());
            const made = await createPromo(sql, draft, operator);
            await recordOpsWrite(sql, {
                action: action, operator: operator, target: draft.code, outcome: 'created',
                detail: {
                    shape: made.shape,
                    packSku: draft.rewardPackSku,
                    crystals: draft.rewardCrystals,
                    coins: draft.rewardCoins,
                    maxRedemptions: draft.maxRedemptions,
                    perPlayerLimit: draft.perPlayerLimit,
                    expiresAt: draft.expiresAt,
                },
            });
            return res.status(200).json({
                ok: true, action: action, at: at, by: operator,
                code: made.row.code,
                state: made.row.active ? 'ACTIVE' : 'DISABLED',
                created_at: made.row.created_at,
                attribution_on_row: made.shape === 'with_created_by',
                warning: made.shape === 'with_created_by' ? null
                    : 'promo_codes.created_by does not exist on the deployed database, so this ' +
                      'code carries no operator attribution on its row. The history row in ' +
                      'analytics_events does. Run: ALTER TABLE promo_codes ADD COLUMN IF NOT ' +
                      'EXISTS created_by TEXT;',
            });
        }

        if (action === 'promo.set_active') {
            const code = normalizePromoCode(body.code);
            const active = body.active === true;
            const row = await setPromoActive(sql, code, active);
            await recordOpsWrite(sql, {
                action: action, operator: operator, target: code,
                outcome: active ? 'enabled' : 'disabled', detail: {},
            });
            return res.status(200).json({
                ok: true, action: action, at: at, by: operator,
                code: row.code,
                state: row.active ? 'ACTIVE' : 'DISABLED',
            });
        }

        if (action === 'purchase.alert_acknowledge') {
            const v = validatePurchaseAlertAcknowledgement(body);
            const result = await acknowledgePurchaseAlert(
                sql, v.signature, v.reason, operator);
            return res.status(200).json({
                ok: true, action: action, at: at, by: operator,
                state: 'ACKNOWLEDGED - NO ACTION',
                tx_signature: v.signature,
                acknowledged_at: result.acknowledgedAt,
                already_acknowledged: result.alreadyAcknowledged,
                note: 'Source telemetry was preserved. No refund, grant, SKU, quote, or entitlement was changed.',
            });
        }

        // Unreachable: the action allowlist above is exhaustive.
        return res.status(400).json({ ok: false, code: 'UNKNOWN_ACTION' });
    } catch (err) {
        if (err instanceof OpsError) {
            return res.status(400).json({ ok: false, code: err.code, hint: err.message });
        }
        // The message may contain SQL text or column names; it never reaches the
        // caller. It goes to the runtime log, which needs no key to read.
        try { console.error('[admin/ops] ' + action + ' failed:', err); } catch (_) { /* noop */ }
        return res.status(500).json({ ok: false, code: 'WRITE_FAILED' });
    }
};
