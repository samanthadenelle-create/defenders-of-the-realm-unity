// =============================================================================
// api/referral/generate.js — Vercel Serverless Function
// -----------------------------------------------------------------------------
// Returns the caller's unique referral code, generate-or-reuse. If the player
// already has a row in `referrals`, the SAME code is returned (the client caches
// code+url in PlayerPrefs and calls repeatedly). Otherwise a new unique code is
// minted, inserted, and returned.
//
// Client : Assets/_Modules/Core/Referral/ReferralService.cs (EnsureCodeAsync)
//   POST  application/json
//   Body  : { playerId }
//   Reply : { success: true, code, referralUrl }
//
// Code shape: 8-char uppercase A-Z/2-9 (ambiguous chars 0/O/1/I excluded), so it
// is shareable verbally and matches the client's ToUpperInvariant() comparison.
//
// Driver: @neondatabase/serverless
// Status codes: 200 | 400 | 500
// =============================================================================

const { neon } = require('@neondatabase/serverless');

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

module.exports = async (req, res) => {
    if (req.method !== 'POST') {
        return res.status(400).json({ error: 'Method not allowed' });
    }

    let body = req.body;
    try {
        if (typeof body === 'string') body = JSON.parse(body);
    } catch (err) {
        console.error('[referral/generate] Body parse error:', err);
        return res.status(400).json({ error: 'Invalid payload' });
    }

    const playerId = body && body.playerId != null ? String(body.playerId) : '';
    if (!playerId) {
        return res.status(400).json({ error: 'Missing playerId' });
    }

    try {
        const sql = neon(process.env.DATABASE_URL);

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
};
