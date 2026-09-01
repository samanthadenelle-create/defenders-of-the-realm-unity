'use strict';

const crypto = require('node:crypto');

const CURRENT_SCHEMA_VERSION = 2;
const MAX_STRUCTURES = 300;
const MAX_COSMETICS = 16;
const MAX_HEROES = 4;
const MAX_ARMY_UNITS = 12;
const MAX_ECHOES = 4;
const MAX_ACHIEVEMENTS = 32;
const MIN_CELL = -256;
const MAX_CELL = 256;
const MAX_LEVEL = 20;
const MAX_PUBLIC_LEVEL = 1000;
const MAX_PUBLIC_COUNT = 1000000;
const TOP_KEYS = new Set([
    'playerId', 'schemaVersion', 'catalogVersion', 'minimumClientVersion',
    'publishRequested', 'structures', 'equippedCosmeticSkus', 'publicHeroLineup',
    'publicArmyLineup', 'selectedEchoes', 'echoesSaved', 'bannerSku', 'titleSku',
    'townLevel', 'publicAchievementSkus',
]);
const STRUCTURE_KEYS = new Set([
    'itemId', 'cellX', 'cellZ', 'yawSteps', 'level', 'yawOffset', 'worldY', 'wallMounted',
]);
const LEVELLED_SKU_KEYS = new Set(['sku', 'level']);
const ARMY_SKU_KEYS = new Set(['sku', 'level', 'count']);
const CATALOG_ID = /^[a-z0-9][a-z0-9_-]{0,63}$/;
const CLIENT_VERSION = /^[0-9A-Za-z][0-9A-Za-z._-]{0,31}$/;
const SHOWCASE_ID = /^sh_[A-Za-z0-9_-]{16,93}$/;
const PUBLIC_OWNER_ID = /^po_[A-Za-z0-9_-]{16,93}$/;

function ownKeysOnly(value, allowed) {
    return value && typeof value === 'object' && !Array.isArray(value) &&
        Object.keys(value).every((key) => allowed.has(key));
}

function integerBetween(value, min, max) {
    return Number.isInteger(value) && value >= min && value <= max;
}

function finiteBetween(value, min, max) {
    return Number.isFinite(value) && value >= min && value <= max;
}

function validateSku(value, nullable = false) {
    return (nullable && value === null) || (typeof value === 'string' && CATALOG_ID.test(value));
}

function sanitizeSkuList(value, max) {
    if (!Array.isArray(value) || value.length > max) return null;
    const result = [];
    const seen = new Set();
    for (const sku of value) {
        if (!validateSku(sku) || seen.has(sku)) return null;
        seen.add(sku);
        result.push(sku);
    }
    return result;
}

function sanitizeLevelledList(value, max, includeCount) {
    if (!Array.isArray(value) || value.length > max) return null;
    const allowed = includeCount ? ARMY_SKU_KEYS : LEVELLED_SKU_KEYS;
    const result = [];
    const seen = new Set();
    for (const item of value) {
        if (!ownKeysOnly(item, allowed) || !validateSku(item.sku) || seen.has(item.sku) ||
            !integerBetween(item.level, 1, MAX_PUBLIC_LEVEL) ||
            (includeCount && !integerBetween(item.count, 1, MAX_PUBLIC_COUNT))) return null;
        seen.add(item.sku);
        result.push(includeCount
            ? { sku: item.sku, level: item.level, count: item.count }
            : { sku: item.sku, level: item.level });
    }
    return result;
}

function validatePublishBody(body) {
    if (!ownKeysOnly(body, TOP_KEYS)) return { ok: false, code: 'PAYLOAD_FIELDS_INVALID' };
    if (body.publishRequested !== true) return { ok: false, code: 'PUBLICATION_OPT_IN_REQUIRED' };
    if (body.schemaVersion !== CURRENT_SCHEMA_VERSION)
        return { ok: false, code: 'SCHEMA_VERSION_UNSUPPORTED' };
    if (!integerBetween(body.catalogVersion, 1, 2147483647))
        return { ok: false, code: 'CATALOG_VERSION_INVALID' };
    if (typeof body.minimumClientVersion !== 'string' || !CLIENT_VERSION.test(body.minimumClientVersion))
        return { ok: false, code: 'CLIENT_VERSION_INVALID' };
    if (!Array.isArray(body.structures) || body.structures.length > MAX_STRUCTURES)
        return { ok: false, code: 'STRUCTURES_INVALID' };

    const equippedCosmeticSkus = sanitizeSkuList(body.equippedCosmeticSkus, MAX_COSMETICS);
    const publicHeroLineup = sanitizeLevelledList(body.publicHeroLineup, MAX_HEROES, false);
    const publicArmyLineup = sanitizeLevelledList(body.publicArmyLineup, MAX_ARMY_UNITS, true);
    const selectedEchoes = sanitizeLevelledList(body.selectedEchoes, MAX_ECHOES, false);
    const publicAchievementSkus = sanitizeSkuList(body.publicAchievementSkus, MAX_ACHIEVEMENTS);
    if (!equippedCosmeticSkus || !publicHeroLineup || !publicArmyLineup || !selectedEchoes ||
        !publicAchievementSkus) return { ok: false, code: 'PUBLIC_PROFILE_INVALID' };
    if (!integerBetween(body.echoesSaved, 0, MAX_PUBLIC_COUNT) ||
        !validateSku(body.bannerSku, true) || !validateSku(body.titleSku, true) ||
        !integerBetween(body.townLevel, 1, MAX_PUBLIC_LEVEL))
        return { ok: false, code: 'PUBLIC_PROFILE_INVALID' };

    const structures = [];
    for (let i = 0; i < body.structures.length; i += 1) {
        const item = body.structures[i];
        if (!ownKeysOnly(item, STRUCTURE_KEYS) || typeof item.itemId !== 'string' ||
            !CATALOG_ID.test(item.itemId) || !integerBetween(item.cellX, MIN_CELL, MAX_CELL) ||
            !integerBetween(item.cellZ, MIN_CELL, MAX_CELL) || !integerBetween(item.yawSteps, 0, 3) ||
            !integerBetween(item.level, 1, MAX_LEVEL) ||
            !finiteBetween(item.yawOffset, -180, 180) || !finiteBetween(item.worldY, -20, 100) ||
            typeof item.wallMounted !== 'boolean') {
            return { ok: false, code: 'STRUCTURE_INVALID', index: i };
        }
        structures.push({
            itemId: item.itemId, cellX: item.cellX, cellZ: item.cellZ,
            yawSteps: item.yawSteps, level: item.level, yawOffset: item.yawOffset,
            worldY: item.worldY, wallMounted: item.wallMounted,
        });
    }
    return { ok: true, value: {
        schemaVersion: body.schemaVersion,
        catalogVersion: body.catalogVersion,
        minimumClientVersion: body.minimumClientVersion,
        structures, equippedCosmeticSkus, publicHeroLineup, publicArmyLineup, selectedEchoes,
        echoesSaved: body.echoesSaved, bannerSku: body.bannerSku, titleSku: body.titleSku,
        townLevel: body.townLevel, publicAchievementSkus,
    } };
}

function opaqueId(prefix, randomBytes = crypto.randomBytes) {
    return prefix + randomBytes(18).toString('base64url');
}

function mapPublicSnapshot(row) {
    if (!row) return null;
    const schemaVersion = Number(row.schema_version);
    // V1 snapshots remain readable as layout-only snapshots. V2 is the only accepted publish
    // shape and all profile fields below are explicit, bounded allowlists.
    const v1 = schemaVersion === 1;
    const leaderboardRank = v1 || row.leaderboard_rank == null ? null : Number(row.leaderboard_rank);
    const checked = validatePublishBody({
        playerId: 'internal-only',
        schemaVersion: CURRENT_SCHEMA_VERSION,
        catalogVersion: Number(row.catalog_version),
        minimumClientVersion: row.minimum_client_version,
        publishRequested: true,
        structures: row.structures,
        equippedCosmeticSkus: v1 ? [] : row.equipped_cosmetic_skus,
        publicHeroLineup: v1 ? [] : row.public_hero_lineup,
        publicArmyLineup: v1 ? [] : row.public_army_lineup,
        selectedEchoes: v1 ? [] : row.selected_echoes,
        echoesSaved: v1 ? 0 : Number(row.echoes_saved),
        bannerSku: v1 ? null : row.banner_sku,
        titleSku: v1 ? null : row.title_sku,
        townLevel: v1 ? 1 : Number(row.town_level),
        publicAchievementSkus: v1 ? [] : row.public_achievement_skus,
    });
    if (!SHOWCASE_ID.test(String(row.showcase_id || '')) ||
        !PUBLIC_OWNER_ID.test(String(row.public_owner_id || '')) ||
        !Number.isSafeInteger(Number(row.snapshot_version)) || Number(row.snapshot_version) < 1 ||
        !(leaderboardRank === null || integerBetween(leaderboardRank, 1, MAX_PUBLIC_COUNT)) ||
        !checked.ok) return null;
    return {
        schemaVersion,
        snapshotId: row.showcase_id,
        publicOwnerId: row.public_owner_id,
        snapshotVersion: Number(row.snapshot_version),
        catalogVersion: checked.value.catalogVersion,
        minimumClientVersion: checked.value.minimumClientVersion,
        publishRequested: true,
        structures: checked.value.structures,
        equippedCosmeticSkus: checked.value.equippedCosmeticSkus,
        publicHeroLineup: checked.value.publicHeroLineup,
        publicArmyLineup: checked.value.publicArmyLineup,
        selectedEchoes: checked.value.selectedEchoes,
        echoesSaved: checked.value.echoesSaved,
        bannerSku: checked.value.bannerSku,
        titleSku: checked.value.titleSku,
        townLevel: checked.value.townLevel,
        publicAchievementSkus: checked.value.publicAchievementSkus,
        leaderboardRank,
    };
}

module.exports = {
    CURRENT_SCHEMA_VERSION, MAX_STRUCTURES, MAX_COSMETICS, MAX_HEROES, MAX_ARMY_UNITS, MAX_ECHOES,
    MAX_ACHIEVEMENTS, MIN_CELL, MAX_CELL, MAX_LEVEL, MAX_PUBLIC_LEVEL, MAX_PUBLIC_COUNT,
    SHOWCASE_ID, PUBLIC_OWNER_ID,
    validatePublishBody, opaqueId, mapPublicSnapshot,
};
