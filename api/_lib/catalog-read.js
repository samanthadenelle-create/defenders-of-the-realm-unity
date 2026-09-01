'use strict';

const COLLECTION_ID = /^[a-z0-9][a-z0-9._-]{0,63}$/;
const SKU = /^[a-z0-9][a-z0-9._-]{0,95}$/;
const SHA256 = /^[0-9a-f]{64}$/;
const CONTEXTS = new Set(['build', 'shop', 'owned', 'showcase']);
const ITEM_KINDS = new Set(['building', 'pack', 'tower', 'cosmetic', 'decoration', 'offer']);
const EXPIRY_BEHAVIORS = new Set(['hide', 'lock', 'fallback']);
const DEFINITION_KEYS = new Set([
    'title', 'description', 'purpose', 'icon_key', 'card_art_key', 'state',
    'contents', 'cost', 'tags', 'platform_asset_key',
]);
const VISIBILITY_KEYS = new Set(['requires_entitlement', 'min_level', 'unlock_key']);
const PRIVATE_KEY = /(wallet|signature|nonce|token|secret|promo.?code|request.?body)/i;

class CatalogError extends Error {
    constructor(code, message) { super(message); this.code = code; }
}

function parseVersion(raw, required) {
    if (raw == null || String(raw).trim() === '') {
        if (required) throw new CatalogError('CLIENT_VERSION_REQUIRED', 'clientVersion is required');
        return null;
    }
    const match = String(raw).trim().match(/^(\d+)\.(\d+)\.(\d+)(?:[-+][0-9A-Za-z.-]+)?$/);
    if (!match) throw new CatalogError('CLIENT_VERSION_INVALID', 'clientVersion must be semantic x.y.z');
    return match.slice(1, 4).map(Number);
}

function versionAllows(client, minimum) {
    if (!minimum) return true;
    const need = parseVersion(minimum, true);
    for (let i = 0; i < 3; i++) {
        if (client[i] !== need[i]) return client[i] > need[i];
    }
    return true;
}

function safeHttpsUrl(raw) {
    if (raw == null) return null;
    let url;
    try { url = new URL(String(raw)); } catch (_) { throw new CatalogError('CATALOG_INVALID', 'invalid asset URL'); }
    if (url.protocol !== 'https:' || url.username || url.password || url.hash) {
        throw new CatalogError('CATALOG_INVALID', 'asset URL must be credential-free HTTPS');
    }
    return url.toString();
}

function plainObject(value, label) {
    if (value == null) return {};
    if (typeof value !== 'object' || Array.isArray(value)) {
        throw new CatalogError('CATALOG_INVALID', label + ' must be an object');
    }
    return value;
}

function boundedPublicObject(value, allowed, label) {
    const input = plainObject(value, label);
    const out = {};
    for (const key of Object.keys(input)) {
        if (!allowed.has(key)) throw new CatalogError('CATALOG_INVALID', label + ' contains unsupported key');
        const encoded = JSON.stringify(input[key]);
        if (encoded === undefined || encoded.length > 8192) {
            throw new CatalogError('CATALOG_INVALID', label + ' value is too large');
        }
        const inspect = (node, depth) => {
            if (depth > 8) throw new CatalogError('CATALOG_INVALID', label + ' is too deeply nested');
            if (Array.isArray(node)) { for (const child of node) inspect(child, depth + 1); return; }
            if (node && typeof node === 'object') {
                for (const childKey of Object.keys(node)) {
                    if (PRIVATE_KEY.test(childKey)) {
                        throw new CatalogError('CATALOG_INVALID', label + ' contains a private field');
                    }
                    inspect(node[childKey], depth + 1);
                }
            }
        };
        inspect(input[key], 0);
        out[key] = input[key];
    }
    return out;
}

function validateRow(row) {
    if (!row || !COLLECTION_ID.test(String(row.collection_id || '')) ||
        !SKU.test(String(row.sku || '')) || !CONTEXTS.has(row.context) ||
        !ITEM_KINDS.has(row.item_kind) || !EXPIRY_BEHAVIORS.has(row.expiry_behavior)) {
        throw new CatalogError('CATALOG_INVALID', 'catalog row has an invalid identity or enum');
    }
    const hasAsset = row.asset_url != null || row.asset_sha256 != null ||
        row.asset_size_bytes != null || row.asset_version != null;
    let asset = null;
    if (hasAsset) {
        if (!SHA256.test(String(row.asset_sha256 || '')) ||
            !Number.isSafeInteger(Number(row.asset_size_bytes)) || Number(row.asset_size_bytes) <= 0 ||
            !Number.isSafeInteger(Number(row.asset_version)) || Number(row.asset_version) <= 0) {
            throw new CatalogError('CATALOG_INVALID', 'remote asset metadata is incomplete');
        }
        asset = { url: safeHttpsUrl(row.asset_url), sha256: row.asset_sha256,
            size_bytes: Number(row.asset_size_bytes), version: Number(row.asset_version) };
    }
    return {
        sku: row.sku, kind: row.item_kind, version: Number(row.item_version),
        definition: boundedPublicObject(row.definition, DEFINITION_KEYS, 'definition'),
        packaged_fallback_key: row.packaged_fallback_key || null,
        fallback_sku: row.fallback_sku || null, expiry_behavior: row.expiry_behavior,
        asset,
        display_order: Number(row.display_order), badge: row.badge || null,
        visibility: boundedPublicObject(row.visibility_rule, VISIBILITY_KEYS, 'visibility_rule'),
    };
}

async function readCollection(sql, input) {
    const id = String(input && input.collectionId || '').trim();
    if (!COLLECTION_ID.test(id)) throw new CatalogError('COLLECTION_ID_INVALID', 'invalid collectionId');
    const client = parseVersion(input && input.clientVersion, true);
    const rows = await sql`
        WITH RECURSIVE chain AS (
            SELECT c.*, 0 AS depth
            FROM catalog_collections c WHERE c.collection_id = ${id}
            UNION ALL
            SELECT fallback.*, chain.depth + 1
            FROM chain
            JOIN catalog_collections fallback
              ON fallback.collection_id = chain.fallback_collection_id
            WHERE chain.depth < 4
        )
        SELECT chain.collection_id, chain.context, chain.title, chain.subtitle,
               chain.icon_key, chain.icon_url, chain.icon_sha256,
               chain.version AS collection_version, chain.active,
               chain.starts_at, chain.ends_at, chain.min_client_version,
               chain.fallback_collection_id, chain.depth,
               member.display_order, member.badge, member.visibility_rule,
               item.sku, item.item_kind, item.definition,
               item.version AS item_version, item.min_client_version AS item_min_client_version,
               item.packaged_fallback_key, item.fallback_sku, item.asset_url,
               item.asset_sha256, item.asset_size_bytes, item.asset_version,
               item.expiry_behavior
        FROM chain
        LEFT JOIN catalog_collection_items member ON member.collection_id = chain.collection_id
        LEFT JOIN catalog_items item ON item.sku = member.sku AND item.active = TRUE
        ORDER BY chain.depth, member.display_order
        LIMIT 500`;
    if (!rows.length) return null;

    const now = Date.now();
    const grouped = new Map();
    for (const row of rows) {
        if (!grouped.has(row.collection_id)) grouped.set(row.collection_id, []);
        grouped.get(row.collection_id).push(row);
    }
    for (const group of grouped.values()) {
        const head = group[0];
        if (!COLLECTION_ID.test(String(head.collection_id || '')) || !CONTEXTS.has(head.context) ||
            !Number.isSafeInteger(Number(head.collection_version)) || Number(head.collection_version) <= 0 ||
            typeof head.title !== 'string' || !head.title.trim() || head.title.length > 160 ||
            (head.subtitle != null && String(head.subtitle).length > 500)) {
            throw new CatalogError('CATALOG_INVALID', 'collection metadata is invalid');
        }
        const scheduled = (!head.starts_at || new Date(head.starts_at).getTime() <= now) &&
            (!head.ends_at || new Date(head.ends_at).getTime() > now);
        if (head.active !== true || !scheduled || !versionAllows(client, head.min_client_version)) continue;
        const items = [];
        for (const row of group) {
            if (!row.sku || !versionAllows(client, row.item_min_client_version)) continue;
            items.push(validateRow(row));
        }
        const iconHasRemote = head.icon_url != null || head.icon_sha256 != null;
        if (iconHasRemote && (!head.icon_url || !SHA256.test(String(head.icon_sha256 || '')))) {
            throw new CatalogError('CATALOG_INVALID', 'collection icon metadata is incomplete');
        }
        return {
            collection_id: head.collection_id, requested_collection_id: id,
            used_fallback: head.collection_id !== id, context: head.context,
            title: head.title, subtitle: head.subtitle || null,
            icon: { key: head.icon_key || null, url: iconHasRemote ? safeHttpsUrl(head.icon_url) : null,
                sha256: iconHasRemote ? head.icon_sha256 : null },
            version: Number(head.collection_version), min_client_version: head.min_client_version || null,
            items,
        };
    }
    return null;
}

module.exports = { CatalogError, parseVersion, readCollection, safeHttpsUrl, versionAllows };
