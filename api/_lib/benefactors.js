// =============================================================================
// api/_lib/benefactors.js -- the Benefactors of the Realm wall (WO-1073, owner
// ruling 2026-08-27: "we add a benefactors of the Realm wall and they get added
// to that, and every kingdom can see it").
// -----------------------------------------------------------------------------
// WHY THIS IS SERVER CODE AND COULD NEVER HAVE BEEN CLIENT CODE.
// The wall is ONE GLOBAL list read by EVERY kingdom. A client cosmetic can only
// ever show the reading player their own state, and a wall only its owner can
// see is not status at all -- it is the exact defect the ruling exists to fix
// (WO-1175: "a title is a SOCIAL reward: it is worth exactly as many people as
// can see it"). So membership lives in one table and is read through one
// endpoint, and the client is a renderer with no authority.
//
// -- WHAT THE CLIENT MAY NEVER DO, structurally -------------------------------
// Nothing here accepts a tier, an amount, a threshold or an entitlement from a
// caller. setPatronName() takes a WALLET and a NAME; the tier is re-derived from
// _lib/patronage.readLifetimePatronage on every single call, straight out of
// purchase_entitlements. That is the ticket's "granted, never purchased": the
// server flips it, the client is told and celebrates.
//
// -- COSMETIC / STATUS ONLY (WO-1073 section 3.1, oracle-pinned) ---------------
// A wall row carries a NAME and a PLACE IN LINE. There is deliberately no
// quantity, no balance, no currency, no timer and no slot anywhere in this
// module or in anything it returns, so there is no shape a future edit could
// accidentally hang a spendable grant off. test/benefactors.test.js scans this
// file's source for that vocabulary and fails if it appears.
//
// -- $500 FOUNDERS ONLY (ruled, verbatim: "Do NOT list $50 or $150") -----------
// Scarcity is what makes a public list read as an honour rather than a
// subscriber roster. Patron ($50) and High Patron ($150) keep their personal
// cosmetics and never appear here. FOUNDER_TIER_ID is the only tier_id the
// table's CHECK constraint accepts, so the database refuses a widened wall even
// if code one day tried.
//
// -- NO DOLLARS LEAVE THIS MODULE ---------------------------------------------
// WO-1073 section 4: "show the TIER, never the dollar figure publicly". Lifetime
// cents are read, compared to the threshold, and dropped. Nothing returned by
// any function here contains a monetary value, and no wallet address appears in
// any public read.
// =============================================================================

const { PATRONAGE_TIERS, readLifetimePatronage } = require('./patronage');
const {
    MAX_PATRON_NAME_EDITS,
    PatronNameError,
    validatePatronName,
} = require('./patron-name');

// PINNED CONSTANTS, NEVER EXPRESSED RELATIVE TO A MOVING VALUE. Writing this as
// "the last row of PATRONAGE_TIERS" would silently re-point the entire wall the
// day a fourth tier is authored -- and a fourth tier is exactly what the owner's
// evidence gate contemplates ("Do not design a $2,500 whale ladder before you
// know whether you have $500 whales"). The wall is the $500 rung by NAME, not by
// position. assertFounderTierPinned() below proves the pinned pair still matches
// the authored table, so the two cannot drift apart in silence.
const FOUNDER_TIER_ID = 'founder_benefactor';
const FOUNDER_THRESHOLD_USD_CENTS = 50000;

const WALL_DEFAULT_ROWS = 50;
const WALL_MAX_ROWS = 200;

// THE MONUMENT IS BESPOKE, PER PATRON. Owner ruling 2026-08-27, verbatim:
// "being it will be a custom fbx i will work with them one on to create and then
// add in game". So the $500 rung is a COLLABORATION and the asset key lives on
// the PATRON'S ROW, never in a shared catalog constant.
//
// This id is the shared STAND-IN every founder starts on, so the tier can switch
// on before any bespoke art exists. It is a PINNED LITERAL for the same reason
// FOUNDER_TIER_ID is: a placeholder derived from "the first monument in some
// list" would silently re-point every un-collaborated founder the day that list
// changed. It is a CONTRACT STRING shared with the Unity addressable key -- do
// not rename it on one side.
//
// ABSENCE is the only representation of "still on the stand-in": the database
// CHECK forbids STORING this id, so a placeholder can never be spelled two ways.
const PLACEHOLDER_MONUMENT_ASSET_ID = 'monument_founder_standin';

// An addressable key, not a path and not a filename: lower snake, no slashes, no
// extension. The bundle it lands in is content-hashed and is NOT this string.
const MONUMENT_ASSET_ID_RE = /^[a-z][a-z0-9_]{2,63}$/;
const MONUMENT_ASSET_ID_MAX_LEN = 64;

const BenefactorError = Object.freeze({
    NOT_ELIGIBLE: 'PATRONAGE_NOT_ELIGIBLE',
    NOT_ON_WALL: 'PATRON_NOT_ON_WALL',
    MONUMENT_ID_INVALID: 'MONUMENT_ASSET_ID_INVALID',
    MONUMENT_IS_PLACEHOLDER: 'MONUMENT_ASSET_IS_PLACEHOLDER',
    MONUMENT_NOT_PUBLISHED: 'MONUMENT_ASSET_NOT_PUBLISHED',
});

/**
 * The pinned tier id/threshold pair must still name a real authored tier.
 * Called by the oracle; cheap enough to call from an endpoint if ever wanted.
 */
function assertFounderTierPinned() {
    const tier = PATRONAGE_TIERS.find(t => t.id === FOUNDER_TIER_ID);
    if (!tier) throw new Error('patronage tier ' + FOUNDER_TIER_ID + ' is not authored');
    if (tier.thresholdUsdCents !== FOUNDER_THRESHOLD_USD_CENTS)
        throw new Error(
            'founder threshold drift: pinned ' + FOUNDER_THRESHOLD_USD_CENTS +
            ', authored ' + tier.thresholdUsdCents);
    return tier;
}

function clampWallLimit(raw) {
    const v = parseInt(raw, 10);
    if (!Number.isFinite(v)) return WALL_DEFAULT_ROWS;
    return Math.min(WALL_MAX_ROWS, Math.max(1, v));
}

/** granted_at -> 'YYYY-MM-DD'. A founding DATE is honour; a timestamp is a fingerprint. */
function toFoundedDate(value) {
    if (value == null) return null;
    const d = value instanceof Date ? value : new Date(String(value));
    if (Number.isNaN(d.getTime())) return null;
    return d.toISOString().slice(0, 10);
}

/**
 * Which monument does this row actually show?
 *
 * NULL -> the shared stand-in. Stated as a function rather than inline so there
 * is exactly ONE answer to that question and the client never has to guess what
 * an empty column means.
 */
function resolveMonumentAssetId(storedValue) {
    if (typeof storedValue !== 'string') return PLACEHOLDER_MONUMENT_ASSET_ID;
    const trimmed = storedValue.trim();
    return trimmed === '' ? PLACEHOLDER_MONUMENT_ASSET_ID : trimmed;
}

/**
 * THE WALL. Public, unauthenticated, identical for every kingdom.
 *
 * Ordered by granted_at ASC -- founding order, so an early founder never loses
 * their place to a later one, and the ordinal is a fact about WHEN rather than
 * about how much. wallet is used only to break a same-instant tie and is NEVER
 * selected, so it cannot leak through this function by any refactor that leaves
 * the SELECT list intact.
 */
async function readBenefactorWall(sql, limit) {
    if (typeof sql !== 'function') throw new TypeError('sql tagged-template function is required');
    const rows = await sql`
        SELECT patron_name, granted_at, monument_asset_id
        FROM patronage_benefactors
        WHERE tier_id = ${FOUNDER_TIER_ID}
        ORDER BY granted_at ASC, wallet ASC
        LIMIT ${clampWallLimit(limit)}`;
    const list = (rows || []).map((r, i) => Object.freeze({
        ordinal: i + 1,
        patronName: r.patron_name,
        foundedOn: toFoundedDate(r.granted_at),
        // Per patron, never per build: one founder can be standing beside their
        // own bespoke monument while the next is still on the stand-in.
        monumentAssetId: resolveMonumentAssetId(r.monument_asset_id),
        monumentIsBespoke: resolveMonumentAssetId(r.monument_asset_id) !== PLACEHOLDER_MONUMENT_ASSET_ID,
    }));
    return Object.freeze({
        tierId: FOUNDER_TIER_ID,
        count: list.length,
        benefactors: Object.freeze(list),
    });
}

/**
 * One wallet's OWN patronage status. Authenticated callers only -- the endpoint
 * enforces that; this function does not decide who may ask.
 *
 * Returns a tier LABEL and wall state. No cents, ever.
 */
async function readOwnPatronage(sql, wallet) {
    const lifetime = await readLifetimePatronage(sql, wallet);
    const rows = await sql`
        SELECT patron_name, name_edits_used, monument_asset_id
        FROM patronage_benefactors
        WHERE wallet = ${wallet}
        LIMIT 1`;
    const row = rows && rows[0] ? rows[0] : null;
    const used = row ? Number(row.name_edits_used) || 0 : 0;
    const monumentAssetId = resolveMonumentAssetId(row ? row.monument_asset_id : null);
    return Object.freeze({
        tierId: lifetime.tierId,
        tierLabel: lifetime.tierLabel,
        wallEligible: lifetime.lifetimeUsdCents >= FOUNDER_THRESHOLD_USD_CENTS,
        onWall: row != null,
        patronName: row ? row.patron_name : null,
        nameEditsRemaining: Math.max(0, MAX_PATRON_NAME_EDITS - used),
        monumentAssetId: monumentAssetId,
        monumentIsBespoke: monumentAssetId !== PLACEHOLDER_MONUMENT_ASSET_ID,
    });
}

/**
 * Set or edit the public patron name, and -- as a CONSEQUENCE, never as a
 * request -- take the wall place the server has already earned for this wallet.
 *
 * Order matters and is deliberate:
 *   1. validate the NAME before touching the database (a bad name never costs a
 *      query, and never reaches a log line beside a wallet),
 *   2. re-derive the TIER from settled purchases (the flip is the server's),
 *   3. only then write.
 *
 * Re-submitting the identical name is a no-op success and does NOT consume an
 * edit -- a retried request after a dropped response must not burn one of three.
 */
async function setPatronName(sql, wallet, rawName) {
    if (typeof sql !== 'function') throw new TypeError('sql tagged-template function is required');
    if (typeof wallet !== 'string' || wallet.trim() === '')
        throw new TypeError('wallet is required');

    const check = validatePatronName(rawName, { wallet: wallet });
    if (!check.ok) return { ok: false, error: check.error };
    const patronName = check.patronName;

    const lifetime = await readLifetimePatronage(sql, wallet);
    if (lifetime.lifetimeUsdCents < FOUNDER_THRESHOLD_USD_CENTS)
        return { ok: false, error: BenefactorError.NOT_ELIGIBLE };

    const existingRows = await sql`
        SELECT patron_name, name_edits_used
        FROM patronage_benefactors
        WHERE wallet = ${wallet}
        LIMIT 1`;
    const existing = existingRows && existingRows[0] ? existingRows[0] : null;

    if (existing && existing.patron_name === patronName) {
        const used = Number(existing.name_edits_used) || 0;
        return {
            ok: true, patronName: patronName, onWall: true, wasEdit: false,
            nameEditsRemaining: Math.max(0, MAX_PATRON_NAME_EDITS - used),
        };
    }

    if (existing) {
        const used = Number(existing.name_edits_used) || 0;
        if (used >= MAX_PATRON_NAME_EDITS)
            return { ok: false, error: PatronNameError.EDITS_EXHAUSTED };
    }

    try {
        await sql`
            INSERT INTO patronage_benefactors
                (wallet, tier_id, patron_name, name_edits_used, granted_at, updated_at)
            VALUES (${wallet}, ${FOUNDER_TIER_ID}, ${patronName}, 0, NOW(), NOW())
            ON CONFLICT (wallet) DO UPDATE
            SET patron_name     = EXCLUDED.patron_name,
                name_edits_used = patronage_benefactors.name_edits_used + 1,
                name_updated_at = NOW(),
                updated_at      = NOW()`;
    } catch (err) {
        if (err && err.code === '23505') return { ok: false, error: PatronNameError.TAKEN };
        throw err;
    }

    const usedAfter = existing ? (Number(existing.name_edits_used) || 0) + 1 : 0;
    return {
        ok: true, patronName: patronName, onWall: true, wasEdit: existing != null,
        nameEditsRemaining: Math.max(0, MAX_PATRON_NAME_EDITS - usedAfter),
    };
}

/**
 * THE SEAM THE COMMAND CENTER CALLS (WO-1244). Assign one patron their bespoke
 * monument. This is an OPERATOR action performed as each collaboration finishes,
 * which is exactly why it is a function and not a catalog file: there is no
 * moment when the whole set is known.
 *
 * -- WHY THE PRESENCE PROOF IS A REQUIRED ARGUMENT, NOT AN OPTION -------------
 * CLAUDE.md section 16: structure art is served from R2 with NO local fallback,
 * and bundle names are CONTENT-HASHED, so EVERY content build produces new
 * filenames and needs ITS OWN push. A monument that was authored but never
 * pushed installs, launches, plays, and renders as NOTHING with no error on
 * screen. That failure has hit this project THREE times (capsule enemies, an
 * Android build carrying Windows content, and an APK whose enemy bundle was
 * never uploaded), and every one of them was a human expected to remember a
 * second command.
 *
 * So the remedy is not a warning anyone can scroll past, and it is not a flag:
 * this function CANNOT BE CALLED WITHOUT PROOF. `verifyAssetPresent` is a
 * required async probe supplied by the caller -- the console passes the one that
 * asks the shipped catalog/bucket, which is the same question tools/r2-ship.ps1
 * answers with R2_PARITY_OK. Omitting it is a TypeError, not a silent default,
 * because a default here would be a default to "ship it and hope".
 *
 *   verifyAssetPresent(assetId) -> { present: boolean, source: string }
 *
 * A false answer REFUSES and writes nothing. A true answer stamps
 * monument_verified_at, so the proof has a date and the next content build can
 * be asked which proofs it invalidated (monumentsNeedingRepush below).
 *
 * @param {function} sql
 * @param {string} wallet   the patron, who must already be on the wall
 * @param {string} assetId  the addressable key of THEIR bespoke monument
 * @param {{verifyAssetPresent: function}} options
 */
async function assignPatronMonument(sql, wallet, assetId, options) {
    if (typeof sql !== 'function') throw new TypeError('sql tagged-template function is required');
    if (typeof wallet !== 'string' || wallet.trim() === '')
        throw new TypeError('wallet is required');

    const verify = options && options.verifyAssetPresent;
    if (typeof verify !== 'function')
        throw new TypeError(
            'verifyAssetPresent is REQUIRED: an unpublished monument renders as ' +
            'nothing with no error on screen (CLAUDE.md section 16)');

    const id = typeof assetId === 'string' ? assetId.trim() : '';
    if (id === PLACEHOLDER_MONUMENT_ASSET_ID)
        return { ok: false, error: BenefactorError.MONUMENT_IS_PLACEHOLDER };
    if (id.length > MONUMENT_ASSET_ID_MAX_LEN || !MONUMENT_ASSET_ID_RE.test(id))
        return { ok: false, error: BenefactorError.MONUMENT_ID_INVALID };

    const rows = await sql`
        SELECT patron_name
        FROM patronage_benefactors
        WHERE wallet = ${wallet}
        LIMIT 1`;
    if (!rows || !rows[0]) return { ok: false, error: BenefactorError.NOT_ON_WALL };

    const proof = await verify(id);
    if (!proof || proof.present !== true) {
        return {
            ok: false,
            error: BenefactorError.MONUMENT_NOT_PUBLISHED,
            source: (proof && proof.source) || null,
        };
    }

    await sql`
        UPDATE patronage_benefactors
        SET monument_asset_id    = ${id},
            monument_assigned_at = NOW(),
            monument_verified_at = NOW(),
            updated_at           = NOW()
        WHERE wallet = ${wallet}`;

    return { ok: true, monumentAssetId: id, source: proof.source || null };
}

/**
 * WHICH MONUMENTS ARE UNPROVEN AGAINST THE NEWEST CONTENT BUILD?
 *
 * The one-time check at assignment time is necessary and NOT sufficient: bundle
 * names are content-hashed, so the NEXT content build re-hashes every bundle and
 * invalidates every earlier proof at once. A monument that was genuinely present
 * last week is exactly as absent as one that never existed if this build's push
 * was skipped.
 *
 * Given the ISO timestamp of the newest content build, this returns the asset
 * ids whose proof predates it -- the list that still needs a push. Asset ids
 * only: no wallet and no patron name leaves this function, so the ship chain can
 * print the answer without printing an identity.
 */
async function monumentsNeedingRepush(sql, contentBuildIso) {
    if (typeof sql !== 'function') throw new TypeError('sql tagged-template function is required');
    if (typeof contentBuildIso !== 'string' || contentBuildIso.trim() === '')
        throw new TypeError('contentBuildIso is required: without it every proof looks current');

    const rows = await sql`
        SELECT monument_asset_id, monument_verified_at
        FROM patronage_benefactors
        WHERE monument_asset_id IS NOT NULL
          AND (monument_verified_at IS NULL OR monument_verified_at < ${contentBuildIso})
        ORDER BY monument_asset_id ASC`;
    return (rows || []).map(r => r.monument_asset_id);
}

module.exports = {
    BenefactorError,
    FOUNDER_THRESHOLD_USD_CENTS,
    FOUNDER_TIER_ID,
    MONUMENT_ASSET_ID_MAX_LEN,
    MONUMENT_ASSET_ID_RE,
    PLACEHOLDER_MONUMENT_ASSET_ID,
    WALL_DEFAULT_ROWS,
    WALL_MAX_ROWS,
    assertFounderTierPinned,
    assignPatronMonument,
    clampWallLimit,
    monumentsNeedingRepush,
    readBenefactorWall,
    readOwnPatronage,
    resolveMonumentAssetId,
    setPatronName,
};
