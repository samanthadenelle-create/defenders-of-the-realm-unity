'use strict';

// Pull-based safety net for Google Play voids. This module records durable,
// deduplicated evidence for operator/entitlement-reversal handling. It never
// removes an entitlement or changes google_play_purchases by itself.
const crypto = require('crypto');
const play = require('./google-play-purchases');

const DEFAULT_LOOKBACK_MS = 30 * 24 * 60 * 60 * 1000;
const DEFAULT_OVERLAP_MS = 6 * 60 * 60 * 1000;
const MAX_PAGES = 100;
const PAGE_SIZE = 1000;

function configurationReady(env) {
    const billing = play.configurationReady(env);
    if (!billing.ok) return billing;
    const e = env || {};
    if (String(e.GOOGLE_PLAY_VOIDED_RECONCILIATION_ENABLED || '').toLowerCase() !== 'true')
        return { ok: false, code: 'play_voided_reconciliation_disabled' };
    return { ok: true, credential: billing.credential,
        packageName: String(e.GOOGLE_PLAY_PACKAGE_NAME) };
}

function boundedWindow(lastSuccessEndTime, nowMs, overlapMs) {
    const endTime = Math.floor(Number(nowMs));
    if (!Number.isSafeInteger(endTime) || endTime < 0) throw new Error('invalid clock');
    const overlap = Number.isSafeInteger(overlapMs) && overlapMs >= 0
        ? overlapMs : DEFAULT_OVERLAP_MS;
    const prior = Number(lastSuccessEndTime);
    // The API rejects startTime older than 30 days. Clamp even if a stale or
    // malformed cursor was persisted, and overlap successful windows so a
    // record observed near a boundary is not lost.
    const floor = endTime - DEFAULT_LOOKBACK_MS;
    const startTime = Number.isSafeInteger(prior) && prior >= 0
        ? Math.max(floor, prior - overlap) : floor;
    return { startTime: Math.max(0, startTime), endTime };
}

function voidFingerprint(packageName, item) {
    const stable = [packageName, item.orderId || '', item.purchaseToken || '',
        item.purchaseTimeMillis || '', item.voidedTimeMillis || '', item.voidedSource,
        item.voidedReason, item.voidedQuantity].map(value => String(value == null ? '' : value));
    return crypto.createHash('sha256').update(JSON.stringify(stable)).digest('hex');
}

function normalizeVoidedPurchase(item) {
    const token = String(item && item.purchaseToken || '');
    const orderId = String(item && item.orderId || '');
    const purchaseTime = Number(item && item.purchaseTimeMillis);
    const voidedTime = Number(item && item.voidedTimeMillis);
    const source = Number(item && item.voidedSource);
    const reason = Number(item && item.voidedReason);
    // Google omits voidedQuantity for a full refund. Preserve that distinction:
    // treating an absent value as 1 would understate a fully refunded
    // multi-quantity purchase.
    const quantity = item && item.voidedQuantity != null ? Number(item.voidedQuantity) : null;
    if (!play.TOKEN_RE.test(token) || !orderId || orderId.length > 256 ||
        !Number.isSafeInteger(purchaseTime) ||
        purchaseTime < 0 || !Number.isSafeInteger(voidedTime) || voidedTime < 0 ||
        !Number.isInteger(source) || source < 0 || source > 2 ||
        !Number.isInteger(reason) || reason < 0 || reason > 8 ||
        (quantity != null && (!Number.isInteger(quantity) || quantity < 1)))
        return { ok: false, reason: 'malformed_google_void' };
    return { ok: true, token, orderId, purchaseTime, voidedTime, source, reason, quantity };
}

async function fetchVoidedPage(packageName, accessToken, query, options) {
    const fetchFn = (options && options.fetchFn) || fetch;
    const url = new URL(play.API_ROOT + '/applications/' + encodeURIComponent(packageName) +
        '/purchases/voidedpurchases');
    // The REST reference models these under PageSelection; the wire example
    // flattens them to maxResults/token.
    url.searchParams.set('maxResults', String(PAGE_SIZE));
    url.searchParams.set('type', '1');
    url.searchParams.set('includeQuantityBasedPartialRefund', 'true');
    if (query.pageToken) url.searchParams.set('token', query.pageToken);
    else {
        url.searchParams.set('startTime', String(query.startTime));
        url.searchParams.set('endTime', String(query.endTime));
    }
    const response = await fetchFn(url.toString(), { headers: {
        Authorization: 'Bearer ' + accessToken, Accept: 'application/json' } });
    if (!response.ok) throw Object.assign(new Error('google_voided_purchases_api_rejected'),
        { code: 'play_api_unavailable', status: response.status });
    const payload = await response.json();
    if (!payload || (payload.voidedPurchases != null && !Array.isArray(payload.voidedPurchases)))
        throw new Error('malformed_google_voided_response');
    return { items: payload.voidedPurchases || [], nextPageToken: String(
        payload.tokenPagination && payload.tokenPagination.nextPageToken || '') };
}

async function recordItem(sql, packageName, item) {
    const normalized = normalizeVoidedPurchase(item);
    const fingerprint = voidFingerprint(packageName, item || {});
    if (!normalized.ok) {
        const rows = await sql`INSERT INTO google_play_voided_events
            (event_fingerprint, package_name, status, quarantine_reason, google_payload)
            VALUES (${fingerprint}, ${packageName}, 'quarantined', ${normalized.reason},
                    ${JSON.stringify(item || {})}::jsonb)
            ON CONFLICT (event_fingerprint) DO NOTHING RETURNING event_fingerprint`;
        return { inserted: !!(rows && rows.length), quarantined: true, reason: normalized.reason };
    }
    const known = await sql`SELECT purchase_token FROM google_play_purchases
        WHERE purchase_token = ${normalized.token} AND package_name = ${packageName} LIMIT 1`;
    const quarantineReason = known && known.length
        ? 'entitlement_reversal_required' : 'purchase_token_not_found';
    const rows = await sql`INSERT INTO google_play_voided_events
        (event_fingerprint, package_name, purchase_token, google_order_id,
         purchase_time, voided_time, voided_source, voided_reason, voided_quantity,
         status, quarantine_reason, google_payload)
        VALUES (${fingerprint}, ${packageName}, ${normalized.token}, ${normalized.orderId},
                ${new Date(normalized.purchaseTime).toISOString()},
                ${new Date(normalized.voidedTime).toISOString()}, ${normalized.source},
                ${normalized.reason}, ${normalized.quantity}, 'quarantined',
                ${quarantineReason}, ${JSON.stringify(item)}::jsonb)
        ON CONFLICT (event_fingerprint) DO NOTHING RETURNING event_fingerprint`;
    return { inserted: !!(rows && rows.length), quarantined: true, reason: quarantineReason };
}

async function reconcile(sql, configured, options) {
    const deps = Object.assign({ serviceAccountAccessToken: play.serviceAccountAccessToken,
        fetchVoidedPage }, options || {});
    const nowMs = Number.isSafeInteger(deps.nowMs) ? deps.nowMs : Date.now();
    const cursors = await sql`SELECT last_success_end_time_ms FROM google_play_voided_cursors
        WHERE package_name = ${configured.packageName} LIMIT 1`;
    const window = boundedWindow(cursors && cursors[0] &&
        Number(cursors[0].last_success_end_time_ms), nowMs, deps.overlapMs);
    const accessToken = await deps.serviceAccountAccessToken(configured.credential, deps);
    let pageToken = '';
    let pages = 0;
    let observed = 0;
    let inserted = 0;
    do {
        if (++pages > MAX_PAGES) throw new Error('voided_reconciliation_page_limit');
        const page = await deps.fetchVoidedPage(configured.packageName, accessToken,
            { ...window, pageToken }, deps);
        for (const item of page.items) {
            observed++;
            const result = await recordItem(sql, configured.packageName, item);
            if (result.inserted) inserted++;
        }
        pageToken = page.nextPageToken;
    } while (pageToken);
    // Advance only after every page and event write succeeded. The next run
    // overlaps this boundary; event fingerprints make re-observation harmless.
    await sql`INSERT INTO google_play_voided_cursors
        (package_name, last_success_start_time_ms, last_success_end_time_ms, last_success_at)
        VALUES (${configured.packageName}, ${window.startTime}, ${window.endTime}, NOW())
        ON CONFLICT (package_name) DO UPDATE SET
            last_success_start_time_ms = CASE
                WHEN EXCLUDED.last_success_end_time_ms >= google_play_voided_cursors.last_success_end_time_ms
                THEN EXCLUDED.last_success_start_time_ms
                ELSE google_play_voided_cursors.last_success_start_time_ms END,
            last_success_end_time_ms = GREATEST(
                google_play_voided_cursors.last_success_end_time_ms,
                EXCLUDED.last_success_end_time_ms),
            last_success_at = NOW(), updated_at = NOW()`;
    return { ok: true, start_time_ms: window.startTime, end_time_ms: window.endTime,
        pages, observed, inserted, quarantined: observed };
}

module.exports = { DEFAULT_LOOKBACK_MS, DEFAULT_OVERLAP_MS, MAX_PAGES, PAGE_SIZE,
    configurationReady, boundedWindow, voidFingerprint, normalizeVoidedPurchase,
    fetchVoidedPage, recordItem, reconcile };
