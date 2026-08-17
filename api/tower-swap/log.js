// =============================================================================
// api/tower-swap/log.js — Vercel Serverless Function
// -----------------------------------------------------------------------------
// Append-only audit log for paid instant tower swaps (Solana Pay).
//
// ── WHAT THIS ROW IS, HONESTLY (security audit 2026-08-15) ───────────────────
// This file used to call its table "on-chain proof" while trusting playerId,
// txSig, currency and costUsdc verbatim from the request body and never asking
// the chain anything. It was a FORGEABLE FINANCIAL AUDIT TRAIL, and worse: dedup
// is a partial unique index on tx_sig, so an attacker who observed or predicted a
// real signature could PRE-INSERT it and the legitimate write would be silently
// deduped away — the record of a real payment simply vanishing.
//
// Two things changed, and the row now states which it is:
//
//   1. WALLET-GATED. The acting identity goes through the same rail
//      /api/game/save uses (_lib/wallet-auth.authenticate) — exactly as
//      referral/install-brag.js already did. Nobody can log under another
//      player's id any more, which is also what removes the pre-insert grief:
//      squatting a signature now costs a proven wallet and is attributable.
//
//   2. ON-CHAIN VERIFIED when an RPC is configured. With SOLANA_RPC_URL set we
//      call getTransaction for the claimed signature and require that it EXISTS,
//      did NOT error, and was SIGNED BY the authenticated wallet. A claim that
//      fails any of those is REFUSED rather than recorded.
//
//      SCOPE OF THAT PROOF, stated plainly so nobody over-reads it: it proves the
//      signature is a real, successful transaction signed by this player. It does
//      NOT yet verify the RECIPIENT or the AMOUNT, so `cost_usdc` and `currency`
//      remain CLIENT-CLAIMED even on a verified row. Amount/recipient checking
//      needs the treasury address and the SPL mint as config; that is the next
//      step, not this one.
//
//      With no SOLANA_RPC_URL configured the row is still written, but stamped
//      verification='client-claimed' — a business record, NOT proof. Nothing in
//      this file, or in schema.sql, may call a client-claimed row proof.
//
// Client : Assets/_Modules/Village/Buildings/TowerSwapService.cs
//          (LogSwapToBackendAsync — fire-and-forget, ignores the response)
//   POST  application/json   (raw body — bodyParser disabled; the signature is
//                             over the EXACT bytes, same as save.js)
//   Headers: X-Guest-Id, or X-Wallet + X-Nonce + X-Signature
//   Body  : { playerId, waveId, fromTower, toTower, currency, costUsdc,
//             txSig, timestamp }
//           - playerId   string  (the authenticated identity — must match)
//           - waveId     int
//           - fromTower  string  (display name swapped FROM)
//           - toTower    string  (display name swapped TO)
//           - currency   string  ("Usdc" | "Skr")   CLIENT-CLAIMED
//           - costUsdc   number  (flat 2.5)         CLIENT-CLAIMED
//           - txSig      string  (Solana tx signature)
//           - timestamp  long    (unix epoch SECONDS — NOT millis)
//   Reply : { success:true, deduped, verification }
//
// Driver: @neondatabase/serverless
// Status codes: 200 | 400 | 401 | 500
// =============================================================================

const { neon } = require('@neondatabase/serverless');
const { AuthCode, authenticate, WALLET_MAX_BODY_BYTES, isGuestId } = require('../_lib/wallet-auth');
const { applyCors, newRef, quietFail, readBodyExact } = require('../_lib/http');
const { logAuthReject, logApiEvent } = require('../_lib/audit');

// Base58 (Solana signatures are 64 bytes -> 87/88 base58 chars).
const TX_SIG_RE = /^[1-9A-HJ-NP-Za-km-z]{80,90}$/;

const VERIFY_ONCHAIN = 'onchain';
const VERIFY_CLAIMED = 'client-claimed';

function rpcUrl() {
    const v = process.env.SOLANA_RPC_URL;
    return typeof v === 'string' && v.trim() !== '' ? v.trim() : null;
}

/**
 * Ask the chain about a signature.
 *
 * Returns { ok:true } only when the transaction EXISTS, succeeded (meta.err ===
 * null) and `wallet` is one of its signers. Any other outcome is a refusal with a
 * short reason — never an optimistic pass. A transport/RPC fault is reported as
 * 'rpc_unavailable' so the caller can decide (we refuse rather than record a
 * claim we could not check while claiming to check it).
 */
async function verifyOnChain(url, signature, wallet) {
    let payload;
    try {
        const resp = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                jsonrpc: '2.0',
                id: 1,
                method: 'getTransaction',
                params: [
                    signature,
                    { commitment: 'confirmed', maxSupportedTransactionVersion: 0 },
                ],
            }),
        });
        if (!resp.ok) return { ok: false, reason: 'rpc_unavailable' };
        payload = await resp.json();
    } catch (_) {
        return { ok: false, reason: 'rpc_unavailable' };
    }

    if (!payload || payload.error) return { ok: false, reason: 'rpc_unavailable' };

    const tx = payload.result;
    if (!tx) return { ok: false, reason: 'signature_not_found' };
    if (tx.meta && tx.meta.err) return { ok: false, reason: 'transaction_failed' };

    // The fee payer is accountKeys[0]; every signer sits in the leading
    // numRequiredSignatures slots. Accept the wallet in any signer slot.
    const msg = tx.transaction && tx.transaction.message ? tx.transaction.message : null;
    const keys = msg && Array.isArray(msg.accountKeys) ? msg.accountKeys : [];
    const required = msg && msg.header && Number.isFinite(msg.header.numRequiredSignatures)
        ? msg.header.numRequiredSignatures
        : 1;

    const signers = keys.slice(0, Math.max(1, required)).map((k) => (
        // accountKeys is either a base58 string per key, or (jsonParsed) an object.
        typeof k === 'string' ? k : (k && k.pubkey ? String(k.pubkey) : '')
    ));

    if (!signers.includes(wallet)) return { ok: false, reason: 'wallet_did_not_sign' };

    return { ok: true };
}

async function handler(req, res) {
    if (applyCors(req, res, 'POST, OPTIONS')) return;

    const ref = newRef();

    if (req.method !== 'POST') {
        return quietFail(res, 400, AuthCode.METHOD_NOT_ALLOWED, ref);
    }

    let rawBody, exactBytes;
    try {
        const read = await readBodyExact(req, WALLET_MAX_BODY_BYTES);
        rawBody = read.buffer;
        exactBytes = read.exact;
    } catch (err) {
        if (err && err.code === 'BODY_TOO_LARGE') {
            return quietFail(res, 400, AuthCode.PAYLOAD_TOO_LARGE, ref);
        }
        console.error('[tower-swap/log] Body read error:', err);
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }

    let body;
    try {
        body = JSON.parse(rawBody.toString('utf8'));
    } catch (err) {
        console.error('[tower-swap/log] Body parse error:', err);
        return quietFail(res, 400, AuthCode.BAD_PAYLOAD, ref);
    }
    if (!body || typeof body !== 'object') {
        return quietFail(res, 400, AuthCode.BAD_PAYLOAD, ref);
    }

    // No "anonymous" fallback: an unprovable id is precisely what made this row
    // forgeable.
    const playerId  = body.playerId  != null ? String(body.playerId).trim() : '';
    const fromTower = body.fromTower != null ? String(body.fromTower) : null;
    const toTower   = body.toTower   != null ? String(body.toTower)   : null;
    const currency  = body.currency  != null ? String(body.currency)  : null;
    const txSig     = body.txSig     != null ? String(body.txSig).trim() : null;

    const waveIdNum   = body.waveId    != null ? Number(body.waveId)    : null;
    const costUsdcNum = body.costUsdc  != null ? Number(body.costUsdc)  : null;
    const clientTsNum = body.timestamp != null ? Number(body.timestamp) : null;

    const waveId   = Number.isFinite(waveIdNum)   ? waveIdNum   : null;
    const costUsdc = Number.isFinite(costUsdcNum) ? costUsdcNum : null;
    const clientTs = Number.isFinite(clientTsNum) ? clientTsNum : null;

    if (!playerId) {
        return quietFail(res, 400, AuthCode.PLAYER_ID_MISSING, ref);
    }
    if (txSig && !TX_SIG_RE.test(txSig)) {
        return quietFail(res, 400, AuthCode.BAD_PAYLOAD, ref);
    }

    let sql;
    try {
        sql = neon(process.env.DATABASE_URL);
    } catch (err) {
        console.error('[tower-swap/log] DB init error:', err);
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }

    if (!exactBytes && !isGuestId(playerId)) {
        await logAuthReject(sql, req, {
            code: AuthCode.SERVER_ERROR, ref, identity: playerId, mode: 'wallet',
            detail: { reason: 'raw_body_unavailable_bodyparser_active' },
        });
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }

    // ── AUTH GATE (same rail as referral/install-brag.js) ──────────────────
    let auth;
    try {
        auth = await authenticate(sql, req, rawBody, playerId);
    } catch (err) {
        console.error('[tower-swap/log] Auth check error:', err);
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }
    if (!auth.ok) {
        await logAuthReject(sql, req, {
            code: auth.code, ref, identity: auth.identity, mode: auth.mode, detail: auth.detail,
        });
        const status = (auth.code === AuthCode.PLAYER_ID_BAD_SHAPE ||
                        auth.code === AuthCode.PLAYER_ID_MISSING ||
                        auth.code === AuthCode.WALLET_MALFORMED) ? 400 : 401;
        return quietFail(res, status, auth.code, ref);
    }

    // ── ON-CHAIN VERIFICATION ──────────────────────────────────────────────
    // Only meaningful on the wallet rail: the guest rail has no on-chain identity
    // to match a signer against, and a guest cannot have paid.
    let verification = VERIFY_CLAIMED;
    const url = rpcUrl();
    if (txSig && url && auth.mode === 'wallet') {
        const chain = await verifyOnChain(url, txSig, auth.identity);
        if (!chain.ok) {
            await logApiEvent(sql, playerId, 'tower_swap_verify_reject', {
                ref, reason: chain.reason, mode: auth.mode,
            });
            // An RPC we cannot reach is a server fault, not the player's fault;
            // a signature the chain contradicts is a refusal.
            const status = chain.reason === 'rpc_unavailable' ? 500 : 400;
            return quietFail(res, status, AuthCode.BAD_PAYLOAD, ref);
        }
        verification = VERIFY_ONCHAIN;
    }

    try {
        // uq_tower_swaps_tx_sig is a PARTIAL unique index (only non-null sigs).
        // ON CONFLICT must therefore name the column + the same predicate.
        const rows = await sql`
            INSERT INTO tower_swaps
                (player_id, wave_id, from_tower, to_tower, currency, cost_usdc,
                 tx_sig, client_ts, verification)
            VALUES (
                ${playerId},
                ${waveId},
                ${fromTower},
                ${toTower},
                ${currency},
                ${costUsdc},
                ${txSig},
                ${clientTs},
                ${verification}
            )
            ON CONFLICT (tx_sig) WHERE tx_sig IS NOT NULL DO NOTHING
            RETURNING swap_id
        `;

        const deduped = rows.length === 0; // conflict → nothing inserted

        // A dedup against a row belonging to a DIFFERENT player is the signature-
        // squatting attack, not an honest retry. Never silent: raise it.
        if (deduped && txSig) {
            try {
                const owner = await sql`
                    SELECT player_id FROM tower_swaps WHERE tx_sig = ${txSig} LIMIT 1
                `;
                if (owner.length > 0 && String(owner[0].player_id) !== playerId) {
                    console.error('[tower-swap/log] tx_sig already logged by a DIFFERENT player — possible squat. ref=', ref);
                    await logApiEvent(sql, playerId, 'tower_swap_txsig_conflict', {
                        ref, mode: auth.mode, verification,
                    });
                }
            } catch (_) { /* diagnostic only — never fail the write path */ }
        }

        return res.status(200).json({ success: true, deduped, verification });
    } catch (err) {
        console.error('[tower-swap/log] DB error:', err);
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }
}

module.exports = handler;
// MUST be assigned AFTER the handler export — see api/game/save.js:427-432.
module.exports.config = { api: { bodyParser: false } };
