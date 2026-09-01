'use strict';

const { neon } = require('@neondatabase/serverless');
const showcase = require('../_lib/town-showcase');

function makeHandler(getSql = () => neon(process.env.DATABASE_URL)) {
    return async function handler(req, res) {
        if (req.method !== 'GET') return res.status(400).json({ error: 'Method not allowed' });
        const id = req.query && typeof req.query.id === 'string' ? req.query.id.trim() : '';
        // Malformed, unknown, and unpublished ids are intentionally indistinguishable.
        if (!showcase.SHOWCASE_ID.test(id)) return res.status(404).json({ success: false, error: 'NOT_FOUND' });
        try {
            const sql = getSql();
            const rows = await sql`
                SELECT s.showcase_id, s.public_owner_id, v.snapshot_version, v.schema_version,
                       v.catalog_version, v.minimum_client_version, v.structures,
                       v.equipped_cosmetic_skus, v.public_hero_lineup, v.public_army_lineup,
                       v.selected_echoes, v.echoes_saved, v.banner_sku, v.title_sku,
                       v.town_level, v.public_achievement_skus, v.leaderboard_rank
                FROM public_town_showcases s
                JOIN public_town_snapshot_versions v
                  ON v.owner_wallet = s.owner_wallet AND v.snapshot_version = s.current_version
                WHERE s.showcase_id = ${id} AND s.published = TRUE
                LIMIT 1
            `;
            if (!rows || rows.length !== 1)
                return res.status(404).json({ success: false, error: 'NOT_FOUND' });
            const snapshot = showcase.mapPublicSnapshot(rows[0]);
            if (!snapshot) return res.status(404).json({ success: false, error: 'NOT_FOUND' });
            return res.status(200).json({ success: true, snapshot });
        } catch (_) { return res.status(500).json({ error: 'Internal server error' }); }
    };
}

module.exports = makeHandler();
module.exports._test = { makeHandler };
