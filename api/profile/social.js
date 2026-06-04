// =============================================================================
// api/profile/social.js — Vercel Serverless Function (WO-129 §2.3)
// -----------------------------------------------------------------------------
// Opt-in link / unlink of a social handle on a profile. WALLET-AUTH GATED (the
// signed wallet MUST equal the profile wallet). Social linking is STRICTLY
// OPT-IN and never a wall — the game is fully playable/rankable with nothing
// linked (WO-129 §2.3 / §5). We store only the minimum (handle + provider +
// the player's "show on profile" toggle); unlink PURGES the stored handle
// (ties to the GDPR delete stance).
//
// Client : ProfileService (Core) — WO-129 §4 (NEW).
//   POST  application/json   (raw body — bodyParser disabled, signature over the
//                             exact bytes, same as save.js)
//   Headers: X-Wallet / X-Nonce / X-Signature
//   Body (link)   : { wallet, action:'link', provider:'x'|'discord',
//                     handle, showOnProfile?:bool }
//   Body (unlink) : { wallet, action:'unlink', provider:'x'|'discord' }
//   Success: { success:true, socials:{ provider:{ handle, public } } }  // full map
//   Failure: { success:false, error:'UNKNOWN_PROVIDER' | 'MISSING_HANDLE'
//              | 'UNKNOWN_ACTION' }   (business failures → 200)
//
// NOTE: we do NOT verify the social account here (no OAuth round-trip in MVP) —
// linking stores the claimed handle. A verified-handle badge is a Phase-2 add
// (WO-129 §2.3) that would gate `public` behind an OAuth confirmation.
//
// Driver: @neondatabase/serverless
// Status codes: 200 | 400 | 401 | 500
// =============================================================================

const { neon } = require('@neondatabase/serverless');
const { verifyAndConsume } = require('../_lib/wallet-auth');

// Signature is over the EXACT raw body bytes — disable the body parser (save.js).
module.exports.config = { api: { bodyParser: false } };

// MVP: X (Twitter) now; Discord reserved for Phase 2 (WO-129 §2.3).
const ALLOWED_PROVIDERS = new Set(['x', 'discord']);
const MAX_HANDLE_LEN = 64;

module.exports = async (req, res) => {
    if (req.method !== 'POST') {
        return res.status(400).json({ error: 'Method not allowed' });
    }

    let rawBody;
    try {
        rawBody = await readBody(req);
    } catch (err) {
        console.error('[profile/social] Body read error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }

    let body;
    try {
        body = JSON.parse(rawBody.toString());
    } catch (err) {
        console.error('[profile/social] Decode error:', err);
        return res.status(400).json({ error: 'Invalid payload' });
    }
    if (!body || typeof body !== 'object') {
        return res.status(400).json({ error: 'Invalid payload' });
    }

    const wallet = body.wallet != null ? String(body.wallet).trim() : '';
    const action = body.action != null ? String(body.action).trim().toLowerCase() : '';
    const provider = body.provider != null ? String(body.provider).trim().toLowerCase() : '';

    if (!wallet) return res.status(400).json({ error: 'Missing wallet' });
    if (action !== 'link' && action !== 'unlink') {
        return res.status(200).json({ success: false, error: 'UNKNOWN_ACTION' });
    }
    if (!ALLOWED_PROVIDERS.has(provider)) {
        return res.status(200).json({ success: false, error: 'UNKNOWN_PROVIDER' });
    }

    let handle = '';
    let showOnProfile = false;
    if (action === 'link') {
        handle = body.handle != null ? String(body.handle).trim().slice(0, MAX_HANDLE_LEN) : '';
        if (!handle) {
            return res.status(200).json({ success: false, error: 'MISSING_HANDLE' });
        }
        showOnProfile = body.showOnProfile === true;
    }

    let sql;
    try {
        sql = neon(process.env.DATABASE_URL);
    } catch (err) {
        console.error('[profile/social] DB init error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }

    // ── AUTH GATE — signed wallet MUST equal the profile wallet ────────────
    let auth;
    try {
        auth = await verifyAndConsume(sql, req.headers, rawBody, wallet);
    } catch (err) {
        console.error('[profile/social] Auth check error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }
    if (!auth.ok) {
        return res.status(401).json({ error: 'Unauthorized', reason: auth.reason });
    }

    try {
        // Ensure a profile row exists (link/unlink can predate a username set).
        await sql`
            INSERT INTO player_profiles (wallet, created_at)
            VALUES (${wallet}, NOW())
            ON CONFLICT (wallet) DO NOTHING
        `;

        if (action === 'link') {
            // jsonb_set the provider entry. Stores only { handle, public }.
            const entry = JSON.stringify({ handle, public: showOnProfile });
            await sql`
                UPDATE player_profiles
                SET social_links = jsonb_set(
                    COALESCE(social_links, '{}'::jsonb),
                    ${`{${provider}}`}::text[],
                    ${entry}::jsonb,
                    true
                )
                WHERE wallet = ${wallet}
            `;
        } else {
            // unlink → purge the provider's stored handle entirely.
            await sql`
                UPDATE player_profiles
                SET social_links = (COALESCE(social_links, '{}'::jsonb) - ${provider})
                WHERE wallet = ${wallet}
            `;
        }

        // Return the full current link map (handle + public flag per provider).
        const rows = await sql`
            SELECT social_links FROM player_profiles WHERE wallet = ${wallet} LIMIT 1
        `;
        const links = rows.length > 0 && rows[0].social_links ? rows[0].social_links : {};

        return res.status(200).json({ success: true, socials: links });
    } catch (err) {
        console.error('[profile/social] DB error:', err);
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
