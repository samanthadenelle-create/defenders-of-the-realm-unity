# Pi Payment Backend (standalone Cloudflare Worker)

The server half of the two-phase Pi payment (Pi requires a server that holds the secret API key
to call approve/complete — the game is client-only, so this is net-new but **standalone + no-regret**).
Built per `PI_INTEGRATION_SPEC.md` §1, **corrected against the real Pi Platform API.**

## Status
- ✅ Code ready (`src/index.ts`, `wrangler.toml`).
- ⏳ **Fast-follow** — deploy + wire AFTER (a) the playable V1 is stable and (b) the mobile-webview gate test passes. Don't wire the Unity side until the game is confirmed to run in Pi Browser.

## What was corrected from the original draft (verified vs pi-apps/pi-platform-docs)
| Draft | Real Pi API |
|---|---|
| `POST /v2/payments/approve` (id in body) | `POST /v2/payments/{payment_id}/approve` (id in **path**) |
| `Authorization: Bearer <key>` | `Authorization: Key <key>` |
| `/complete` body `{ paymentId }` | `/complete` body `{ txid }` (txid from `onReadyForServerCompletion`) |
Plus added: idempotency (KV keyed by paymentId), `/reconcile` completion, safe error bodies.

## Deploy (owner — needs your Cloudflare account + Pi credentials)
```
cd pi-backend
npm i -g wrangler            # if needed
wrangler kv namespace create PAYMENT_KV    # paste the id into wrangler.toml
wrangler secret put PI_API_KEY             # your Pi SERVER API key (Pi Developer Portal)
wrangler secret put PI_APP_ID
wrangler deploy                            # -> https://pi-payment-backend.<you>.workers.dev
```
Start on **Testnet** (swap `PI_BASE` / use the testnet app + key) before Mainnet.

## Endpoints
- `POST /approve`  `{ paymentId, amount, memo?, userId? }` -> `{ approved }`
- `POST /complete` `{ paymentId, txid }` -> `{ success, entitlement }`
- `POST /reconcile` `{ paymentId, txid? }` -> for `incompletePaymentFound` on app start

## Integration (the V1 grant flow)
1. Unity `.jslib` bridge calls `Pi.createPayment(...)`.
2. `onReadyForServerApproval(paymentId)` -> bridge POSTs **/approve** to this Worker.
3. `onReadyForServerCompletion(paymentId, txid)` -> bridge POSTs **/complete** with the txid.
4. On `{ success:true }`, the Unity side calls **`PackStore.ApplyPackContents`** (the existing grant point) to land the pack into `GameState.Resources`.

**V1 security note:** the *payment* is server-verified (this Worker proves approve+complete to Pi); the
*grant* is then applied client-side. Acceptable for the proof-of-loop. Harden later by holding the
entitlement server-side + a game-server webhook so the client can't self-grant without a real payment.

## TODO before mainnet
- Tighten CORS `*` -> the Pi app origin.
- Per-pack entitlement mapping (V1 = one `pi_pack_small`).
- Move grant authority server-side (anti-cheat) once there's a game backend.
