'use strict';

// WO-1282 PIN-1b: was `isWalletId`, which refused every Google Play player with
// PLAYER_ID_BAD_SHAPE — the Play rail could have taken money and then read back an
// empty entitlement list forever. `isProvenValueId` is the ONE authority on "may an id
// of this shape hold real value" (wallet OR play-, never guest) and is the same
// predicate authenticateGranting() enforces, so the grant path and the read path cannot
// drift apart. NO DDL: sku_entitlements.wallet is bare TEXT and keeps its name — it is
// a live key, and it now holds a SUBJECT rather than strictly a wallet address.
const { isProvenValueId } = require('./wallet-auth');

const SOURCE_KINDS = new Set([
    'progression', 'tournament', 'promotion', 'community', 'operator', 'migration',
]);
const SKU = /^[a-z0-9][a-z0-9._-]{0,95}$/;

class EntitlementReadError extends Error {
    constructor(code) { super(code); this.code = code; }
}

function validatePlayerId(raw) {
    const playerId = raw == null ? '' : String(raw).trim();
    if (!isProvenValueId(playerId)) throw new EntitlementReadError('PLAYER_ID_BAD_SHAPE');
    return playerId;
}

function publicEntitlement(row) {
    if (!row || !SKU.test(String(row.sku || '')) ||
        !Number.isSafeInteger(Number(row.quantity)) || Number(row.quantity) <= 0 ||
        !SOURCE_KINDS.has(String(row.source_kind || ''))) {
        throw new EntitlementReadError('ENTITLEMENT_ROW_INVALID');
    }
    return {
        sku: String(row.sku),
        quantity: Number(row.quantity),
        source: String(row.source_kind),
        granted_at: row.granted_at || null,
        expires_at: row.expires_at || null,
    };
}

async function readActiveEntitlements(sql, playerId) {
    const wallet = validatePlayerId(playerId);
    const rows = await sql`
        SELECT sku, quantity, source_kind, granted_at, expires_at
        FROM sku_entitlements
        WHERE wallet = ${wallet}
          AND state = 'active'
          AND (expires_at IS NULL OR expires_at > NOW())
        ORDER BY granted_at, sku
        LIMIT 500`;
    if (!Array.isArray(rows)) throw new EntitlementReadError('ENTITLEMENT_ROWS_INVALID');
    return rows.map(publicEntitlement);
}

module.exports = {
    EntitlementReadError,
    publicEntitlement,
    readActiveEntitlements,
    validatePlayerId,
};
