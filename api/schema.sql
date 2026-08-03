-- =============================================================================
-- schema.sql — COMPLETE Neon Postgres schema for Defenders of the Realm v2
-- -----------------------------------------------------------------------------
-- Run once in the Neon console: Dashboard → SQL Editor → paste & run.
-- Idempotent: every object uses IF NOT EXISTS, so re-running is safe.
--
-- This file is the single source of truth for the backend DB. It covers EVERY
-- backend feature the Unity client calls:
--
--   ENDPOINT                       TABLE(S)                          FUNCTION IN api/?
--   ----------------------------   -------------------------------   -----------------
--   GET  /api/auth/nonce           auth_nonces                       YES (auth/nonce.js)
--   GET  /api/game/load            player_data, auth_nonces          YES (game/load.js)
--   POST /api/game/save            player_data, auth_nonces          YES (game/save.js)
--   POST /api/events/track         analytics_events                  NO  (client-only)
--   POST /api/promo/redeem         promo_codes, promo_redemptions    NO  (client-only)
--   POST /api/referral/generate    referrals                         NO  (client-only)
--   POST /api/referral/claim       referrals, referral_claims        NO  (client-only)
--   POST /api/tower-swap/log       tower_swaps                       NO  (client-only)
--   POST /api/bug-report           bug_reports                       NO  (client-only)
--   GET  /api/leaderboard          leaderboard_scores, player_profiles  YES (leaderboard/get.js)   [WO-129]
--   POST /api/leaderboard/submit   leaderboard_scores                YES (leaderboard/submit.js) [WO-129]
--   GET  /api/profile              player_profiles, leaderboard_scores  YES (profile/get.js)       [WO-129]
--   POST /api/profile/username     player_profiles                   YES (profile/username.js)   [WO-129]
--   POST /api/profile/social       player_profiles                   YES (profile/social.js)     [WO-129]
--   POST /api/referral/install-brag  achievement_grants              YES (referral/install-brag.js) [WO-129]
--
-- "client-only" = the Unity client POSTs to the endpoint but the serverless
-- function does NOT yet exist in api/. The table is defined here so that when
-- the owner writes the function, the DB is already aligned with the client's
-- payload. See api/DB_SETUP.md for the per-field provenance and any guesses.
--
-- CONVENTIONS
--   • player_id  TEXT — the BoundWallet address (Solana). Same value across all
--     tables; it is the join key. We DO NOT add a hard FK from feature tables to
--     player_data because a player can fire analytics / bug reports / promo
--     redemptions before their first save row exists (player_data is created
--     lazily on the first /api/game/save). A hard FK would reject those rows.
--     Where the relationship is guaranteed (referral_claims → referrals) we DO
--     add an FK.
--   • Timestamps are TIMESTAMPTZ DEFAULT NOW() (server receive time). Client-
--     supplied timestamps (clientTs / timestamp) are stored in their own column
--     so we can measure clock skew and never trust the client for ordering.
--   • JSONB for free-form / nested payloads (event properties, save state).
-- =============================================================================


-- =============================================================================
-- 1. player_data  — one row per player; the delta-merged save blob.
-- -----------------------------------------------------------------------------
-- UNCHANGED from the original schema (kept verbatim so /api/game/save's upsert
-- and /api/game/load's SELECT keep working). game_state holds the client's
-- SyncDeltaPayload merged into a single JSONB document (camelCase keys).
--
-- Written by  : api/game/save.js  (INSERT … ON CONFLICT (player_id) DO UPDATE,
--               merging  game_state || EXCLUDED.game_state).
-- Read by     : api/game/load.js  (SELECT game_state, schema_version, updated_at).
-- =============================================================================
CREATE TABLE IF NOT EXISTS player_data (
    player_id      TEXT        PRIMARY KEY,          -- BoundWallet address
    schema_version INTEGER     NOT NULL DEFAULT 10,  -- SaveSchema.CurrentVersion
    game_state     JSONB       NOT NULL DEFAULT '{}',
    created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- DRIFT RECONCILE (2026-05-31): an earlier deploy created player_data with ONLY
-- (player_id, game_state, updated_at) -- missing schema_version + created_at that
-- save.js/load.js read+write, so the live save function FAILED against it. CREATE
-- ... IF NOT EXISTS above does NOT alter an existing table, so add the columns
-- explicitly. Additive + idempotent (no data touched, no drops):
ALTER TABLE player_data ADD COLUMN IF NOT EXISTS schema_version INTEGER     NOT NULL DEFAULT 10;
ALTER TABLE player_data ADD COLUMN IF NOT EXISTS created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW();

-- TRUST TIER (2026-08-02, the guest rail). Which auth rail last wrote this row:
--   'wallet' — an ed25519 signature over a single-use nonce proved key ownership.
--   'guest'  — an unverified device-hash bearer id (see guest_rate_limit's header
--              for exactly how little that is worth).
-- Recorded so the distinction is VISIBLE in one column instead of inferred from
-- the id's shape, and so any future real-value feature can filter on it rather
-- than trusting every row equally. Written by api/game/save.js on every upsert.
-- The DEFAULT is 'legacy', never 'wallet': any row that predates this column was
-- written before the two rails existed, and back-filling it as wallet-proven
-- would be a lie told by a schema migration.
ALTER TABLE player_data ADD COLUMN IF NOT EXISTS trust          TEXT        NOT NULL DEFAULT 'legacy';

-- Index for frequent sorted queries (leaderboard, etc.)
CREATE INDEX IF NOT EXISTS idx_player_data_best_wave
    ON player_data ((game_state->>'bestWave') DESC NULLS LAST);

-- Index to quickly find players updated recently (analytics / ops)
CREATE INDEX IF NOT EXISTS idx_player_data_updated_at
    ON player_data (updated_at DESC);

-- -----------------------------------------------------------------------------
-- game_state JSONB structure (all keys optional — only sent when changed):
-- {
--   "bestWave":       integer,
--   "crystals":       integer,
--   "food":           integer,
--   "coins":          integer,
--   "voidshards":     integer,
--   "stone":          integer,
--   "iron":           integer,
--   "wood":           integer,
--   "towers":         [int, int, int, int, int, int, int, int, int],  -- 9 slots
--   "towerAbilities": [int, int, int, int, int, int, int, int, int],  -- 9 slots
--   "pets":           [ { ... PetData ... } ],
--   "ownedPets":      [ "Aether" | "Flame" | "Ice" | ... ],
--   "starterPetId":   string | null
-- }
-- NOTE: the live client (GameStateService.SendDelta) actually POSTs the FULL
-- camelCase PersistedState snapshot (with null fields stripped) PLUS "playerId",
-- so game_state may also carry nested objects like "resources":{...} and the
-- many other PersistedState fields. save.js only promotes the whitelisted keys
-- above into the merged delta; any extra keys it ignores. The JSONB column will
-- hold whatever save.js chooses to write — keep save.js as the gatekeeper.
-- -----------------------------------------------------------------------------


-- =============================================================================
-- 1b. auth_nonces  — single-use wallet-signature challenges (WO-120 §D security).
-- -----------------------------------------------------------------------------
-- Endpoint : GET  /api/auth/nonce?wallet=<base58>   (api/auth/nonce.js — issues)
--            POST /api/game/save  (verifies a signature over the nonce; consumes)
-- Client   : Assets/_Modules/Core/State/GameStateService.cs (fetch → sign → send)
--
-- WHY: /api/game/save + /api/game/load were keyed ONLY by the PUBLIC wallet
-- address (?playerId=<wallet>) with NO proof of ownership, so anyone who knew a
-- wallet could overwrite/read that player's save. This table backs a
-- challenge–response: the server issues a random one-time nonce bound to the
-- claimed wallet; the client signs {nonce}+payload with the wallet's ed25519
-- key (Solana); save verifies the signature against the wallet pubkey, then
-- BURNS the nonce so it can never be replayed.
--
-- WALLET SCHEME (ASSUMPTION — flagged): Solana / ed25519. The whole client is
-- Solana (WalletService base58 address = player_id; tower-swap uses Solana tx
-- sigs; Solana Pay). Verification therefore uses tweetnacl ed25519 over the
-- base58-decoded wallet pubkey. If the chain were ever EVM, swap the verify
-- helper for ecrecover — the table and flow are scheme-agnostic.
--
--   nonce       — the random challenge (server-generated, PK). The client signs
--                 a deterministic message that embeds this value.
--   wallet      — the base58 address the nonce was issued TO. The save endpoint
--                 must verify the signature against THIS pubkey AND match it to
--                 the payload's playerId, else 401.
--   used        — false on issue; set true the instant a save consumes it. A
--                 second save presenting the same nonce is rejected (replay).
--   expires_at  — short TTL (default 5 min). Expired nonces are unusable even if
--                 never consumed; a periodic sweep (or the WHERE clause on
--                 verify) discards them.
--
-- Consume is atomic: the verify step does
--   UPDATE auth_nonces SET used = TRUE
--   WHERE nonce = $1 AND wallet = $2 AND used = FALSE AND expires_at > NOW()
--   RETURNING nonce;
-- A zero-row result means missing / already-used / expired / wrong-wallet → 401.
-- =============================================================================
CREATE TABLE IF NOT EXISTS auth_nonces (
    nonce      TEXT        PRIMARY KEY,             -- random one-time challenge (base64url, 32 bytes)
    wallet     TEXT        NOT NULL,                -- base58 wallet the nonce was issued to
    used       BOOLEAN     NOT NULL DEFAULT FALSE,  -- burned on first successful verify (replay guard)
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),  -- issue time
    expires_at TIMESTAMPTZ NOT NULL                 -- created_at + short TTL (issuer sets, e.g. 5 min)
);

-- Look an issued nonce up by wallet (and prune a wallet's stale challenges).
CREATE INDEX IF NOT EXISTS idx_auth_nonces_wallet
    ON auth_nonces (wallet);

-- Sweep expired/used nonces cheaply (a cron or the next issue call can run:
--   DELETE FROM auth_nonces WHERE expires_at < NOW() OR used = TRUE;).
CREATE INDEX IF NOT EXISTS idx_auth_nonces_expires
    ON auth_nonces (expires_at);


-- =============================================================================
-- 1c. guest_rate_limit  — the GUEST rail's only defence (2026-08-02).
-- -----------------------------------------------------------------------------
-- Endpoint : POST /api/game/save, GET /api/game/load (both via
--            api/_lib/wallet-auth.verifyGuest → touchGuestRate)
-- Client   : Assets/_Modules/Core/State/GameStateService.cs — the id this table
--            keys on is EXACTLY the one EnsureAccount already mints:
--              "guest-local-" + sha256(SystemInfo.deviceUniqueIdentifier + salt)
--
-- WHY A GUEST RAIL EXISTS AT ALL: the APK front door is "Connect Wallet OR Play
-- as Guest", and testers are being recruited now. Before this, save/load required
-- a wallet signature UNCONDITIONALLY, so every guest tester was structurally
-- unable to reach the database — their progress lived and died in PlayerPrefs on
-- one device, and the owner could see nothing of what they played.
--
-- WHAT A GUEST IDENTITY IS WORTH — stated plainly so nobody later mistakes it for
-- authentication: it is a BEARER TOKEN. The only secret is the 256-bit device
-- hash itself; whoever presents it gets that row, like an unguessable URL. It
-- cannot be revoked and cannot move to a new device. That is an honest trade for
-- a throwaway tester save and is NEVER acceptable for real value — which is
-- enforced structurally, not by policy: a guest id is 76 chars containing '-' and
-- hex '0', so it can never satisfy the base58 wallet regex, and therefore can
-- never key a wallet row or reach a wallet-gated feature.
--
-- The budget below is per guest id and SHARED by save+load (one bounded total,
-- not two). At 30/60s it is ~4x the client's own maximum sync rate (its
-- MinSyncDelay is 8s), so an honest tester never sees it.
--
--   hits / window_started_at — the sliding window, advanced in one atomic UPSERT.
--   total_hits              — lifetime counter, purely for "is this id abusive".
--   last_seen               — drives the cleanup sweep.
-- =============================================================================
CREATE TABLE IF NOT EXISTS guest_rate_limit (
    guest_id          TEXT        PRIMARY KEY,             -- "guest-local-<64 lowercase hex>"
    window_started_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),  -- start of the current 60s window
    hits              INTEGER     NOT NULL DEFAULT 0,      -- requests inside the current window
    total_hits        BIGINT      NOT NULL DEFAULT 0,      -- lifetime requests (abuse signal)
    last_seen         TIMESTAMPTZ NOT NULL DEFAULT NOW()   -- last request from this id
);

-- Sweep idle guests (api/admin/cleanup.js drops rows untouched for 30 days).
CREATE INDEX IF NOT EXISTS idx_guest_rate_limit_last_seen
    ON guest_rate_limit (last_seen);


-- =============================================================================
-- 2. analytics_events  — player behaviour analytics.
-- -----------------------------------------------------------------------------
-- Endpoint : POST /api/events/track   (FUNCTION NOT YET IN api/ — client-only)
-- Client   : Assets/_Modules/Core/Analytics/EventTracker.cs
--
-- The client batches events and POSTs:
--   { "events": [ { "playerId", "eventName", "properties", "clientTs" }, ... ] }
-- where each event is EventTracker.TrackedEvent:
--   playerId   string  — BoundWallet or "anonymous"
--   eventName  string  — snake_case: session_start, wave_completed,
--                        purchase_completed, bundle_viewed, promo_redeemed,
--                        referral_code_generated, referral_shared,
--                        referral_claimed, tower_swap_completed, + custom
--   properties string  — JSON STRING (the client serializes the props object to
--                        a string before sending). The function should parse it
--                        to JSONB on insert:  properties::jsonb  (or store raw
--                        text in properties_raw if it ever fails to parse).
--   clientTs   long    — unix epoch MILLISECONDS captured on the device.
--
-- One DB row per event (the function loops over the events array and inserts
-- each). event_id is a generated surrogate PK because events are not unique by
-- any client field (a player can fire the same eventName many times).
-- =============================================================================
CREATE TABLE IF NOT EXISTS analytics_events (
    event_id    BIGINT      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    player_id   TEXT        NOT NULL,             -- "playerId" (BoundWallet | "anonymous")
    event_name  TEXT        NOT NULL,             -- "eventName" (snake_case)
    properties  JSONB       NOT NULL DEFAULT '{}',-- parsed from the client's "properties" JSON string
    client_ts   BIGINT,                           -- "clientTs" — device unix epoch MILLIS
    received_at TIMESTAMPTZ NOT NULL DEFAULT NOW()-- server receive time (trusted ordering)
);

-- Events for one player, newest first (per-player funnels / debugging).
CREATE INDEX IF NOT EXISTS idx_analytics_events_player_time
    ON analytics_events (player_id, received_at DESC);

-- Aggregate by event name over time (e.g. count session_start per day).
CREATE INDEX IF NOT EXISTS idx_analytics_events_name_time
    ON analytics_events (event_name, received_at DESC);


-- =============================================================================
-- 3. promo_codes  — operator-issued promo codes (the catalog of valid codes).
-- -----------------------------------------------------------------------------
-- Endpoint : POST /api/promo/redeem   (FUNCTION NOT YET IN api/ — client-only)
-- Client   : Assets/_Modules/Core/Promo/PromoCodeService.cs
--
-- The client POSTs:  { playerId, code }  and expects back
--   success: { success:true, reward:{ crystals, coins }, message }
--   failure: { success:false, error:"INVALID_CODE"|"ALREADY_REDEEMED"
--                                   |"EXPIRED"|"PLAYER_LIMIT_REACHED" }
--
-- This table is the SOURCE OF TRUTH for what a code grants and its limits. The
-- owner/operator inserts rows here by hand (or via an admin tool). The redeem
-- function looks the code up here, checks expiry + caps, then records the
-- redemption in promo_redemptions (table 4).
--
-- Codes are normalised to UPPERCASE by the client (code.Trim().ToUpperInvariant())
-- BEFORE sending, so store + compare uppercase. code is the PK / lookup key.
--
-- Error-code mapping the redeem function must implement:
--   row missing                          → INVALID_CODE
--   NOW() > expires_at (when not null)   → EXPIRED
--   active = false                       → INVALID_CODE (or a disabled state)
--   global redemption count >= max_redemptions (when not null) → ALREADY_REDEEMED
--   this player already in promo_redemptions for this code     → ALREADY_REDEEMED
--   player's total distinct redeemed codes >= per_player_limit → PLAYER_LIMIT_REACHED
-- =============================================================================
CREATE TABLE IF NOT EXISTS promo_codes (
    code             TEXT        PRIMARY KEY,           -- normalised UPPERCASE code (e.g. "TEST10")
    reward_crystals  INTEGER     NOT NULL DEFAULT 0,    -- → response reward.crystals
    reward_coins     INTEGER     NOT NULL DEFAULT 0,    -- → response reward.coins
    message          TEXT,                              -- → response.message (shown to player)
    active           BOOLEAN     NOT NULL DEFAULT TRUE, -- operator kill-switch
    max_redemptions  INTEGER,                           -- NULL = unlimited global uses
    per_player_limit INTEGER,                           -- NULL = no cross-code cap (PLAYER_LIMIT_REACHED gate)
    expires_at       TIMESTAMPTZ,                       -- NULL = never expires
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);


-- =============================================================================
-- 4. promo_redemptions  — one row each time a player redeems a code.
-- -----------------------------------------------------------------------------
-- Endpoint : POST /api/promo/redeem   (same function as table 3)
--
-- Enforces one-time-use: a UNIQUE (code, player_id) constraint means a second
-- redeem of the same code by the same player violates the unique index, which
-- the function maps to ALREADY_REDEEMED. Counting rows per code enforces
-- max_redemptions; counting distinct codes per player enforces per_player_limit.
--
-- player_id is NOT FK'd to player_data (an anonymous player may redeem before
-- their first save). code IS FK'd to promo_codes (a redemption can't exist for
-- a code that isn't in the catalog).
-- =============================================================================
CREATE TABLE IF NOT EXISTS promo_redemptions (
    redemption_id BIGINT      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    code          TEXT        NOT NULL REFERENCES promo_codes(code) ON DELETE CASCADE,
    player_id     TEXT        NOT NULL,             -- "playerId" (BoundWallet | "anonymous")
    crystals      INTEGER     NOT NULL DEFAULT 0,   -- snapshot of reward granted (audit; code reward may change later)
    coins         INTEGER     NOT NULL DEFAULT 0,   -- snapshot of reward granted
    redeemed_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (code, player_id)                        -- one redemption per code per player
);

-- Count redemptions per code fast (max_redemptions enforcement).
CREATE INDEX IF NOT EXISTS idx_promo_redemptions_code
    ON promo_redemptions (code);

-- Count a player's distinct redeemed codes fast (per_player_limit enforcement).
CREATE INDEX IF NOT EXISTS idx_promo_redemptions_player
    ON promo_redemptions (player_id);


-- =============================================================================
-- 5. referrals  — each player's own unique referral code.
-- -----------------------------------------------------------------------------
-- Endpoint : POST /api/referral/generate  (FUNCTION NOT YET IN api/ — client-only)
-- Client   : Assets/_Modules/Core/Referral/ReferralService.cs
--
-- generate POSTs:  { playerId }  and expects:
--   { success:true, code, referralUrl }
-- The function should UPSERT one row per player (generate-or-reuse): if the
-- player already has a code, return it; otherwise mint a new unique code and a
-- referralUrl, insert, and return them. The client caches code+url in
-- PlayerPrefs, so the function must return the SAME code on repeat calls.
--
--   player_id    — owner of the code (the referrer). PK: one code per player.
--   code         — the unique shareable code (uppercase; claimers send it
--                  uppercased). UNIQUE so claim can look a referrer up by code.
--   referral_url — "referralUrl" returned to the client for the share sheet.
--   reward_cap   — optional per-period cap on how many successful claims this
--                  referrer is rewarded for (CAP_REACHED in claim). NULL =
--                  no cap. (GUESS: the client lists "Referrer reward cap" as a
--                  backend rule but never sends a number — owner sets policy.)
-- =============================================================================
CREATE TABLE IF NOT EXISTS referrals (
    player_id    TEXT        PRIMARY KEY,            -- "playerId" — the referrer (code owner)
    code         TEXT        NOT NULL UNIQUE,        -- "code" — unique shareable referral code
    referral_url TEXT,                               -- "referralUrl" — deep/share link
    reward_cap   INTEGER,                            -- max rewarded claims (NULL = unlimited); CAP_REACHED gate
    claim_count  INTEGER     NOT NULL DEFAULT 0,     -- denormalised count of successful claims (cap check)
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Look a referrer up by their code (claim flow resolves code → referrer).
CREATE INDEX IF NOT EXISTS idx_referrals_code
    ON referrals (code);


-- =============================================================================
-- 6. referral_claims  — one row each time a player claims someone's code.
-- -----------------------------------------------------------------------------
-- Endpoint : POST /api/referral/claim  (FUNCTION NOT YET IN api/ — client-only)
-- Client   : Assets/_Modules/Core/Referral/ReferralService.cs
--
-- claim POSTs:  { playerId, code }  and expects:
--   success: { success:true, reward:{ crystals }, message }
--   failure: { success:false, error:"SELF_REFERRAL"|"ALREADY_CLAIMED"
--                                   |"INVALID_CODE"|"CAP_REACHED" }
--
-- claimer_id = the player redeeming the code; we resolve code → referrer via
-- referrals. The function must enforce:
--   code not in referrals                         → INVALID_CODE
--   referrer player_id == claimer_id              → SELF_REFERRAL
--   claimer already has a row here                → ALREADY_CLAIMED
--                                                   (UNIQUE(claimer_id) below)
--   referrals.claim_count >= referrals.reward_cap → CAP_REACHED
-- On success: insert this row, bump referrals.claim_count, grant the claimer
-- crystals (response reward.crystals), and trigger the referrer's reward.
--
-- UNIQUE(claimer_id): a player may only ever claim ONE referral code (matches
-- the client's "one claim per player" guard). claimer_id is therefore the
-- natural one-per-player key, but we keep a surrogate PK for FK ergonomics.
-- referral_code is FK'd to referrals(code) — can't claim a non-existent code.
-- =============================================================================
CREATE TABLE IF NOT EXISTS referral_claims (
    claim_id      BIGINT      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    referral_code TEXT        NOT NULL REFERENCES referrals(code) ON DELETE CASCADE,
    referrer_id   TEXT        NOT NULL,             -- denormalised: referrals.player_id at claim time
    claimer_id    TEXT        NOT NULL,             -- "playerId" of the claiming player
    crystals      INTEGER     NOT NULL DEFAULT 0,   -- claimer reward granted (response reward.crystals)
    message       TEXT,                             -- response.message
    claimed_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (claimer_id)                             -- one claim per player ever (ALREADY_CLAIMED gate)
);

-- Count / list claims for a given referral code (cap + referrer dashboards).
CREATE INDEX IF NOT EXISTS idx_referral_claims_code
    ON referral_claims (referral_code);

-- Count a referrer's earned claims (reward cap per referrer).
CREATE INDEX IF NOT EXISTS idx_referral_claims_referrer
    ON referral_claims (referrer_id);


-- =============================================================================
-- 7. tower_swaps  — audit log of paid instant tower swaps (Solana Pay).
-- -----------------------------------------------------------------------------
-- Endpoint : POST /api/tower-swap/log  (FUNCTION NOT YET IN api/ — client-only)
-- Client   : Assets/_Modules/Village/Buildings/TowerSwapService.cs
--
-- Fire-and-forget log POSTed after a successful on-chain payment:
--   { playerId, waveId, fromTower, toTower, currency, costUsdc, txSig, timestamp }
--     playerId   string — BoundWallet | "anonymous"
--     waveId     int    — wave number the swap happened on
--     fromTower  string — tower display name swapped FROM (e.g. "Arcane")
--     toTower    string — tower display name swapped TO
--     currency   string — CurrencyKind.ToString() ("Usdc" | "Skr")
--     costUsdc   number — flat cost in USDC (currently 2.5)
--     txSig      string — Solana transaction signature (on-chain proof)
--     timestamp  long   — client unix epoch SECONDS (note: SECONDS, not millis)
--
-- This is a pure append-only audit/analytics log. tx_sig is UNIQUE so a
-- duplicate POST of the same on-chain payment can't be logged twice (the client
-- sends fire-and-forget with no dedup, so the DB guards it). tx_sig may be NULL
-- if a swap path ever logs without an on-chain signature, so the UNIQUE index
-- is partial (NULLs allowed, only non-null sigs deduped).
-- =============================================================================
CREATE TABLE IF NOT EXISTS tower_swaps (
    swap_id    BIGINT      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    player_id  TEXT        NOT NULL,                -- "playerId"
    wave_id    INTEGER,                             -- "waveId"
    from_tower TEXT,                                -- "fromTower" (display name)
    to_tower   TEXT,                                -- "toTower" (display name)
    currency   TEXT,                                -- "currency" ("Usdc" | "Skr")
    cost_usdc  NUMERIC(12,4),                       -- "costUsdc" (flat 2.5)
    tx_sig     TEXT,                                -- "txSig" (Solana signature)
    client_ts  BIGINT,                              -- "timestamp" — client unix epoch SECONDS
    logged_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()   -- server receive time
);

-- One player's swap history, newest first.
CREATE INDEX IF NOT EXISTS idx_tower_swaps_player_time
    ON tower_swaps (player_id, logged_at DESC);

-- Dedup on-chain payments (only non-null signatures are constrained).
CREATE UNIQUE INDEX IF NOT EXISTS uq_tower_swaps_tx_sig
    ON tower_swaps (tx_sig)
    WHERE tx_sig IS NOT NULL;


-- =============================================================================
-- 8. bug_reports  — in-game bug reports (the "?"/gear Help menu).
-- -----------------------------------------------------------------------------
-- Endpoint : POST /api/bug-report   (FUNCTION NOT YET IN api/ — client-only)
-- Client   : Assets/_Modules/HUD/HelpMenu.cs
--
-- IMPORTANT: HelpMenu posts to a DIFFERENT host than every other service:
--   https://defenders-of-the-realm.vercel.app/api/bug-report   (NO "-v2")
-- The save/load/analytics/promo/referral/tower-swap services all use
--   https://defenders-of-the-realm-v2.vercel.app/...
-- So bug-report currently lands in the OLDER (React app's) deployment. See
-- DB_SETUP.md → "Host mismatch" for the decision the owner must make
-- (point the client at -v2, or run this table on the older project's DB).
--
-- The client POSTs:
--   { "description": <text>,
--     "context": { "route": <sceneName>, "appVersion": <Application.version> } }
-- Description is capped at 4000 chars client-side (per HelpMenu comment) and
-- carries the auto-captured block (scene, time, build, device, screen, the
-- on-disk screenshot PATH — the image itself is NOT uploaded, only its path).
-- No playerId is sent by HelpMenu; we still add a nullable player_id column for
-- when the function is wired to attach the caller's wallet.
-- =============================================================================
CREATE TABLE IF NOT EXISTS bug_reports (
    report_id    BIGINT      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    description  TEXT        NOT NULL,               -- "description" (<= 4000 chars)
    route        TEXT,                               -- context.route (active scene name)
    app_version  TEXT,                               -- context.appVersion (Application.version)
    player_id    TEXT,                               -- NULL — HelpMenu does not send one (yet)
    context      JSONB       NOT NULL DEFAULT '{}',  -- full "context" object, future-proofed
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- DRIFT RECONCILE (2026-08-02) — THE REASON bug_reports HAS 0 ROWS.
-- Captured from production, request 02:25:43 UTC 2026-08-03:
--     NeonDbError: column "player_id" of relation "bug_reports" does not exist
--     SQLSTATE 42703, api/bug-report.js:124
-- The LIVE table was created before this file's definition and is missing columns
-- api/bug-report.js writes, so EVERY report a tester has submitted from Settings
-- returned 500 and was never stored. Same class of drift as player_data above.
-- CREATE TABLE ... IF NOT EXISTS does NOT alter an existing table, so every
-- column has to be added explicitly. Additive + idempotent (no data touched):
ALTER TABLE bug_reports ADD COLUMN IF NOT EXISTS route       TEXT;
ALTER TABLE bug_reports ADD COLUMN IF NOT EXISTS app_version TEXT;
ALTER TABLE bug_reports ADD COLUMN IF NOT EXISTS player_id   TEXT;
ALTER TABLE bug_reports ADD COLUMN IF NOT EXISTS context     JSONB       NOT NULL DEFAULT '{}';
ALTER TABLE bug_reports ADD COLUMN IF NOT EXISTS created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW();

-- Recent reports first (triage view).
CREATE INDEX IF NOT EXISTS idx_bug_reports_created
    ON bug_reports (created_at DESC);


-- =============================================================================
-- 9. player_profiles  — the identity layer the leaderboard needs (WO-129 §2.2).
-- -----------------------------------------------------------------------------
-- Endpoint : GET  /api/profile?wallet=<addr>        (api/profile/get.js)
--            POST /api/profile/username             (api/profile/username.js)
--            POST /api/profile/social/link|unlink   (api/profile/social.js)
-- Client   : ProfileService (Core) — WO-129 §4 (NEW; not yet built)
--
-- The durable identity key stays the WALLET (player_id, same join key as every
-- other table). username is a DISPLAY LABEL mapped onto that wallet — the public
-- name shown on the leaderboard instead of a raw address (WO-129 §2.2).
--
--   wallet           — base58 address (PK). Same value as player_data.player_id;
--                      NOT FK'd (a profile may be created before the first save).
--   username         — public display name. NULL until the player sets one (the
--                      client shows "Defender#<short-wallet>" as a default until
--                      then; we do NOT persist that default — it's derived).
--   username_ci      — lower(username), kept UNIQUE so the server enforces
--                      case-insensitive uniqueness (WO-129 §2.2 USERNAME_TAKEN).
--                      A generated column so it can never drift from username.
--   avatar_id        — chosen hero/portrait id (defaults to current hero client-
--                      side; NULL = "use current hero"). No new art for MVP.
--   social_links     — JSONB opt-in map { "x": {handle, public}, "discord":{...} }.
--                      Opt-in only; surfaced publicly only when public=true.
--   created_at       — first profile touch.
--   renamed_at       — last username change (NULL = never renamed). Backs the
--                      "one free rename, then cost/cap" policy (WO-129 §2.2 US-8);
--                      the rename-cost lever itself is enforced client/economy
--                      side — this column is the server's timestamp of record.
--
-- username_ci is UNIQUE; the username endpoint maps a 23505 on it → USERNAME_TAKEN.
-- =============================================================================
CREATE TABLE IF NOT EXISTS player_profiles (
    wallet       TEXT        PRIMARY KEY,            -- base58 address (= player_data.player_id)
    username     TEXT,                               -- public display name (NULL until set)
    username_ci  TEXT        GENERATED ALWAYS AS (lower(username)) STORED, -- case-insensitive key
    avatar_id    TEXT,                               -- chosen hero/portrait (NULL = current hero)
    social_links JSONB       NOT NULL DEFAULT '{}',  -- opt-in { provider: { handle, public } }
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    renamed_at   TIMESTAMPTZ                         -- last username change (NULL = never)
);

-- Case-insensitive uniqueness for usernames (NULLs excluded → many un-named rows OK).
CREATE UNIQUE INDEX IF NOT EXISTS uq_player_profiles_username_ci
    ON player_profiles (username_ci)
    WHERE username_ci IS NOT NULL;


-- =============================================================================
-- 10. leaderboard_scores  — server-authoritative standings (WO-129 §2.1).
-- -----------------------------------------------------------------------------
-- Endpoint : GET  /api/leaderboard?metric=<m>&period=<id>&wallet=<addr>
--                                                   (api/leaderboard/get.js)
--            POST /api/leaderboard/submit           (api/leaderboard/submit.js)
-- Client   : LeaderboardService (Core) — WO-129 §4 (NEW; read-only client).
--
-- ONE row per (wallet, metric, period_id) — a player's BEST score on that board
-- for that period. The submit endpoint only ever RAISES a score (a max-merge via
-- GREATEST on conflict), so it is a monotonic high-water mark per board and a
-- replayed/stale submit can never lower a standing. The client NEVER asserts a
-- final rank — rank is computed at read time by ordering scores (WO-129 §5:
-- "NO client-authoritative scores").
--
--   wallet      — the player (= player_data.player_id). NOT FK'd (score may land
--                 before first save).
--   metric      — which board: 'highest_wave' (SHIP FIRST), 'longest_hold',
--                 'total_resources', 'clan', 'arena' (reserved). Free TEXT so a
--                 new board needs no migration — the endpoints whitelist values.
--   period_id   — 'alltime' (never resets) or a week key 'YYYY-Www' (e.g.
--                 '2026-W22', resets Mon 00:00 UTC — WO-129 §2.1).
--   score       — the standing value (BIGINT — wave count, seconds held, total
--                 resources). Higher = better for every MVP board.
--   meta        — optional JSONB context for the row (e.g. run id, hero used).
--   updated_at  — when this best was last raised.
--
-- The PK (wallet, metric, period_id) is what makes submit idempotent + a clean
-- upsert target; uq is implied by the composite PK.
-- =============================================================================
CREATE TABLE IF NOT EXISTS leaderboard_scores (
    wallet     TEXT        NOT NULL,                 -- player (= player_data.player_id)
    metric     TEXT        NOT NULL,                 -- board id ('highest_wave', ...)
    period_id  TEXT        NOT NULL,                 -- 'alltime' | 'YYYY-Www'
    score      BIGINT      NOT NULL DEFAULT 0,       -- standing (higher = better)
    meta       JSONB       NOT NULL DEFAULT '{}',    -- optional row context
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (wallet, metric, period_id)
);

-- The ranking query: ORDER BY score DESC within one board. This index makes both
-- the top-N read and the caller's rank-window (COUNT scores above them) fast.
CREATE INDEX IF NOT EXISTS idx_leaderboard_scores_board_rank
    ON leaderboard_scores (metric, period_id, score DESC);


-- =============================================================================
-- 11. achievement_grants  — one-time, server-validated grants (WO-129 §2.4).
-- -----------------------------------------------------------------------------
-- Endpoint : POST /api/referral/install-brag        (api/referral/install-brag.js)
-- Client   : ReferralService (Core) — install-brag bonus (WO-129 §2.4, extend).
--
-- Generic idempotency ledger for "grant this player X exactly once". The install
-- brag is modelled as achievement_id = 'install_brag'. The PK (wallet,
-- achievement_id) STRUCTURALLY prevents a double-grant — a second request for the
-- same pair violates the PK, which the endpoint maps to "already granted" and
-- returns the original (no second reward). This is the same anti-abuse shape the
-- WO calls for: "the client requests; the server grants", keyed on durable wallet.
--
--   wallet         — who was granted (= player_data.player_id). NOT FK'd.
--   achievement_id — the one-time grant key ('install_brag', and future grants).
--   reward         — JSONB snapshot of what was granted (e.g. { crystals, flair })
--                    so the audit record is self-describing even if policy changes.
--   granted_at     — when (also the idempotency timestamp returned on re-request).
-- =============================================================================
CREATE TABLE IF NOT EXISTS achievement_grants (
    wallet         TEXT        NOT NULL,             -- player (= player_data.player_id)
    achievement_id TEXT        NOT NULL,             -- 'install_brag' | future one-time grants
    reward         JSONB       NOT NULL DEFAULT '{}',-- snapshot of granted reward (audit)
    granted_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (wallet, achievement_id)
);

-- List a player's one-time grants (profile "Founding Herald" flair, audits).
CREATE INDEX IF NOT EXISTS idx_achievement_grants_wallet
    ON achievement_grants (wallet);


-- =============================================================================
-- OPTIONAL SEED — a test promo code so /api/promo/redeem can be smoke-tested.
-- The client ships an editor menu "Simulate Promo Redeem (TEST10)", so seed it.
-- Comment this out in production if you don't want a live test code.
-- =============================================================================
INSERT INTO promo_codes (code, reward_crystals, reward_coins, message, active)
VALUES ('TEST10', 10, 0, 'Thanks for testing — 10 Aether Crystals!', TRUE)
ON CONFLICT (code) DO NOTHING;

-- =============================================================================
-- END OF SCHEMA
-- =============================================================================
