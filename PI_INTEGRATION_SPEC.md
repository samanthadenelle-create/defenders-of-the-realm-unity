# Pi Integration — Resolved Specs (V1, minimal proof-of-loop)
### Owner-resolved 2026-06-26. Wire-ready contracts for PiWalletProvider + the grant flow. Don't re-litigate architecture.

## 1. Minimal Pi Payment Backend (server-mediated, two-phase)
Pi requires a SERVER to hold the secret + mediate approve/complete. Client never sees the API key.
State machine per `paymentId`: **Pending** (client-initiated, server-approved) → **Completed** (server-confirmed, entitlement granted) → **Failed/Cancelled** (timeout/error/incompletePaymentFound reconcile).
Endpoints (serverless — Cloudflare Worker / Vercel / Lambda):
- **POST /approve** (from .jslib on `onReadyForServerApproval`): `{ paymentId, amount, memo?, userId? }` → validate (rate-limit, amount sanity, no dupe), call Pi approve w/ secret, store pending record (`paymentId → user + entitlement type` e.g. `"pi_pack_small"`). Resp `{ approved:true, txId? }`.
- **POST /complete** (from bridge on `onReadyForServerCompletion`): `{ paymentId, txId? }` → call Pi complete, mark Completed, emit entitlement via economy service (add resources / flag timer-skip). Resp success.
- **POST /reconcile** (optional, for `incompletePaymentFound`): `{ paymentId }` → query Pi status; if completed-but-not-granted, apply entitlement + mark done. Call on app start / after failed flow.
Rules: secret key server-only (env var); `paymentId` = correlation key (client passes through bridge → C# UniTask); idempotency via durable KV/SQLite/Supabase keyed by `paymentId`; V1 = ONE pack type, no catalog. Thin facade over Pi's APIs.

## 2. WebGL ↔ Pi-JS Bridge Contract
`.jslib` plugin + `SendMessage` to a named GameObject `"PiBridge"` (DontDestroyOnLoad / persists scenes).
- **C# → JS:** `PiBridge.CreatePayment({paymentId, amount, memo})` → JS calls `Pi.createPayment(...)`.
- **JS → C#:** `SendMessage("PiBridge","OnPiCallback", json)` where json = `{ "type": "approvalReady"|"completionReady"|"error"|"cancelled", "paymentId": "...", "data": { txId, ... } }`.
- **C# pattern:** `PiWalletProvider.CreatePiPayment(...) : UniTask<PaymentResult>`. Internally a `TaskCompletionSource<PaymentResult>` keyed by `paymentId` (dict). On callback: lookup by paymentId → complete the TCS (or handle error). Handle `incompletePaymentFound` by firing /reconcile on init. Mockable (mock the JS side).

## 3. Starved Pi Economy Model
Free soft-currency (wood/iron/food/gold + offline farm) drives the FULL loop. Pi = meaningful-but-optional accelerator/sink. NO pay-to-win (no power spikes that break balance).
Minimal V1 hook = ONE pack + ONE timer-skip:
- **Pack** "Builder's Pack / Pi Resource Burst": fixed bundle (+500 wood/iron + crystals) → immediate economy-service credit.
- **Timer-skip** "Instant Raid Ready / Defense Boost": skip a build/upgrade cooldown OR accelerate the offline cap once. (The fight stays skill/timing-based.)
Faucet (generous free offline 10h + raid rewards keep progression viable w/o Pi) / Sink (Pi buys CONVENIENCE not progression gates; raids stay the hook + primary advancement). Price modestly in Pi; free path slower-but-complete. Wire as ONE entitlement type. Test: buy → resources appear + loop continues.

## 4. Anti-Tamper / Offline-Clock Trust (V1: accept-risk + light handshake)
V1 stays client-heavy (full server-time per accrual = too much latency/complexity now; Pi already adds a backend). Device-clock tamper is a known idle risk but low-impact for early proof.
- Client computes delta from device time (existing).
- **Light handshake:** on app start + after a significant offline claim, POST to the backend a time-check `{ clientNow, claimedDelta, lastSave }`. Server compares vs its time; if delta unreasonable (>12h / impossible speed) → flag suspicious (log / cap reward at normal max). **Don't block — throttle.**
- Persist last-validated server time; future loads blend (trust device, clamp to last-server + max-offline). Future-proof to full server-authoritative accrual once backend exists.

## Sequencing note (owner): implement backend + bridge first (they unlock the others), **run the mobile-webview gate test**, then wire one pack.
## CLI note (re-sequence): the **mobile-webview gate test is cheaper + decisive — do it FIRST/parallel**, before the heavy bridge+game integration, so we don't build the rail for a game that can't run in Pi's webview. The backend is standalone (safe to scaffold anytime); the WebGL bridge + in-game wiring is the part wasted if the gate fails.
