// =============================================================================
// api/profile/username.js — Vercel Serverless Function (WO-129 §2.2)
// -----------------------------------------------------------------------------
// Set or rename a player's username. WALLET-AUTH GATED (signed-nonce, same as
// save): the signed wallet MUST equal the profile's wallet, so a player can only
// claim a name for THEMSELVES. The server is the authority on:
//   • format + profanity   → _lib/username-policy.validateUsername
//   • case-insensitive uniqueness → DB unique index (23505 → USERNAME_TAKEN)
// (WO-129 §2.2 / §5 — usernames are the one free-text field and get the gate.)
//
// Rename POLICY (one free, then cost/cap — US-8): this endpoint RECORDS the
// rename (sets renamed_at) and reports whether it was the first set vs a rename;
// the rename COST lever (soft-currency / 1-per-30-days) is applied client/economy
// side. The server never charges currency here — it stamps the record of change.
//
// Client : ProfileService (Core) — WO-129 §4 (NEW).
//   POST  application/json   (raw body — bodyParser disabled, signature over the
//                             exact bytes, same as save.js)
//   Headers: X-Wallet / X-Nonce / X-Signature
//   Body  : { wallet, username }
//   Success: { success:true, username, wasRename:bool }
//   Failure: { success:false, error:'USERNAME_TAKEN' | 'USERNAME_TOO_SHORT'
//              | 'USERNAME_TOO_LONG' | 'USERNAME_INVALID_CHARS'
//              | 'USERNAME_REJECTED' }   (business failures → 200, like promo/redeem)
//
// Driver: @neondatabase/serverless
// Status codes: 200 | 400 | 401 | 500
// =============================================================================

const { neon } = require('@neondatabase/serverless');
const { verifyAndConsume } = require('../_lib/wallet-auth');
const { validateUsername } = require('../_lib/username-policy');

// Signature is over the EXACT raw body bytes — disable the body parser (save.js).
module.exports.config = { api: { bodyParser: false } };

module.exports = async (req, res) => {
    if (req.method !== 'POST') {
        return res.status(400).json({ error: 'Method not allowed' });
    }

    let rawBody;
    try {
        rawBody = await readBody(req);
    } catch (err) {
        console.error('[profile/username] Body read error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }

    let body;
    try {
        body = JSON.parse(rawBody.toString());
    } catch (err) {
        console.error('[profile/username] Decode error:', err);
        return res.status(400).json({ error: 'Invalid payload' });
    }
    if (!body || typeof body !== 'object') {
        return res.status(400).json({ error: 'Invalid payload' });
    }

    const wallet = body.wallet != null ? String(body.wallet).trim() : '';
    if (!wallet) {
        return res.status(400).json({ error: 'Missing wallet' });
    }

    // ── Safety gate: format + profanity (uniqueness is the DB's job) ───────
    const check = validateUsername(body.username);
    if (!check.ok) {
        return res.status(200).json({ success: false, error: check.error });
    }
    const username = check.username;

    let sql;
    try {
        sql = neon(process.env.DATABASE_URL);
    } catch (err) {
        console.error('[profile/username] DB init error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }

    // ── AUTH GATE — signed wallet MUST equal the profile wallet ────────────
    let auth;
    try {
        auth = await verifyAndConsume(sql, req.headers, rawBody, wallet);
    } catch (err) {
        console.error('[profile/username] Auth check error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }
    if (!auth.ok) {
        return res.status(401).json({ error: 'Unauthorized', reason: auth.reason });
    }

    try {
        // Did this player already have a name? (first set vs rename → renamed_at)
        const prior = await sql`
            SELECT username FROM player_profiles WHERE wallet = ${wallet} LIMIT 1
        `;
        const wasRename = prior.length > 0 && prior[0].username != null;

        // Upsert. username_ci is a generated column → the unique index enforces
        // case-insensitive uniqueness; a collision raises 23505 → USERNAME_TAKEN.
        // renamed_at is set only when this is a rename (an existing name changing).
        try {
            await sql`
                INSERT INTO player_profiles (wallet, username, created_at)
                VALUES (${wallet}, ${username}, NOW())
                ON CONFLICT (wallet) DO UPDATE
                SET
                    username   = EXCLUDED.username,
                    renamed_at = CASE WHEN player_profiles.username IS NOT NULL
                                      THEN NOW() ELSE player_profiles.renamed_at END
            `;
        } catch (insertErr) {
            if (insertErr && insertErr.code === '23505') {
                return res.status(200).json({ success: false, error: 'USERNAME_TAKEN' });
            }
            throw insertErr;
        }

        return res.status(200).json({ success: true, username, wasRename });
    } catch (err) {
        console.error('[profile/username] DB error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }
};

// ── Utility: collect raw request body into a Buffer ────────────────────────
function readBody(req) {
    return new Promise((resolve, reject) => {
        const chunks = [];
        req.on('data', (chunk) => chunks.push(chunk));
        req.on('end', () => resolve(Buffer.concat(chunks)));
        req.on('error', (err) => reject(err));
    });
}
