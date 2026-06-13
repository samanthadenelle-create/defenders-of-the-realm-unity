# WORK ORDER 414 — RENUMBERED → WO-429 (see `WORK_ORDER_429_store_stock_from_db.md`)

**Status: SUPERSEDED — do not implement from this file.**

Number collision: the Notion board's WO-414 = "Black circle under TALK button — AttentionGlowUi
first-frame color" (minted 06-12, cross-referenced by WO-416/428). This store-stock spec was
renumbered to **WO-429** by the 2026-06-12 nightly reconcile. The live spec (content unchanged,
plus WO-412/406 coordination notes) is `WORK_ORDER_429_store_stock_from_db.md`.

---

*(original text below, kept for history)*

**Priority:** P1
**Status:** READY (client side) — depends on a backend GET endpoint (React repo)
**Lane:** 7 — Persistence / Backend (+ Lane 8 store)
**Filed:** 2026-06-11 (owner). Next free WO after this = 415.

---

## Problem
The CLI loaded store item data into the **Neon DB** (verified persisting). But the items don't
appear in-game because the **Unity client never calls the DB for store stock** — verified: the
client has live backend calls (`/api/game/save|load`, `/api/auth/nonce`, events, promo, referral)
but **NO store/items fetch** (grep for store/items/catalog endpoints = empty). The shop's only
source is the local `GearCatalog` (`Resources/Data/Canonical/weapons.json`/`armor.json`). The
"real call" to fetch DB store stock must be built.

## Architecture note (offline-first — read before building)
Store STOCK has lived in the LOCAL catalog by design (so the shop works offline; player STATE is
what syncs to Neon). Serving stock from the DB is a deliberate change — **it MUST fall back to the
local catalog when the call fails / offline**, or the shop breaks with no connection. Recommended
shape: DB stock is an OVERRIDE/refresh on top of the baked catalog, not a hard dependency.

## Implementation
**Backend (React repo — confirm/needed):** a GET endpoint that returns the store stock the CLI
loaded (e.g. `/api/store/items` or `/api/items`). The owner loaded the data via some route — confirm
a READ route exists; if only a write/load script ran, the GET endpoint must be added.

**Unity client (this repo — I can drive):**
1. A `StoreService` (mirror `GameStateService`'s `UnityWebRequest` + auth-header pattern,
   `Assets/_Modules/Core/State/GameStateService.cs:892` `TryAttachAuthHeaders`) that GETs the store
   stock from the endpoint, deserializes to the existing `GearCatalog` item shape (Newtonsoft).
2. The shop (`ShopPanel`/`GearCatalog`) consumes DB stock when available, **falling back to the
   baked local catalog** on failure/offline (offline-first guardrail above).
3. Reconcile with `CanonicalJson` (the local catalog loader) — additive; don't rip out the local path.

## Separate but related (do NOT conflate)
Player PURCHASES still don't persist: `VillageInventory` is session-only and **not in `SaveSchema`**,
so bought gear never enters the live Neon sync → lost on reload. That's its own fix (add
`GearInventory` to `SaveSchema` + wire `VillageInventory` ↔ `GameStateService`). Spec/track separately.

## SECURITY (non-negotiable)
- The Neon connection string lives **ONLY** in the backend's server env vars (Vercel), **never** in
  the Unity client (it would ship in the build, extractable = full DB access). Client → HTTPS → backend → Neon.
- The owner will **rotate the Neon role password once the integration is complete** (it was shared in
  chat → treat as exposed); update the backend env var with the new password after rotating.

## Verification
With the DB live: load store data → in-game shop shows it via the real call → reload/offline → shop
still works from the local fallback. Owner-confirmed.
