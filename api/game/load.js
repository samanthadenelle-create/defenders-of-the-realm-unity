// =============================================================================
// api/game/load.js — Vercel Serverless Function
// -----------------------------------------------------------------------------
// Returns the stored game_state for a player. The Unity client calls this on
// scene enter and merges the server record onto the local GameState SO.
//
// WHAT CHANGED 2026-08-02:
//   • Same two-rail auth as save.js (_lib/wallet-auth.authenticate): a base58
//     wallet id still requires a signed, single-use nonce; a "guest-local-<hex>"
//     id takes the rate-limited guest rail. There was no guest path before, so
//     no guest could ever read their own row back.
//   • Structured, quiet failure codes + an audit row per refusal.
//   • THE FULL STATE IS RETURNED. The old handler hand-listed 13 keys, so even a
//     complete server record came back as a husk — no base layout, no queue, no
//     army, no hero level. The client deserialises `data` straight into
//     SaveSchema.PersistedState, so the whole stored object IS the right shape;
//     the 13 keys are still emitted explicitly for older clients.
//   • CORS + preflight (a cross-origin GET carrying X-Wallet preflights, and this
//     function never answered OPTIONS — the web build could not load at all).
//
// Status codes: 200 | 400 | 401 | 404 | 500
// =============================================================================

const { neon } = require('@neondatabase/serverless');
const { AuthCode, authenticate } = require('../_lib/wallet-auth');
const { applyCors, newRef, quietFail } = require('../_lib/http');
const { logAuthReject } = require('../_lib/audit');

// Kept explicit so a client older than this deploy still finds every key it
// expects even if the stored row predates a field.
const LEGACY_KEYS = [
    'bestWave', 'crystals', 'food', 'coins', 'voidshards', 'stone', 'iron', 'wood',
    'towers', 'towerAbilities', 'pets', 'ownedPets', 'starterPetId',
];

module.exports = async (req, res) => {
    if (applyCors(req, res, 'GET, OPTIONS')) return;

    const ref = newRef();

    if (req.method !== 'GET') {
        return quietFail(res, 400, AuthCode.METHOD_NOT_ALLOWED, ref);
    }

    const { playerId } = req.query || {};
    if (!playerId) {
        return quietFail(res, 400, AuthCode.PLAYER_ID_MISSING, ref);
    }

    let sql;
    try {
        sql = neon(process.env.DATABASE_URL);
    } catch (err) {
        console.error('[load] DB init error:', err);
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }

    // ── AUTH GATE — no body, so the wallet rail signs the literal "load" tag ──
    let auth;
    try {
        auth = await authenticate(sql, req, null, playerId);
    } catch (err) {
        console.error('[load] Auth check error:', err);
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }
    if (!auth.ok) {
        await logAuthReject(sql, req, {
            code: auth.code, ref, identity: auth.identity, mode: auth.mode, detail: auth.detail,
        });
        const status = (auth.code === AuthCode.PLAYER_ID_BAD_SHAPE ||
                        auth.code === AuthCode.WALLET_MALFORMED) ? 400 : 401;
        return quietFail(res, status, auth.code, ref);
    }

    try {
        const rows = await sql`
            SELECT game_state, schema_version, updated_at
            FROM player_data
            WHERE player_id = ${playerId}
            LIMIT 1
        `;

        if (rows.length === 0) {
            // Not an error — a first-run player simply has no row yet. Kept at 404
            // because the client already treats a non-2xx here as "keep local".
            return res.status(404).json({ ok: false, code: 'NO_SAVE', ref: ref });
        }

        const row = rows[0];
        const state = row.game_state ?? {};

        // Return the stored state VERBATIM (it is already the client's
        // PersistedState shape), then backfill the legacy keys as explicit nulls
        // so an older client never trips over a missing member.
        const data = Object.assign({}, state);
        for (const k of LEGACY_KEYS) {
            if (data[k] === undefined) data[k] = null;
        }

        return res.status(200).json({
            ok: true,
            success: true,
            // WO-912 s7.2: authoritative server time. The client anchors ServerClock
            // to this against a MONOTONIC timer, so the rewarded-ad window cannot be
            // reset by rolling the device clock (= fabricated ad impressions).
            // Always send it, even on an otherwise-empty response: the handshake is
            // the valuable part, not the payload.
            serverNowMs: Date.now(),
            mode: auth.mode,
            schemaVersion: row.schema_version,
            updatedAt: row.updated_at,
            data: data,
        });
    } catch (err) {
        console.error('[load] DB error:', err);
        await logAuthReject(sql, req, {
            code: AuthCode.SERVER_ERROR, ref, identity: auth.identity, mode: auth.mode,
            detail: { stage: 'select', message: String(err.message || err).slice(0, 300) },
        });
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }
};
