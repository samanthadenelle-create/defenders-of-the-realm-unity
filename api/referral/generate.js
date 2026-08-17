// =============================================================================
// api/referral/generate.js — Vercel Serverless Function
// -----------------------------------------------------------------------------
// Returns the caller's unique referral code, generate-or-reuse. If the player
// already has a row in `referrals`, the SAME code is returned (the client caches
// code+url in PlayerPrefs and calls repeatedly). Otherwise a new unique code is
// minted, inserted, and returned.
//
// IDENTITY-GATED (security audit 2026-08-15). playerId used to be taken straight
// from the body with no proof, so anyone could mint or read back another player's
// referral row. It now runs through the SAME rail /api/game/save uses
// (_lib/wallet-auth.authenticate) — no new scheme, just the one that existed.
//
// Client : Assets/_Modules/Core/Referral/ReferralService.cs (EnsureCodeAsync)
//   POST  application/json   (raw body — bodyParser disabled)
//   Headers: X-Guest-Id, or X-Wallet + X-Nonce + X-Signature
//   Body  : { playerId }
//   Reply : { success: true, code, referralUrl }
//
// Code shape: 8-char uppercase A-Z/2-9 (ambiguous chars 0/O/1/I excluded), so it
// is shareable verbally and matches the client's ToUpperInvariant() comparison.
//
// Driver: @neondatabase/serverless
// Status codes: 200 | 400 | 401 | 500
// =============================================================================

const { neon } = require('@neondatabase/serverless');
const { AuthCode, authenticate, WALLET_MAX_BODY_BYTES, isGuestId } = require('../_lib/wallet-auth');
const { applyCors, newRef, quietFail, readBodyExact } = require('../_lib/http');
const { logAuthReject } = require('../_lib/audit');

// Unambiguous alphabet (no 0/O/1/I) for human-readable share codes.
const CODE_ALPHABET = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
const CODE_LENGTH = 8;

// Base host for the share link. The client only displays/opens whatever we send,
// so this URL just needs to deep-link or attribute the code. (Owner may repoint.)
const REFERRAL_URL_BASE = 'https://defenders-of-the-realm-v2.vercel.app/r/';

function makeCode() {
    let out = '';
    for (let i = 0; i < CODE_LENGTH; i++) {
        out += CODE_ALPHABET[Math.floor(Math.random() * CODE_ALPHABET.length)];
    }
    return out;
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
        console.error('[referral/generate] Body read error:', err);
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }

    let body;
    try {
        body = JSON.parse(rawBody.toString('utf8'));
    } catch (err) {
        console.error('[referral/generate] Body parse error:', err);
        return quietFail(res, 400, AuthCode.BAD_PAYLOAD, ref);
    }

    const playerId = body && body.playerId != null ? String(body.playerId).trim() : '';
    if (!playerId) {
        return quietFail(res, 400, AuthCode.PLAYER_ID_MISSING, ref);
    }

    let sql;
    try {
        sql = neon(process.env.DATABASE_URL);
    } catch (err) {
        console.error('[referral/generate] DB init error:', err);
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }

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
        console.error('[referral/generate] Auth check error:', err);
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
        // ── Reuse: return the existing code if the player already has one ─────
        const existing = await sql`
            SELECT code, referral_url FROM referrals
            WHERE player_id = ${playerId}
            LIMIT 1
        `;
        if (existing.length > 0) {
            const row = existing[0];
            const url = row.referral_url || (REFERRAL_URL_BASE + row.code);
            return res.status(200).json({ success: true, code: row.code, referralUrl: url });
        }

        // ── Generate: mint a unique code, retrying on UNIQUE(code) collision ─
        // referrals has UNIQUE(code) and PRIMARY KEY(player_id). The INSERT also
        // races concurrent first-calls from the SAME player; ON CONFLICT
        // (player_id) DO NOTHING + re-select handles that case without erroring.
        for (let attempt = 0; attempt < 6; attempt++) {
            const code = makeCode();
            const url  = REFERRAL_URL_BASE + code;

            try {
                const inserted = await sql`
                    INSERT INTO referrals (player_id, code, referral_url)
                    VALUES (${playerId}, ${code}, ${url})
                    ON CONFLICT (player_id) DO NOTHING
                    RETURNING code, referral_url
                `;

                if (inserted.length > 0) {
                    // We won the insert for this player.
                    return res.status(200).json({
                        success: true,
                        code: inserted[0].code,
                        referralUrl: inserted[0].referral_url,
                    });
                }

                // ON CONFLICT(player_id) fired — another concurrent call created
                // this player's row first. Re-select and return it.
                const reread = await sql`
                    SELECT code, referral_url FROM referrals
                    WHERE player_id = ${playerId}
                    LIMIT 1
                `;
                if (reread.length > 0) {
                    const row = reread[0];
                    const u = row.referral_url || (REFERRAL_URL_BASE + row.code);
                    return res.status(200).json({ success: true, code: row.code, referralUrl: u });
                }
                // Fall through to retry if somehow still missing.
            } catch (insertErr) {
                // 23505 on the UNIQUE(code) index = code collision with ANOTHER
                // player. Retry with a fresh code.
                if (insertErr && insertErr.code === '23505') {
                    continue;
                }
                throw insertErr;
            }
        }

        // Exhausted attempts (astronomically unlikely with an 8-char code).
        console.error('[referral/generate] Could not mint a unique code after retries.');
        return res.status(500).json({ error: 'Internal server error' });
    } catch (err) {
        console.error('[referral/generate] DB error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }
}

module.exports = handler;
// MUST be assigned AFTER the handler export — see api/game/save.js:427-432.
module.exports.config = { api: { bodyParser: false } };
