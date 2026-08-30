# WORK ORDER 1272 - Generic card and database-driven collection foundation

**Status:** FIXED 2026-08-29 — shared card/collection/modal, packaged fallback, remote validation/cache, and server entitlement restore foundation implemented and headless-verified; awaiting owner device test.
**Minted:** 2026-08-28 by Codex CLI under WO-1271.
**Lane:** Shared UI presentation + catalog/backend contract. Coordinate Wallet, Village, Core Data, and API boundaries without duplicating them.

## Player problem

Build cards currently compress six items into a phone-width strip. Names, descriptions, and costs
exist but are unreadable. Build and Shop also need one consistent focused interaction model rather
than independent screen-specific card implementations.

## Goal

Create reusable **Generic Card**, **Card Collection**, and **Focused Modal Host** components driven by
resolved data. A database collection is an ordered list of pointers to canonical SKU/item records;
it does not duplicate item definitions.

## Data contract

Collections require at least:

- stable `collection_id`, context (`build`, `shop`, `owned`, `showcase`), title, subtitle
- collection-level `icon_key` plus optional versioned CDN URL/hash
- ordered item pointers: `item_id`/`sku`, display order, optional badge and visibility rule
- version, active state, schedule, minimum compatible client version, fallback collection id

Items require a stable SKU/id and may point to card art and platform-compatible asset bundles with
version, size, hash, minimum client version, packaged fallback key, and safe fallback SKU.

The database response must be validated and converted into a presentation-neutral model. Existing
canonical JSON supplies the fallback snapshot and existing item definitions.

## UI contract

### Generic Card

- artwork/icon, title, one-line purpose, state/badge, contents or full resource cost, primary action
- scalable type with a minimum physical-phone readability floor
- locked/unavailable state communicated in words and shape, never hue alone
- Build may bind `Place`; Shop may bind `Buy`; callers supply behavior through interfaces/events

### Card Collection

- collection header/icon, 3-4 large cards visible at phone landscape size
- horizontal swipe/page for overflow; never shrink a fifth card into unreadability
- deterministic DB-authored order, back navigation, loading/error/offline states
- no item-specific switches in presentation code

### Focused Modal Host

- occupies approximately 80% of the safe-area screen and dims the game behind it
- acquires a shared pause lease while the high-attention flow is open
- releases exactly once on every close, cancellation, exception, scene exit, or completed handoff
- supports nested detail/confirmation without briefly resuming gameplay

## Cache resolution

Resolution order for definitions and assets:

1. packaged item/collection at an equal or newer version
2. verified local cache
3. versioned CDN download with size/hash verification
4. explicit packaged fallback

Catalog metadata may persist locally for offline use. Asset ownership never derives from cache
presence. Cache entries are evictable and re-downloadable.

## Existing seams to inspect/reuse

- `CanonicalJson`, `ICatalogSource`, and Resources-first canonical fallback
- `structures-catalog.json`, `build-categories.json`, and `packs.json`
- current Addressables/R2 structure-art loader and shipped R2 parity gate
- existing UI kit/factory and existing pause/lock ownership before adding any new service
- existing wallet/server entitlement records before defining new tables

## Acceptance

- A fixture collection renders correctly from packaged fallback and from a mocked DB response.
- A collection with five items renders four readable cards and pages/swipes to the fifth.
- Invalid, incompatible, or hash-failed remote content fails to an explicit fallback, never blank UI.
- Opening the host pauses simulation; all exit paths restore the prior simulation state exactly once.
- Cache deletion followed by reconnect restores an entitled item from server truth.
- Regression proves no item-specific UI switch and no second canonical loader was introduced.
- Full compile/regression/UI capture gates are required before device handoff.

## Must not

- Do not replace all packaged JSON with a mandatory network catalog.
- Do not store raw models in Neon or import arbitrary raw FBX/GLB in the shipped client.
- Do not treat device time, cached files, or client claims as entitlement authority.
- Do not change Build membership, store pricing, payment flags, or live promo rows in this foundation.
