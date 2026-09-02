# WORK ORDER 1318 — Pi U2A payments, one SKU (Hearth Spark), on the EXISTING purchase seam

**Status:** READY TO IMPLEMENT
**Silo:** Web / Pi / Monetization
**Minted:** 2026-09-02 (CLI) from the owner's direct instruction + her product and pricing rulings.
**Severity:** P1 feature — first revenue path on Pi.

## Owner rulings (binding, verbatim where quoted)

- Product: **ONE starter pack first — `Hearth Spark`** (tier 1, `pricing.usd = 4.99`). Chosen over the
  full 28-pack shelf deliberately: **no purchase has ever completed in this game**, so proving
  approve -> complete -> grant on ONE sku beats shipping 28 that could all fail identically.
- Pricing: *"just like with SKR we're gonna do the floor of 24 hour window"* -> the CoinGecko
  **`low_24h`** value, exactly as SKR already does.
- Pi API key: supplied by the owner and already set as **`PI_NETWORK_API_KEY`** (Production,
  Encrypted) on BOTH Vercel projects. It is a test key. **It is NOT in the repo and must never be.**

## ⛔ REUSE THE EXISTING SEAM. DO NOT BUILD A SECOND PURCHASE SYSTEM.

A complete, mature USD-anchored purchase rail already exists and is the model to extend:

| file | role |
|---|---|
| `api/_lib/purchase-catalog.js` | `quotableSkus`, `usdAnchor`, `QUOTE_TTL_SECONDS`, the rate fetcher |
| `api/purchases/quote.js` | issues a BINDING, TTL'd, server-owned quote (persisted with rate + source) |
| `api/purchases/verify.js` | settles it |

`purchase-catalog.js:133-136` is the pattern to mirror:
```js
const RATE_URL      = 'https://api.coingecko.com/api/v3/coins/markets?vs_currency=usd&ids=seeker';
const RATE_SOURCE   = 'coingecko:seeker:low_24h';
const RATE_CACHE_MS = 120_000;
const RATE_TIMEOUT_MS = 8_000;
```
**Verified 2026-09-02:** `ids=pi-network` on that same endpoint returns `low_24h` (0.091171 at the
time of writing; `$4.99 -> 54.73 Pi`). Same endpoint, same field, same shape.

⚠ **`fetchSkrUsdRate` FAILS CLOSED** — `return null; // fail closed - never a stale or invented price`,
and `quote.js:246` answers `503 PURCHASE_RATE_UNAVAILABLE`. **Keep that behaviour for Pi.** Do NOT add
a static fallback price "so purchases still work"; charging a wrong price is worse than not charging.

## ⛔ THE SECURITY INVARIANT — the client never sets the amount

The backend computes the Pi amount, persists it against a quote id, and **re-validates it at approve**.
A modified client must not be able to quote itself 0.1 Pi and have the server approve it. This is the
whole reason `quote.js` persists `rate` + `rateSource` + a quote id rather than trusting a request body.

## The Pi payment flow (per the owner's referenced SDK docs)

1. **`await Pi.init(...)` MUST resolve before any `Pi.createPayment(...)`.** `PiBridge.jslib:48` already
   treats init as a promise and awaits it — preserve that.
2. Auth scope must be extended to include **`payments`** (today `PiSignInController` requests
   `username` only — see the captured trace `PiAuthenticate(scopes=username)`).
3. `Pi.createPayment({ amount, memo, metadata }, callbacks)` with ALL of:
   - `onReadyForServerApproval(paymentId)` -> POST our backend -> `POST https://api.minepi.com/v2/payments/:id/approve`
   - `onReadyForServerCompletion(paymentId, txid)` -> POST our backend -> `POST https://api.minepi.com/v2/payments/:id/complete`
   - `onCancel`, `onError` — both must surface through `FlowTrace` so a failure is diagnosable.
4. **`onIncompletePaymentFound(payment)` is MANDATORY and must COMPLETE the in-flight payment via the
   backend — never silently ignore it.** A dropped incomplete payment is a player who paid and got
   nothing.
5. Server-to-server calls carry `Authorization: Key <PI_NETWORK_API_KEY>`. That key is server-side
   ONLY — it must never reach the client, a log line, or the repo.

## Amount + memo + metadata must agree on both sides

Use the SAME values in the client request and the backend validation:
- `amount`   : the server-quoted Pi figure (from `low_24h`)
- `memo`     : `"Echoes of Elarion - Hearth Spark"` (ASCII only)
- `metadata` : `{ sku: "hearth-spark", quoteId, uid }`

At **complete**, grant Hearth Spark's authored contents from `packs.json` — crystals 150, stone 500,
coins 100, wood 1500, iron 800 — through the EXISTING grant path. Do not hand-roll a grant.

## Acceptance criteria

1. A Pi Browser purchase of Hearth Spark completes end to end and the player receives the pack.
2. The Pi amount is computed SERVER-side from `low_24h` and re-validated at approve. A forged client
   amount is REFUSED, and there is a test or a captured line proving the refusal.
3. Rate unavailable -> a clean refusal (`503`-shaped, like `PURCHASE_RATE_UNAVAILABLE`), never a
   guessed price.
4. `onIncompletePaymentFound` completes a pending payment on next launch — **prove it** by
   interrupting a payment and relaunching, not by reading the code.
5. `PI_NETWORK_API_KEY` appears in NO client bundle, NO log line, NO committed file.
6. Sign-in still works for players who only granted `username` before — the added `payments` scope
   must not lock out an existing session. State how this was handled.
7. Pre-ship gates green: `COMPILE_GATE_OK`, `REGRESSION_OK <n>/<n>`.

## What NOT to touch

- ⛔ Do NOT build a second store, catalog, quote table or grant path (ARCHITECTURE_PRINCIPLES 2b).
- ⛔ Do NOT add a fallback/static Pi price. Fail closed, as SKR does.
- ⛔ Do NOT put the API key in the repo, in `packs.json`, or in any client-reachable file.
- ⛔ Do NOT change the SKR/Solana path, `walletAllowed`, or `MAINNET_SALES_ENABLED`. Pi is additive.
- ⛔ Do NOT alter `Assets/AddressableAssetsData/**` or `ServerData/` (CLAUDE.md sec.16).
- ⛔ Do NOT weaken `PiIsPiBrowser` (WO-1317) — the Pi skin depends on it.
