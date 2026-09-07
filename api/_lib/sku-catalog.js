'use strict';

// =============================================================================
// api/_lib/sku-catalog.js - WO-1532. THE READ-ONLY SKU CATALOG, JOINED.
// -----------------------------------------------------------------------------
// Owner ask 2026-09-06, verbatim: "can we add a list in command center of All
// SKU's and contents".
//
// ⛔ THIS FILE IS A JOIN, NOT A FOURTH COPY OF THE CATALOG.
// Three facts about a SKU, three owners, none of them re-typed here:
//
//   what it is + what it grants  -> Assets/Resources/Data/Canonical/packs.json,
//                                   copied verbatim to ./sku-catalog.generated.json
//                                   by tools/gen-sku-catalog.mjs (see that file
//                                   for why a copy exists: .vercelignore never
//                                   uploads Assets/ to the deployment).
//   may it be QUOTED             -> USD_ANCHORS in ./purchase-catalog.js. No row
//                                   there means usdAnchor() returns null, no quote
//                                   is built, and the pack is UNBUYABLE on the
//                                   wallet rail.
//   may it be sold through PLAY  -> PRODUCT_TYPES in ./google-play-purchases.js.
//                                   No row there means productTypeForSku() returns
//                                   null and validRequest() refuses.
//
// ⛔ WHY THE JOIN IS THE POINT. WO-1165 s2 is the failure this reports: the
// Monthly Ledger cards were authored with a real `pricing.usd`, had no anchor
// row, and were therefore silently unbuyable on the LIVE rail - discovered by a
// human reading two files side by side. Nothing on any page said so. This module
// makes that a column, and it reports the gap in BOTH directions, because a list
// titled "All SKUs" that quietly omits a row is the same defect wearing a
// different coat.
//
// ⛔ IT REPORTS. IT NEVER REPAIRS. A missing anchor is surfaced, never invented -
// the server's price ladder is what a player is charged against (purchase-catalog
// MIRROR LAW), and a catalog view that defaulted a price would be authoring money.
//
// ⛔ NO DATABASE. Nothing here opens a connection, and the endpoint that serves it
// dispatches BEFORE neon() is called, so "this view is not a database read" is
// structural rather than asserted.
//
// Files under api/_lib/ are not routed by Vercel (leading underscore): a library,
// never an endpoint. CommonJS, no dependencies.
// =============================================================================

const CATALOG = require('./sku-catalog.generated.json');
const { USD_ANCHORS } = require('./purchase-catalog');
const { PRODUCT_TYPES } = require('./google-play-purchases');

/** The authored packs, in authored order. Never mutated. */
function packs() {
    return Array.isArray(CATALOG.packs) ? CATALOG.packs : [];
}

function str(v) {
    return v == null ? null : String(v);
}

function numOrNull(v) {
    if (v == null) return null;
    const n = Number(v);
    return Number.isFinite(n) ? n : null;
}

// ── CONTENTS, FLATTENED FOR READING, NOT RESHAPED ───────────────────────────
// The three shapes in packs.json are genuinely different and stay different:
//   cosmetics   string[]  - ownership ids (CosmeticOwnershipService keys)
//   economy     object    - resource -> amount
//   convenience object[]  - { kind, count, description }
// A "unified item" list here would flatten away exactly the distinction the
// covenant rests on (convenience is time, never combat power - packs.json
// _schemaNotes.convenience), so each keeps its own shape and its own count.
function contentsOf(pack) {
    const c = (pack && pack.contents) || {};
    const cosmetics = Array.isArray(c.cosmetics) ? c.cosmetics.map((x) => String(x)) : [];

    const economySrc = (c.economy && typeof c.economy === 'object' && !Array.isArray(c.economy))
        ? c.economy : {};
    const economy = Object.keys(economySrc).map((k) => ({
        resource: String(k),
        amount: numOrNull(economySrc[k]),
    }));

    const convenience = (Array.isArray(c.convenience) ? c.convenience : []).map((item) => {
        if (item == null || typeof item !== 'object') {
            // Older authoring allowed a bare string kind. Read it rather than drop
            // it: a silently omitted grant is how a pack advertises less than it
            // gives, and nobody notices.
            return { kind: String(item), count: null, description: null };
        }
        return {
            kind: str(item.kind),
            count: numOrNull(item.count),
            description: str(item.description),
        };
    });

    return {
        cosmetics: cosmetics,
        economy: economy,
        convenience: convenience,
        // Counts so the console can say "nothing" in words without re-walking the
        // arrays, and so an empty list is visibly empty rather than absent.
        cosmetic_count: cosmetics.length,
        economy_count: economy.length,
        convenience_count: convenience.length,
        is_empty: cosmetics.length === 0 && economy.length === 0 && convenience.length === 0,
    };
}

/**
 * ONE ROW, PURE. anchors/productTypes are parameters and not module lookups so a
 * SYNTHETIC pack can be driven through this function in a test with no HTTP, no
 * canonical file and no environment - which is the only way to prove the MISSING
 * path for a gap that (today) no real pack has.
 */
function parityRow(pack, anchors, productTypes) {
    const p = pack || {};
    const sku = str(p.sku);
    const table = anchors || {};
    const types = productTypes || {};

    const hasAnchor = sku != null && Object.prototype.hasOwnProperty.call(table, sku);
    const anchor = hasAnchor ? numOrNull(table[sku]) : null;

    const hasType = sku != null && Object.prototype.hasOwnProperty.call(types, sku);
    const playType = hasType ? str(types[sku]) : null;

    const storeVisible = p.storeVisible === true;

    // promoGrantOnly rows (packs.json `welcome-500` / `welcome-100`) are NEVER
    // offered for sale, so a missing anchor on one of them is the design, not a
    // defect. They are still LISTED - the owner asked for all SKUs - and the
    // absence is still reported truthfully in usd_anchor_present. What changes is
    // only whether it counts as a GAP.
    const promoOnly = p.promoGrantOnly === true;

    const pricing = (p.pricing && typeof p.pricing === 'object') ? p.pricing : {};

    const gaps = [];
    if (!hasAnchor && !promoOnly) {
        gaps.push('no USD anchor in api/_lib/purchase-catalog.js - this SKU cannot be quoted, ' +
                  'so the wallet rail cannot sell it');
    }
    if (!hasType && !promoOnly) {
        gaps.push('no product type in api/_lib/google-play-purchases.js - Google Play billing ' +
                  'refuses this SKU');
    }
    // The authored price and the server's ladder are two different numbers on one
    // screen, which purchase-catalog calls out as worse than a stale one. If they
    // ever disagree the SERVER's figure is what the player is charged.
    const authoredUsd = numOrNull(pricing.usd);
    const anchorMismatch = hasAnchor && authoredUsd != null && anchor != null && authoredUsd !== anchor;
    if (anchorMismatch) {
        gaps.push('authored pricing.usd (' + authoredUsd + ') disagrees with the server anchor (' +
                  anchor + ') - the server figure is what the player is charged');
    }

    return {
        sku: sku,
        name: str(p.name),
        tagline: str(p.tagline),
        tier: numOrNull(p.tier),
        // storeSection is the documented FALLBACK the newer `band` key supersedes
        // (packs.json _schemaNotes.nightMarketPresentation). Both are reported; the
        // console shows band where a row has one.
        section: str(p.storeSection),
        band: str(p.band),
        store_visible: storeVisible,
        founder_only: p.founderOnly === true,
        promo_grant_only: promoOnly,
        pricing: {
            usd: authoredUsd,
            usdc: numOrNull(pricing.usdc),
            sol: numOrNull(pricing.sol),
            skr: numOrNull(pricing.skr),
        },
        contents: contentsOf(p),

        // ---- parity columns, computed here and never in the page --------------
        usd_anchor: anchor,
        usd_anchor_present: hasAnchor,
        play_product_type: playType,
        play_product_type_present: hasType,
        // "Could the store build actually sell this row today?" Two conditions,
        // AND-ed, said out loud: a shelf row with no anchor is a card that reads
        // "Price unavailable", and an anchored row with storeVisible:false is
        // deliberately off the shelf (still redeemable for an existing owner -
        // packs.json _schemaNotes.shelf).
        sellable: hasAnchor && storeVisible,
        sellable_reason: hasAnchor
            ? (storeVisible ? 'anchored and on the shelf'
                            : 'anchored but storeVisible:false - hidden from browse on purpose')
            : (promoOnly ? 'promo-grant only - never offered for sale'
                         : 'NO ANCHOR - cannot be quoted, so it cannot be sold'),
        parity_gaps: gaps,
    };
}

/**
 * The whole view. Both directions of the join, because the reverse one is the
 * half that catches an omission.
 */
function build(anchors, productTypes, list) {
    const table = anchors || USD_ANCHORS;
    const types = productTypes || PRODUCT_TYPES;
    const source = Array.isArray(list) ? list : packs();

    const rows = source.map((p) => parityRow(p, table, types));
    const known = new Set(rows.map((r) => r.sku).filter((s) => s != null));

    // ⛔ THE REVERSE DIRECTION. USD_ANCHORS carries monthly-wayfarer and
    // monthly-keeper, which are authored in battle_monthly.json `monthlyCards[]`
    // and are NOT packs; the mainnet canary is likewise a proof-of-rail and not a
    // pack. Listing "All SKUs" purely from packs.json would make them disappear -
    // the exact shape of the WO-1165 s2 defect, in reverse. So they are named.
    const anchorsWithoutPack = Object.keys(table)
        .filter((sku) => !known.has(sku))
        .map((sku) => ({ sku: sku, usd_anchor: numOrNull(table[sku]) }));
    const typesWithoutPack = Object.keys(types)
        .filter((sku) => !known.has(sku))
        .map((sku) => ({ sku: sku, play_product_type: str(types[sku]) }));

    const gapped = rows.filter((r) => r.parity_gaps.length > 0);

    return {
        source: 'Assets/Resources/Data/Canonical/packs.json, copied verbatim to ' +
                'api/_lib/sku-catalog.generated.json by tools/gen-sku-catalog.mjs. ' +
                'Assets/ is never uploaded to the deployment (.vercelignore), so the ' +
                'function cannot read the canonical file directly.',
        catalog_version: numOrNull(CATALOG.version),
        currency_disclaimer: str(CATALOG.currencyDisclaimer),
        counts: {
            packs: rows.length,
            on_shelf: rows.filter((r) => r.store_visible).length,
            sellable: rows.filter((r) => r.sellable).length,
            with_parity_gap: gapped.length,
            anchors_without_pack: anchorsWithoutPack.length,
            product_types_without_pack: typesWithoutPack.length,
        },
        packs: rows,
        anchors_without_pack: anchorsWithoutPack,
        product_types_without_pack: typesWithoutPack,
        notes: [
            'A SKU with no USD anchor cannot be quoted by /api/purchases/quote, so the wallet ' +
            'rail cannot sell it however the card looks. That failure has shipped before ' +
            '(WO-1165 section 2, the Monthly Ledger cards) and is the reason this column exists.',
            'A SKU with no Google Play product type is refused by validRequest() on the Play rail.',
            'promoGrantOnly rows are listed but are never offered for sale, so a missing anchor ' +
            'on one of them is not counted as a gap.',
            'anchors_without_pack is not automatically a defect: monthly-wayfarer and ' +
            'monthly-keeper are authored in battle_monthly.json monthlyCards[], and the mainnet ' +
            'canary is a proof-of-rail, not a pack.',
            'Nothing here reads or writes the database, and nothing here can change a price.',
        ],
    };
}

module.exports = { packs, contentsOf, parityRow, build, CATALOG };
