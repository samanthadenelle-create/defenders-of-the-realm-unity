// =============================================================================
// api/game/save.js — Vercel Serverless Function
// -----------------------------------------------------------------------------
// Receives a MessagePack-encoded SyncDeltaPayload from the Unity client and
// upserts the changed fields into the player_data JSONB column in Neon Postgres.
//
// Driver: @neondatabase/serverless  (Vercel Postgres is now Neon — use this)
//   npm install @neondatabase/serverless @msgpack/msgpack
//
// Env vars required (set in Vercel dashboard → Settings → Environment Variables):
//   DATABASE_URL  — Neon connection string (postgres://...)
//
// Status codes: 200 | 400 | 401 | 404 | 500  (project constraint — no others)
// =============================================================================

const { neon } = require('@neondatabase/serverless');
const { decode } = require('@msgpack/msgpack');
const { verifyAndConsume } = require('../_lib/wallet-auth');

// Disable Vercel's default body parser so we get the raw Buffer for MsgPack.
// (Also REQUIRED for auth: the wallet signature is over the EXACT raw body
// bytes, so we must verify against the unparsed Buffer — see _lib/wallet-auth.)
module.exports.config = { api: { bodyParser: false } };

// ── Sanity-check bounds (WO-120 §2 — soft-currency stays client-owned, with
//    server guards). These are anti-grief / anti-corruption ceilings, NOT a
//    full server-authoritative economy (that flips ON when currency buys real
//    value — crypto/IAP — see the BUILT-TO-FLIP seam at the bottom of this file).
const MAX_RESOURCE   = 1_000_000_000; // any single soft-currency balance ceiling
const MAX_BEST_WAVE  = 100_000;       // implausible-wave ceiling
// A save may not DROP a monotonic counter (best_wave) at all, and may not drop a
// spendable balance by more than this fraction in one sync (guards a rollback/
// tamper that zeroes a wallet; legitimate spends are far smaller per sync).
const MAX_BALANCE_DROP_FRACTION = 0.95;

module.exports = async (req, res) => {
    if (req.method !== 'POST') {
        return res.status(400).json({ error: 'Method not allowed' });
    }

    // ── Read raw body ──────────────────────────────────────────────────────
    let rawBody;
    try {
        rawBody = await readBody(req);
    } catch (err) {
        console.error('[save] Body read error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }

    // ── Decode MessagePack ─────────────────────────────────────────────────
    let body;
    try {
        const isMsgPack = req.headers['content-type'] === 'application/x-msgpack';
        body = isMsgPack ? decode(rawBody) : JSON.parse(rawBody.toString());
    } catch (err) {
        console.error('[save] Decode error:', err);
        return res.status(400).json({ error: 'Invalid payload' });
    }

    // The client posts camelCase "playerId" (GameStateService.SendDelta); the
    // older MsgPack path used "PlayerId". Accept either.
    const playerId = body.PlayerId != null ? body.PlayerId
                   : body.playerId != null ? body.playerId
                   : null;
    const schemaVersion = body.SchemaVersion != null ? body.SchemaVersion : body.schemaVersion;
    const deltaFields = normalizeDeltaFields(body);

    if (!playerId) {
        return res.status(400).json({ error: 'Missing PlayerId' });
    }

    // ── AUTH GATE (WO-120 §D) ──────────────────────────────────────────────
    // Verify a wallet-signed nonce over the EXACT raw body before any write, and
    // burn the nonce (single-use, replay-proof). The signed wallet must equal the
    // playerId being written. Any failure → 401, no DB write.
    let sql;
    try {
        sql = neon(process.env.DATABASE_URL);
    } catch (err) {
        console.error('[save] DB init error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }

    let auth;
    try {
        auth = await verifyAndConsume(sql, req.headers, rawBody, playerId);
    } catch (err) {
        console.error('[save] Auth check error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }
    if (!auth.ok) {
        await logReject(sql, playerId, 'auth_failed', { reason: auth.reason });
        return res.status(401).json({ error: 'Unauthorized', reason: auth.reason });
    }

    // ── Build the delta JSONB object ───────────────────────────────────────
    // Only include fields the client actually sent (null = domain unchanged).
    const delta = {};

    // Resources
    if (deltaFields.Crystals  != null) delta.crystals  = deltaFields.Crystals;
    if (deltaFields.Food      != null) delta.food      = deltaFields.Food;
    if (deltaFields.Coins     != null) delta.coins     = deltaFields.Coins;
    if (deltaFields.Voidshards != null) delta.voidshards = deltaFields.Voidshards;
    if (deltaFields.Stone     != null) delta.stone     = deltaFields.Stone;
    if (deltaFields.Iron      != null) delta.iron      = deltaFields.Iron;
    if (deltaFields.Wood      != null) delta.wood      = deltaFields.Wood;

    // Towers
    if (deltaFields.Towers         != null) delta.towers          = deltaFields.Towers;
    if (deltaFields.TowerAbilities  != null) delta.towerAbilities  = deltaFields.TowerAbilities;

    // Wave
    if (deltaFields.BestWave != null) delta.bestWave = deltaFields.BestWave;

    // Pets (JSON string from client — parse so Postgres stores proper JSONB)
    if (deltaFields.PetsJson) {
        try { delta.pets = JSON.parse(deltaFields.PetsJson); } catch { /* skip malformed */ }
    }
    if (deltaFields.OwnedPetsJson) {
        try { delta.ownedPets = JSON.parse(deltaFields.OwnedPetsJson); } catch { /* skip malformed */ }
    }
    if (deltaFields.StarterPetId != null) delta.starterPetId = deltaFields.StarterPetId;

    if (Object.keys(delta).length === 0) {
        return res.status(200).json({ success: true, note: 'empty delta — no write' });
    }

    // ── SANITY-CHECK GUARDS (WO-120 §2) ────────────────────────────────────
    // Soft currency stays CLIENT-OWNED, but the server refuses obviously-
    // tampered writes: out-of-bounds values, best_wave going backwards (it is a
    // monotonic high-water mark — server already wins on it), and implausible
    // wholesale balance wipes. Rejected fields are STRIPPED from the delta (the
    // rest of the save still lands) and logged to analytics_events for audit.
    let prior = {};
    try {
        const priorRows = await sql`
            SELECT game_state FROM player_data WHERE player_id = ${playerId} LIMIT 1
        `;
        if (priorRows.length > 0 && priorRows[0].game_state) prior = priorRows[0].game_state;
    } catch (err) {
        // A read failure shouldn't block the save; just skip the comparative
        // guards (bounds checks below still apply).
        console.warn('[save] prior-state read failed, skipping rollback guards:', err.message);
    }

    const rejects = [];

    // (a) Bounds + anti-rollback on best_wave (monotonic high-water mark).
    if (delta.bestWave != null) {
        const bw = Number(delta.bestWave);
        if (!Number.isFinite(bw) || bw < 0 || bw > MAX_BEST_WAVE) {
            rejects.push({ field: 'bestWave', value: delta.bestWave, rule: 'out_of_bounds' });
            delete delta.bestWave;
        } else if (prior.bestWave != null && bw < Number(prior.bestWave)) {
            rejects.push({ field: 'bestWave', value: bw, prior: prior.bestWave, rule: 'rollback' });
            delete delta.bestWave; // never lower the high-water mark
        }
    }

    // (b) Bounds + anti-wipe on each soft-currency / material balance.
    for (const key of ['crystals', 'food', 'coins', 'voidshards', 'stone', 'iron', 'wood']) {
        if (delta[key] == null) continue;
        const v = Number(delta[key]);
        if (!Number.isFinite(v) || v < 0 || v > MAX_RESOURCE) {
            rejects.push({ field: key, value: delta[key], rule: 'out_of_bounds' });
            delete delta[key];
            continue;
        }
        const priorVal = prior[key] != null ? Number(prior[key]) : null;
        if (priorVal != null && priorVal > 0 && v < priorVal * (1 - MAX_BALANCE_DROP_FRACTION)) {
            // Dropped more than the allowed fraction in a single sync → suspicious.
            rejects.push({ field: key, value: v, prior: priorVal, rule: 'implausible_drop' });
            delete delta[key];
        }
    }

    if (rejects.length > 0) {
        await logReject(sql, playerId, 'save_sanity_reject', { rejects });
    }

    if (Object.keys(delta).length === 0) {
        // Everything was stripped by the guards — nothing safe to write.
        return res.status(200).json({ success: true, note: 'all fields rejected by guards', rejects });
    }

    // ── Upsert into Neon ───────────────────────────────────────────────────
    try {
        // game_state || $2::jsonb merges the delta onto the existing JSONB column.
        // The cast is required because Neon's HTTP driver sends parameters as strings.
        await sql`
            INSERT INTO player_data (player_id, schema_version, game_state, updated_at)
            VALUES (
                ${playerId},
                ${schemaVersion ?? 10},
                ${JSON.stringify(delta)}::jsonb,
                NOW()
            )
            ON CONFLICT (player_id) DO UPDATE
            SET
                schema_version = EXCLUDED.schema_version,
                game_state     = player_data.game_state || EXCLUDED.game_state,
                updated_at     = NOW()
        `;

        return res.status(200).json({ success: true, rejects: rejects.length ? rejects : undefined });
    } catch (err) {
        console.error('[save] DB error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }
};

// =============================================================================
//  BUILT-TO-FLIP SEAM (WO-120 §3 — DO NOT IMPLEMENT NOW; documented only)
// -----------------------------------------------------------------------------
//  Today: soft currency is CLIENT-OWNED; the guards above are anti-grief only.
//  The economy flips to FULLY SERVER-AUTHORITATIVE the moment currency buys real
//  value (crypto/Solana or IAP goes live). At that point:
//    • The server STOPS trusting client-sent balances — it derives them from
//      authoritative events (wave rewards computed server-side, verified IAP
//      receipts, on-chain entitlements re-fetched from an RPC and made idempotent
//      on tx_hash) and writes the result itself.
//    • The save path becomes: accept gameplay EVENTS, not balances; recompute the
//      wallet; persist. The wallet columns become read-only to the client.
//    • The auth gate (above) stays AS-IS — it already proves wallet ownership.
//      The guards below become hard rejects (4xx) rather than field-strips.
//  The current shape is forward-compatible: save_blobs/player_data is already
//  server-side, best_wave already server-wins, and the nonce auth already binds
//  the writer to the wallet. Flipping is a save.js policy change, not a redesign.
// =============================================================================

// ── Telemetry: record a rejected/forged write for audit (best-effort) ──────
async function logReject(sql, playerId, eventName, properties) {
    try {
        await sql`
            INSERT INTO analytics_events (player_id, event_name, properties, client_ts)
            VALUES (
                ${playerId != null ? String(playerId) : 'anonymous'},
                ${eventName},
                ${JSON.stringify(properties || {})}::jsonb,
                NULL
            )
        `;
    } catch (err) {
        // Never let audit logging fail the request.
        console.warn('[save] reject-log insert failed:', err.message);
    }
}

// ── Normalize the client payload into the flat PascalCase shape the delta ──
// builder reads. The LIVE client (GameStateService.SendDelta) posts a FULL
// camelCase snapshot with a nested "resources" object; the legacy MsgPack path
// posted flat PascalCase fields. Accept BOTH so the guards + delta see a single
// consistent shape regardless of which client built the body.
function normalizeDeltaFields(body) {
    const f = { ...body };
    const res = body.resources || body.Resources || null;
    const num = (x) => (x != null ? Number(x) : undefined);

    // Resources: prefer flat PascalCase (legacy), else nested camelCase (live).
    if (f.Crystals   == null) f.Crystals   = num(res ? res.crystals   : body.crystals);
    if (f.Food       == null) f.Food       = num(res ? res.food       : body.food);
    if (f.Coins      == null) f.Coins      = num(res ? res.coins      : body.coins);
    if (f.Voidshards == null) f.Voidshards = num(body.voidshards ?? body.Voidshards);
    if (f.Stone      == null) f.Stone      = num(body.stone  ?? body.Stone);
    if (f.Iron       == null) f.Iron       = num(body.iron   ?? body.Iron);
    if (f.Wood       == null) f.Wood       = num(body.wood   ?? body.Wood);

    if (f.BestWave == null) f.BestWave = num(body.bestWave ?? body.BestWave);

    if (f.Towers         == null) f.Towers         = body.towers         ?? body.Towers;
    if (f.TowerAbilities == null) f.TowerAbilities = body.towerAbilities ?? body.TowerAbilities;

    // Pets: the live client sends arrays under camelCase keys; the legacy path
    // sends pre-serialized JSON strings under *Json keys. Carry whichever exists.
    if (f.PetsJson == null && body.pets != null) {
        f.PetsJson = typeof body.pets === 'string' ? body.pets : JSON.stringify(body.pets);
    }
    if (f.OwnedPetsJson == null && body.ownedPets != null) {
        f.OwnedPetsJson = typeof body.ownedPets === 'string' ? body.ownedPets : JSON.stringify(body.ownedPets);
    }
    if (f.StarterPetId == null) f.StarterPetId = body.starterPetId ?? body.StarterPetId;

    return f;
}

// ── Utility: collect raw request body into a Buffer ────────────────────────
function readBody(req) {
    return new Promise((resolve, reject) => {
        const chunks = [];
        req.on('data', chunk => chunks.push(chunk));
        req.on('end',  ()    => resolve(Buffer.concat(chunks)));
        req.on('error', err  => reject(err));
    });
}
