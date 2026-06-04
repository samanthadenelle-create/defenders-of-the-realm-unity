// =============================================================================
// api/referral/install-brag.js — Vercel Serverless Function (WO-129 §2.4)
// -----------------------------------------------------------------------------
// One-time "I just installed Defenders of the Realm" bonus. The client fires this
// after the share intent completes; the SERVER grants the bonus exactly once per
// durable identity (wallet) and records it. The client NEVER self-grants
// (WO-129 §2.4 / §5: "the client requests; the server grants").
//
// IDEMPOTENCY: modelled as a one-time achievement grant (achievement_id =
// 'install_brag'). The PK (wallet, achievement_id) STRUCTURALLY prevents a second
// grant — a repeat request hits a 23505 on insert and returns the ORIGINAL grant
// with alreadyGranted:true and NO second reward. This is the same shape the WO
// names (mirrors the referral one-claim-per-player guard).
//
// WALLET-AUTH GATED: a reward grant is wallet-bound, so it requires a signed
// nonce (same protocol as save). The signed wallet MUST equal the granted wallet.
//
// REWARD (small + COSMETIC by design so it can't be farmed for real value —
// WO-129 §2.4, NORTH STAR "flex, not power"): a modest crystal grant + a cosmetic
// 'Founding Herald' profile flair. Amount is env-tunable; owner sets the final
// number. NO crypto / high-value reward rides this rail.
//
// Client : ReferralService (Core) — install-brag bonus (WO-129 §4, EXTEND).
//   POST  application/json   (raw body — bodyParser disabled, signature over the
//                             exact bytes, same as save.js)
//   Headers: X-Wallet / X-Nonce / X-Signature
//   Body  : { wallet }   (== X-Wallet)
//   Reply : { success:true, reward:{ crystals, flair }, alreadyGranted:bool, message }
//
// Driver: @neondatabase/serverless
// Status codes: 200 | 400 | 401 | 500
// =============================================================================

const { neon } = require('@neondatabase/serverless');
const { verifyAndConsume } = require('../_lib/wallet-auth');

// Signature is over the EXACT raw body bytes — disable the body parser (save.js).
module.exports.config = { api: { bodyParser: false } };

const ACHIEVEMENT_ID = 'install_brag';
const FLAIR = 'founding_herald';

// Small cosmetic-tier crystal grant. Env-tunable; owner sets the final amount.
const INSTALL_BRAG_CRYSTALS = (() => {
    const v = parseInt(process.env.INSTALL_BRAG_CRYSTALS, 10);
    return Number.isFinite(v) && v >= 0 ? v : 50;
})();

module.exports = async (req, res) => {
    if (req.method !== 'POST') {
        return res.status(400).json({ error: 'Method not allowed' });
    }

    let rawBody;
    try {
        rawBody = await readBody(req);
    } catch (err) {
        console.error('[referral/install-brag] Body read error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }

    let body;
    try {
        body = JSON.parse(rawBody.toString());
    } catch (err) {
        console.error('[referral/install-brag] Decode error:', err);
        return res.status(400).json({ error: 'Invalid payload' });
    }
    if (!body || typeof body !== 'object') {
        return res.status(400).json({ error: 'Invalid payload' });
    }

    const wallet = body.wallet != null ? String(body.wallet).trim() : '';
    if (!wallet) {
        return res.status(400).json({ error: 'Missing wallet' });
    }

    let sql;
    try {
        sql = neon(process.env.DATABASE_URL);
    } catch (err) {
        console.error('[referral/install-brag] DB init error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }

    // ── AUTH GATE — signed wallet MUST equal the granted wallet ────────────
    let auth;
    try {
        auth = await verifyAndConsume(sql, req.headers, rawBody, wallet);
    } catch (err) {
        console.error('[referral/install-brag] Auth check error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }
    if (!auth.ok) {
        return res.status(401).json({ error: 'Unauthorized', reason: auth.reason });
    }

    const reward = { crystals: INSTALL_BRAG_CRYSTALS, flair: FLAIR };

    try {
        // Atomic one-time grant: INSERT ... ON CONFLICT DO NOTHING. If a row was
        // inserted (length > 0) this is the FIRST grant; if not, the player was
        // already granted — re-read the original and report alreadyGranted.
        const inserted = await sql`
            INSERT INTO achievement_grants (wallet, achievement_id, reward)
            VALUES (${wallet}, ${ACHIEVEMENT_ID}, ${JSON.stringify(reward)}::jsonb)
            ON CONFLICT (wallet, achievement_id) DO NOTHING
            RETURNING reward
        `;

        if (inserted.length > 0) {
            return res.status(200).json({
                success: true,
                reward,
                alreadyGranted: false,
                message: `Welcome, Founding Herald! +${reward.crystals} Aether Crystals.`,
            });
        }

        // Already granted — return the ORIGINAL recorded reward, no second grant.
        const existing = await sql`
            SELECT reward FROM achievement_grants
            WHERE wallet = ${wallet} AND achievement_id = ${ACHIEVEMENT_ID}
            LIMIT 1
        `;
        const original = existing.length > 0 && existing[0].reward ? existing[0].reward : reward;

        return res.status(200).json({
            success: true,
            reward: original,
            alreadyGranted: true,
            message: 'Install bonus already claimed.',
        });
    } catch (err) {
        console.error('[referral/install-brag] DB error:', err);
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
