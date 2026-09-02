'use strict';

// =============================================================================
// POST /api/pi/approve — WO-1318. THE AMOUNT GATE. Nothing else in this rail can
// refuse a forged price, because after this call the Pioneer's Pi can move.
// -----------------------------------------------------------------------------
// The Pi SDK calls `onReadyForServerApproval(paymentId)` in the player's browser.
// We do NOT approve on being asked. We:
//
//   1. look the quote up BY ID in purchase_quotes — the row the SERVER wrote,
//   2. read the payment back FROM PI (GET /v2/payments/:id) — not from the client,
//   3. refuse unless the payment's amount, in INTEGER base units, equals the
//      persisted amount EXACTLY, and its metadata/uid/memo all agree,
//   4. and only then POST /v2/payments/:id/approve.
//
// ⛔ A CLIENT THAT INVENTS AN AMOUNT LANDS ON STEP 3 AND IS REFUSED WITH
// PI_AMOUNT_MISMATCH. Nothing is approved, so nothing is charged and nothing is
// granted. That refusal is pinned by test/pi-payments.test.js.
//
// WIRE
//   ->  { paymentId, quoteId }
//   <-  200 { ok:true, state:'approved', paymentId, sku, amount }
//       400/403 { ok:false, code, message, ref }   — refused, nothing charged
//       503 { ok:false, code:'PI_UNREACHABLE'|'PI_NOT_CONFIGURED', message, ref }
// =============================================================================

const { neon } = require('@neondatabase/serverless');
const { applyCors, newRef, quietFail, readBodyExact } = require('../_lib/http');
const { logApiEvent } = require('../_lib/audit');
const pi = require('../_lib/pi-payments');

const MAX_BODY_BYTES = 16 * 1024;

function refuse(res, status, code, ref, extra) {
    return res.status(status).json(Object.assign({ ok: false, code,
        message: pi.PI_MESSAGES[code] || undefined, ref }, extra || {}));
}

/** Record a refusal durably. Never silent (CLAUDE.md §12.2). */
async function recordRejection(sql, playerId, ref, paymentId, quoteId, code, sku) {
    try {
        await sql`
            INSERT INTO pi_payments
                (payment_id, player_id, pi_uid, sku, quote_ref, amount_base_units, decimals,
                 state, reject_reason)
            VALUES (${paymentId}, ${playerId || 'unknown'}, ${pi.piUidOf(playerId) || 'unknown'},
                    ${sku || 'unknown'}, ${quoteId}, 1, ${pi.PI_DECIMALS}, 'rejected', ${code})
            ON CONFLICT (payment_id) DO UPDATE
               SET reject_reason = EXCLUDED.reject_reason, updated_at = NOW()
             WHERE pi_payments.state = 'rejected'`;
    } catch (_) { /* the audit row below is the second, independent record */ }
    await logApiEvent(sql, playerId, 'pi_payment_rejected',
        { ref, paymentId, quoteId, sku: sku || null, reason: code });
}

async function handler(req, res) {
    if (applyCors(req, res, 'POST, OPTIONS')) return;
    const ref = newRef();
    if (req.method !== 'POST') return quietFail(res, 400, 'METHOD_NOT_ALLOWED', ref);
    if (!pi.configured()) return refuse(res, 503, 'PI_NOT_CONFIGURED', ref);

    let body;
    try { body = JSON.parse((await readBodyExact(req, MAX_BODY_BYTES)).buffer.toString('utf8')); }
    catch (_) { return quietFail(res, 400, 'BAD_PAYLOAD', ref); }

    const paymentId = String(body.paymentId || '').trim();
    const quoteId = String(body.quoteId || '').trim();
    // ⛔ NOTE WHAT IS *NOT* READ FROM THE BODY: an amount, a uid, a sku. Every one
    // of those comes from the persisted quote or from Pi. A client that invents one
    // has nowhere to put it.
    if (!pi.PAYMENT_ID_RE.test(paymentId) || !pi.QUOTE_REF_RE.test(quoteId))
        return quietFail(res, 400, 'BAD_PAYLOAD', ref);

    let sql;
    try { sql = neon(process.env.DATABASE_URL); }
    catch (_) { return quietFail(res, 500, 'SERVER_ERROR', ref); }

    // ── 1. The quote WE issued. ──────────────────────────────────────────────
    let rows;
    try {
        rows = await sql`
            SELECT quote_ref, wallet, sku, network, currency, amount_base_units, decimals,
                   usd_anchor, usd_rate, rate_source, expires_at, consumed_at, consumed_tx
            FROM purchase_quotes WHERE quote_ref = ${quoteId} LIMIT 1`;
    } catch (_) { return quietFail(res, 500, 'SERVER_ERROR', ref); }
    const row = rows && rows.length ? rows[0] : null;
    if (!row) return refuse(res, 400, 'PI_QUOTE_UNKNOWN', ref);

    const playerId = String(row.wallet);
    const usable = pi.evaluatePiQuoteRow(row, playerId, row.sku, paymentId);
    if (!usable.ok) {
        await recordRejection(sql, playerId, ref, paymentId, quoteId, usable.code, row.sku);
        return refuse(res, 400, usable.code, ref);
    }

    // Expiry IS judged by wall clock here, and that is correct on THIS half of the
    // rail: approve happens BEFORE any money moves, so refusing an expired quote
    // costs the player nothing but a fresh price. (The SKR rail judges expiry by
    // blockTime instead precisely because ITS check runs after settlement.)
    if (!(Date.now() < new Date(row.expires_at).getTime())) {
        await recordRejection(sql, playerId, ref, paymentId, quoteId, 'PI_QUOTE_EXPIRED', row.sku);
        return refuse(res, 400, 'PI_QUOTE_EXPIRED', ref);
    }

    // ── 2. The payment, READ BACK FROM PI. Never from the request body. ──────
    const fetched = await pi.getPayment(paymentId);
    if (!fetched.ok) {
        await logApiEvent(sql, playerId, 'pi_payment_lookup_failed',
            { ref, paymentId, quoteId, reason: fetched.code });
        return refuse(res, fetched.status === 404 ? 400 : 503,
            fetched.status === 404 ? 'PI_PAYMENT_UNKNOWN' : 'PI_UNREACHABLE', ref);
    }
    const payment = fetched.body;

    // ── 3. THE GATE. ────────────────────────────────────────────────────────
    const check = pi.validatePaymentAgainstQuote(payment, row, quoteId, playerId);
    if (!check.ok) {
        await recordRejection(sql, playerId, ref, paymentId, quoteId, check.code, row.sku);
        return refuse(res, check.code === 'PI_AMOUNT_MISMATCH' ? 403 : 400, check.code, ref,
            { state: 'rejected' });
    }

    const status = (payment && payment.status) || {};
    if (status.cancelled === true) {
        await recordRejection(sql, playerId, ref, paymentId, quoteId, 'PI_PAYMENT_CANCELLED', row.sku);
        return refuse(res, 400, 'PI_PAYMENT_CANCELLED', ref, { state: 'cancelled' });
    }

    // ── 4. Approve, unless Pi already recorded our approval (idempotent replay). ─
    if (status.developer_approved !== true) {
        const approved = await pi.approvePayment(paymentId);
        if (!approved.ok) {
            await logApiEvent(sql, playerId, 'pi_payment_approve_failed',
                { ref, paymentId, quoteId, reason: approved.code });
            return refuse(res, 503, 'PI_UNREACHABLE', ref);
        }
    }

    // The lifecycle ledger. NOT an entitlement — it grants nothing. It exists so a
    // replayed callback is recognisable as a replay, and so a payment that dies
    // between approve and complete is findable.
    try {
        await sql`
            INSERT INTO pi_payments
                (payment_id, player_id, pi_uid, sku, quote_ref, amount_base_units, decimals,
                 state, to_address, approved_at)
            VALUES (${paymentId}, ${playerId}, ${pi.piUidOf(playerId)}, ${row.sku}, ${quoteId},
                    ${row.amount_base_units}, ${row.decimals}, 'approved', ${check.toAddress}, NOW())
            ON CONFLICT (payment_id) DO UPDATE
               SET state = CASE WHEN pi_payments.state = 'rejected' THEN 'approved'
                                ELSE pi_payments.state END,
                   approved_at = COALESCE(pi_payments.approved_at, NOW()),
                   to_address = COALESCE(pi_payments.to_address, EXCLUDED.to_address),
                   reject_reason = NULL, updated_at = NOW()
             WHERE pi_payments.player_id = EXCLUDED.player_id
               AND pi_payments.sku = EXCLUDED.sku
               AND pi_payments.quote_ref = EXCLUDED.quote_ref`;
    } catch (_) {
        // Approval already stands with Pi at this point. Say so rather than
        // reporting a refusal the player would read as "it did not go through".
        await logApiEvent(sql, playerId, 'pi_payment_record_failed',
            { ref, paymentId, quoteId, stage: 'record_approved' });
    }

    await logApiEvent(sql, playerId, 'pi_payment_approved', { ref, paymentId, quoteId,
        sku: row.sku, amountBaseUnits: String(row.amount_base_units),
        rate: row.usd_rate, rateSource: row.rate_source });

    return res.status(200).json({ ok: true, state: 'approved', paymentId, quoteId,
        sku: row.sku, amount: pi.baseUnitsToAmount(row.amount_base_units), ref });
}

module.exports = handler;
module.exports.config = { api: { bodyParser: false } };
module.exports._test = { recordRejection, refuse };
