'use strict';

const VALID_SCOPES = new Set(['account', 'associated_data']);
const VALID_CATEGORIES = new Set([
    'cloud_saves',
    'gameplay_analytics',
    'diagnostics',
    'bug_reports',
]);

function normalizeDeletionRequest(body) {
    if (!body || typeof body !== 'object' || Array.isArray(body)) {
        return { ok: false, code: 'DELETION_BAD_PAYLOAD' };
    }

    const playerId = typeof body.playerId === 'string' ? body.playerId.trim() : '';
    if (!playerId) return { ok: false, code: 'PLAYER_ID_MISSING' };

    const scope = typeof body.scope === 'string' ? body.scope.trim() : '';
    if (!VALID_SCOPES.has(scope)) return { ok: false, code: 'DELETION_SCOPE_INVALID' };

    const supplied = body.categories == null ? [] : body.categories;
    if (!Array.isArray(supplied)) return { ok: false, code: 'DELETION_CATEGORIES_INVALID' };

    const categories = [];
    for (const raw of supplied) {
        const category = typeof raw === 'string' ? raw.trim() : '';
        if (!VALID_CATEGORIES.has(category)) {
            return { ok: false, code: 'DELETION_CATEGORIES_INVALID' };
        }
        if (!categories.includes(category)) categories.push(category);
    }
    categories.sort();

    if (scope === 'account' && categories.length !== 0) {
        return { ok: false, code: 'DELETION_CATEGORIES_INVALID' };
    }
    if (scope === 'associated_data' && categories.length === 0) {
        return { ok: false, code: 'DELETION_CATEGORIES_REQUIRED' };
    }

    return { ok: true, playerId, scope, categories };
}

async function createDeletionRequest(sql, request, identityKind) {
    const result = await sql`
        INSERT INTO account_deletion_requests
            (player_id, identity_kind, request_scope, request_categories)
        VALUES
            (${request.playerId}, ${identityKind}, ${request.scope}, ${request.categories})
        ON CONFLICT (player_id) WHERE status IN ('requested', 'in_progress')
        DO UPDATE SET updated_at = account_deletion_requests.updated_at
        RETURNING request_id, status, requested_at
    `;
    const row = result && result.rows ? result.rows[0] : result && result[0];
    if (!row) throw new Error('deletion request insert returned no row');
    return row;
}

module.exports = {
    VALID_CATEGORIES,
    VALID_SCOPES,
    normalizeDeletionRequest,
    createDeletionRequest,
};
