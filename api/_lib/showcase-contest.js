'use strict';

const CONTEST_ID = /^[a-z0-9][a-z0-9_-]{2,63}$/;
const CATEGORY_ID = /^[a-z0-9][a-z0-9_-]{1,31}$/;
const SHOWCASE_ID = /^sh_[A-Za-z0-9_-]{16,93}$/;

function enabled(env = process.env) {
    return String(env.COMMUNITY_SHOWCASE_VOTING_ENABLED || '').toLowerCase() === 'true';
}

function exactBody(body, keys) {
    return body && typeof body === 'object' && !Array.isArray(body) &&
        Object.keys(body).every(k => keys.includes(k)) && keys.every(k => typeof body[k] === 'string');
}

function validateVote(body) {
    if (!exactBody(body, ['playerId', 'contestId', 'categoryId', 'showcaseId'])) return null;
    const value = {
        playerId: body.playerId.trim(), contestId: body.contestId.trim(),
        categoryId: body.categoryId.trim(), showcaseId: body.showcaseId.trim(),
    };
    return value.playerId && CONTEST_ID.test(value.contestId) &&
        CATEGORY_ID.test(value.categoryId) && SHOWCASE_ID.test(value.showcaseId) ? value : null;
}

function validateDiscovery(body) {
    if (!exactBody(body, ['playerId', 'contestId', 'categoryId'])) return null;
    const value = { playerId: body.playerId.trim(), contestId: body.contestId.trim(),
        categoryId: body.categoryId.trim() };
    return value.playerId && CONTEST_ID.test(value.contestId) && CATEGORY_ID.test(value.categoryId)
        ? value : null;
}

module.exports = { CONTEST_ID, CATEGORY_ID, SHOWCASE_ID, enabled, validateDiscovery, validateVote };
