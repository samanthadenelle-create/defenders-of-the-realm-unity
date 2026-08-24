// =============================================================================
// api/referral/claim.js — Vercel Serverless Function
// -----------------------------------------------------------------------------
// Claims someone else's referral code. Resolves code → referrer via `referrals`,
// enforces the abuse gates, records the claim (referral_claims), bumps the
// referrer's claim_count, and returns the claimer's reward.
//
// IDENTITY-GATED (security audit 2026-08-15). The claimer id used to be taken
// STRAIGHT FROM THE BODY with no proof, and referral_claims is UNIQUE(claimer_id)
// — so ONE forged claim permanently consumed a victim's only claim, and the
// SELF_REFERRAL guard (bare string equality on an unproven id) was trivially
// sybilled. It now runs through the SAME rail /api/game/save uses.
//
// ⚠ CORRECTED 2026-08-18 — THE 08-15 AUDIT ONLY HALF-CLOSED THAT. The paragraph
// above used to end "(_lib/wallet-auth.authenticate). No new auth scheme; the one
// that existed." That is true and it was NOT ENOUGH: authenticate() routes a
// guest-shaped id to verifyGuest, which merely regex-checks a client-MINTED
// `guest-local-<64 hex>` bearer id and echoes it back — no signature at all
// (BackendRequestSigner.TryAttachAsync:111-114). So the sybil this file's own
// header names as the thing being fixed SURVIVED the fix: mint a fresh guest id,
// claim, mint another, claim again. UNIQUE(claimer_id) bounds a PERSON; it does
// not bound someone who can invent people. And SELF_REFERRAL still compared a
// chosen string to a chosen string whenever both sides were guests.
//
// NOW: this route calls _lib/wallet-auth.authenticateGranting() — WALLET RAIL
// ONLY. Claiming pays crystals, so the claimer must present an ed25519 signature
// over the exact body bytes plus a single-use nonce. A guest is refused with
// AUTH_WALLET_REQUIRED. THIS is what finally makes the SELF_REFERRAL check and the
// one-claim-ever constraint mean something: both now key off an identity backed by
// a private key the attacker would have to own, not a string they typed.
//
// Client : Assets/_Modules/Core/Referral/ReferralService.cs (ClaimAsync)
//   POST  application/json   (raw body — bodyParser disabled)
//   Headers: X-Wallet + X-Nonce + X-Signature   (WALLET RAIL ONLY as of 2026-08-18;
//            X-Guest-Id is no longer accepted here — see the correction above)
//   Body  : { playerId, code }   (code uppercased client-side)
//   Success: { success: true, reward: { crystals }, message }
//   Failure: { success: false, error: "SELF_REFERRAL" | "ALREADY_CLAIMED"
//                                    | "INVALID_CODE" | "CAP_REACHED" }
//
// GATE → ERROR mapping (per schema.sql, table 6):
//   code not in referrals                 → INVALID_CODE
//   referrer player_id == claimer_id       → SELF_REFERRAL
//   claimer already has a claim row        → ALREADY_CLAIMED  (UNIQUE(claimer_id))
//   referrals.claim_count >= reward_cap    → CAP_REACHED      (only when cap set)
//
// CLAIMER REWARD: the client never sends an amount; the backend decides it.
// Configurable via env REFERRAL_CLAIM_CRYSTALS (default 25). ASSUMPTION — owner
// to confirm the intended grant (see api/DB_SETUP.md).
//
// Driver: @neondatabase/serverless
// Status codes: 200 | 400 | 401 | 500
//   Business failures return 200 + { success:false, error } (client reads the
//   body, not the HTTP status — same pattern as promo/redeem.js).
// =============================================================================

const { neon } = require('@neondatabase/serverless');
const { AuthCode, authenticateGranting, WALLET_MAX_BODY_BYTES, isGuestId } = require('../_lib/wallet-auth');
const { applyCors, newRef, quietFail, readBodyExact } = require('../_lib/http');
const { logAuthReject } = require('../_lib/audit');

const CLAIM_REWARD_CRYSTALS = (() => {
    const v = parseInt(process.env.REFERRAL_CLAIM_CRYSTALS, 10);
    return Number.isFinite(v) && v >= 0 ? v : 25;
})();

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
        console.error('[referral/claim] Body read error:', err);
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }

    let body;
    try {
        body = JSON.parse(rawBody.toString('utf8'));
    } catch (err) {
        console.error('[referral/claim] Body parse error:', err);
        return quietFail(res, 400, AuthCode.BAD_PAYLOAD, ref);
    }

    if (!body || typeof body !== 'object') {
        return quietFail(res, 400, AuthCode.BAD_PAYLOAD, ref);
    }

    const claimerId = body.playerId != null ? String(body.playerId).trim() : '';
    const code = body.code != null ? String(body.code).trim().toUpperCase() : '';

    if (!claimerId) {
        return quietFail(res, 400, AuthCode.PLAYER_ID_MISSING, ref);
    }
    if (!code) {
        return res.status(400).json({ error: 'Missing code' });
    }

    let sql;
    try {
        sql = neon(process.env.DATABASE_URL);
    } catch (err) {
        console.error('[referral/claim] DB init error:', err);
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }

    // ⛔ SCOPED TO THE SIGNATURE PATH (2026-08-24). A session bearer does NOT sign the body:
    // wallet-auth.js verifyWallet() accepts `x-session` and returns via:'session' without ever
    // reading `payload`. This guard predates WO-1157's session rail and rejected BEFORE
    // authenticate() ran, so a session-authed call was refused for lacking bytes it never needed.
    // Same defect fixed in api/game/save.js, where it had silently 500ed EVERY wallet save in
    // production — all 21 rows in player_data were guest rows.
    const hasSessionHeader = !!(req.headers && req.headers['x-session']);
    if (!exactBytes && !isGuestId(claimerId) && !hasSessionHeader) {
        await logAuthReject(sql, req, {
            code: AuthCode.SERVER_ERROR, ref, identity: claimerId, mode: 'wallet',
            detail: { reason: 'raw_body_unavailable_bodyparser_active' },
        });
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }

    // ── AUTH GATE — WALLET RAIL ONLY (2026-08-18) ──────────────────────────
    // The claimer must PROVE they are the claimer, and "proof" here has to mean a
    // signature: this route PAYS crystals, and a guest id is a string the client
    // chooses. authenticateGranting, NOT authenticate — see the correction in this
    // file's header. This is also what makes the SELF_REFERRAL check below mean
    // anything: the string equality now compares a proven identity, not a chosen
    // one. Fails CLOSED — a thrown auth check is a refusal, never a pass-through.
    let auth;
    try {
        auth = await authenticateGranting(sql, req, rawBody, claimerId);
    } catch (err) {
        console.error('[referral/claim] Auth check error:', err);
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }
    if (!auth.ok) {
        // LOUD server-side (full audit row + runtime line), QUIET to the player
        // (a stable code + ref, never a wall of JSON).
        await logAuthReject(sql, req, {
            code: auth.code, ref, identity: auth.identity, mode: auth.mode, detail: auth.detail,
        });
        const status = (auth.code === AuthCode.PLAYER_ID_BAD_SHAPE ||
                        auth.code === AuthCode.PLAYER_ID_MISSING ||
                        auth.code === AuthCode.WALLET_MALFORMED) ? 400 : 401;
        return quietFail(res, status, auth.code, ref);
    }

    try {
        // ── 1. Resolve the code → referrer ────────────────────────────────────
        const refRows = await sql`
            SELECT player_id, code, reward_cap, claim_count
            FROM referrals
            WHERE code = ${code}
            LIMIT 1
        `;
        if (refRows.length === 0) {
            return res.status(200).json({ success: false, error: 'INVALID_CODE' });
        }
        const referral = refRows[0];

        // ── 2. Self-referral ──────────────────────────────────────────────────
        if (referral.player_id === claimerId) {
            return res.status(200).json({ success: false, error: 'SELF_REFERRAL' });
        }

        // ── 3. This player already claimed ANY code? (one claim ever) ────────
        const priorClaim = await sql`
            SELECT 1 FROM referral_claims WHERE claimer_id = ${claimerId} LIMIT 1
        `;
        if (priorClaim.length > 0) {
            return res.status(200).json({ success: false, error: 'ALREADY_CLAIMED' });
        }

        // ── 4. Referrer reward cap (only when reward_cap is set) ─────────────
        if (referral.reward_cap != null && referral.claim_count >= referral.reward_cap) {
            return res.status(200).json({ success: false, error: 'CAP_REACHED' });
        }

        const crystals = CLAIM_REWARD_CRYSTALS;
        const message  = `You received ${crystals} Aether Crystals!`;

        // ── 5. Record the claim (UNIQUE(claimer_id) is the hard guard) ───────
        try {
            await sql`
                INSERT INTO referral_claims
                    (referral_code, referrer_id, claimer_id, crystals, message)
                VALUES (
                    ${code},
                    ${referral.player_id},
                    ${claimerId},
                    ${crystals},
                    ${message}
                )
            `;
        } catch (insertErr) {
            // 23505 on UNIQUE(claimer_id) = lost the race; player already claimed.
            if (insertErr && insertErr.code === '23505') {
                return res.status(200).json({ success: false, error: 'ALREADY_CLAIMED' });
            }
            throw insertErr;
        }

        // ── 6. Bump the referrer's denormalised claim_count ──────────────────
        // Best-effort: the claim is already recorded; a failed counter bump must
        // not roll back the player's reward. (Counter can be recomputed from
        // referral_claims if it ever drifts.)
        try {
            await sql`
                UPDATE referrals
                SET claim_count = claim_count + 1
                WHERE player_id = ${referral.player_id}
            `;
        } catch (bumpErr) {
            console.error('[referral/claim] claim_count bump failed (non-fatal):', bumpErr);
        }

        // ── 7. Success ────────────────────────────────────────────────────────
        return res.status(200).json({
            success: true,
            reward: { crystals },
            message,
        });
    } catch (err) {
        console.error('[referral/claim] DB error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }
}

module.exports = handler;
// MUST be assigned AFTER the handler export — see api/game/save.js:427-432.
module.exports.config = { api: { bodyParser: false } };
