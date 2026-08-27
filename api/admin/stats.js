// =============================================================================
// api/admin/stats.js — OWNER-ONLY live-ops STATS endpoint (2026-08-17, WO-1116 phase 1)
// -----------------------------------------------------------------------------
// Backs site/admin.html (the unlisted, key-gated operator dashboard).
//
// Answers the owner's question — "player stats, retention, active players,
// purchase stats" — from the 87k+ rows already sitting in analytics_events.
// api/admin/db.js is the RAW-TABLE viewer (row counts, one save, one trace);
// this is the AGGREGATE viewer (who is playing, who comes back, where the
// tutorial loses them, what sells). Deliberately a SECOND file: db.js is a
// working triage tool and is not to be destabilised by dashboard work.
//
// ── READ-ONLY BY CONSTRUCTION (same contract as db.js) ───────────────────────
//   * GET + OPTIONS only. Anything else → 400.
//   * X-Admin-Key must match process.env.ADMIN_DASH_KEY, compared in constant
//     time. The auth block below is COPIED VERBATIM from db.js on purpose —
//     one auth scheme for the admin surface, not two that can drift apart.
//   * Every statement is a SELECT. There is no INSERT/UPDATE/DELETE anywhere in
//     this file and none may ever be added — writes belong in a separate,
//     audited endpoint (the WO-1116 phase-2 spec: WorkOrders/WORK_ORDER_1116_admin_dashboard_and_grants.md).
//   * Every query carries a hard LIMIT. analytics_events is ~87k rows today and
//     only grows; an unbounded aggregate here is a self-inflicted outage later.
//   * All caller input reaches SQL only through neon tagged-template parameters.
//     No string-built SQL, ever.
//   * `properties` is NEVER returned wholesale. It is free-form JSONB written by
//     the client and may contain anything; each view pulls the named keys it
//     needs and nothing else.
//
// ── PLAYER IDS ARE MASKED (first4…last4) ─────────────────────────────────────
//   player_id IS a Solana wallet address — a real, permanent, on-chain identity.
//   This page will end up in screenshots, so the list views emit
//   `player_masked` ("CHKK…sfkC") plus `player_ref`, a stable 12-hex SHA-256
//   handle used to drill down without ever putting a full address on screen.
//   THE ONE EXCEPTION: ?view=players&player=<id> / &ref=<handle> returns the
//   FULL id. That exists because the operator genuinely needs the real wallet to
//   act on one player — binding a promo code to them (promo_codes.bound_wallet)
//   or answering a support ticket — and it is a single, deliberate lookup rather
//   than a bulk dump.
//
// ── HONESTY RULES BAKED INTO THE RESPONSES ───────────────────────────────────
//   * Every retention percentage ships with its COHORT SIZE and a `low_n` flag.
//     "50%" over two players is noise, and a dashboard that hides that is worse
//     than no dashboard.
//   * A cohort younger than the window it is measured over is marked immature
//     (`d7_mature:false`) rather than reported as 0% — the players have not had
//     seven days yet.
//   * player_id is the literal string "anonymous" for every player with no bound
//     wallet (EventTracker.cs:168 — `BoundWallet ?? "anonymous"`). That is ONE
//     bucket shared by everyone, so it can never be a distinct-player count.
//     Every player metric here EXCLUDES it and reports its volume separately as
//     `anonymous_*`. Read a large anonymous share as "this dashboard cannot see
//     most of the playerbase", not as "one very busy player".
//
//   GET /api/admin/stats?view=overview[&days=N]
//   GET /api/admin/stats?view=retention[&days=N]
//   GET /api/admin/stats?view=funnel[&days=N]
//   GET /api/admin/stats?view=economy[&days=N]
//   GET /api/admin/stats?view=purchases[&days=N]
//   GET /api/admin/stats?view=ops[&days=N]      (WO-1244 Command Center: toggles,
//                                                 promos, player issues - ALL READ)
//   GET /api/admin/stats?view=players[&limit=N][&player=<id>|&ref=<12hex>]
//
// ── ⛔ TWO PURCHASE VIEWS, AND THEY ARE NOT INTERCHANGEABLE ──────────────────
//   ?view=economy   — CLIENT-REPORTED INTENT. Aggregates the purchase_completed
//                     event the game emits. It is a real funnel (bundle_viewed →
//                     purchase_completed) and it carries NO money.
//   ?view=purchases — SERVER TRUTH. Aggregates purchase_entitlements (a row
//                     exists only after the backend verified the finalized chain
//                     transaction itself) and purchase_quotes. This is where
//                     revenue comes from.
//   ⛔ NEVER merge the two into one figure. WO-1158 fixed exactly this direction
//   of trust inside the rail — the server issues the price because the client's
//   number is not authoritative — and a blended dashboard number would undo that
//   by hiding the disagreement. The disagreement IS the alert; ?view=purchases
//   surfaces it as `disagreement`.
//
// EVENT NAMES: only names the client actually emits are queried here (verified
// against Assets/ by grep, 2026-08-17):
//   session_start (EventTracker.Start), wave_completed (WaveManager),
//   purchase_completed + bundle_viewed (PackStore), promo_redeemed
//   (PromoCodeService), referral_code_generated / referral_shared /
//   referral_claimed (ReferralService), tutorial_started / tutorial_step_enter /
//   tutorial_step_complete / tutorial_step_skip / tutorial_step_drop /
//   tutorial_skipped_all / tutorial_completed / contextual_step_enter
//   (TutorialFlow). No metric here is invented on an event that does not exist.
// =============================================================================

const { neon } = require('@neondatabase/serverless');
const crypto = require('crypto');

// The six kill-switch area ids, imported rather than re-typed. A seventh area
// invented here would render a toggle the enforcement layer has never heard of.
const { AREAS: MAINTENANCE_AREAS } = require('../_lib/maintenance');

// Constant-time key check. Hashing both sides first makes timingSafeEqual
// usable on unequal lengths without leaking length information.
// (Identical to api/admin/db.js — one admin auth scheme, not two.)
function adminKeyOk(given, expected) {
    if (!given || !expected) return false;
    const a = crypto.createHash('sha256').update(String(given)).digest();
    const b = crypto.createHash('sha256').update(String(expected)).digest();
    return crypto.timingSafeEqual(a, b);
}

function clampLimit(raw, def, max) {
    const n = parseInt(raw, 10);
    if (!Number.isFinite(n) || n <= 0) return def;
    return Math.min(n, max);
}

// The literal id every un-bound player shares (EventTracker.cs:168). Excluded
// from every distinct-player metric; counted separately so its size is visible.
const ANON_ID = 'anonymous';

// first4…last4. Never widen this — the whole point is that a screenshot of the
// dashboard cannot be used to look a player's wallet up on-chain.
function maskId(id) {
    if (id == null) return null;
    const s = String(id);
    if (s === ANON_ID) return ANON_ID;
    if (s.length <= 9) return s.slice(0, 2) + '…' + s.slice(-2);
    return s.slice(0, 4) + '…' + s.slice(-4);
}

// Safe numeric read of a JSONB text value. Client-authored, so anything may be
// in there; a non-number returns null instead of poisoning an aggregate.
function num(v) {
    if (v == null) return null;
    const n = Number(String(v).trim());
    return Number.isFinite(n) ? n : null;
}

function pct(part, whole) {
    if (!whole || whole <= 0) return null;   // null renders as "no data yet", never 0%
    return Math.round((Number(part) / Number(whole)) * 1000) / 10;
}

// A cohort smaller than this cannot support a percentage. 10 is a judgement
// call, stated out loud rather than hidden: below it the page shows the raw
// counts and labels the percentage as unreliable.
const LOW_N_THRESHOLD = 10;

module.exports = async (req, res) => {
    // CORS: site/admin.html is deployed on the `echoes-of-elarion` Vercel project
    // and this function on `defenders-of-the-realm-v2` — the dashboard is ALWAYS
    // a cross-origin caller. Same header set as db.js.
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'GET, OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type, X-Admin-Key');
    if (req.method === 'OPTIONS') { return res.status(204).end(); }

    if (req.method !== 'GET') {
        return res.status(400).json({ error: 'Method not allowed' });
    }

    const expected = process.env.ADMIN_DASH_KEY;
    if (!expected) {
        // Not configured yet — refuse everything (never fail open).
        return res.status(400).json({ error: 'Admin access not configured' });
    }
    if (!adminKeyOk(req.headers['x-admin-key'], expected)) {
        return res.status(400).json({ error: 'Unauthorized' });
    }

    const q = req.query || {};
    const view = String(q.view || 'overview');
    const days = clampLimit(q.days, 30, 180);   // analysis window, hard-capped
    const now = new Date();
    const meta = { view: view, generated_at: now.toISOString(), window_days: days };

    try {
        const sql = neon(process.env.DATABASE_URL);

        // ============================================================ overview
        // WHO IS PLAYING.
        //   active_today / _7d / _30d — DISTINCT player_id that fired a
        //     session_start inside the window. session_start is emitted once per
        //     app boot (EventTracker.Start), so this is "opened the game", not
        //     "was online at that moment".
        //   sessions_* — the session_start ROW count over the same windows (app
        //     opens, several per player per day).
        //   new_players_per_day — players whose FIRST-EVER event of any kind
        //     landed on that day. First-ever is computed over the WHOLE table, so
        //     someone who installed in June never re-counts as new in August.
        if (view === 'overview') {
            const active = await sql`
                SELECT
                    COUNT(DISTINCT player_id) FILTER (WHERE received_at > NOW() - INTERVAL '1 day')::bigint   AS active_today,
                    COUNT(DISTINCT player_id) FILTER (WHERE received_at > NOW() - INTERVAL '7 days')::bigint  AS active_7d,
                    COUNT(DISTINCT player_id) FILTER (WHERE received_at > NOW() - INTERVAL '30 days')::bigint AS active_30d,
                    COUNT(*) FILTER (WHERE received_at > NOW() - INTERVAL '1 day')::bigint   AS sessions_today,
                    COUNT(*) FILTER (WHERE received_at > NOW() - INTERVAL '7 days')::bigint  AS sessions_7d,
                    COUNT(*) FILTER (WHERE received_at > NOW() - INTERVAL '30 days')::bigint AS sessions_30d
                FROM analytics_events
                WHERE event_name = 'session_start'
                  AND player_id <> ${ANON_ID}
                  -- FIXED 30-day scan, deliberately NOT the ?days window: these three
                  -- tiles ARE the 1/7/30-day definitions, so letting a "last 7 days"
                  -- selection clip them would silently render active_30d as a 7-day
                  -- number under a "30 days" label. ?days drives the per-day tables
                  -- below, not these.
                  AND received_at > NOW() - INTERVAL '30 days'
                LIMIT 1`;

            // The blind spot, measured. If this dwarfs the numbers above, the
            // dashboard is reporting on a minority of the playerbase.
            const anon = await sql`
                SELECT COUNT(*) FILTER (WHERE event_name = 'session_start')::bigint AS anonymous_sessions,
                       COUNT(*)::bigint                                             AS anonymous_events
                FROM analytics_events
                WHERE player_id = ${ANON_ID}
                  AND received_at > NOW() - (${days} * INTERVAL '1 day')
                LIMIT 1`;

            const totals = await sql`
                SELECT COUNT(*)::bigint                  AS total_events,
                       COUNT(DISTINCT player_id)::bigint AS total_ids_seen,
                       MIN(received_at)                  AS first_event_at,
                       MAX(received_at)                  AS last_event_at
                FROM analytics_events
                LIMIT 1`;

            const newPerDay = await sql`
                WITH firsts AS (
                    SELECT player_id, MIN(received_at) AS first_seen
                    FROM analytics_events
                    WHERE player_id <> ${ANON_ID}
                    GROUP BY player_id
                )
                SELECT date_trunc('day', first_seen)::date::text AS day,
                       COUNT(*)::bigint                          AS new_players
                FROM firsts
                WHERE first_seen > NOW() - (${days} * INTERVAL '1 day')
                GROUP BY 1
                ORDER BY 1 DESC
                LIMIT 181`;

            const perDay = await sql`
                SELECT date_trunc('day', received_at)::date::text AS day,
                       COUNT(DISTINCT player_id) FILTER (WHERE event_name = 'session_start')::bigint AS active_players,
                       COUNT(*)                  FILTER (WHERE event_name = 'session_start')::bigint AS sessions,
                       COUNT(*)::bigint                                                              AS events
                FROM analytics_events
                WHERE received_at > NOW() - (${days} * INTERVAL '1 day')
                  AND player_id <> ${ANON_ID}
                GROUP BY 1
                ORDER BY 1 DESC
                LIMIT 181`;

            // What the game is actually emitting — the sanity check that a metric
            // reads 0 because nobody did it, not because the event never fires.
            const byEvent = await sql`
                SELECT event_name,
                       COUNT(*)::bigint                  AS events,
                       COUNT(DISTINCT player_id)::bigint AS ids,
                       MAX(received_at)                  AS latest
                FROM analytics_events
                WHERE received_at > NOW() - (${days} * INTERVAL '1 day')
                GROUP BY 1
                ORDER BY 2 DESC
                LIMIT 60`;

            const a = active[0] || {};
            return res.status(200).json(Object.assign(meta, {
                active: {
                    today: Number(a.active_today || 0),
                    d7: Number(a.active_7d || 0),
                    d30: Number(a.active_30d || 0),
                    sessions_today: Number(a.sessions_today || 0),
                    sessions_7d: Number(a.sessions_7d || 0),
                    sessions_30d: Number(a.sessions_30d || 0),
                },
                anonymous: {
                    sessions: Number((anon[0] || {}).anonymous_sessions || 0),
                    events: Number((anon[0] || {}).anonymous_events || 0),
                    note: 'Every player with no bound wallet shares the single id "anonymous", '
                        + 'so these cannot be split into people. Excluded from all player counts.',
                },
                totals: totals[0] || null,
                new_players_per_day: newPerDay,
                per_day: perDay,
                events_by_name: byEvent,
            }));
        }

        // =========================================================== retention
        // Classic DAY-N retention, by SIGNUP COHORT.
        //   cohort_day  — the day a player's FIRST-EVER event landed.
        //   cohort_size — how many players are in that cohort (ALWAYS returned
        //                 next to the percentages; see LOW_N_THRESHOLD).
        //   d1/d7/d30   — of that cohort, how many fired a session_start on
        //                 EXACTLY cohort_day + N. Exact-day, not "within N days"
        //                 — the stricter and more standard reading.
        //   *_mature    — false when the cohort has not existed for N days yet.
        //                 An immature bucket is NOT 0% retention; it is unknown,
        //                 and the page must render it as such.
        if (view === 'retention') {
            const rows = await sql`
                WITH firsts AS (
                    SELECT player_id, MIN(received_at) AS first_seen
                    FROM analytics_events
                    WHERE player_id <> ${ANON_ID}
                    GROUP BY player_id
                ),
                cohort AS (
                    SELECT player_id, date_trunc('day', first_seen)::date AS cohort_day
                    FROM firsts
                    WHERE first_seen > NOW() - (${days} * INTERVAL '1 day')
                ),
                sessions_by_day AS (
                    SELECT DISTINCT e.player_id,
                           date_trunc('day', e.received_at)::date AS day
                    FROM analytics_events e
                    JOIN cohort c ON c.player_id = e.player_id
                    WHERE e.event_name = 'session_start'
                )
                SELECT c.cohort_day::text AS cohort_day,
                       COUNT(*)::bigint   AS cohort_size,
                       COUNT(*) FILTER (WHERE EXISTS (
                           SELECT 1 FROM sessions_by_day r
                           WHERE r.player_id = c.player_id AND r.day = c.cohort_day + 1))::bigint  AS d1,
                       COUNT(*) FILTER (WHERE EXISTS (
                           SELECT 1 FROM sessions_by_day r
                           WHERE r.player_id = c.player_id AND r.day = c.cohort_day + 7))::bigint  AS d7,
                       COUNT(*) FILTER (WHERE EXISTS (
                           SELECT 1 FROM sessions_by_day r
                           WHERE r.player_id = c.player_id AND r.day = c.cohort_day + 30))::bigint AS d30
                FROM cohort c
                GROUP BY 1
                ORDER BY 1 DESC
                LIMIT 181`;

            const today = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate()));
            const cohorts = rows.map(r => {
                const size = Number(r.cohort_size || 0);
                const day = new Date(r.cohort_day + 'T00:00:00Z');
                const ageDays = Math.floor((today - day) / 86400000);
                return {
                    cohort_day: r.cohort_day,
                    cohort_size: size,
                    low_n: size < LOW_N_THRESHOLD,
                    d1_players: Number(r.d1 || 0), d1_pct: pct(r.d1, size), d1_mature: ageDays >= 1,
                    d7_players: Number(r.d7 || 0), d7_pct: pct(r.d7, size), d7_mature: ageDays >= 7,
                    d30_players: Number(r.d30 || 0), d30_pct: pct(r.d30, size), d30_mature: ageDays >= 30,
                };
            });

            // Pooled rollup over MATURE cohorts only — one honest number each,
            // instead of averaging percentages (which over-weights tiny cohorts).
            const roll = (key, matureKey) => {
                const eligible = cohorts.filter(c => c[matureKey]);
                const size = eligible.reduce((s, c) => s + c.cohort_size, 0);
                const ret = eligible.reduce((s, c) => s + c[key], 0);
                return { cohort_size: size, returned: ret, pct: pct(ret, size), low_n: size < LOW_N_THRESHOLD, cohorts: eligible.length };
            };

            return res.status(200).json(Object.assign(meta, {
                definition: 'Day-N retention: of players whose FIRST-EVER event landed on cohort_day, '
                    + 'the share that fired a session_start on EXACTLY cohort_day + N.',
                low_n_threshold: LOW_N_THRESHOLD,
                rollup: {
                    d1: roll('d1_players', 'd1_mature'),
                    d7: roll('d7_players', 'd7_mature'),
                    d30: roll('d30_players', 'd30_mature'),
                },
                cohorts: cohorts,
            }));
        }

        // ============================================================== funnel
        // THE TUTORIAL FUNNEL — the highest-value view on this page. There is a
        // live FTUE defect (WO-1036); this is what measures its cost in players.
        //
        // Per step, from the events TutorialFlow actually emits:
        //   enter    tutorial_step_enter    (properties: stepId, order, flowId)
        //   complete tutorial_step_complete (stepId, order, seconds)
        //   skip     tutorial_step_skip     (stepId, order, seconds) — player tapped skip
        //   drop     tutorial_step_drop     (stepId, secondsIdle, ...) — the WATCHDOG
        //            fired: the player sat stuck on this step until it auto-advanced.
        //            THIS IS THE DEFECT SIGNAL. It carries the step in properties,
        //            which is why it is read out per-step rather than as a total.
        // Ordering comes from properties->>'order' on the enter event, sorted
        // numerically in JS — never cast in SQL, because the value is client
        // JSONB and one malformed row would fail the whole query.
        if (view === 'funnel') {
            const flow = await sql`
                SELECT event_name,
                       COUNT(*)::bigint                  AS events,
                       COUNT(DISTINCT player_id) FILTER (WHERE player_id <> ${ANON_ID})::bigint AS players,
                       MAX(received_at)                  AS latest
                FROM analytics_events
                WHERE event_name IN ('tutorial_started', 'tutorial_completed', 'tutorial_skipped_all')
                  AND received_at > NOW() - (${days} * INTERVAL '1 day')
                GROUP BY 1
                LIMIT 10`;

            const enters = await sql`
                SELECT properties->>'stepId' AS step_id,
                       MIN(properties->>'order') AS step_order,
                       COUNT(*)::bigint AS events,
                       COUNT(DISTINCT player_id) FILTER (WHERE player_id <> ${ANON_ID})::bigint AS players,
                       MAX(received_at) AS latest
                FROM analytics_events
                WHERE event_name = 'tutorial_step_enter'
                  AND received_at > NOW() - (${days} * INTERVAL '1 day')
                GROUP BY 1
                ORDER BY 3 DESC
                LIMIT 200`;

            const completes = await sql`
                SELECT properties->>'stepId' AS step_id,
                       COUNT(*)::bigint AS events,
                       COUNT(DISTINCT player_id) FILTER (WHERE player_id <> ${ANON_ID})::bigint AS players
                FROM analytics_events
                WHERE event_name = 'tutorial_step_complete'
                  AND received_at > NOW() - (${days} * INTERVAL '1 day')
                GROUP BY 1
                LIMIT 200`;

            const skips = await sql`
                SELECT properties->>'stepId' AS step_id,
                       COUNT(*)::bigint AS events,
                       COUNT(DISTINCT player_id) FILTER (WHERE player_id <> ${ANON_ID})::bigint AS players
                FROM analytics_events
                WHERE event_name = 'tutorial_step_skip'
                  AND received_at > NOW() - (${days} * INTERVAL '1 day')
                GROUP BY 1
                LIMIT 200`;

            // The drop rows carry HOW LONG the player was stuck before the
            // watchdog rescued them — the difference between "a slow step" and
            // "a step nobody can get past".
            const drops = await sql`
                SELECT properties->>'stepId' AS step_id,
                       COUNT(*)::bigint AS events,
                       COUNT(DISTINCT player_id) FILTER (WHERE player_id <> ${ANON_ID})::bigint AS players,
                       MAX(properties->>'secondsIdle') AS max_seconds_idle_sample,
                       MAX(received_at) AS latest
                FROM analytics_events
                WHERE event_name = 'tutorial_step_drop'
                  AND received_at > NOW() - (${days} * INTERVAL '1 day')
                GROUP BY 1
                ORDER BY 2 DESC
                LIMIT 200`;

            const contextual = await sql`
                SELECT properties->>'stepId' AS step_id,
                       COUNT(*)::bigint AS events,
                       COUNT(DISTINCT player_id) FILTER (WHERE player_id <> ${ANON_ID})::bigint AS players
                FROM analytics_events
                WHERE event_name = 'contextual_step_enter'
                  AND received_at > NOW() - (${days} * INTERVAL '1 day')
                GROUP BY 1
                ORDER BY 2 DESC
                LIMIT 100`;

            const byId = (rows) => {
                const m = new Map();
                for (const r of rows) m.set(r.step_id == null ? '(no stepId)' : String(r.step_id), r);
                return m;
            };
            const mC = byId(completes), mS = byId(skips), mD = byId(drops);

            const steps = enters.map(e => {
                const id = e.step_id == null ? '(no stepId)' : String(e.step_id);
                const c = mC.get(id) || {}, s = mS.get(id) || {}, d = mD.get(id) || {};
                const players = Number(e.players || 0);
                return {
                    step_id: id,
                    order: num(e.step_order),
                    entered_players: players,
                    entered_events: Number(e.events || 0),
                    completed_players: Number(c.players || 0),
                    skipped_players: Number(s.players || 0),
                    dropped_players: Number(d.players || 0),
                    dropped_events: Number(d.events || 0),
                    max_seconds_idle_sample: num(d.max_seconds_idle_sample),
                    completion_pct: pct(c.players || 0, players),
                    drop_pct: pct(d.players || 0, players),
                    low_n: players < LOW_N_THRESHOLD,
                    latest: e.latest,
                };
            });
            // Numeric order where the client supplied one; unordered steps last,
            // by volume — never silently dropped.
            steps.sort((a, b) => {
                if (a.order == null && b.order == null) return b.entered_players - a.entered_players;
                if (a.order == null) return 1;
                if (b.order == null) return -1;
                return a.order - b.order;
            });
            // Drop-off BETWEEN consecutive steps: how many of the players who
            // entered step i never entered step i+1.
            for (let i = 0; i < steps.length; i++) {
                const next = steps[i + 1];
                steps[i].next_step_id = next ? next.step_id : null;
                steps[i].lost_to_next = next ? Math.max(0, steps[i].entered_players - next.entered_players) : null;
                steps[i].lost_to_next_pct = next ? pct(Math.max(0, steps[i].entered_players - next.entered_players), steps[i].entered_players) : null;
            }

            const flowMap = byId(flow.map(r => ({ step_id: r.event_name, ...r })));
            const started = Number((flowMap.get('tutorial_started') || {}).players || 0);
            const finished = Number((flowMap.get('tutorial_completed') || {}).players || 0);
            const skippedAll = Number((flowMap.get('tutorial_skipped_all') || {}).players || 0);

            return res.status(200).json(Object.assign(meta, {
                low_n_threshold: LOW_N_THRESHOLD,
                flow: {
                    started_players: started,
                    completed_players: finished,
                    skipped_all_players: skippedAll,
                    completion_pct: pct(finished, started),
                    skip_all_pct: pct(skippedAll, started),
                    low_n: started < LOW_N_THRESHOLD,
                    raw: flow,
                },
                steps: steps,
                contextual_hints: contextual,
                note: 'tutorial_step_drop = the TutorialFlow watchdog auto-advanced a stuck player. '
                    + 'A step with drops is a step players cannot get past on their own.',
            }));
        }

        // ============================================================= economy
        // ⚠ CLIENT-REPORTED INTENT — NOT SETTLEMENT, NOT REVENUE.
        // WHAT THE GAME SAID SOLD, AND WHAT THE PROMO/REFERRAL RAILS ACTUALLY DID.
        //
        // Everything under `purchases` / `bundle_views` / `conversion` here comes
        // from analytics_events, i.e. from the CLIENT's own word. That makes this
        // a genuine intent funnel (opened the pack card → said it completed) and
        // makes it useless as a money record. For what actually settled on chain,
        // read ?view=purchases — which is a SEPARATE view on purpose: see the
        // "TWO PURCHASE VIEWS" note in the file header. Do not blend the two.
        //
        // ⚠ purchase_completed carries NO price field. PackStore.cs:582 emits
        // { packId, packName, currency, txSig } only — the `price` in
        // EventTracker's doc comment is an EXAMPLE, not a live field. So this
        // view reports COUNTS, never revenue. `price_sample`/`amount_sample`
        // are read opportunistically in case a future emitter adds one; when
        // they come back null that means "the client never sent an amount",
        // NOT "the sale was free". Revenue must come from the chain or from
        // tower_swaps.cost_usdc, not from here.
        if (view === 'economy') {
            const purchases = await sql`
                SELECT properties->>'packId'   AS pack_id,
                       properties->>'packName' AS pack_name,
                       properties->>'currency' AS currency,
                       COUNT(*)::bigint AS purchases,
                       COUNT(DISTINCT player_id) FILTER (WHERE player_id <> ${ANON_ID})::bigint AS buyers,
                       MAX(properties->>'price')  AS price_sample,
                       MAX(properties->>'amount') AS amount_sample,
                       MAX(received_at) AS latest
                FROM analytics_events
                WHERE event_name = 'purchase_completed'
                  AND received_at > NOW() - (${days} * INTERVAL '1 day')
                GROUP BY 1, 2, 3
                ORDER BY 4 DESC
                LIMIT 100`;

            const views = await sql`
                SELECT properties->>'bundleId'   AS bundle_id,
                       properties->>'bundleName' AS bundle_name,
                       COUNT(*)::bigint AS views,
                       COUNT(DISTINCT player_id) FILTER (WHERE player_id <> ${ANON_ID})::bigint AS viewers,
                       MAX(received_at) AS latest
                FROM analytics_events
                WHERE event_name = 'bundle_viewed'
                  AND received_at > NOW() - (${days} * INTERVAL '1 day')
                GROUP BY 1, 2
                ORDER BY 3 DESC
                LIMIT 100`;

            // DB truth for promos (not analytics): what was redeemed, of which
            // code, and whether that code is wallet-bound. bound_wallet is
            // reported as a BOOLEAN — the bound address is another player's
            // wallet and does not belong on a dashboard.
            const promos = await sql`
                SELECT c.code,
                       c.active,
                       c.reward_crystals,
                       c.reward_coins,
                       c.max_redemptions,
                       c.expires_at,
                       (c.bound_wallet IS NOT NULL AND c.bound_wallet <> '') AS is_bound,
                       COUNT(r.redemption_id)::bigint AS redemptions,
                       COALESCE(SUM(r.crystals), 0)::bigint AS crystals_granted,
                       COALESCE(SUM(r.coins), 0)::bigint    AS coins_granted,
                       MAX(r.redeemed_at) AS latest_redemption
                FROM promo_codes c
                LEFT JOIN promo_redemptions r ON r.code = c.code
                GROUP BY c.code, c.active, c.reward_crystals, c.reward_coins,
                         c.max_redemptions, c.expires_at, c.bound_wallet
                ORDER BY 11 DESC NULLS LAST, c.created_at DESC
                LIMIT 100`;

            const referrals = await sql`
                SELECT (SELECT COUNT(*) FROM referrals)::bigint       AS codes_minted,
                       (SELECT COUNT(*) FROM referral_claims)::bigint AS claims,
                       (SELECT COUNT(DISTINCT referrer_id) FROM referral_claims)::bigint AS referrers_with_a_claim,
                       (SELECT COALESCE(SUM(crystals), 0) FROM referral_claims)::bigint  AS claim_crystals_granted
                LIMIT 1`;

            // The client-side view of the same rails — divergence between these
            // and the tables above is itself a finding (an event with no row =
            // the write never landed).
            const clientSide = await sql`
                SELECT event_name,
                       COUNT(*)::bigint AS events,
                       COUNT(DISTINCT player_id) FILTER (WHERE player_id <> ${ANON_ID})::bigint AS players,
                       MAX(received_at) AS latest
                FROM analytics_events
                WHERE event_name IN ('promo_redeemed', 'referral_code_generated',
                                     'referral_shared', 'referral_claimed',
                                     'purchase_completed', 'bundle_viewed')
                  AND received_at > NOW() - (${days} * INTERVAL '1 day')
                GROUP BY 1
                ORDER BY 2 DESC
                LIMIT 20`;

            // View → buy, per pack. bundle_viewed's bundleId and
            // purchase_completed's packId are BOTH pack.Sku (PackStore.cs), so
            // they join cleanly.
            const buyersByPack = new Map();
            for (const p of purchases) {
                const k = p.pack_id == null ? '(unknown)' : String(p.pack_id);
                buyersByPack.set(k, (buyersByPack.get(k) || 0) + Number(p.buyers || 0));
            }
            const conversion = views.map(v => {
                const k = v.bundle_id == null ? '(unknown)' : String(v.bundle_id);
                const viewers = Number(v.viewers || 0);
                const buyers = buyersByPack.get(k) || 0;
                return {
                    pack_id: k,
                    pack_name: v.bundle_name,
                    viewers: viewers,
                    buyers: buyers,
                    conversion_pct: pct(buyers, viewers),
                    low_n: viewers < LOW_N_THRESHOLD,
                };
            }).sort((a, b) => b.viewers - a.viewers);

            return res.status(200).json(Object.assign(meta, {
                low_n_threshold: LOW_N_THRESHOLD,
                // Kept as `purchases` because site/admin.html reads that key; the
                // LABEL is what changes, and it changes to the truth.
                source: 'analytics_events — CLIENT-REPORTED INTENT, not settlement.',
                client_reported: true,
                see_also: '?view=purchases — server-verified settlement and revenue from '
                    + 'purchase_entitlements. The two are never blended; where they disagree, that '
                    + 'disagreement is itself the signal.',
                revenue_note: 'CLIENT-REPORTED, NOT REVENUE. purchase_completed carries no amount '
                    + '(PackStore emits packId/packName/currency/txSig only), so these are COUNTS of '
                    + 'what the client said it did. A null price_sample means the client never sent '
                    + 'one — never that the sale was free. Real revenue lives in ?view=purchases.',
                purchases: purchases,
                bundle_views: views,
                conversion: conversion,
                promo_codes: promos,
                referrals: referrals[0] || null,
                client_events: clientSide,
            }));
        }

        // =========================================================== purchases
        // SERVER TRUTH. Sourced from purchase_entitlements + purchase_quotes —
        // the rows api/purchases/{quote,verify,fulfill} write — and NEVER from
        // analytics_events.
        //
        // ⛔ WHY THIS IS A SECOND VIEW RATHER THAN A FIX TO ?view=economy:
        // the two answer different questions and MUST NOT be blended.
        //   ?view=economy    — what the CLIENT said happened (purchase_completed).
        //   ?view=purchases  — what the SERVER independently verified on chain.
        // A single merged figure would average away the disagreement, and the
        // DISAGREEMENT IS THE ALERT: a client purchase_completed whose txSig has
        // no entitlement row means a grant may have gone out with no settlement
        // recorded behind it. It is surfaced below, never reconciled — because
        // reconciling is a WRITE and no write lives in this file.
        //
        // ── REVENUE ─────────────────────────────────────────────────────────
        // usd_anchor is the AUTHORED LADDER PRICE (2.99, 4.99 …) persisted onto
        // the row at verify time, so it is a stable historical figure and not a
        // re-derivation against today's market. It is the only honest revenue
        // number the database holds; analytics cannot produce one at all.
        // ⚠ It is NULL on the two CANARY skus (schema.sql — pinned protocol
        // constants with no rate behind them), so every total ships next to
        // `rows_without_usd_anchor`: a non-zero count means the total UNDERSTATES
        // the row count, NOT that those sales were free.
        // ⚠ observed_lamports is a BASE-UNIT integer whose decimals live on the
        // QUOTE (6 on mainnet SKR, 9 on devnet), not on the entitlement. It is
        // therefore reported raw, per-currency, and never summed into a
        // human-readable token amount here.
        //
        // ── THE QUOTE FUNNEL is the health signal ───────────────────────────
        // A quote is issued when a player opens the wallet prompt and consumed
        // when their payment verifies; TTL is 5 minutes. A fall in
        // consumed/issued means people are TRYING TO BUY AND FAILING, which no
        // other metric on this dashboard can see.
        //
        // Wallets are masked exactly as everywhere else. tx_signature is NOT
        // masked: it is already a public chain record, and it is the precise
        // string the operator needs to answer "did that actually land".
        //
        // Each table is probed INDEPENDENTLY. Schema drift is real here (three
        // of these tables were invisible to the admin surface until 2026-08-24,
        // and a stale CHECK constraint silently failed a settled mainnet sale),
        // so a missing or altered table degrades to an entry in `errors` rather
        // than 500-ing the whole view.
        if (view === 'purchases') {
            const errors = [];
            const probe = async (label, run) => {
                try {
                    return await run();
                } catch (err) {
                    console.error('[admin/stats] purchases probe failed:', label, err);
                    errors.push({ probe: label, error: String((err && err.message) || err) });
                    return null;
                }
            };

            // ---- settled totals (purchase_entitlements) ---------------------
            const totals = await probe('entitlements_totals', () => sql`
                SELECT COUNT(*)::bigint                                   AS settled_all_time,
                       COUNT(DISTINCT wallet)::bigint                     AS buyers_all_time,
                       COALESCE(SUM(usd_anchor), 0)::float8               AS usd_all_time,
                       COUNT(*) FILTER (WHERE usd_anchor IS NULL)::bigint AS rows_without_usd_anchor,
                       COUNT(*) FILTER (WHERE created_at > NOW() - (${days} * INTERVAL '1 day'))::bigint AS settled_window,
                       COUNT(DISTINCT wallet) FILTER (WHERE created_at > NOW() - (${days} * INTERVAL '1 day'))::bigint AS buyers_window,
                       COALESCE(SUM(usd_anchor) FILTER (WHERE created_at > NOW() - (${days} * INTERVAL '1 day')), 0)::float8 AS usd_window,
                       MIN(created_at) AS first_settled_at,
                       MAX(created_at) AS last_settled_at
                FROM purchase_entitlements
                LIMIT 1`);

            // ---- counts by settlement status --------------------------------
            // verified      = chain-confirmed, grant not yet handed over
            // fulfilled     = grant delivered
            // manual_review = verified but something did not match (e.g. the
            //                 payment landed outside the quote window) — the
            //                 money moved and a human has to look.
            const byStatus = await probe('entitlements_by_status', () => sql`
                SELECT status,
                       COUNT(*)::bigint                     AS rows,
                       COUNT(DISTINCT wallet)::bigint       AS wallets,
                       COALESCE(SUM(usd_anchor), 0)::float8 AS usd_anchor_total,
                       MAX(created_at)                      AS latest
                FROM purchase_entitlements
                GROUP BY 1
                ORDER BY 2 DESC
                LIMIT 10`);

            // ---- per-SKU breakdown ------------------------------------------
            const bySku = await probe('entitlements_by_sku', () => sql`
                SELECT sku,
                       currency,
                       network,
                       COUNT(*)::bigint                                        AS settled,
                       COUNT(DISTINCT wallet)::bigint                          AS buyers,
                       COUNT(*) FILTER (WHERE status = 'fulfilled')::bigint     AS fulfilled,
                       COUNT(*) FILTER (WHERE status = 'verified')::bigint      AS awaiting_fulfilment,
                       COUNT(*) FILTER (WHERE status = 'manual_review')::bigint AS manual_review,
                       COALESCE(SUM(usd_anchor), 0)::float8                     AS usd_anchor_total,
                       COUNT(*) FILTER (WHERE usd_anchor IS NULL)::bigint       AS rows_without_usd_anchor,
                       COALESCE(SUM(observed_lamports), 0)::float8              AS base_units_observed,
                       MIN(created_at)                                          AS first_settled_at,
                       MAX(created_at)                                          AS last_settled_at
                FROM purchase_entitlements
                GROUP BY 1, 2, 3
                ORDER BY 4 DESC
                LIMIT 100`);

            // ---- revenue per day, over the window ---------------------------
            const perDay = await probe('entitlements_per_day', () => sql`
                SELECT date_trunc('day', created_at)::date::text AS day,
                       COUNT(*)::bigint                          AS settled,
                       COUNT(DISTINCT wallet)::bigint            AS buyers,
                       COALESCE(SUM(usd_anchor), 0)::float8      AS usd_anchor_total
                FROM purchase_entitlements
                WHERE created_at > NOW() - (${days} * INTERVAL '1 day')
                GROUP BY 1
                ORDER BY 1 DESC
                LIMIT 181`);

            // ---- the rows that need a human ---------------------------------
            // Anything not yet 'fulfilled': the chain confirmed the money and the
            // grant is not recorded as delivered. This is the "verify they
            // received it" list.
            const unfulfilled = await probe('entitlements_unfulfilled', () => sql`
                SELECT entitlement_id, tx_signature, wallet, sku, currency, network,
                       status, usd_anchor, quote_ref, chain_slot,
                       verified_at, fulfilled_at, created_at
                FROM purchase_entitlements
                WHERE status <> 'fulfilled'
                ORDER BY created_at DESC
                LIMIT 100`);

            // ---- most recent settlements ------------------------------------
            const recent = await probe('entitlements_recent', () => sql`
                SELECT entitlement_id, tx_signature, wallet, sku, currency, network,
                       status, expected_lamports, observed_lamports, chain_slot,
                       usd_anchor, usd_rate, rate_source, quote_ref,
                       verified_at, fulfilled_at, created_at
                FROM purchase_entitlements
                ORDER BY created_at DESC
                LIMIT 50`);

            // ---- ⭐ the quote → settle funnel (purchase_quotes) --------------
            const funnel = await probe('quotes_funnel', () => sql`
                SELECT COUNT(*)::bigint                                                            AS issued,
                       COUNT(*) FILTER (WHERE consumed_at IS NOT NULL)::bigint                     AS consumed,
                       COUNT(*) FILTER (WHERE consumed_at IS NULL AND expires_at <= NOW())::bigint AS expired_unconsumed,
                       COUNT(*) FILTER (WHERE consumed_at IS NULL AND expires_at >  NOW())::bigint AS live,
                       COUNT(DISTINCT wallet)::bigint                                              AS wallets_quoted,
                       COUNT(DISTINCT wallet) FILTER (WHERE consumed_at IS NOT NULL)::bigint        AS wallets_that_paid,
                       MAX(issued_at)                                                              AS last_issued_at,
                       MAX(consumed_at)                                                            AS last_consumed_at
                FROM purchase_quotes
                WHERE issued_at > NOW() - (${days} * INTERVAL '1 day')
                LIMIT 1`);

            const funnelBySku = await probe('quotes_funnel_by_sku', () => sql`
                SELECT sku,
                       network,
                       COUNT(*)::bigint                                                            AS issued,
                       COUNT(*) FILTER (WHERE consumed_at IS NOT NULL)::bigint                     AS consumed,
                       COUNT(*) FILTER (WHERE consumed_at IS NULL AND expires_at <= NOW())::bigint AS expired_unconsumed,
                       COUNT(*) FILTER (WHERE consumed_at IS NULL AND expires_at >  NOW())::bigint AS live,
                       MAX(issued_at)                                                              AS last_issued_at
                FROM purchase_quotes
                WHERE issued_at > NOW() - (${days} * INTERVAL '1 day')
                GROUP BY 1, 2
                ORDER BY 3 DESC
                LIMIT 100`);

            const funnelPerDay = await probe('quotes_funnel_per_day', () => sql`
                SELECT date_trunc('day', issued_at)::date::text                                    AS day,
                       COUNT(*)::bigint                                                            AS issued,
                       COUNT(*) FILTER (WHERE consumed_at IS NOT NULL)::bigint                     AS consumed,
                       COUNT(*) FILTER (WHERE consumed_at IS NULL AND expires_at <= NOW())::bigint AS expired_unconsumed
                FROM purchase_quotes
                WHERE issued_at > NOW() - (${days} * INTERVAL '1 day')
                GROUP BY 1
                ORDER BY 1 DESC
                LIMIT 181`);

            // ---- ⛔ client-vs-server disagreement ----------------------------
            // Reported as COUNTS ON BOTH SIDES plus the orphan list. Never as one
            // blended number.
            const clientSide = await probe('client_reported_counts', () => sql`
                SELECT COUNT(*)::bigint                                                            AS completed_events,
                       COUNT(DISTINCT properties->>'txSig')::bigint                                AS distinct_tx_signatures,
                       COUNT(DISTINCT player_id) FILTER (WHERE player_id <> ${ANON_ID})::bigint     AS players,
                       MAX(received_at)                                                            AS latest
                FROM analytics_events
                WHERE event_name = 'purchase_completed'
                  AND received_at > NOW() - (${days} * INTERVAL '1 day')
                LIMIT 1`);

            // THE ALERT: the client announced a completed purchase and the server
            // holds NO verified entitlement for that signature.
            const clientOrphans = await probe('client_events_without_entitlement', () => sql`
                SELECT e.properties->>'txSig'  AS tx_signature,
                       e.properties->>'packId' AS pack_id,
                       e.player_id             AS player_id,
                       COUNT(*)::bigint        AS events,
                       MAX(e.received_at)      AS latest
                FROM analytics_events e
                WHERE e.event_name = 'purchase_completed'
                  AND e.received_at > NOW() - (${days} * INTERVAL '1 day')
                  AND NOT EXISTS (
                      SELECT 1 FROM purchase_entitlements p
                      WHERE p.tx_signature = e.properties->>'txSig')
                GROUP BY 1, 2, 3
                ORDER BY 5 DESC
                LIMIT 100`);

            // The mirror case. Far less alarming — the client can simply have
            // failed to report, or the sale came in through /purchases/reconcile
            // — but a large number here means the analytics funnel UNDERSTATES
            // sales, which is worth knowing before anyone reads ?view=economy as
            // if it were revenue.
            const serverOrphans = await probe('entitlements_without_client_event', () => sql`
                SELECT COUNT(*)::bigint AS settled_without_client_event
                FROM purchase_entitlements p
                WHERE p.created_at > NOW() - (${days} * INTERVAL '1 day')
                  AND NOT EXISTS (
                      SELECT 1 FROM analytics_events e
                      WHERE e.event_name = 'purchase_completed'
                        AND e.properties->>'txSig' = p.tx_signature)
                LIMIT 1`);

            const t = (totals && totals[0]) || {};
            const f = (funnel && funnel[0]) || {};
            const c = (clientSide && clientSide[0]) || {};

            const issued = Number(f.issued || 0);
            const consumed = Number(f.consumed || 0);

            // Rows carry a real wallet address; mask it the way every other view
            // here does. tx_signature stays whole on purpose (see header).
            const maskRow = (r) => {
                const out = Object.assign({}, r);
                delete out.wallet;
                out.wallet_masked = maskId(r.wallet);
                return out;
            };

            return res.status(200).json(Object.assign(meta, {
                source: 'purchase_entitlements + purchase_quotes — SERVER-VERIFIED settlement, not '
                    + 'client-reported. ?view=economy reports the client side; the two are '
                    + 'deliberately never blended.',
                low_n_threshold: LOW_N_THRESHOLD,
                revenue_note: 'Revenue is SUM(usd_anchor) — the authored ladder price persisted onto '
                    + 'the row at verify time. usd_anchor is NULL on the CANARY skus (pinned protocol '
                    + 'constants with no rate behind them), so rows_without_usd_anchor > 0 means the '
                    + 'total understates the row count, NOT that those sales were free. '
                    + 'base_units_observed is a raw integer whose decimals live on the quote (6 mainnet, '
                    + '9 devnet) and is deliberately not converted to a token amount here.',
                settled: {
                    all_time: Number(t.settled_all_time || 0),
                    all_time_buyers: Number(t.buyers_all_time || 0),
                    all_time_usd_anchor: Number(t.usd_all_time || 0),
                    window: Number(t.settled_window || 0),
                    window_buyers: Number(t.buyers_window || 0),
                    window_usd_anchor: Number(t.usd_window || 0),
                    rows_without_usd_anchor: Number(t.rows_without_usd_anchor || 0),
                    first_settled_at: t.first_settled_at || null,
                    last_settled_at: t.last_settled_at || null,
                },
                by_status: byStatus || [],
                by_sku: bySku || [],
                per_day: perDay || [],
                quote_funnel: {
                    definition: 'A quote is issued when the wallet prompt opens and consumed when the '
                        + 'payment verifies (5-minute TTL). consumed/issued falling is players TRYING '
                        + 'TO BUY AND FAILING — the earliest warning this rail has.',
                    issued: issued,
                    consumed: consumed,
                    expired_unconsumed: Number(f.expired_unconsumed || 0),
                    live: Number(f.live || 0),
                    wallets_quoted: Number(f.wallets_quoted || 0),
                    wallets_that_paid: Number(f.wallets_that_paid || 0),
                    consumed_pct: pct(consumed, issued),
                    low_n: issued < LOW_N_THRESHOLD,
                    last_issued_at: f.last_issued_at || null,
                    last_consumed_at: f.last_consumed_at || null,
                    by_sku: (funnelBySku || []).map(r => Object.assign({}, r, {
                        consumed_pct: pct(r.consumed, r.issued),
                        low_n: Number(r.issued || 0) < LOW_N_THRESHOLD,
                    })),
                    per_day: (funnelPerDay || []).map(r => Object.assign({}, r, {
                        consumed_pct: pct(r.consumed, r.issued),
                        low_n: Number(r.issued || 0) < LOW_N_THRESHOLD,
                    })),
                },
                needs_attention: {
                    note: 'status <> fulfilled: the chain confirmed the money and the grant is not '
                        + 'recorded as delivered. Re-granting is a WRITE and does not live on this '
                        + 'endpoint — this view only tells you which rows to act on.',
                    rows: (unfulfilled || []).map(maskRow),
                },
                recent_settlements: (recent || []).map(maskRow),
                disagreement: {
                    note: 'Client-reported vs server-settled, side by side and NEVER merged. A client '
                        + 'purchase_completed with no entitlement row for its txSig means a grant may '
                        + 'have been handed out with no verified settlement behind it. Surfaced, not '
                        + 'reconciled — reconciling is a write.',
                    client_completed_events: Number(c.completed_events || 0),
                    client_distinct_tx_signatures: Number(c.distinct_tx_signatures || 0),
                    client_players: Number(c.players || 0),
                    client_latest: c.latest || null,
                    server_settled_window: Number(t.settled_window || 0),
                    client_events_without_entitlement: (clientOrphans || []).map(r => ({
                        tx_signature: r.tx_signature,
                        pack_id: r.pack_id,
                        player_masked: maskId(r.player_id),
                        events: Number(r.events || 0),
                        latest: r.latest,
                    })),
                    settled_without_client_event: Number(
                        ((serverOrphans && serverOrphans[0]) || {}).settled_without_client_event || 0),
                },
                errors: errors,
            }));
        }

        // ================================================================= ops
        // WO-1244 - the READ half of the Command Center console's operations
        // pillars: the six kill-switch toggles (WO-1243), the promo catalog with
        // its redemption counts, and the player-issue queue.
        //
        // ⛔ IT IS A READ. Every statement below is a SELECT with a hard LIMIT,
        // like every other statement in this file. Flipping a toggle and
        // authoring a promo are WRITES and they live at api/admin/ops.js, behind
        // a SECOND key. WO-1169 and WO-1244 both put that boundary at the
        // ENDPOINT, not in the UI, and adding one INSERT here would erase it.
        //
        // ⛔ NO WALLET LEAVES THIS BLOCK. promo_codes.bound_wallet is reported as
        // the BOOLEAN `is_bound` and the column itself is never selected - the
        // console must be able to say "this code is private" without ever putting
        // an address on a screen that gets screenshotted. bug_reports carries a
        // wallet too (WO-1169, server-verified); it is likewise reduced to
        // `wallet_verified`, because a BURST of unverified reports is the triage
        // signal, and WHOSE wallet it is never was.
        if (view === 'ops') {
            const errors = [];
            const probe = async (label, run) => {
                try {
                    return await run();
                } catch (err) {
                    console.error('[admin/stats] ops probe failed:', label, err);
                    errors.push({ probe: label, error: String((err && err.message) || err) });
                    return null;
                }
            };

            // ---- the six kill switches --------------------------------------
            // updated_by / updated_at are the whole point: WO-1244 pillar 5 asks
            // for "current state, and WHEN each was last flipped". The public
            // /api/maintenance endpoint deliberately does not carry them.
            const toggles = await probe('maintenance_toggles', () => sql`
                SELECT area_id, closed, message, updated_by, updated_at
                FROM maintenance_toggles
                ORDER BY area_id
                LIMIT 20`);

            // ---- the promo catalog ------------------------------------------
            const promos = await probe('promo_codes', () => sql`
                SELECT code, reward_crystals, reward_coins, reward_pack_sku, message,
                       active, max_redemptions, per_player_limit, expires_at, created_at,
                       (bound_wallet IS NOT NULL) AS is_bound
                FROM promo_codes
                ORDER BY created_at DESC
                LIMIT 100`);

            const redemptions = await probe('promo_redemptions', () => sql`
                SELECT code,
                       COUNT(*)::bigint               AS redemptions,
                       COUNT(DISTINCT player_id)::bigint AS players,
                       MAX(redeemed_at)               AS latest
                FROM promo_redemptions
                GROUP BY 1
                ORDER BY 2 DESC
                LIMIT 200`);

            // ---- the player-issue queue -------------------------------------
            // ⚠ DIFFERENT FROM BOARD.html AND DELIBERATELY NOT MERGED WITH IT.
            // BOARD.html is DEV work, generated from WorkOrders/*.md - anything
            // written there is overwritten on the next tools/board_build.py run
            // (WO-1169 section 4). This is the PLAYER queue. Two boards, linked,
            // never folded together.
            const reports = await probe('bug_reports', () => sql`
                SELECT report_id, created_at, description, route, app_version, player_id,
                       (COALESCE(wallet, context->>'verifiedWallet') IS NOT NULL) AS wallet_verified,
                       context->>'platform' AS platform,
                       (context ? 'screenshotB64' AND context->>'screenshotB64' IS NOT NULL) AS has_screenshot
                FROM bug_reports
                ORDER BY report_id DESC
                LIMIT 50`);

            const reportsPerDay = await probe('bug_reports_per_day', () => sql`
                SELECT date_trunc('day', created_at)::date::text AS day,
                       COUNT(*)::bigint AS reports
                FROM bug_reports
                WHERE created_at > NOW() - (${days} * INTERVAL '1 day')
                GROUP BY 1
                ORDER BY 1 DESC
                LIMIT 181`);

            // ---- the ops write history --------------------------------------
            // Every write api/admin/ops.js performs leaves one row here. Reading
            // it back is how "who sealed raiding, and when" is answered after the
            // fact from the console rather than from a psql prompt.
            const opsHistory = await probe('admin_ops_write_history', () => sql`
                SELECT received_at,
                       properties->>'action'   AS action,
                       properties->>'operator' AS operator,
                       properties->>'target'   AS target,
                       properties->>'outcome'  AS outcome
                FROM analytics_events
                WHERE event_name = 'admin_ops_write'
                ORDER BY received_at DESC
                LIMIT 50`);

            const redemptionByCode = {};
            for (const r of (redemptions || [])) {
                redemptionByCode[r.code] = {
                    redemptions: Number(r.redemptions || 0),
                    players: Number(r.players || 0),
                    latest: r.latest,
                };
            }

            // A row missing from maintenance_toggles is NOT an error and NOT a
            // seal: under the WO-1243 fail-open ruling an absent row means the
            // area is OPEN. Reported as such, in words, rather than as a gap the
            // console has to interpret.
            const toggleById = {};
            for (const r of (toggles || [])) toggleById[String(r.area_id)] = r;
            const areaRows = MAINTENANCE_AREAS.map((id) => {
                const r = toggleById[id];
                const closed = !!(r && r.closed === true);
                return {
                    area: id,
                    closed: closed,
                    state: closed ? 'CLOSED' : 'open',
                    message: (r && r.message) || null,
                    updated_by: (r && r.updated_by) || null,
                    updated_at: (r && r.updated_at) || null,
                    row_present: !!r,
                    note: r ? null : 'no row - fail-open means this area is OPEN',
                };
            });
            const serverClosed = areaRows.some(a => a.area === 'server' && a.closed);

            return res.status(200).json(Object.assign(meta, {
                source: 'maintenance_toggles + promo_codes + promo_redemptions + bug_reports. '
                    + 'READ ONLY. Writes live at POST /api/admin/ops behind a second key.',
                toggles: {
                    note: 'State is a WORD ("CLOSED" / "open"), never a colour. When `server` is '
                        + 'CLOSED every area is closed whatever its own row says.',
                    read_ok: toggles !== null,
                    server_closed: serverClosed,
                    sealed_count: areaRows.filter(a => a.closed).length,
                    areas: areaRows,
                },
                promos: {
                    note: 'bound_wallet is reported as is_bound only - the address itself is never '
                        + 'selected, so no screenshot of this console can carry one.',
                    rows: (promos || []).map(p => {
                        const used = redemptionByCode[p.code] || { redemptions: 0, players: 0, latest: null };
                        const expired = p.expires_at ? (new Date(p.expires_at).getTime() <= Date.now()) : false;
                        const capped = p.max_redemptions != null && used.redemptions >= Number(p.max_redemptions);
                        return {
                            code: p.code,
                            state: !p.active ? 'DISABLED' : expired ? 'EXPIRED' : capped ? 'FULLY REDEEMED' : 'ACTIVE',
                            active: p.active === true,
                            expired: expired,
                            capped: capped,
                            is_bound: p.is_bound === true,
                            reward_pack_sku: p.reward_pack_sku,
                            reward_crystals: Number(p.reward_crystals || 0),
                            reward_coins: Number(p.reward_coins || 0),
                            message: p.message,
                            max_redemptions: p.max_redemptions == null ? null : Number(p.max_redemptions),
                            per_player_limit: p.per_player_limit == null ? null : Number(p.per_player_limit),
                            expires_at: p.expires_at,
                            created_at: p.created_at,
                            redemptions: used.redemptions,
                            redeemed_by_players: used.players,
                            last_redeemed_at: used.latest,
                        };
                    }),
                },
                reports: {
                    note: 'The PLAYER issue queue. BOARD.html is the DEV board and is generated '
                        + 'from WorkOrders/*.md - the two are linked, never merged.',
                    per_day: reportsPerDay || [],
                    rows: (reports || []).map(r => ({
                        report_id: r.report_id == null ? null : Number(r.report_id),
                        created_at: r.created_at,
                        description: r.description,
                        route: r.route,
                        app_version: r.app_version,
                        platform: r.platform,
                        player_masked: maskId(r.player_id),
                        identity: r.wallet_verified ? 'verified' : 'unverified',
                        has_screenshot: r.has_screenshot === true,
                    })),
                },
                ops_history: (opsHistory || []).map(r => ({
                    at: r.received_at,
                    action: r.action,
                    operator: r.operator,
                    target: r.target,
                    outcome: r.outcome,
                })),
                errors: errors,
            }));
        }

        // ============================================================= players
        // Recent players. LIST = masked only. SINGLE = full id (see header).
        if (view === 'players') {
            // ---- single-player drill-down -----------------------------------
            // Reached either by full id (?player=) or by the opaque handle the
            // list emits (?ref=). This is the ONLY place a full wallet address
            // is returned, and only for one explicitly-requested player: the
            // operator needs the real address to bind a promo code to them
            // (promo_codes.bound_wallet) or to answer a support ticket. A bulk
            // list has no such need, so it never gets one.
            if (q.player || q.ref) {
                let playerId = q.player ? String(q.player) : null;

                if (!playerId) {
                    // Resolve the 12-hex handle inside a BOUNDED recent-players
                    // set — never a hash scan over the whole event table.
                    const ref = String(q.ref).toLowerCase().replace(/[^0-9a-f]/g, '').slice(0, 12);
                    if (ref.length !== 12) {
                        return res.status(400).json({ error: 'ref must be 12 hex chars' });
                    }
                    const found = await sql`
                        WITH recent AS (
                            SELECT player_id, MAX(received_at) AS last_seen
                            FROM analytics_events
                            WHERE player_id <> ${ANON_ID}
                            GROUP BY player_id
                            ORDER BY 2 DESC
                            LIMIT 500
                        )
                        SELECT player_id
                        FROM recent
                        WHERE substr(encode(sha256(convert_to(player_id, 'UTF8')), 'hex'), 1, 12) = ${ref}
                        LIMIT 1`;
                    if (found.length === 0) {
                        return res.status(200).json(Object.assign(meta, { player: null, found: false,
                            note: 'No match among the 500 most recently active players.' }));
                    }
                    playerId = found[0].player_id;
                }

                const summary = await sql`
                    SELECT MIN(received_at) AS first_seen,
                           MAX(received_at) AS last_seen,
                           COUNT(*)::bigint AS events,
                           COUNT(*) FILTER (WHERE event_name = 'session_start')::bigint AS sessions,
                           COUNT(DISTINCT date_trunc('day', received_at))::bigint       AS active_days
                    FROM analytics_events
                    WHERE player_id = ${playerId}
                    LIMIT 1`;

                const byEvent = await sql`
                    SELECT event_name, COUNT(*)::bigint AS events, MAX(received_at) AS latest
                    FROM analytics_events
                    WHERE player_id = ${playerId}
                    GROUP BY 1
                    ORDER BY 2 DESC
                    LIMIT 60`;

                // Names + times only. `properties` is client-authored free-form
                // JSONB and is never echoed here.
                const recent = await sql`
                    SELECT event_id, event_name, received_at
                    FROM analytics_events
                    WHERE player_id = ${playerId}
                    ORDER BY received_at DESC
                    LIMIT 50`;

                const save = await sql`
                    SELECT schema_version, trust, created_at, updated_at,
                           pg_column_size(game_state) AS payload_bytes
                    FROM player_data
                    WHERE player_id = ${playerId}
                    LIMIT 1`;

                return res.status(200).json(Object.assign(meta, {
                    found: true,
                    player: playerId,            // FULL id — the documented single exception
                    player_masked: maskId(playerId),
                    summary: summary[0] || null,
                    save: save[0] || null,
                    events_by_name: byEvent,
                    recent_events: recent,
                }));
            }

            // ---- list -------------------------------------------------------
            const limit = clampLimit(q.limit, 50, 200);
            const rows = await sql`
                SELECT player_id,
                       substr(encode(sha256(convert_to(player_id, 'UTF8')), 'hex'), 1, 12) AS player_ref,
                       MIN(received_at) AS first_seen,
                       MAX(received_at) AS last_seen,
                       COUNT(*)::bigint AS events,
                       COUNT(*) FILTER (WHERE event_name = 'session_start')::bigint AS sessions,
                       COUNT(DISTINCT date_trunc('day', received_at))::bigint       AS active_days
                FROM analytics_events
                WHERE player_id <> ${ANON_ID}
                GROUP BY player_id
                ORDER BY 4 DESC
                LIMIT ${limit}`;

            return res.status(200).json(Object.assign(meta, {
                limit: limit,
                note: 'Ids are masked. Use player_ref with ?view=players&ref=<handle> to open one player.',
                rows: rows.map(r => ({
                    player_masked: maskId(r.player_id),
                    player_ref: r.player_ref,
                    is_guest: String(r.player_id).startsWith('guest-local-'),
                    first_seen: r.first_seen,
                    last_seen: r.last_seen,
                    events: Number(r.events || 0),
                    sessions: Number(r.sessions || 0),
                    active_days: Number(r.active_days || 0),
                })),
            }));
        }

        return res.status(400).json({
            error: 'Unknown view. Use: overview | retention | funnel | economy | purchases | ops | players',
        });
    } catch (err) {
        console.error('[admin/stats] error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }
};
