'use strict';

const { neon } = require('@neondatabase/serverless');
const { applyCors } = require('../_lib/http');
const { CatalogError, readCollection } = require('../_lib/catalog-read');

module.exports = async (req, res) => {
    if (applyCors(req, res, 'GET, OPTIONS')) return;
    res.setHeader('Cache-Control', 'public, max-age=60, s-maxage=60, stale-while-revalidate=300');
    if (req.method !== 'GET') return res.status(400).json({ success: false, error: 'Method not allowed' });
    try {
        const sql = neon(process.env.DATABASE_URL);
        const q = req.query || {};
        const collection = await readCollection(sql, {
            collectionId: q.collectionId,
            clientVersion: q.clientVersion,
        });
        if (!collection) return res.status(200).json({ success: false, code: 'COLLECTION_UNAVAILABLE' });
        return res.status(200).json({ success: true, serverNowMs: Date.now(), collection });
    } catch (err) {
        if (err instanceof CatalogError) {
            return res.status(400).json({ success: false, code: err.code });
        }
        console.error('[catalog/collection] read failed:', err && err.code);
        return res.status(500).json({ success: false, error: 'Internal server error' });
    }
};
