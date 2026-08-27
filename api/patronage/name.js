// =============================================================================
// api/patronage/name.js -- read your own patronage status, or set/edit the
// public patron name you appear under on the Benefactors wall.
// WO-1073, owner ruling 2026-08-27.
// -----------------------------------------------------------------------------
// WALLET-AUTH GATED, both directions, and it is POST-only for a reason worth
// writing down: the signed-nonce rail signs the EXACT RAW BODY BYTES
// (_lib/wallet-auth), so a GET -- having no body -- cannot be authenticated on
// this rail at all. Rather than invent a second, weaker door for a read, the
// read travels as a POST with no patronName. That also keeps a wallet's TIER off
// an unauthenticated URL: the public wall is the only anonymous read in this
// feature, and it carries no addresses.
//
//   POST /api/patronage/name          (raw body; bodyParser disabled)
//   Headers: X-Wallet / X-Nonce / X-Signature
//   Body   : { wallet }                      -> READ own status
//            { wallet, patronName }          -> SET or EDIT the name
//
//   Read reply : { success:true, tierId, tierLabel, wallEligible, onWall,
//                  patronName, nameEditsRemaining,
//                  monumentAssetId, monumentIsBespoke }
//   Set reply  : { success:true, patronName, onWall:true, wasEdit,
//                  nameEditsRemaining }
//   Refusal    : { success:false, error:'<CODE>' } at HTTP 200 -- business
//                failures are 200 here exactly as in profile/username.js and
//                promo/redeem, so the client branches on a stable code instead
//                of on a status class.
//
// CODES: PATRON_NAME_TOO_SHORT | _TOO_LONG | _INVALID_CHARS | _REJECTED
//        | _RESEMBLES_WALLET | _TAKEN | _EDITS_EXHAUSTED | PATRONAGE_NOT_ELIGIBLE
//
// THE CLIENT CANNOT GRANT ITSELF ANYTHING HERE. It sends a wallet and a
// string. The tier is re-derived server-side from settled purchase_entitlements
// on every call (_lib/benefactors -> _lib/patronage), so a forged body cannot
// claim founder status, and a founder who has not chosen a name is simply not
// listed. Wall entry is a CONSEQUENCE of paying, never a purchasable item; this
// endpoint grants no resource, no currency and no timer -- it stores a name.
//
// AND IT CANNOT ASSIGN A MONUMENT. Choosing a bespoke monument is an OPERATOR
// action (the Command Center, WO-1244), performed as each one-on-one
// collaboration finishes; the seam it calls is
// _lib/benefactors.assignPatronMonument, which refuses unless the asset is
// proven present in the shipped catalog. A player-facing endpoint must never be
// able to name its own art.
//
// Driver: @neondatabase/serverless
// Status codes: 200 | 400 | 401 | 500
// =============================================================================

const { neon } = require('@neondatabase/serverless');
const { verifyAndConsume } = require('../_lib/wallet-auth');
const { readRawBody } = require('../_lib/http');
const { readOwnPatronage, setPatronName } = require('../_lib/benefactors');

// The signature covers the exact bytes -- the parser must stay off, and this
// assignment must stay BELOW module.exports = handler (see _lib/http.js: doing
// it the other way round silently discarded the config and hung save.js).
const MAX_BODY_BYTES = 4 * 1024;

async function handler(req, res) {
    if (req.method !== 'POST') {
        return res.status(400).json({ success: false, error: 'Method not allowed' });
    }

    let rawBody;
    try {
        rawBody = await readRawBody(req, MAX_BODY_BYTES);
    } catch (err) {
        console.error('[patronage/name] body read error:', err && err.code);
        return res.status(400).json({ success: false, error: 'Invalid payload' });
    }

    let body;
    try {
        body = JSON.parse(rawBody.toString());
    } catch (err) {
        return res.status(400).json({ success: false, error: 'Invalid payload' });
    }
    if (!body || typeof body !== 'object') {
        return res.status(400).json({ success: false, error: 'Invalid payload' });
    }

    const wallet = body.wallet != null ? String(body.wallet).trim() : '';
    if (!wallet) {
        return res.status(400).json({ success: false, error: 'Missing wallet' });
    }

    let sql;
    try {
        sql = neon(process.env.DATABASE_URL);
    } catch (err) {
        console.error('[patronage/name] DB init error:', err && err.message);
        return res.status(500).json({ success: false, error: 'Internal server error' });
    }

    // AUTH GATE -- the signed wallet MUST be the wallet being read or renamed.
    let auth;
    try {
        auth = await verifyAndConsume(sql, req.headers, rawBody, wallet);
    } catch (err) {
        console.error('[patronage/name] auth check error:', err && err.code);
        return res.status(500).json({ success: false, error: 'Internal server error' });
    }
    if (!auth.ok) {
        return res.status(401).json({ success: false, error: 'Unauthorized', reason: auth.reason });
    }

    const wantsWrite = body.patronName != null;

    try {
        if (!wantsWrite) {
            const status = await readOwnPatronage(sql, wallet);
            return res.status(200).json(Object.assign({ success: true }, status));
        }

        const result = await setPatronName(sql, wallet, body.patronName);
        if (!result.ok) {
            return res.status(200).json({ success: false, error: result.error });
        }
        return res.status(200).json({
            success: true,
            patronName: result.patronName,
            onWall: result.onWall,
            wasEdit: result.wasEdit,
            nameEditsRemaining: result.nameEditsRemaining,
        });
    } catch (err) {
        // Quiet for the player, loud in the log -- and never the name or the
        // wallet in that log line. This is production money data.
        console.error('[patronage/name] DB error:', err && err.code);
        return res.status(500).json({ success: false, error: 'Internal server error' });
    }
}

module.exports = handler;
module.exports.config = { api: { bodyParser: false } };
module.exports._test = { MAX_BODY_BYTES };
