'use strict';

// =============================================================================
// POST /api/pi/complete — WO-1318. The money HAS moved by the time this runs.
// -----------------------------------------------------------------------------
// The Pi SDK calls `onReadyForServerCompletion(paymentId, txid)` once the
// Pioneer's transaction is on the Pi blockchain. Everything here therefore runs
// AFTER settlement, which changes what "refuse" is allowed to mean:
//
//   ⛔ A PAYMENT WE CANNOT GRANT IS RECORDED, NEVER DROPPED. A dropped incomplete
//   payment is a player who paid and got nothing. Every path below either grants
//   or writes a durable row that says a payment exists and needs a human.
//
// IDEMPOTENCY — a payment completed twice must not grant twice. Three independent
// layers, the same three the SKR rail uses:
//   1. pi_payments.state = 'granted' short-circuits before Pi is even called,
//   2. purchase_quotes is SINGLE-USE (conditional UPDATE claims it for exactly
//      one payment id),
//   3. purchase_entitlements.tx_signature is UNIQUE and the insert is
//      ON CONFLICT DO NOTHING, with a read-back of the race winner.
//
// ⭐ `quoteId` IS OPTIONAL HERE, AND THAT IS LOAD-BEARING. `onIncompletePayment
// Found(payment)` fires on a LATER LAUNCH, in a session that never saw the quote
// id. Demanding it would strand exactly the payment that flow exists to rescue,
// so the id is recovered from our own ledger, or from the payment's metadata.
//
// WIRE
//   ->  { paymentId, txid, quoteId? }
//   <-  200 { ok:true, state:'granted', paymentId, txid, sku, entitlementId }
//       409 { ok:false, code:'PI_MANUAL_REVIEW', message, ref }  — paid, not granted
//       503 { ok:false, code:'PI_UNREACHABLE'|'PI_NOT_CONFIGURED', message, ref }
// =============================================================================

const { neon } = require('@neondatabase/serverless');
const { applyCors, newRef, quietFail, readBodyExact } = require('../_lib/http');
const { logApiEvent } = require('../_lib/audit');
const pi = require('../_lib/pi-payments');

const MAX_BODY_BYTES = 16 * 1024;

// The payee of record when Pi does not report `to_address` on the payment object.
// ⛔ A MARKER, NOT A FABRICATED ADDRESS. purchase_entitlements.recipient is NOT
// NULL, and writing a made-up wallet there would make a revenue row lie. This
// string is unmistakably not an address, so a reconciliation query can find it.
const UNKNOWN_PAYEE = 'pi:app-wallet';

function refuse(res, status, code, ref, extra) {
    return res.status(status).json(Object.assign({ ok: false, code,
        message: pi.PI_MESSAGES[code] || undefined, ref }, extra || {}));
}

async function tryDb(run) {
    try { return { ok: true, rows: await run() }; }
    catch (err) { return { ok: false, err }; }
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
    const txid = String(body.txid || '').trim();
    let quoteId = String(body.quoteId || '').trim();
    if (!pi.PAYMENT_ID_RE.test(paymentId) || !pi.TXID_RE.test(txid))
        return quietFail(res, 400, 'BAD_PAYLOAD', ref);
    if (quoteId && !pi.QUOTE_REF_RE.test(quoteId)) return quietFail(res, 400, 'BAD_PAYLOAD', ref);

    let sql;
    try { sql = neon(process.env.DATABASE_URL); }
    catch (_) { return quietFail(res, 500, 'SERVER_ERROR', ref); }

    // ── LAYER 1: have we already granted this payment? ──────────────────────
    const ledgerQ = await tryDb(() => sql`
        SELECT payment_id, player_id, sku, quote_ref, state, txid, to_address
        FROM pi_payments WHERE payment_id = ${paymentId} LIMIT 1`);
    if (!ledgerQ.ok) return refuse(res, 503, 'PI_RECORD_FAILED', ref);
    const ledger = ledgerQ.rows && ledgerQ.rows.length ? ledgerQ.rows[0] : null;
    if (ledger && ledger.state === 'granted') {
        await logApiEvent(sql, ledger.player_id, 'pi_payment_complete_replayed',
            { ref, paymentId, quoteId: ledger.quote_ref, txid });
        return res.status(200).json({ ok: true, state: 'granted', replay: true,
            paymentId, txid: ledger.txid || txid, sku: ledger.sku, ref });
    }
    if (!quoteId && ledger && ledger.quote_ref) quoteId = String(ledger.quote_ref);

    // ── The payment, read back FROM PI. ─────────────────────────────────────
    const fetched = await pi.getPayment(paymentId);
    if (!fetched.ok) {
        await logApiEvent(sql, ledger ? ledger.player_id : null, 'pi_payment_lookup_failed',
            { ref, paymentId, quoteId: quoteId || null, reason: fetched.code, stage: 'complete' });
        return refuse(res, fetched.status === 404 ? 400 : 503,
            fetched.status === 404 ? 'PI_PAYMENT_UNKNOWN' : 'PI_UNREACHABLE', ref);
    }
    const payment = fetched.body;
    const meta = (payment && payment.metadata) || {};
    // Last resort for the rescue path: the id the payment itself carries.
    if (!quoteId && pi.QUOTE_REF_RE.test(String(meta.quoteId || ''))) quoteId = String(meta.quoteId);
    if (!quoteId) {
        await logApiEvent(sql, null, 'pi_payment_manual_review',
            { ref, paymentId, txid, reason: 'quote_unrecoverable' });
        return refuse(res, 409, 'PI_MANUAL_REVIEW', ref);
    }

    const rowsQ = await tryDb(() => sql`
        SELECT quote_ref, wallet, sku, network, currency, amount_base_units, decimals,
               usd_anchor, usd_rate, rate_source, expires_at, consumed_at, consumed_tx
        FROM purchase_quotes WHERE quote_ref = ${quoteId} LIMIT 1`);
    if (!rowsQ.ok) return refuse(res, 503, 'PI_RECORD_FAILED', ref);
    const row = rowsQ.rows && rowsQ.rows.length ? rowsQ.rows[0] : null;
    if (!row) {
        await logApiEvent(sql, null, 'pi_payment_manual_review',
            { ref, paymentId, txid, quoteId, reason: 'quote_unknown_after_payment' });
        return refuse(res, 409, 'PI_MANUAL_REVIEW', ref);
    }
    const playerId = String(row.wallet);

    // ── The amount gate, RUN AGAIN. It passed at approve; it is re-run because
    // this is the side of the rail where being wrong costs real money and cannot
    // be taken back. A mismatch here is recorded for a human, NEVER granted and
    // NEVER discarded.
    const check = pi.validatePaymentAgainstQuote(payment, row, quoteId, playerId);
    if (!check.ok) {
        await tryDb(() => sql`
            UPDATE pi_payments SET state = 'manual_review', reject_reason = ${check.code},
                   txid = COALESCE(txid, ${txid}), updated_at = NOW()
             WHERE payment_id = ${paymentId}`);
        await logApiEvent(sql, playerId, 'pi_payment_manual_review',
            { ref, paymentId, txid, quoteId, reason: check.code,
              expectedBaseUnits: String(row.amount_base_units) });
        return refuse(res, 409, 'PI_MANUAL_REVIEW', ref, { reason: check.code });
    }

    // Pi's own record of the transaction outranks the client's claim of it.
    const reported = payment.transaction && payment.transaction.txid
        ? String(payment.transaction.txid) : null;
    if (reported && reported !== txid) {
        await logApiEvent(sql, playerId, 'pi_payment_manual_review',
            { ref, paymentId, quoteId, reason: 'PI_TXID_MISMATCH' });
        return refuse(res, 409, 'PI_MANUAL_REVIEW', ref, { reason: 'PI_TXID_MISMATCH' });
    }

    // ── Complete with Pi, unless Pi already has. ────────────────────────────
    const status = (payment && payment.status) || {};
    if (status.developer_completed !== true) {
        const done = await pi.completePayment(paymentId, txid);
        if (!done.ok) {
            // Re-read: "already completed" is a success we must not report as a
            // failure, or the client retries forever on a payment that is finished.
            const again = await pi.getPayment(paymentId);
            const ok = again.ok && again.body && again.body.status &&
                again.body.status.developer_completed === true;
            if (!ok) {
                await logApiEvent(sql, playerId, 'pi_payment_complete_failed',
                    { ref, paymentId, quoteId, txid, reason: done.code });
                return refuse(res, 503, 'PI_UNREACHABLE', ref);
            }
        }
    }

    // ── LAYER 2: the quote is SINGLE-USE, claimed atomically for THIS payment. ─
    const consumedQ = await tryDb(() => sql`
        UPDATE purchase_quotes
           SET consumed_at = COALESCE(consumed_at, NOW()), consumed_tx = ${paymentId}
         WHERE quote_ref = ${quoteId}
           AND (consumed_tx IS NULL OR consumed_tx = ${paymentId})
        RETURNING quote_ref`);
    if (!consumedQ.ok) return refuse(res, 503, 'PI_RECORD_FAILED', ref);
    if (!consumedQ.rows.length) {
        // Another PAYMENT already spent this quote. The money for THIS one has
        // moved, so it is recorded for review, not dropped.
        await tryDb(() => sql`
            UPDATE pi_payments SET state = 'manual_review', reject_reason = 'PI_QUOTE_ALREADY_USED',
                   txid = COALESCE(txid, ${txid}), updated_at = NOW()
             WHERE payment_id = ${paymentId}`);
        await logApiEvent(sql, playerId, 'pi_payment_manual_review',
            { ref, paymentId, txid, quoteId, reason: 'PI_QUOTE_ALREADY_USED' });
        return refuse(res, 409, 'PI_MANUAL_REVIEW', ref, { reason: 'PI_QUOTE_ALREADY_USED' });
    }

    // ── LAYER 3: THE GRANT, in the EXISTING ledger. ─────────────────────────
    // Same table, same columns, same replay protection as the SKR settlement —
    // `rail` is what distinguishes them, so revenue reporting sees one ledger.
    // The Pi txid takes the tx_signature slot: it is this rail's globally unique,
    // once-only proof that money moved.
    const payee = check.toAddress || (ledger && ledger.to_address) || UNKNOWN_PAYEE;
    const insertedQ = await tryDb(() => sql`
        INSERT INTO purchase_entitlements
            (tx_signature, wallet, sku, rail, network, currency, expected_lamports,
             observed_lamports, recipient, observed_recipient, chain_slot, status, verified_at,
             quote_ref, usd_anchor, usd_rate, rate_source)
        VALUES (${txid}, ${playerId}, ${row.sku}, ${pi.PI_RAIL}, ${pi.PI_NETWORK}, ${pi.PI_CURRENCY},
                ${row.amount_base_units}, ${row.amount_base_units}, ${payee}, ${payee},
                NULL, 'verified', NOW(), ${quoteId}, ${row.usd_anchor}, ${row.usd_rate},
                ${row.rate_source})
        ON CONFLICT (tx_signature) DO NOTHING RETURNING entitlement_id`);
    if (!insertedQ.ok) {
        await logApiEvent(sql, playerId, 'pi_payment_record_failed',
            { ref, paymentId, txid, quoteId, stage: 'record_entitlement' });
        return refuse(res, 503, 'PI_RECORD_FAILED', ref);
    }
    let entitlementId = insertedQ.rows.length ? String(insertedQ.rows[0].entitlement_id) : null;
    if (!entitlementId) {
        // Lost a harmless race with our own retry: read the winner back rather
        // than telling a paid, RECORDED player that their payment conflicts.
        const racedQ = await tryDb(() => sql`
            SELECT entitlement_id, wallet, sku FROM purchase_entitlements
             WHERE tx_signature = ${txid} LIMIT 1`);
        if (!racedQ.ok || !racedQ.rows.length) return refuse(res, 503, 'PI_RECORD_FAILED', ref);
        if (String(racedQ.rows[0].wallet) !== playerId || String(racedQ.rows[0].sku) !== String(row.sku))
            return refuse(res, 409, 'PI_MANUAL_REVIEW', ref, { reason: 'PI_TXID_REUSED' });
        entitlementId = String(racedQ.rows[0].entitlement_id);
    }

    await tryDb(() => sql`
        INSERT INTO pi_payments
            (payment_id, player_id, pi_uid, sku, quote_ref, amount_base_units, decimals,
             state, txid, to_address, approved_at, completed_at, granted_at)
        VALUES (${paymentId}, ${playerId}, ${pi.piUidOf(playerId)}, ${row.sku}, ${quoteId},
                ${row.amount_base_units}, ${row.decimals}, 'granted', ${txid}, ${payee},
                NOW(), NOW(), NOW())
        ON CONFLICT (payment_id) DO UPDATE
           SET state = 'granted', txid = ${txid},
               completed_at = COALESCE(pi_payments.completed_at, NOW()),
               granted_at = COALESCE(pi_payments.granted_at, NOW()),
               reject_reason = NULL, updated_at = NOW()`);

    await logApiEvent(sql, playerId, 'pi_entitlement_created', { ref, paymentId, txid, quoteId,
        sku: row.sku, amountBaseUnits: String(row.amount_base_units),
        rate: row.usd_rate, rateSource: row.rate_source });

    // The pack CONTENTS are not enumerated here: the client already holds the
    // canonical packs.json and applies them, exactly as it does on the SKR rail.
    // One catalog, one grant path.
    return res.status(200).json({ ok: true, state: 'granted', paymentId, txid,
        quoteId, sku: row.sku, entitlementId,
        amount: pi.baseUnitsToAmount(row.amount_base_units), ref });
}

module.exports = handler;
module.exports.config = { api: { bodyParser: false } };
module.exports._test = { refuse, tryDb, UNKNOWN_PAYEE };
