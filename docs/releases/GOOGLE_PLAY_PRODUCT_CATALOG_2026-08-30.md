# Google Play one-time product catalogue — 2026-08-30

Package: `com.denellestudios.echoesofelarion`

This is the operator copy of the product contract shared by
`GooglePlayProductCatalog.cs`, `api/_lib/google-play-purchases.js`, and canonical
`packs.json`. Product IDs are immutable after creation. Every row is a **one-time
product** with a **Buy** purchase option and **Digital content** classification;
there are no subscriptions or rentals. Leave multi-quantity disabled for the first
release. The server decides whether a completed purchase is consumed or acknowledged.

## First Internal-test activation set

Create and activate these eight rows first. They are the only packs currently visible
in the Play storefront. Use the USD base price shown and allow Play to calculate local
prices, reviewing the resulting price points before activation.

| Product ID | Player-facing name | USD | Fulfilment |
|---|---|---:|---|
| `com.denellestudios.echoesofelarion.folks_thanks` | Folk's Thanks | $9.99 | consumable |
| `com.denellestudios.echoesofelarion.patron_of_elarion` | Resource Pack I | $19.99 | consumable |
| `com.denellestudios.echoesofelarion.founders_vow` | Resource Pack II | $49.99 | consumable |
| `com.denellestudios.echoesofelarion.starters_hand` | Starter's Hand | $4.99 | consumable |
| `com.denellestudios.echoesofelarion.impulse_wood_medium` | Timber Wagon | $2.99 | consumable |
| `com.denellestudios.echoesofelarion.impulse_iron_medium` | Ingot Crate | $2.99 | consumable |
| `com.denellestudios.echoesofelarion.impulse_stone_medium` | Quarry Cart | $2.99 | consumable |
| `com.denellestudios.echoesofelarion.permanent_builder` | Permanent Builder | $9.99 | non-consumable |

For consumables, the backend grants durably and then calls Play consume so they can be
purchased again. For Permanent Builder, the backend grants durably and acknowledges;
the ownership grant is idempotent and must restore after reinstall.

## Hidden compatibility rows

The client queries these IDs because old saves and future merchandising must continue to
resolve, but canonical `storeVisible` is false. Create them inactive after the first eight
are correct; do not make them purchasable merely to eliminate an unfetched-product entry.

| Product ID | Player-facing name | USD | Fulfilment |
|---|---|---:|---|
| `com.denellestudios.echoesofelarion.hearth_spark` | Hearth Spark | $4.99 | consumable |
| `com.denellestudios.echoesofelarion.keepers_satchel` | Keeper's Satchel | $4.99 | consumable |
| `com.denellestudios.echoesofelarion.impulse_wood_small` | Cord of Timber | $1.99 | consumable |
| `com.denellestudios.echoesofelarion.impulse_wood_large` | Timber Barge | $4.99 | consumable |
| `com.denellestudios.echoesofelarion.impulse_iron_small` | Pouch of Ingots | $1.99 | consumable |
| `com.denellestudios.echoesofelarion.impulse_iron_large` | Foundry Load | $4.99 | consumable |
| `com.denellestudios.echoesofelarion.impulse_stone_small` | Mason's Satchel | $1.99 | consumable |
| `com.denellestudios.echoesofelarion.impulse_stone_large` | Mason's Wagon | $4.99 | consumable |
| `com.denellestudios.echoesofelarion.impulse_crystals_small` | Crystal Shard | $1.99 | consumable |
| `com.denellestudios.echoesofelarion.impulse_crystals_medium` | Crystal Cluster | $2.99 | consumable |
| `com.denellestudios.echoesofelarion.impulse_crystals_large` | Crystal Vein | $4.99 | consumable |
| `com.denellestudios.echoesofelarion.frostfall_bundle` | Frostfall Bundle | $9.99 | non-consumable |
| `com.denellestudios.echoesofelarion.embergrove_bundle` | Embergrove Bundle | $9.99 | non-consumable |
| `com.denellestudios.echoesofelarion.bloomtide_bundle` | Spring Awakening | $4.99 | non-consumable |
| `com.denellestudios.echoesofelarion.echo_patron_pack` | Echo Patron Pack | $19.99 | non-consumable |
| `com.denellestudios.echoesofelarion.hero_wardrobe_pack` | Hero Wardrobe Pack | $9.99 | non-consumable |
| `com.denellestudios.echoesofelarion.realm_defender_bundle` | Realm Defender Bundle | $9.99 | non-consumable |
| `com.denellestudios.echoesofelarion.builders_cache` | Builder's Cache | $19.99 | non-consumable |

## Activation gate

Before setting `GOOGLE_PLAY_BILLING_ENABLED=true`, prove all of the following on a
Play-installed Internal-track build using a license tester:

1. All eight visible product details resolve with Play-localized prices; no authored USD
   string is substituted for an unavailable Play price.
2. A consumable success grants once, consumes only after the durable grant, and can be
   purchased again. Duplicate callbacks and app termination after charge do not double-grant.
3. A pending purchase grants nothing until Play reports `PURCHASED`; test both delayed
   approval and delayed decline.
4. Permanent Builder grants once, is acknowledged only after the durable grant, cannot be
   bought twice, and restores after reinstall using the same Google account.
5. Cancelled, unavailable, offline, refunded, voided, and chargeback cases remain visible
   and recoverable without client-only authority. Refund evidence remains quarantined until
   the owner-approved entitlement reversal policy exists.

Authoritative Google references: [one-time product model](https://developer.android.com/google/play/billing/one-time-products),
[Console creation fields](https://support.google.com/googleplay/android-developer/answer/16430488), and
[purchase lifecycle](https://developer.android.com/google/play/billing/lifecycle/one-time).
