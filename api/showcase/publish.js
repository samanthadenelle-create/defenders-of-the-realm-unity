'use strict';

const { neon } = require('@neondatabase/serverless');
const { verifySession, WALLET_MAX_BODY_BYTES } = require('../_lib/wallet-auth');
const { applyCors, newRef, quietFail, readBodyExact } = require('../_lib/http');
const showcase = require('../_lib/town-showcase');

function makeHandler(deps = {}) {
    const getSql = deps.getSql || (() => neon(process.env.DATABASE_URL));
    const authenticate = deps.verifySession || verifySession;
    const makeId = deps.opaqueId || showcase.opaqueId;
    return async function handler(req, res) {
        if (applyCors(req, res, 'POST, OPTIONS')) return;
        const ref = newRef();
        if (req.method !== 'POST') return quietFail(res, 400, 'METHOD_NOT_ALLOWED', ref);
        let body;
        try {
            const raw = (await readBodyExact(req, WALLET_MAX_BODY_BYTES)).buffer;
            body = JSON.parse(raw.toString('utf8'));
        } catch (_) { return quietFail(res, 400, 'BAD_PAYLOAD', ref); }

        const playerId = body && typeof body.playerId === 'string' ? body.playerId.trim() : '';
        if (!playerId) return quietFail(res, 400, 'BAD_PAYLOAD', ref);
        const checked = showcase.validatePublishBody(body);
        if (!checked.ok) return quietFail(res, 400, checked.code, ref);

        let sql;
        try { sql = getSql(); }
        catch (_) { return quietFail(res, 500, 'SERVER_ERROR', ref); }
        let auth;
        try { auth = await authenticate(sql, String(req.headers['x-session'] || ''), playerId); }
        catch (_) { return quietFail(res, 500, 'SERVER_ERROR', ref); }
        if (!auth.ok) return quietFail(res, 401, auth.code || 'AUTH_REQUIRED', ref);

        const snapshotId = makeId('sh_');
        const publicOwnerId = makeId('po_');
        const value = checked.value;
        try {
            // A public snapshot can request cosmetic/achievement presentation, but it cannot
            // manufacture ownership. Prove every requested reward against server authority
            // before persisting the immutable public version.
            const proofRows = await sql`
                SELECT
                    ARRAY(SELECT DISTINCT e.sku FROM sku_entitlements e
                          WHERE e.wallet = ${playerId} AND e.state = 'active'
                            AND (e.expires_at IS NULL OR e.expires_at > NOW())
                            AND e.sku = ANY(${value.equippedCosmeticSkus}::text[])
                          ORDER BY e.sku) AS owned_cosmetics,
                    ARRAY(SELECT DISTINCT a.achievement_id FROM achievement_grants a
                          WHERE a.wallet = ${playerId}
                            AND a.achievement_id = ANY(${value.publicAchievementSkus}::text[])
                          ORDER BY a.achievement_id) AS owned_achievements
            `;
            const proof = proofRows && proofRows[0];
            const same = (claimed, owned) => {
                const a = [...claimed].sort();
                const b = Array.isArray(owned) ? [...owned].sort() : [];
                return a.length === b.length && a.every((sku, index) => sku === b[index]);
            };
            if (!proof || !same(value.equippedCosmeticSkus, proof.owned_cosmetics) ||
                !same(value.publicAchievementSkus, proof.owned_achievements))
                return quietFail(res, 403, 'PUBLIC_PROFILE_NOT_OWNED', ref);

            const rows = await sql`
                WITH advanced AS (
                    INSERT INTO public_town_showcases
                        (owner_wallet, showcase_id, public_owner_id, current_version,
                         published, published_at, updated_at)
                    VALUES (${playerId}, ${snapshotId}, ${publicOwnerId}, 1, TRUE, NOW(), NOW())
                    ON CONFLICT (owner_wallet) DO UPDATE SET
                        current_version = public_town_showcases.current_version + 1,
                        published = TRUE, published_at = NOW(), updated_at = NOW()
                    RETURNING owner_wallet, showcase_id, public_owner_id, current_version
                ), inserted AS (
                    INSERT INTO public_town_snapshot_versions
                        (owner_wallet, showcase_id, snapshot_version, schema_version, catalog_version,
                         minimum_client_version, structures, equipped_cosmetic_skus,
                         public_hero_lineup, public_army_lineup, selected_echoes, echoes_saved,
                         banner_sku, title_sku, town_level, public_achievement_skus, leaderboard_rank)
                    SELECT owner_wallet, showcase_id, current_version, ${value.schemaVersion},
                           ${value.catalogVersion}, ${value.minimumClientVersion},
                           ${JSON.stringify(value.structures)}::jsonb,
                           ${JSON.stringify(value.equippedCosmeticSkus)}::jsonb,
                           ${JSON.stringify(value.publicHeroLineup)}::jsonb,
                           ${JSON.stringify(value.publicArmyLineup)}::jsonb,
                           ${JSON.stringify(value.selectedEchoes)}::jsonb, ${value.echoesSaved},
                           ${value.bannerSku}, ${value.titleSku}, ${value.townLevel},
                           ${JSON.stringify(value.publicAchievementSkus)}::jsonb,
                           (SELECT r.rank::integer FROM (
                                SELECT s.wallet, ROW_NUMBER() OVER (
                                    ORDER BY s.score DESC, s.updated_at ASC, s.wallet ASC
                                ) AS rank
                                FROM leaderboard_scores s
                                WHERE s.metric = 'highest_wave' AND s.period_id = 'alltime'
                            ) r WHERE r.wallet = advanced.owner_wallet LIMIT 1)
                    FROM advanced
                    RETURNING owner_wallet, snapshot_version, schema_version, catalog_version,
                              minimum_client_version, structures, equipped_cosmetic_skus,
                              public_hero_lineup, public_army_lineup, selected_echoes, echoes_saved,
                              banner_sku, title_sku, town_level, public_achievement_skus,
                              leaderboard_rank
                )
                SELECT a.showcase_id, a.public_owner_id, i.snapshot_version, i.schema_version,
                       i.catalog_version, i.minimum_client_version, i.structures,
                       i.equipped_cosmetic_skus, i.public_hero_lineup, i.public_army_lineup,
                       i.selected_echoes, i.echoes_saved, i.banner_sku, i.title_sku,
                       i.town_level, i.public_achievement_skus, i.leaderboard_rank
                FROM advanced a JOIN inserted i USING (owner_wallet)
            `;
            if (!rows || rows.length !== 1) return quietFail(res, 500, 'SERVER_ERROR', ref);
            const snapshot = showcase.mapPublicSnapshot(rows[0]);
            if (!snapshot) return quietFail(res, 500, 'SERVER_ERROR', ref);
            return res.status(200).json({ success: true, snapshot });
        } catch (_) { return quietFail(res, 500, 'SERVER_ERROR', ref); }
    };
}

module.exports = makeHandler();
module.exports.config = { api: { bodyParser: false } };
module.exports._test = { makeHandler };
