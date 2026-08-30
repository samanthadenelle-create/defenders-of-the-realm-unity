# WORK ORDER 1271 - Program: database-driven card collections, rewards, and showcases

**Status:** FIXED 2026-08-29 — architecture/dependency wrapper completed and awaiting owner device verification of the delivered WO-1272 through WO-1275 slice. Later social consumers remain independently tracked as WO-1276 and WO-1277 specs.
**Minted:** 2026-08-28 by Codex CLI from the owner's card-system and social-progression direction; banner bumped 1271 -> 1278 in the same edit.
**Lane:** Program / architecture coordination. No direct runtime implementation in this wrapper.

## Goal

Establish one reusable, phone-readable card system whose collections are database-authored pointers
to stable item SKUs. Existing packaged JSON remains the offline baseline. New or rewarded items may
resolve versioned CDN assets into a local cache until the same SKU is packaged in a later APK.

The same foundation serves Build and Night Market immediately, then rewarded unlocks and animated
Town Showcases without creating parallel card, ownership, or asset-loading systems.

## Delivery order

1. **WO-1272** - generic card, card collection, focused modal, DB collection contract, cache resolver.
2. **WO-1273** - Build collections, category icons, readable 80% layout, gameplay pause.
3. **WO-1274** - Night Market on the shared cards/modal with purchase lifecycle pause.
4. **WO-1275** - rewarded SKU entitlements, temporary ownership, Stone Gate and Healing Caravan unlocks.
5. **WO-1276** - animated read-only Town Showcase and Visit Top 10 Towns.
6. **WO-1277** - community voting and competitively earned cosmetic rewards.

WO-1272 is the foundation. WO-1273 and WO-1274 may proceed in parallel only after its data and UI
contracts are stable. WO-1276 and WO-1277 are follow-up consumers and must not expand the overnight
card-system build.

## Architectural laws

- Neon stores catalog metadata, collection membership/order, and entitlements; it does not store
  large model or texture blobs.
- R2/CDN stores immutable, versioned asset bundles and images with hashes.
- The server is the ownership and expiry authority. The local device is only a cache.
- Existing canonical JSON and packaged assets remain the offline fallback.
- Stable SKU identity survives remote trial, APK promotion, reinstall, and cache eviction.
- Remote data may configure behavior already shipped in the client; it must not deliver executable
  gameplay code.
- Build, Shop, Redeem, and purchase decisions must not leave the player vulnerable to active combat.

## Acceptance

- Each child WO has explicit acceptance, tests, rollout/rollback behavior, and files/seams to inspect.
- No child invents a second item catalog, entitlement ledger, asset loader, or modal-pause owner.
- Existing save IDs and catalog IDs remain stable.
- Program documentation clearly distinguishes tonight's card delivery from later social features.

## Must not

- Do not implement runtime behavior in this wrapper.
- Do not move existing canonical catalogs wholly online.
- Do not make cached files proof of ownership.
- Do not activate payments, change prices, or alter production entitlements under this program.
