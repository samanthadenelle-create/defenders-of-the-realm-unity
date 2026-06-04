// =============================================================================
// api/promo/redeem.js — Vercel Serverless Function
// -----------------------------------------------------------------------------
// Redeems an operator-issued promo code. Reads the code catalog (promo_codes),
// enforces the gates, then records the redemption (promo_redemptions) and
// returns the reward.
//
// Client : Assets/_Modules/Core/Promo/PromoCodeService.cs
//   POST  application/json
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
// Status codes: 200 | 400 | 500
//   NOTE: a *business* failure (bad/expired/used code) is returned as 200 with
//   { success:false, error } — the client reads the JSON body, not the HTTP
//   status, to map the user-facing message. 4xx/5xx are reserved for malformed
//   requests / server faults.
// =============================================================================

const { neon } = require('@neondatabase/serverless');

module.exports = async (req, res) => {
    if (req.method !== 'POST') {
        return res.status(400).json({ error: 'Method not allowed' });
    }

    let body = req.body;
    try {
        if (typeof body === 'string') body = JSON.parse(body);
    } catch (err) {
        console.error('[promo/redeem] Body parse error:', err);
        return res.status(400).json({ error: 'Invalid payload' });
    }

    if (!body || typeof body !== 'object') {
        return res.status(400).json({ error: 'Invalid payload' });
    }

    const playerId = body.playerId != null ? String(body.playerId) : 'anonymous';
    const code = body.code != null ? String(body.code).trim().toUpperCase() : '';

    if (!code) {
        return res.status(400).json({ error: 'Missing code' });
    }

    try {
        const sql = neon(process.env.DATABASE_URL);

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
};
