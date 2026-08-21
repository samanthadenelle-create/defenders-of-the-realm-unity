// =============================================================================
// api/game/save.js — Vercel Serverless Function
// -----------------------------------------------------------------------------
// Persists a player's save into the player_data JSONB column in Neon Postgres.
//
// WHAT CHANGED 2026-08-02 (cloud save was DEAD in both directions):
//   1. AUTH IS NOW TWO RAILS, chosen by the SHAPE of playerId — see
//      _lib/wallet-auth.js. A base58 wallet id still demands a full ed25519
//      signature over the exact raw body plus a single-use nonce (UNCHANGED and
//      unweakened). A "guest-local-<64hex>" id — the id the Unity client already
//      mints for every un-connected player — takes the guest rail: X-Guest-Id,
//      rate-limited, explicitly second-class. Before this there was NO guest
//      path at all, so every "Play as Guest" tester was structurally unable to
//      reach the db, and the front door offers exactly that button.
//   2. EVERY refusal returns a STABLE CODE and writes an audit row. A 401 used
//      to be indistinguishable from no-header / bad-signature / replayed nonce /
//      expired nonce / wrong wallet, and left no server-side trace at all.
//   3. THE WHOLE SNAPSHOT IS STORED. The old delta builder cherry-picked 13
//      keys (resources, towers, bestWave, pets) out of a ~60-field save and
//      silently dropped the rest — base layout, obsidian queue, army, hero
//      level, echo lanes, quests, zones, everything a player would actually
//      mourn. Even with perfect auth, "cloud save" would have restored a husk.
//   4. CORS + preflight, so the WebGL/pinet build can reach this at all (a
//      cross-origin request with X-Wallet triggers an OPTIONS preflight that
//      this function never answered).
//   5. The body parser is genuinely disabled now — see the note at the bottom.
//
// Env vars: DATABASE_URL (Neon). Optional: GUEST_SAVE_ENABLED=false to kill the
// guest rail without a code change.
//
// Status codes: 200 | 400 | 401 | 404 | 500 (project constraint — no others).
// =============================================================================

const { neon } = require('@neondatabase/serverless');
const { decode } = require('@msgpack/msgpack');
const {
    AuthCode, authenticate, isGuestId,
    GUEST_MAX_BODY_BYTES, WALLET_MAX_BODY_BYTES,
} = require('../_lib/wallet-auth');
const { applyCors, newRef, quietFail, readBodyExact } = require('../_lib/http');
const { logAuthReject, logApiEvent } = require('../_lib/audit');

// ── Sanity-check bounds (WO-120 §2 — soft currency stays client-owned, with
//    server guards). Anti-grief / anti-corruption ceilings, NOT a server-
//    authoritative economy (see the BUILT-TO-FLIP seam at the bottom).
const MAX_RESOURCE   = 1_000_000_000; // any single soft-currency balance ceiling
const MAX_BEST_WAVE  = 100_000;       // implausible-wave ceiling
// A save may not DROP a monotonic counter (bestWave) at all, and may not drop a
// spendable balance by more than this fraction in one sync.
const MAX_BALANCE_DROP_FRACTION = 0.95;

// Keys that are transport, not game state — never stored inside game_state.
const RESERVED_KEYS = new Set([
    'playerId', 'PlayerId', 'schemaVersion', 'SchemaVersion',
    'wallet', 'nonce', 'signature', 'guestId',
]);

// The numeric fields the anti-tamper guards police. Balances live in TWO places
// in the live payload — flat top-level (legacy) and nested under "resources"
// (the live PersistedState shape) — and a guard that only checked one of them
// would be trivially bypassed by writing the other.
const GUARDED_BALANCES = ['crystals', 'food', 'coins', 'voidshards', 'stone', 'iron', 'wood'];
const NESTED_BALANCES  = ['crystals', 'food', 'coins'];   // the ResourceBalance struct's own fields

async function handler(req, res) {
    if (applyCors(req, res, 'POST, OPTIONS')) return;

    const ref = newRef();

    if (req.method !== 'POST') {
        return quietFail(res, 400, AuthCode.METHOD_NOT_ALLOWED, ref);
    }

    // ── Read raw body ──────────────────────────────────────────────────────
    // Read at the WALLET ceiling: which rail applies isn't known until playerId
    // has been parsed OUT of this body. The tighter guest cap is applied below,
    // once the identity is known.
    let rawBody, exactBytes;
    try {
        const read = await readBodyExact(req, WALLET_MAX_BODY_BYTES);
        rawBody = read.buffer;
        exactBytes = read.exact;
    } catch (err) {
        if (err && err.code === 'BODY_TOO_LARGE') {
            await logAuthReject(null, req, { code: AuthCode.PAYLOAD_TOO_LARGE, ref, detail: { cap: WALLET_MAX_BODY_BYTES } });
            return quietFail(res, 400, AuthCode.PAYLOAD_TOO_LARGE, ref);
        }
        console.error('[save] Body read error:', err);
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }

    // ── Decode (MessagePack or JSON) ───────────────────────────────────────
    let body;
    try {
        const isMsgPack = req.headers['content-type'] === 'application/x-msgpack';
        body = isMsgPack ? decode(rawBody) : JSON.parse(rawBody.toString('utf8'));
    } catch (err) {
        console.error('[save] Decode error:', err.message, 'bytes=', rawBody ? rawBody.length : 0);
        await logAuthReject(null, req, {
            code: AuthCode.BAD_PAYLOAD, ref,
            detail: { bytes: rawBody ? rawBody.length : 0, exactBytes: exactBytes },
        });
        return quietFail(res, 400, AuthCode.BAD_PAYLOAD, ref);
    }
    if (!body || typeof body !== 'object') {
        return quietFail(res, 400, AuthCode.BAD_PAYLOAD, ref);
    }

    // The client posts camelCase "playerId"; the legacy MsgPack path used
    // "PlayerId". Accept either.
    const playerId = body.PlayerId != null ? body.PlayerId
                   : body.playerId != null ? body.playerId
                   : null;
    const schemaVersion = body.SchemaVersion != null ? body.SchemaVersion : body.schemaVersion;

    let sql;
    try {
        sql = neon(process.env.DATABASE_URL);
    } catch (err) {
        console.error('[save] DB init error:', err);
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }

    if (!playerId) {
        await logAuthReject(sql, req, { code: AuthCode.PLAYER_ID_MISSING, ref, detail: { keys: Object.keys(body).length } });
        return quietFail(res, 400, AuthCode.PLAYER_ID_MISSING, ref);
    }

    // Guest bodies get the tighter cap now that we know the rail.
    if (isGuestId(String(playerId)) && rawBody.length > GUEST_MAX_BODY_BYTES) {
        await logAuthReject(sql, req, {
            code: AuthCode.PAYLOAD_TOO_LARGE, ref, identity: playerId, mode: 'guest',
            detail: { bytes: rawBody.length, cap: GUEST_MAX_BODY_BYTES },
        });
        return quietFail(res, 400, AuthCode.PAYLOAD_TOO_LARGE, ref);
    }

    // A wallet signature is over the EXACT raw bytes. If the runtime already
    // parsed and we had to re-serialise, verification is IMPOSSIBLE — say that
    // precisely instead of emitting a lying AUTH_BAD_SIGNATURE.
    if (!exactBytes && !isGuestId(String(playerId))) {
        await logAuthReject(sql, req, {
            code: AuthCode.SERVER_ERROR, ref, identity: playerId, mode: 'wallet',
            detail: { reason: 'raw_body_unavailable_bodyparser_active' },
        });
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }

    // ── AUTH GATE ──────────────────────────────────────────────────────────
    let auth;
    try {
        auth = await authenticate(sql, req, rawBody, playerId);
    } catch (err) {
        console.error('[save] Auth check error:', err);
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }
    if (!auth.ok) {
        await logAuthReject(sql, req, {
            code: auth.code, ref, identity: auth.identity, mode: auth.mode, detail: auth.detail,
        });
        // 400 for a shape/argument problem, 401 for a genuine authorization
        // refusal — the client can retry the second (fetch a fresh nonce) and
        // must never retry the first.
        const status = (auth.code === AuthCode.PLAYER_ID_BAD_SHAPE ||
                        auth.code === AuthCode.PLAYER_ID_MISSING ||
                        auth.code === AuthCode.WALLET_MALFORMED) ? 400 : 401;
        return quietFail(res, status, auth.code, ref);
    }

    // ── Build the state to persist ─────────────────────────────────────────
    const delta = buildState(body);

    if (Object.keys(delta).length === 0) {
        return res.status(200).json({ ok: true, success: true, serverNowMs: Date.now(), note: 'empty payload — no write', ref: ref });
    }

    // ── SANITY-CHECK GUARDS (WO-120 §2) ────────────────────────────────────
    let prior = {};
    try {
        const priorRows = await sql`
            SELECT game_state FROM player_data WHERE player_id = ${playerId} LIMIT 1
        `;
        if (priorRows.length > 0 && priorRows[0].game_state) prior = priorRows[0].game_state;
    } catch (err) {
        // A read failure shouldn't block the save; skip the comparative guards
        // (the bounds checks below still apply).
        console.warn('[save] prior-state read failed, skipping rollback guards:', err.message);
    }

    const rejects = applyGuards(delta, prior);

    if (rejects.length > 0) {
        await logApiEvent(sql, playerId, 'save_sanity_reject', { rejects: rejects, ref: ref, mode: auth.mode });
    }

    if (Object.keys(delta).length === 0) {
        return res.status(200).json({ ok: true, success: true, serverNowMs: Date.now(), note: 'all fields rejected by guards', rejects, ref });
    }

    // ── Upsert into Neon ───────────────────────────────────────────────────
    try {
        // game_state || EXCLUDED.game_state is a SHALLOW merge of the incoming
        // object onto the stored one. The client posts a FULL snapshot with null
        // fields stripped, so this preserves any key a partial/older client did
        // not send instead of nulling it.
        // The ::jsonb cast is required — Neon's HTTP driver sends parameters as
        // strings.
        await sql`
            INSERT INTO player_data (player_id, schema_version, game_state, trust, updated_at)
            VALUES (
                ${playerId},
                ${schemaVersion ?? 10},
                ${JSON.stringify(delta)}::jsonb,
                ${auth.mode},
                NOW()
            )
            ON CONFLICT (player_id) DO UPDATE
            SET
                schema_version = EXCLUDED.schema_version,
                game_state     = player_data.game_state || EXCLUDED.game_state,
                trust          = EXCLUDED.trust,
                updated_at     = NOW()
        `;

        return res.status(200).json({
            ok: true,
            success: true,
            mode: auth.mode,
            fields: Object.keys(delta).length,
            bytes: rawBody.length,
            rejects: rejects.length ? rejects : undefined,
            ref: ref,
            // WO-912 s7.2: authoritative server time for ServerClock. The save round trip
            // is the most frequent handshake the client makes, so this is the main way the
            // rewarded-ad window stays anchored during a session.
            serverNowMs: Date.now(),
        });
    } catch (err) {
        console.error('[save] DB error:', err);
        await logAuthReject(sql, req, {
            code: AuthCode.SERVER_ERROR, ref, identity: playerId, mode: auth.mode,
            detail: { stage: 'upsert', message: String(err.message || err).slice(0, 300) },
        });
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }
}

// =============================================================================
//  State assembly
// =============================================================================

/**
 * Assemble what actually gets stored.
 *
 * STORE EVERYTHING the client sent (minus transport keys), THEN normalise the
 * handful of legacy PascalCase / *Json fields onto their camelCase homes. The
 * old implementation did only the second half, against a 13-key whitelist, which
 * is why a full ~60-field save round-tripped as a husk: the client posts the
 * complete PersistedState snapshot and 47 fields of it hit the floor.
 *
 * The client's own load path deserialises `data` straight into
 * SaveSchema.PersistedState, so storing the snapshot verbatim is exactly the
 * shape it wants back — no translation layer, nothing to drift.
 */
function buildState(body) {
    const delta = {};

    // 1. Everything camelCase the client sent (the live PersistedState snapshot).
    //    Null/undefined are skipped: the client strips nulls, and a null here
    //    would blank a good server value on a partial sync.
    for (const key of Object.keys(body)) {
        if (RESERVED_KEYS.has(key)) continue;
        const v = body[key];
        if (v === null || v === undefined) continue;
        // Legacy PascalCase fields are handled by the promotion pass below; do
        // not store both spellings.
        if (/^[A-Z]/.test(key)) continue;
        delta[key] = v;
    }

    // 2. Legacy/alternate spellings promoted onto the canonical camelCase keys.
    const f = normalizeDeltaFields(body);

    if (f.Crystals   != null) delta.crystals   = f.Crystals;
    if (f.Food       != null) delta.food       = f.Food;
    if (f.Coins      != null) delta.coins      = f.Coins;
    if (f.Voidshards != null) delta.voidshards = f.Voidshards;
    if (f.Stone      != null) delta.stone      = f.Stone;
    if (f.Iron       != null) delta.iron       = f.Iron;
    if (f.Wood       != null) delta.wood       = f.Wood;

    if (f.Towers         != null) delta.towers         = f.Towers;
    if (f.TowerAbilities != null) delta.towerAbilities = f.TowerAbilities;
    if (f.BestWave       != null) delta.bestWave       = f.BestWave;

    // Pets arrive as pre-serialized JSON strings on the legacy path — parse so
    // Postgres stores real JSONB rather than a string containing JSON.
    if (f.PetsJson) {
        try { delta.pets = typeof f.PetsJson === 'string' ? JSON.parse(f.PetsJson) : f.PetsJson; }
        catch { /* skip malformed */ }
    }
    if (f.OwnedPetsJson) {
        try { delta.ownedPets = typeof f.OwnedPetsJson === 'string' ? JSON.parse(f.OwnedPetsJson) : f.OwnedPetsJson; }
        catch { /* skip malformed */ }
    }
    if (f.StarterPetId != null) delta.starterPetId = f.StarterPetId;

    return delta;
}

/**
 * Bounds + anti-rollback + anti-wipe. Rejected fields are STRIPPED (the rest of
 * the save still lands) and returned for the audit row.
 *
 * Guards BOTH the flat key and the nested "resources" object — they are two
 * spellings of the same balance and the client reads the nested one back, so
 * guarding only the flat copy would leave the guard trivially bypassable.
 *
 * FLAT vs NESTED REJECTION ARE NOT THE SAME OPERATION, and conflating them eats
 * the balance you were protecting. The upsert merges SHALLOWLY
 * (game_state || EXCLUDED.game_state), so:
 *   • deleting a FLAT key leaves the stored top-level key untouched — correct;
 *   • deleting a NESTED key still replaces the ENTIRE stored "resources" object
 *     with the incoming one, so the key vanishes instead of keeping its old
 *     value. A tamper attempt would therefore succeed at zeroing the balance by
 *     way of the very guard meant to stop it.
 * So a rejected nested field is RESTORED to the prior server value rather than
 * deleted. (Caught by the endpoint harness, 2026-08-02 — the first draft of this
 * function deleted both and wiped 500 crystals it had just refused to lower.)
 */
function applyGuards(delta, prior) {
    const rejects = [];
    const nested = (delta.resources && typeof delta.resources === 'object') ? delta.resources : null;
    const priorNested = (prior && prior.resources && typeof prior.resources === 'object') ? prior.resources : null;

    // (a) bestWave — a monotonic high-water mark the server already wins on.
    if (delta.bestWave != null) {
        const bw = Number(delta.bestWave);
        if (!Number.isFinite(bw) || bw < 0 || bw > MAX_BEST_WAVE) {
            rejects.push({ field: 'bestWave', value: delta.bestWave, rule: 'out_of_bounds' });
            delete delta.bestWave;
        } else if (prior.bestWave != null && bw < Number(prior.bestWave)) {
            rejects.push({ field: 'bestWave', value: bw, prior: prior.bestWave, rule: 'rollback' });
            delete delta.bestWave;   // never lower the high-water mark
        }
    }

    // (b) Bounds + anti-wipe on each balance, flat and nested.
    for (const key of GUARDED_BALANCES) {
        const isNestedField = NESTED_BALANCES.includes(key);
        const incoming = delta[key] != null ? delta[key]
                       : (isNestedField && nested && nested[key] != null) ? nested[key]
                       : null;
        if (incoming == null) continue;

        const priorFlat = prior[key] != null ? Number(prior[key])
                        : (isNestedField && priorNested && priorNested[key] != null) ? Number(priorNested[key])
                        : null;

        const strip = (rule, extra) => {
            rejects.push(Object.assign({ field: key, value: incoming, rule: rule }, extra || {}));
            delete delta[key];                       // shallow merge keeps the stored top-level key
            if (isNestedField && nested) {
                if (priorFlat != null) nested[key] = priorFlat;   // restore — see the note above
                else delete nested[key];                          // nothing to preserve (first write)
            }
        };

        const v = Number(incoming);
        if (!Number.isFinite(v) || v < 0 || v > MAX_RESOURCE) { strip('out_of_bounds'); continue; }

        if (priorFlat != null && priorFlat > 0 && v < priorFlat * (1 - MAX_BALANCE_DROP_FRACTION)) {
            strip('implausible_drop', { prior: priorFlat });
        }
    }

    return rejects;
}

// ── Normalize the client payload into the flat PascalCase shape the promotion ──
// pass reads. The LIVE client posts a FULL camelCase snapshot with a nested
// "resources" object; the legacy MsgPack path posted flat PascalCase fields.
// Accept BOTH so the guards and the promotion see one consistent shape.
function normalizeDeltaFields(body) {
    const f = { ...body };
    const res = body.resources || body.Resources || null;
    const num = (x) => (x != null ? Number(x) : undefined);

    if (f.Crystals   == null) f.Crystals   = num(res ? res.crystals : body.crystals);
    if (f.Food       == null) f.Food       = num(res ? res.food     : body.food);
    if (f.Coins      == null) f.Coins      = num(res ? res.coins    : body.coins);
    if (f.Voidshards == null) f.Voidshards = num(body.voidshards ?? body.Voidshards);
    if (f.Stone      == null) f.Stone      = num(body.stone  ?? body.Stone);
    if (f.Iron       == null) f.Iron       = num(body.iron   ?? body.Iron);
    if (f.Wood       == null) f.Wood       = num(body.wood   ?? body.Wood);

    if (f.BestWave == null) f.BestWave = num(body.bestWave ?? body.BestWave);

    if (f.Towers         == null) f.Towers         = body.towers         ?? body.Towers;
    if (f.TowerAbilities == null) f.TowerAbilities = body.towerAbilities ?? body.TowerAbilities;

    if (f.PetsJson == null && body.pets != null) {
        f.PetsJson = typeof body.pets === 'string' ? body.pets : JSON.stringify(body.pets);
    }
    if (f.OwnedPetsJson == null && body.ownedPets != null) {
        f.OwnedPetsJson = typeof body.ownedPets === 'string' ? body.ownedPets : JSON.stringify(body.ownedPets);
    }
    if (f.StarterPetId == null) f.StarterPetId = body.starterPetId ?? body.StarterPetId;

    return f;
}

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
//    • The wallet auth rail stays AS-IS — it already proves wallet ownership. The
//      GUEST rail is excluded from that flip by construction: a guest id can never
//      key a wallet row, so it can never hold real value.
//    • The guards below become hard rejects (4xx) rather than field-strips.
// =============================================================================

module.exports = handler;
// MUST be assigned AFTER the handler. `module.exports.config = ...` followed by
// `module.exports = handler` (the original ordering) silently discards the config
// and leaves the runtime body parser ON — which drains the stream the raw-body
// reader needs for signature verification. See _lib/http.readBodyExact.
module.exports.config = { api: { bodyParser: false } };
