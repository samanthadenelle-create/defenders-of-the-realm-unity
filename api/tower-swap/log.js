// =============================================================================
// api/tower-swap/log.js — Vercel Serverless Function
// -----------------------------------------------------------------------------
// Append-only audit log for paid instant tower swaps (Solana Pay). Fire-and-
// forget from the client. Deduped on tx_sig so a re-post of the same on-chain
// payment can't be logged twice.
//
// Client : Assets/_Modules/Village/Buildings/TowerSwapService.cs
//          (LogSwapToBackendAsync — fire-and-forget, ignores the response)
//   POST  application/json
//   Body  : { playerId, waveId, fromTower, toTower, currency, costUsdc,
//             txSig, timestamp }
//           - playerId   string  (BoundWallet | "anonymous")
//           - waveId     int
//           - fromTower  string  (display name swapped FROM)
//           - toTower    string  (display name swapped TO)
//           - currency   string  ("Usdc" | "Skr")
//           - costUsdc   number  (flat 2.5)
//           - txSig      string  (Solana tx signature — on-chain proof)
//           - timestamp  long    (unix epoch SECONDS — NOT millis)
//   Reply : { "success": true }   (client ignores; this is fire-and-forget)
//
// Driver: @neondatabase/serverless
// Status codes: 200 | 400 | 500
// =============================================================================

const { neon } = require('@neondatabase/serverless');

module.exports = async (req, res) => {
    if (req.method !== 'POST') {
        return res.status(400).json({ error: 'Method not allowed' });
    }

    let body = req.body;
    try {
        if (typeof body === 'string') body = JSON.parse(body);
    } catch (err) {
        console.error('[tower-swap/log] Body parse error:', err);
        return res.status(400).json({ error: 'Invalid payload' });
    }

    if (!body || typeof body !== 'object') {
        return res.status(400).json({ error: 'Invalid payload' });
    }

    const playerId  = body.playerId  != null ? String(body.playerId)  : 'anonymous';
    const fromTower = body.fromTower != null ? String(body.fromTower) : null;
    const toTower   = body.toTower   != null ? String(body.toTower)   : null;
    const currency  = body.currency  != null ? String(body.currency)  : null;
    const txSig     = body.txSig     != null ? String(body.txSig)     : null;

    const waveIdNum   = body.waveId    != null ? Number(body.waveId)    : null;
    const costUsdcNum = body.costUsdc  != null ? Number(body.costUsdc)  : null;
    const clientTsNum = body.timestamp != null ? Number(body.timestamp) : null;

    const waveId   = Number.isFinite(waveIdNum)   ? waveIdNum   : null;
    const costUsdc = Number.isFinite(costUsdcNum) ? costUsdcNum : null;
    const clientTs = Number.isFinite(clientTsNum) ? clientTsNum : null;

    try {
        const sql = neon(process.env.DATABASE_URL);

        // uq_tower_swaps_tx_sig is a PARTIAL unique index (only non-null sigs).
        // ON CONFLICT must therefore name the column + the same predicate, so a
        // duplicate on-chain payment is silently deduped instead of erroring.
        const rows = await sql`
            INSERT INTO tower_swaps
                (player_id, wave_id, from_tower, to_tower, currency, cost_usdc, tx_sig, client_ts)
            VALUES (
                ${playerId},
                ${waveId},
                ${fromTower},
                ${toTower},
                ${currency},
                ${costUsdc},
                ${txSig},
                ${clientTs}
            )
            ON CONFLICT (tx_sig) WHERE tx_sig IS NOT NULL DO NOTHING
            RETURNING swap_id
        `;

        const deduped = rows.length === 0; // conflict → nothing inserted
        return res.status(200).json({ success: true, deduped });
    } catch (err) {
        console.error('[tower-swap/log] DB error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }
};
