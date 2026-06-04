// =============================================================================
// api/game/load.js — Vercel Serverless Function
// -----------------------------------------------------------------------------
// Returns the stored game_state JSONB for a player. The Unity client calls this
// on scene enter (especially the TowerDefense scene) to merge the authoritative
// server record onto the local GameState ScriptableObject.
//
// AUTH (WO-120 §D): like /api/game/save, load now requires a wallet-signed nonce
// (X-Wallet / X-Nonce / X-Signature) so a public address alone can't read another
// player's save. The signed message for a load (no body) is over the literal
// "load" payload tag — see _lib/wallet-auth.buildSignedMessage.
//
// Driver: @neondatabase/serverless
// Status codes: 200 | 400 | 401 | 404 | 500
// =============================================================================

const { neon } = require('@neondatabase/serverless');
const { verifyAndConsume } = require('../_lib/wallet-auth');

module.exports = async (req, res) => {
    if (req.method !== 'GET') {
        return res.status(400).json({ error: 'Method not allowed' });
    }

    const { playerId } = req.query;
    if (!playerId) {
        return res.status(400).json({ error: 'Missing playerId' });
    }

    let sql;
    try {
        sql = neon(process.env.DATABASE_URL);
    } catch (err) {
        console.error('[load] DB init error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }

    // ── AUTH GATE — verify + burn nonce (no body → "load" payload tag) ──────
    let auth;
    try {
        auth = await verifyAndConsume(sql, req.headers, null, playerId);
    } catch (err) {
        console.error('[load] Auth check error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }
    if (!auth.ok) {
        return res.status(401).json({ error: 'Unauthorized', reason: auth.reason });
    }

    try {
        const rows = await sql`
            SELECT game_state, schema_version, updated_at
            FROM player_data
            WHERE player_id = ${playerId}
            LIMIT 1
        `;

        if (rows.length === 0) {
            return res.status(404).json({ success: false, error: 'Player not found' });
        }

        const row = rows[0];

        // Map JSONB column field names back to C# SaveSchema.PersistedState keys.
        // The Unity client's BackendLoadResponse.Data is deserialized with
        // Newtonsoft.Json using [JsonProperty] attributes, so we emit camelCase
        // to match those attributes.
        const state = row.game_state ?? {};

        return res.status(200).json({
            success: true,
            schemaVersion: row.schema_version,
            updatedAt: row.updated_at,
            data: {
                bestWave:        state.bestWave      ?? null,
                crystals:        state.crystals      ?? null,
                food:            state.food          ?? null,
                coins:           state.coins         ?? null,
                voidshards:      state.voidshards    ?? null,
                stone:           state.stone         ?? null,
                iron:            state.iron          ?? null,
                wood:            state.wood          ?? null,
                towers:          state.towers        ?? null,
                towerAbilities:  state.towerAbilities ?? null,
                pets:            state.pets          ?? null,
                ownedPets:       state.ownedPets     ?? null,
                starterPetId:    state.starterPetId  ?? null,
            }
        });
    } catch (err) {
        console.error('[load] DB error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }
};
