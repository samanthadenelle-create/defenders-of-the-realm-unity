'use strict';

// =============================================================================
// api/pi/ads-verify.js — WO-1320. THE ONLY THING THAT MAY AUTHORISE AN AD REWARD.
// -----------------------------------------------------------------------------
// The Pi Ads docs are explicit: "you must verify the rewarded status of the ad using
// Pi Platform API, before rewarding users" — because a player may be running a hacked
// SDK build, in which case `Pi.Ads.showAd()` returning `{ result: "AD_REWARDED" }` is
// a claim made by an attacker on their own device. The client's word is evidence of
// nothing. This endpoint is where the claim becomes a fact, or does not.
//
//   GET https://api.minepi.com/v2/ads_network/status/<adId>
//   Authorization: Key <PI_NETWORK_API_KEY>
//
// ⛔ THE ONE GRANT CONDITION: mediator_ack_status === "granted". Not "the ad exists",
// not "Pi answered 200", not "the status is not 'denied'". A single equality against a
// single string, and every other shape of the world is a refusal.
//
// ⛔ FAIL CLOSED, EVERYWHERE. Key not configured, Pi unreachable, timeout, malformed
// body, an ack status we have never seen — all of them answer granted:false. The cost
// of refusing a legitimate reward is one annoyed player who can watch another ad. The
// cost of granting an illegitimate one is fabricated impressions against a live ad
// account, which is how a publisher account gets terminated. Those are not symmetric,
// so the refusal is the default and the grant is the exception that must be earned.
// This mirrors the rate fetcher in _lib/pi-payments.js, which refuses to quote a price
// rather than invent one.
//
// ⛔ THE API KEY NEVER LEAVES THE SERVER. It is read only through pi-payments.js's
// piApiKey(), used only inside piCall()'s Authorization header, and is never returned,
// logged, or echoed into an error string. `configured()` is the only thing this file
// asks about it. Nothing about the key's presence or absence is distinguishable to the
// caller beyond a generic PI_NOT_CONFIGURED code.
//
// CLIENT: Assets/_Modules/Village/Monetization/Providers/Pi/PiAdProvider.cs
//   POST /api/pi/ads-verify   body { adId }
//   Reply 200 { success:true, granted:<bool>, code:"<why>" }
// =============================================================================

const { PI_API_ROOT, configured, piCall } = require('../_lib/pi-payments.js');

// The ONLY value that authorises a grant. Compared with === against a trimmed string.
const GRANTED = 'granted';

// ── The null-ack retry window ───────────────────────────────────────────────
// `mediator_ack_status` is null for a short while after the ad completes: the mediator
// has not yet acknowledged the impression to Pi. That is a RACE, not a refusal, and
// answering granted:false immediately would deny a legitimate reward on timing alone.
//
// But a bounded wait is the whole point. If the ack has not landed after this window we
// answer granted:false and say WHY (PI_ADS_ACK_PENDING) — we do not keep the player's
// button spinning, and we never treat "still null" as "probably fine". A pending ack is
// simply not a granted ack.
const ACK_ATTEMPTS = 3;
const ACK_RETRY_MS = 1000;

// adId shape guard, applied BEFORE the value is ever interpolated into a URL. Pi does
// not publish the format, so this is deliberately permissive about CONTENT and strict
// about CHARACTER CLASS: URL-safe token characters only, bounded length. It exists to
// make path traversal and header/URL injection structurally impossible, not to validate
// that a well-formed id is real — only Pi can answer that.
const AD_ID_RE = /^[A-Za-z0-9._~:-]{1,128}$/;

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

/**
 * Ask Pi whether this ad was actually rewarded.
 *
 * @returns {{granted:boolean, code:string}}  `code` is a short machine token for the
 *          client trace and the audit line. It never carries upstream body text.
 */
async function readAdStatus(adId) {
    let lastCode = 'PI_ADS_UNREACHABLE';

    for (let attempt = 1; attempt <= ACK_ATTEMPTS; attempt++) {
        const result = await piCall('GET', `/ads_network/status/${encodeURIComponent(adId)}`, null);

        if (!result.ok) {
            // A 404 means Pi has no such ad. That is terminal and there is nothing to wait
            // for, so it does not consume the retry budget — retrying an unknown ad only
            // delays the refusal the caller is already owed.
            if (result.status === 404) return { granted: false, code: 'PI_ADS_UNKNOWN_AD' };
            lastCode = result.code || 'PI_ADS_UPSTREAM';
            // Transport/5xx: the ack may still be coming, so this one IS worth another look.
            if (attempt < ACK_ATTEMPTS) { await sleep(ACK_RETRY_MS); continue; }
            return { granted: false, code: lastCode };
        }

        const body = result.body || {};
        const ack = body.mediator_ack_status;

        if (typeof ack === 'string' && ack.trim().toLowerCase() === GRANTED) {
            return { granted: true, code: 'PI_ADS_GRANTED' };
        }

        // A non-null ack that is not "granted" is a DECIDED refusal. Pi has answered; there
        // is nothing further to wait for, so return immediately rather than burning the
        // retry budget re-reading a settled negative.
        if (ack !== null && ack !== undefined) {
            return { granted: false, code: 'PI_ADS_NOT_GRANTED' };
        }

        // ack === null: the documented brief window before the mediator acknowledges.
        lastCode = 'PI_ADS_ACK_PENDING';
        if (attempt < ACK_ATTEMPTS) await sleep(ACK_RETRY_MS);
    }

    // Still null after the whole window. NOT granted — fail closed.
    return { granted: false, code: lastCode };
}

module.exports = async (req, res) => {
    // CORS — copied from api/pi/verify.js:27-35 and for the same reason. The PUBLISHED app
    // is served under https://echoesofelarions6578.pinet.com (Pi's proxy), so every call to
    // this Vercel function is CROSS-ORIGIN and the browser blocks it without these headers.
    //
    // ACAO:* is safe here because there are no cookies and no credentials, and — the load
    // bearing part — this endpoint reads NOTHING from a custom request header. Only
    // Content-Type is advertised, so the POST stays a simple request and no additional
    // preflight surface is created. (See the note at the top of api/_lib/http.js: a
    // cross-origin request carrying a custom header that the endpoint does not advertise is
    // blocked BEFORE the function ever runs.)
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'POST, OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type');
    if (req.method === 'OPTIONS') { res.status(204).end(); return; }

    if (req.method !== 'POST') {
        res.status(400).json({ success: false, granted: false, code: 'METHOD_NOT_ALLOWED' });
        return;
    }

    try {
        // Vercel auto-parses JSON bodies; tolerate a raw string body too (verify.js pattern).
        let body = req.body;
        if (typeof body === 'string') {
            try { body = JSON.parse(body); } catch (_) { body = {}; }
        }

        const adId = body && typeof body.adId === 'string' ? body.adId.trim() : '';
        if (!adId || !AD_ID_RE.test(adId)) {
            // A missing adId is the client telling us it has no proof at all. That is the
            // ordinary shape of a NON-rewarded outcome, so it is a clean refusal (200 with
            // granted:false), not a transport error the client has to special-case.
            res.status(200).json({ success: true, granted: false, code: 'PI_ADS_ADID_MISSING' });
            return;
        }

        if (!configured()) {
            // Dormant-unless-configured, exactly like the Pi payment rail. Nothing is granted
            // and nothing about the key is disclosed.
            console.error('[pi/ads-verify] PI_NETWORK_API_KEY is not configured - refusing every grant.');
            res.status(200).json({ success: true, granted: false, code: 'PI_NOT_CONFIGURED' });
            return;
        }

        const verdict = await readAdStatus(adId);

        // Server-side only. The adId is not a secret (the client supplied it) but the upstream
        // body never appears here, per the house pattern.
        console.log(`[pi/ads-verify] adId=${adId} granted=${verdict.granted} code=${verdict.code} root=${PI_API_ROOT}`);

        res.status(200).json({ success: true, granted: verdict.granted, code: verdict.code });
    } catch (e) {
        // House pattern (security audit 2026-08-15): generic string out, detail server-side.
        // 500 still carries granted:false so a client that only reads the body cannot mistake
        // an error for a reward.
        console.error('[pi/ads-verify] error:', e);
        res.status(500).json({ success: false, granted: false, code: 'INTERNAL_ERROR' });
    }
};
