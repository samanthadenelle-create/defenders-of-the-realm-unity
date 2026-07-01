// =============================================================================
// api/pi/verify.js — Vercel Serverless Function (Pi Network auth verification)
// -----------------------------------------------------------------------------
// Validates a Pi access token server-side before a session is established.
// The client (Unity WebGL via PiBridge.jslib → PiSignInController) authenticates
// with Pi.authenticate(['username']) and POSTs the returned accessToken here.
// We confirm identity by calling Pi's own /me with the user's bearer token —
// NEVER trusting the frontend's claimed username/uid.
//
// Ref: https://pi-apps.github.io/pi-sdk-docs/quick-start/genai/Authentication
//   - GET https://api.minepi.com/v2/me  with  Authorization: Bearer <accessToken>
//   - NO Pi API key is required for this flow (the key is only for payment
//     approve/complete, which live on the pi-backend Worker).
//
// Client : Assets/_Modules/Core/Platform/PiSignInController.cs
//   POST  /api/pi/verify   body { accessToken }
//   Reply : { success:true, uid, username }   |   { success:false, error }
//
// Status codes: 200 | 400 | 500   (project constraint — auth FAILURE is a 200
// with success:false so the client can show a clean message, not a transport error).
// =============================================================================

const PI_ME_URL = 'https://api.minepi.com/v2/me';

module.exports = async (req, res) => {
    // CORS (2026-07-01): the PUBLISHED app is served under <app>.pinet.com (Pi's proxy),
    // so this vercel API is called CROSS-ORIGIN. Without these headers the browser blocks
    // the sign-in POST and verification fails for real Pioneers. ACAO:* is safe here — there
    // are no cookies/credentials; identity is the bearer accessToken in the body, which is
    // validated against Pi's own /me below, so a permissive origin can't forge an identity.
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'POST, OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type');
    if (req.method === 'OPTIONS') { res.status(204).end(); return; }

    if (req.method !== 'POST') {
        res.status(400).json({ success: false, error: 'POST only' });
        return;
    }

    try {
        // Vercel auto-parses JSON bodies; tolerate a raw string body too.
        let body = req.body;
        if (typeof body === 'string') {
            try { body = JSON.parse(body); } catch (_) { body = {}; }
        }
        const accessToken = body && body.accessToken;
        if (!accessToken || typeof accessToken !== 'string') {
            res.status(400).json({ success: false, error: 'missing accessToken' });
            return;
        }

        // The ONLY source of truth for identity: ask Pi who this token belongs to.
        const piResp = await fetch(PI_ME_URL, {
            method: 'GET',
            headers: { Authorization: `Bearer ${accessToken}` },
        });

        if (!piResp.ok) {
            // Invalid/expired token → auth fails, but it's not a server error.
            res.status(200).json({ success: false, error: `pi /me returned ${piResp.status}` });
            return;
        }

        const me = await piResp.json(); // { uid, username, ... } per Pi /me
        if (!me || !me.uid) {
            res.status(200).json({ success: false, error: 'pi /me missing uid' });
            return;
        }

        // Session established on the verified identity. (Persisting the Pi uid
        // against the player's save is the client's job; this endpoint is the
        // trust boundary — it only returns what Pi itself confirmed.)
        res.status(200).json({ success: true, uid: me.uid, username: me.username || null });
    } catch (e) {
        res.status(500).json({ success: false, error: String(e && e.message ? e.message : e) });
    }
};
