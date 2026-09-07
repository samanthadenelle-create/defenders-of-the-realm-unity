// =============================================================================
// api/events/track.js — Vercel Serverless Function
// -----------------------------------------------------------------------------
// Receives a batch of analytics events from the Unity client (EventTracker.cs)
// and inserts one row per event into the analytics_events table.
//
// Client : Assets/_Modules/Core/Analytics/EventTracker.cs
//   POST  application/json
//   Body  : { "events": [ { "eventName", "properties", "clientTs" }, … ] }
//           - eventName  string  (snake_case)
//           - properties string  (JSON STRING — parsed to JSONB on insert)
//           - clientTs   long    (unix epoch MILLISECONDS)
//   Reply : { "success": true }   (client is fire-and-forget; only checks 2xx)
//
// ⛔ THE BODY NO LONGER NAMES THE PLAYER (WO-1506). It used to: the row's
//    player_id came straight off each event as "BoundWallet | anonymous", with no
//    auth and no rate limit on the route, so ANY caller could write unbounded rows
//    attributed to ANY wallet — and those rows feed the retention and funnel
//    numbers the owner makes business decisions from. The client may still SEND a
//    playerId; it is ignored. The row is bound to the CALLER instead:
//
//      X-Session   → wallet-auth.verifySession names the wallet   → _auth:'session'
//      X-Guest-Id  → a guest-shaped id binds to itself            → _auth:'guest'
//      neither     → the literal id `unverified`                  → _auth:'unverified'
//
//    `unverified` is ONE bucket on purpose: a single entry in
//    ANALYTICS_EXCLUDED_PLAYER_IDS (api/admin/stats.js excludedPlayerIds) then
//    removes every unproven row from the Command Center at once. Pre-wallet funnel
//    traffic still LANDS — the route never rejects it — it just cannot forge an
//    identity while doing so.
//
// ⚠ THE GUEST RAIL HERE IS BEARER TRUST, AND DELIBERATELY DOES NOT CALL
//    verifyGuest(). That helper spends guest_rate_limit, which wallet-auth.js keys
//    on the guest id alone and SHARES with game/save + game/load (30 per 60s). A
//    busy analytics flush would therefore rate-limit the player's own saves. The
//    IP budget below is this route's rate control instead.
//
// ⚠ WO §4 acceptance 2 asked for a "server-minted guest id" for anonymous events.
//    There is no minting helper in this project — a guest id is minted on the
//    DEVICE (GameStateService.EnsureAccount) — and a per-request server id would be
//    either unbounded cardinality or forgeable. Anonymous traffic is TAGGED
//    `unverified` instead, per the implementing lane's instruction. Stated rather
//    than quietly reinterpreted (CLAUDE.md §11B).
//
// Rate limit: the project's ONE budget helper, api/_lib/ip-budget.js (WO-1456),
// scope 'EVENTS_TRACK', FAIL-OPEN — analytics is not allowed to take the game down,
// so an unreadable budget table degrades to "allow" and says so loudly.
//
// Driver: @neondatabase/serverless   (same as game/save.js, game/load.js)
// Status codes: 200 | 400 | 500   (project constraint — no others). A budget
// refusal answers 200 {success:false,error:'RATE_LIMITED'} — the same shape
// promo/redeem.js uses — because EventTracker.FlushWithRetry retries any non-2xx
// four times with backoff, and an over-budget attempt still increments the counter.
//
// ⚠ TWO FOLLOW-UPS THIS SILO CANNOT MAKE, both measured 2026-09-06, not inferred:
//   1. THE CLIENT SENDS NEITHER HEADER YET. EventTracker.cs:293 sets exactly one
//      header, "Content-Type". So until a client WO adds X-Session / X-Guest-Id,
//      EVERY row lands as `unverified` — correct (the server must not trust the
//      body either way), but the per-player funnel goes dark until that ships.
//   2. `unverified` IS NOT AUTO-EXCLUDED. api/admin/stats.js excludedPlayerIds()
//      hardcodes only ANON_ID as always-excluded, so this bucket counts as one
//      "player" in retention until either ANALYTICS_EXCLUDED_PLAYER_IDS=unverified
//      is set on the deployment, or that array gains it (out of this silo).
//
// STALE ELSEWHERE (out of this silo, named per §15): api/schema.sql:319 and :336
// still document player_id as "BoundWallet | anonymous". docs/MASTER_CATALOG was
// checked and does NOT repeat that claim (core.md:702 describes the client only).
// =============================================================================

const { neon } = require('@neondatabase/serverless');
const { verifySession, isGuestId, guestEnabled } = require('../_lib/wallet-auth');
const { reserveIpBudget } = require('../_lib/ip-budget');
const { hashIp } = require('../_lib/audit');

// Hard ceiling on one POST. The client batches a handful of events; anything past
// this is either a bug or an attempt to make one request do unbounded DB work.
// Surplus events are DROPPED and the count is reported, never silently eaten.
const MAX_EVENTS_PER_BATCH = 100;

// The one id every unproven row shares. Never a real player.
const UNVERIFIED_PLAYER_ID = 'unverified';

// Per caller IP, per minute. The client's own flush cadence is a batch every few
// seconds at full tilt, so no honest device is anywhere near this; it exists to
// stop a script writing unbounded rows.
const IP_WINDOW_SECONDS = 60;
const IP_MAX_PER_WINDOW = 60;

/**
 * Who is this caller? Never throws: a broken auth table degrades to `unverified`
 * rather than losing the event, because analytics is not a value-granting rail.
 */
async function resolveIdentity(sql, headers, sessionVerifier) {
    const sessionToken = headers['x-session'];
    if (sessionToken) {
        try {
            const ses = await sessionVerifier(sql, String(sessionToken), null);
            if (ses.ok) return { playerId: ses.wallet, auth: 'session' };
            console.warn('[events/track] session offered but not valid (' + ses.code + ') — row lands unverified.');
        } catch (err) {
            console.warn('[events/track] session check failed — row lands unverified:', err && err.message);
        }
    }

    const guest = headers['x-guest-id'];
    if (guest && guestEnabled() && isGuestId(String(guest))) {
        return { playerId: String(guest), auth: 'guest' };
    }

    return { playerId: UNVERIFIED_PLAYER_ID, auth: 'unverified' };
}

function makeHandler(deps = {}) {
    const getSql = deps.getSql || (() => neon(process.env.DATABASE_URL));
    const sessionVerifier = deps.verifySession || verifySession;

    return async (req, res) => {
        // CORS: the published app runs under <app>.pinet.com and POSTs events
        // cross-origin. The identity headers must be listed or the browser
        // preflight strips them and every row silently lands unverified.
        res.setHeader('Access-Control-Allow-Origin', '*');
        res.setHeader('Access-Control-Allow-Methods', 'POST, OPTIONS');
        res.setHeader('Access-Control-Allow-Headers', 'Content-Type, X-Session, X-Guest-Id');
        if (req.method === 'OPTIONS') { return res.status(204).end(); }

        if (req.method !== 'POST') {
            return res.status(400).json({ error: 'Method not allowed' });
        }

        // ── Parse body (Vercel auto-parses application/json into req.body) ─────
        let body = req.body;
        try {
            if (typeof body === 'string') body = JSON.parse(body);
        } catch (err) {
            console.error('[events/track] Body parse error:', err);
            return res.status(400).json({ error: 'Invalid payload' });
        }

        const headers = req.headers || {};
        const events = body && Array.isArray(body.events) ? body.events : null;

        // ⚠ EVERY FREE CHECK HAPPENS BEFORE THE BUDGET IS SPENT. A malformed
        // request must never cost a household a unit — the placement rule WO-1440
        // wrote into the promo route and WO-1456 into the nonce route.
        if (!events) {
            return res.status(400).json({ error: 'Missing events array' });
        }
        if (events.length === 0) {
            // Nothing to write, but a valid request — keep the client happy.
            return res.status(200).json({ success: true, inserted: 0 });
        }

        try {
            const sql = getSql();

            const identity = await resolveIdentity(sql, headers, sessionVerifier);

            const spend = await reserveIpBudget(sql, hashIp(req), 'EVENTS_TRACK', {
                windowSeconds: IP_WINDOW_SECONDS,
                maxPerWindow: IP_MAX_PER_WINDOW,
                failClosed: false,
                label: 'events/track',
            });
            if (!spend.ok) {
                console.warn('[events/track] REFUSED — IP budget exhausted (grants=' + (spend.grants || '?') + ').');
                return res.status(200).json({ success: false, error: spend.error || 'RATE_LIMITED', inserted: 0 });
            }

            // ── Build ONE multi-row insert (security audit 2026-08-15) ──────────
            // Was: uncapped array, one AWAITED round-trip per element. A single POST
            // could therefore hold a function open for thousands of sequential
            // queries. Now the batch is capped and lands in one statement.
            const batch = events.slice(0, MAX_EVENTS_PER_BATCH);

            const values = [];
            const params = [];
            for (const ev of batch) {
                if (!ev) continue;

                const eventName = ev.eventName != null ? String(ev.eventName) : null;
                if (!eventName) continue; // skip malformed events rather than fail the batch

                // properties arrives as a JSON STRING from the client; parse so it
                // lands as proper JSONB. Fall back to {} if it isn't valid JSON.
                let propsObj = {};
                if (ev.properties != null) {
                    if (typeof ev.properties === 'string') {
                        try { propsObj = JSON.parse(ev.properties); }
                        catch { propsObj = { _raw: ev.properties }; }
                    } else if (typeof ev.properties === 'object') {
                        propsObj = ev.properties;
                    }
                }
                // The row says how its identity was established, so a reader can
                // tell proven traffic from asserted traffic without joining anything.
                propsObj._auth = identity.auth;

                const clientTs = ev.clientTs != null ? Number(ev.clientTs) : null;

                // The ::jsonb cast is required because the Neon HTTP driver sends
                // parameters as strings (same pattern as game/save.js).
                const i = params.length;
                values.push(`($${i + 1}, $${i + 2}, $${i + 3}::jsonb, $${i + 4})`);
                params.push(
                    identity.playerId,
                    eventName,
                    JSON.stringify(propsObj),
                    Number.isFinite(clientTs) ? clientTs : null,
                );
            }

            if (values.length === 0) {
                return res.status(200).json({ success: true, inserted: 0 });
            }

            await sql(
                'INSERT INTO analytics_events (player_id, event_name, properties, client_ts) VALUES ' +
                values.join(', '),
                params,
            );

            return res.status(200).json({
                success: true,
                inserted: values.length,
                dropped: events.length - batch.length,
                auth: identity.auth,
            });
        } catch (err) {
            console.error('[events/track] DB error:', err);
            return res.status(500).json({ error: 'Internal server error' });
        }
    };
}

module.exports = makeHandler();
module.exports._test = { makeHandler, UNVERIFIED_PLAYER_ID };
