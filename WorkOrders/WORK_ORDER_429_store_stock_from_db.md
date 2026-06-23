# WORK ORDER 429 — Store stock served from the DB (the "real call")

**Priority:** P1
**Status:** READY (client side) — depends on a backend GET endpoint (React repo)
**Lane:** 7 — Persistence / Backend (+ Lane 8 store)
**Filed:** 2026-06-11 (owner) as WO-414. **Renumbered → 429 on 2026-06-12** (nightly reconcile):
the number 414 was also used on the Notion board for "Black circle under TALK button —
AttentionGlowUi first-frame color" (cross-referenced by WO-416/428), so this spec takes the next
free number. Content unchanged. Next free WO after this = 430.

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

## Related / coordinate
- **WO-412** (Vendor Wares BUY tab empty — ShopPanel layout fix landed `ca89d9b`; gear-catalog
  runtime load still unverified in builds) — verify 412's build test BEFORE wiring DB stock, or
  the empty-list layout bug masks this WO's results.
- **WO-406** (vendor inventories not populated) — same surface; don't duplicate fixes.

## Separate but related (do NOT conflate)
Player PURCHASES still don't persist: `VillageInventory` is session-only and **not in `SaveSchema`**,
so bought gear never enters the live Neon sync → lost on reload. That's its own fix (add
`GearInventory` to `SaveSchema` + wire `VillageInventory` ↔ `GameStateService`). Spec/track separately.

## SECURITY (non-negotiable)
- The Neon connection string lives **ONLY** in the backend's server env vars (Vercel), **never** in
  the Unity client (it would ship in the build, extractable = full DB access). Client → HTTPS → backend → Neon.
- The owner will **rotate the Neon role password once the integration is complete** (it was shared in
  chat → treat as exposed); update the backend env var with the new password after rotating.

## Acceptance criteria
- [ ] Backend GET endpoint confirmed/added (React repo) returning the loaded store stock
- [ ] `StoreService` GETs stock with auth headers; deserializes to `GearCatalog` item shape
- [ ] Shop consumes DB stock when available; falls back to baked local catalog on failure/offline
- [ ] Reload + offline → shop still works from local fallback
- [ ] No Neon connection string anywhere in the Unity client
- [ ] Owner-confirmed in a build

## What NOT to touch
- Do NOT greenfield store code (PackStore ~70% built)
- Do NOT remove the local catalog path (`CanonicalJson`) — additive only
- Do NOT restyle the shop UI (WO-405 gates visual design; WO-415 owns the storefront skin)
