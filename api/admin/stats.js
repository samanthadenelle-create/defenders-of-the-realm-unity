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
//   GET /api/admin/stats?view=players[&limit=N][&player=<id>|&ref=<12hex>]
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
        // WHAT SELLS, AND WHAT THE PROMO/REFERRAL RAILS ACTUALLY DID.
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
                revenue_note: 'purchase_completed carries no amount (PackStore emits packId/packName/'
                    + 'currency/txSig only), so these are COUNTS, not revenue. A null price_sample '
                    + 'means the client never sent one.',
                purchases: purchases,
                bundle_views: views,
                conversion: conversion,
                promo_codes: promos,
                referrals: referrals[0] || null,
                client_events: clientSide,
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
            error: 'Unknown view. Use: overview | retention | funnel | economy | players',
        });
    } catch (err) {
        console.error('[admin/stats] error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }
};
