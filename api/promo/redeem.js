// =============================================================================
// api/promo/redeem.js — Vercel Serverless Function
// -----------------------------------------------------------------------------
// Redeems an operator-issued promo code. Reads the code catalog (promo_codes),
// enforces the gates, then records the redemption (promo_redemptions) and
// returns the reward.
//
// IDENTITY-GATED (security audit 2026-08-15). playerId used to be taken STRAIGHT
// FROM THE BODY with no signature, no nonce and no header check, and it reaches
// OTHER players: POST a victim's id with a live code and UNIQUE(code, player_id)
// locks them out of it forever, while a loop of invented ids burns a launch
// code's max_redemptions before anyone real arrives. It now goes through the SAME
// rail /api/game/save uses (_lib/wallet-auth.authenticate): a base58 id demands
// an ed25519 signature over the exact body bytes plus a single-use nonce; a
// guest-local id demands the matching X-Guest-Id. No second auth scheme, no
// weaker path — the route simply never had the one that already existed.
//
// Client : Assets/_Modules/Core/Promo/PromoCodeService.cs
//   POST  application/json   (raw body — bodyParser disabled; the signature is
//                             over the EXACT bytes, same as save.js)
//   Headers: X-Guest-Id, or X-Wallet + X-Nonce + X-Signature
//   Body  : { playerId, code }   (code is uppercased client-side; store/compare uppercase)
//   Success: { success: true, reward: { crystals, coins }, message }
//   Failure: { success: false, error: "INVALID_CODE" | "ALREADY_REDEEMED"
//                                    | "EXPIRED" | "PLAYER_LIMIT_REACHED" }
//
// GATE → ERROR mapping (per schema.sql, table 3):
//   row missing / active=false                 → INVALID_CODE
//   NOW() > expires_at (when not null)          → EXPIRED
//   global redemptions >= max_redemptions       → ALREADY_REDEEMED
//   this player already redeemed this code      → ALREADY_REDEEMED
//   player's distinct redeemed codes >= per_player_limit → PLAYER_LIMIT_REACHED
//
// Driver: @neondatabase/serverless
// Status codes: 200 | 400 | 401 | 500
//   NOTE: a *business* failure (bad/expired/used code) is returned as 200 with
//   { success:false, error } — the client reads the JSON body, not the HTTP
//   status, to map the user-facing message. 4xx/5xx are reserved for malformed
//   requests / auth refusals / server faults.
// =============================================================================

const { neon } = require('@neondatabase/serverless');
const { AuthCode, authenticate, WALLET_MAX_BODY_BYTES, isGuestId } = require('../_lib/wallet-auth');
const { applyCors, newRef, quietFail, readBodyExact } = require('../_lib/http');
const { logAuthReject } = require('../_lib/audit');

async function handler(req, res) {
    if (applyCors(req, res, 'POST, OPTIONS')) return;

    const ref = newRef();

    if (req.method !== 'POST') {
        return quietFail(res, 400, AuthCode.METHOD_NOT_ALLOWED, ref);
    }

    // The wallet signature covers the EXACT raw bytes, so read them ourselves
    // (with a hard cap) rather than trusting a re-serialised parsed body.
    let rawBody, exactBytes;
    try {
        const read = await readBodyExact(req, WALLET_MAX_BODY_BYTES);
        rawBody = read.buffer;
        exactBytes = read.exact;
    } catch (err) {
        if (err && err.code === 'BODY_TOO_LARGE') {
            return quietFail(res, 400, AuthCode.PAYLOAD_TOO_LARGE, ref);
        }
        console.error('[promo/redeem] Body read error:', err);
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }

    let body;
    try {
        body = JSON.parse(rawBody.toString('utf8'));
    } catch (err) {
        console.error('[promo/redeem] Body parse error:', err);
        return quietFail(res, 400, AuthCode.BAD_PAYLOAD, ref);
    }

    if (!body || typeof body !== 'object') {
        return quietFail(res, 400, AuthCode.BAD_PAYLOAD, ref);
    }

    // No "anonymous" fallback any more: an id nobody can prove is exactly the
    // hole this gate closes, and authenticate() would reject it anyway.
    const playerId = body.playerId != null ? String(body.playerId).trim() : '';
    const code = body.code != null ? String(body.code).trim().toUpperCase() : '';

    if (!playerId) {
        return quietFail(res, 400, AuthCode.PLAYER_ID_MISSING, ref);
    }
    if (!code) {
        return res.status(400).json({ error: 'Missing code' });
    }

    let sql;
    try {
        sql = neon(process.env.DATABASE_URL);
    } catch (err) {
        console.error('[promo/redeem] DB init error:', err);
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }

    // A signature can only be verified against the ORIGINAL bytes. If the runtime
    // parsed the body out from under us, say so precisely instead of emitting a
    // lying AUTH_BAD_SIGNATURE (see _lib/http.readBodyExact).
    if (!exactBytes && !isGuestId(playerId)) {
        await logAuthReject(sql, req, {
            code: AuthCode.SERVER_ERROR, ref, identity: playerId, mode: 'wallet',
            detail: { reason: 'raw_body_unavailable_bodyparser_active' },
        });
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }

    // ── AUTH GATE ──────────────────────────────────────────────────────────
    let auth;
    try {
        auth = await authenticate(sql, req, rawBody, playerId);
    } catch (err) {
        console.error('[promo/redeem] Auth check error:', err);
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

    try {
        // ── 1. Look the code up in the catalog ────────────────────────────────
        const codeRows = await sql`
            SELECT code, reward_crystals, reward_coins, message,
                   active, max_redemptions, per_player_limit, expires_at
            FROM promo_codes
            WHERE code = ${code}
            LIMIT 1
        `;

        if (codeRows.length === 0 || codeRows[0].active === false) {
            return res.status(200).json({ success: false, error: 'INVALID_CODE' });
        }

        const promo = codeRows[0];

        // ── 2. Expiry ─────────────────────────────────────────────────────────
        if (promo.expires_at != null && new Date(promo.expires_at).getTime() < Date.now()) {
            return res.status(200).json({ success: false, error: 'EXPIRED' });
        }

        // ── 3. This player already redeemed this code? ───────────────────────
        const already = await sql`
            SELECT 1 FROM promo_redemptions
            WHERE code = ${code} AND player_id = ${playerId}
            LIMIT 1
        `;
        if (already.length > 0) {
            return res.status(200).json({ success: false, error: 'ALREADY_REDEEMED' });
        }

        // ── 4. Global redemption cap for this code ───────────────────────────
        if (promo.max_redemptions != null) {
            const countRows = await sql`
                SELECT COUNT(*)::int AS n FROM promo_redemptions WHERE code = ${code}
            `;
            if (countRows[0].n >= promo.max_redemptions) {
                return res.status(200).json({ success: false, error: 'ALREADY_REDEEMED' });
            }
        }

        // ── 5. Per-player cap on DISTINCT codes redeemed ─────────────────────
        if (promo.per_player_limit != null) {
            const distinctRows = await sql`
                SELECT COUNT(DISTINCT code)::int AS n
                FROM promo_redemptions
                WHERE player_id = ${playerId}
            `;
            if (distinctRows[0].n >= promo.per_player_limit) {
                return res.status(200).json({ success: false, error: 'PLAYER_LIMIT_REACHED' });
            }
        }

        // ── 6. Record the redemption (snapshot the reward for audit) ─────────
        const crystals = promo.reward_crystals || 0;
        const coins    = promo.reward_coins    || 0;

        try {
            await sql`
                INSERT INTO promo_redemptions (code, player_id, crystals, coins)
                VALUES (${code}, ${playerId}, ${crystals}, ${coins})
            `;
        } catch (insertErr) {
            // UNIQUE(code, player_id) — lost the race against a concurrent redeem.
            // Treat as already redeemed (idempotent, no double-grant).
            if (insertErr && insertErr.code === '23505') {
                return res.status(200).json({ success: false, error: 'ALREADY_REDEEMED' });
            }
            throw insertErr;
        }

        // ── 7. Success ────────────────────────────────────────────────────────
        return res.status(200).json({
            success: true,
            reward: { crystals, coins },
            message: promo.message ?? null,
        });
    } catch (err) {
        console.error('[promo/redeem] DB error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }
}

module.exports = handler;
// MUST be assigned AFTER the handler export. `module.exports.config = ...`
// followed by `module.exports = handler` silently DISCARDS the config and leaves
// the runtime body parser ON, which drains the stream the raw-body reader needs.
// See api/game/save.js:427-432 and _lib/http.readBodyExact.
module.exports.config = { api: { bodyParser: false } };
