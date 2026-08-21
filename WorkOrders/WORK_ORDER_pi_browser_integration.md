<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-28
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-28) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER — Pi Browser / Pi Network Integration (Feasibility + Design)

**Type:** RESEARCH + DESIGN SPEC (feasibility, architecture, phased plan). **No `.cs` in this WO** — implementation is a follow-up WO once the owner greenlights a phase.
**Status:** PARKED — FUTURE WORK, not a priority (owner ruling 2026-08-21).
**Author lane:** Monetization/Backend + Distribution (§9 parallel lane — isolated from gameplay).
**Date:** 2026-06-28
**Supersedes nothing.** *Layers on top of* the ratified `docs/DATA_ARCHITECTURE_DECISION_2026-06-27.md` (staged local→cloud→Solana) and `WorkOrders/WORK_ORDER_skr_store_design.md` (held-SKR premium store). Pi is a **new payment rail + distribution channel**, not a new economy.

---

## 0. Executive feasibility verdict

**Can we tie the game into Pi Browser? — YES, technically feasible, but it is a real workstream, not a flag flip.**

| Question | Verdict |
|---|---|
| Is the platform real and live? | **Yes.** Pi launched **Open Network (open mainnet)** in **Feb 2025**; the Pi App Platform (Developer Portal at `develop.pi`, JS SDK v2.0, Payments API, Ads) is live and third-party apps are running in **Pi Browser** today. |
| What kind of app does Pi run? | **Web apps only.** A Pi app is a website you host at your own domain, registered in the Developer Portal, loaded inside Pi Browser. There is **no native SDK** — integration is **JavaScript SDK over a webview**. |
| Can our Unity game be that web app? | **Yes, via a Unity WebGL build** hosted at a URL, with a **`.jslib` bridge** that calls the Pi JS SDK (`Pi.authenticate`, `Pi.createPayment`, `Pi.Ads`). This is the only realistic path. |
| Are real Pi payments usable now? | **Yes on mainnet** (with a **Sandbox/Testnet** mode for development, `Pi.init({sandbox:true})`). Real-Pi payments require the app to be approved/listed and the user to be **KYC-verified**. |
| How hard? | **Medium-High.** The Pi SDK wiring is small (days). The hard parts are: (a) a **thin backend** for the mandatory server-side `/approve` + `/complete` payment handshake; (b) **Unity WebGL viability on mobile Pi Browser** (the real risk — perf/size/load on phones); (c) **app review + domain validation + KYC** gating real-Pi listing. |
| What's blocked / unknown right now? | Unity-WebGL-in-Pi-Browser has **no verified proof point in our project** — must be **spiked first** (§7 Phase 0). Pi's app-review/listing throughput and any revenue-share terms are **externally gated** and need a portal account to confirm. |

**Recommendation:** Approve a **Phase 0 spike** (host a trivial Unity WebGL build at a validated domain, open it in Pi Browser, do auth-only) before committing to payments. Auth-only is low-risk and proves the whole pipe. Payments and listing follow only if the spike's mobile perf is acceptable.

---

## 1. What Pi Browser / the Pi App Platform actually is

- **Pi Browser** is Pi Network's in-app/standalone browser (the "super-app" surface) through which Pioneers reach third-party "Pi Apps." [minepi.com/developers](https://minepi.com/developers/)
- **Pi Apps are decentralized Web 3.0 web apps** — you "put your app on any domain you want (including a `.pi` domain) and still have a fully functional Pi app." [Developer Portal guide](https://pi-apps.github.io/community-developer-guide/docs/gettingStarted/devPortal/)
- **Developer Portal** lives at **`develop.pi`** inside Pi Browser (tile on the Pi Browser home). There you: register as a developer, register an app, **declare the app's URL**, **validate domain ownership**, and **generate the Server API Key**. [Developer Portal guide](https://pi-apps.github.io/community-developer-guide/docs/gettingStarted/devPortal/)
- **Domain validation:** the portal gives you a key string; you host it at **`/validation-key.txt` at the root of your domain**, then click "Verify domain." Once verified you can open the app by typing its URL in Pi Browser. [Developer Portal guide](https://pi-apps.github.io/community-developer-guide/docs/gettingStarted/devPortal/)
- **The platform connects the web app to Pi servers + the Pi blockchain** — auth, payments, ads — via the **Pi SDK**. Frameworks are unconstrained (any JS frontend, any backend: Express/Django/Rails/etc.). [Pi App Platform SDK guide](https://pi-apps.github.io/community-developer-guide/docs/gettingStarted/piAppPlatform/piAppPlatformSDK/)

**Mainnet state (as of June 2026):** Pi launched **Open Network in Q1 2025** (Feb 20, 2025); KYC + Mainnet migration grace periods ran through **March 14, 2025**, and second-migration rollouts continued through Pi Day 2026. So mainnet, KYC, and the app platform are all **live** today. [Open Network update](https://minepi.com/blog/open-network-update/), [coinfomania 2026 tracker](https://coinfomania.com/pi-network-news-open-mainnet-kyc-migration-updates-2026/)

---

## 2. The Pi JavaScript SDK — exactly what a web app implements

Source: [pi-platform-docs SDK_reference.md](https://github.com/pi-apps/pi-platform-docs/blob/master/SDK_reference.md), [Pi SDK docs](https://pi-apps.github.io/pi-sdk-docs/).

Loaded via `<script src="https://sdk.minepi.com/pi-sdk.js"></script>` in the hosting HTML, then:

### 2.1 Init
```js
Pi.init({ version: "2.0" })               // production
Pi.init({ version: "2.0", sandbox: true }) // dev / Testnet
```

### 2.2 Authentication
```js
Pi.authenticate(scopes, onIncompletePaymentFound): Promise<AuthResult>
```
- **Scopes available:** `"username"`, `"payments"`, `"wallet_address"`.
- **Returns:** `{ accessToken, user: { uid, username } }`.
- `onIncompletePaymentFound(payment)` fires when a prior payment hit the blockchain but `status.developer_completed` is still `false` — you must finish it server-side (see §3.4).
- Our backend verifies the `accessToken` by calling Pi's `/me` endpoint (`GET https://api.minepi.com/v2/me` with `Authorization: Bearer <accessToken>`) to get the trusted `uid`/`username`.

### 2.3 Payments (user-to-app)
```js
Pi.createPayment(
  { amount: 3.14, memo: "Lanternlight pack", metadata: { sku: "skr_pouch_small" } },
  {
    onReadyForServerApproval:   (paymentId)        => { /* POST to our /approve */ },
    onReadyForServerCompletion: (paymentId, txid)  => { /* POST to our /complete */ },
    onCancel:                   (paymentId)        => { /* mark cancelled */ },
    onError:                    (error, payment?)  => { /* log via FlowTrace */ },
  }
)
```
- **`paymentData`** = `{ amount:number, memo:string, metadata:object }`.
- The flow **opens a native Pi payment dialog on top of the app**; the user reviews and submits the blockchain transaction or rejects it.
- **Requires the `payments` scope** at auth time.

### 2.4 Ads (optional secondary monetization)
```js
Pi.Ads.isAdReady("rewarded" | "interstitial")
Pi.Ads.requestAd("rewarded" | "interstitial")
Pi.Ads.showAd("rewarded" | "interstitial")  // → "AD_REWARDED" | "AD_CLOSED" | "AD_NOT_AVAILABLE" | ...
```
Gated by `Pi.nativeFeaturesList()` containing `"ad_network"`. Rewarded ads could fund **earned SKR/soft-currency drops** (covenant-friendly: time, not power).

### 2.5 Other
- `Pi.openShareDialog(title, message)` — native share (virality).
- `Pi.openUrlInSystemBrowser(url)` — break out to system browser.

---

## 3. The mandatory backend — server-side approve/complete

**You cannot do Pi payments client-only.** Every user-to-app payment requires **two server-to-server calls** to Pi, authenticated with the **Server API Key** (generated in the Developer Portal, kept as a backend secret — never in the WebGL bundle). Sources: [SDK_reference.md](https://github.com/pi-apps/pi-platform-docs/blob/master/SDK_reference.md), [pi-sdk-integration-guide](https://github.com/pi-apps/pi-sdk-integration-guide), [Platform API issue #45](https://github.com/pi-apps/pi-platform-docs/issues/45).

### 3.1 Auth header (all server calls)
```
Authorization: Key <SERVER_API_KEY>
```

### 3.2 Approve (Phase I)
On `onReadyForServerApproval(paymentId)`, our backend calls:
```
POST https://api.minepi.com/v2/payments/{paymentId}/approve
Authorization: Key <SERVER_API_KEY>
```
This tells Pi the app recognizes the payment so the user can submit the blockchain transaction. Backend should **record the paymentId + expected amount/SKU** here (so completion can be validated against what was ordered).

### 3.3 Complete (Phase III)
On `onReadyForServerCompletion(paymentId, txid)`, our backend calls:
```
POST https://api.minepi.com/v2/payments/{paymentId}/complete
Authorization: Key <SERVER_API_KEY>
Body: { "txid": "<txid>" }
```
**Security (verbatim from Pi docs):** *"Users might be running a hacked version of the SDK, pretending that they have made a payment. If the API call for Server-Side completion returns a non-200 error code, do NOT mark the payment as complete on your side."* [payments.md](https://github.com/pi-apps/pi-platform-docs/blob/master/payments.md) — **Only after a 200 from `/complete` does the backend grant the SKU/SKR** (then signal entitlement back to the client / cloud save).

### 3.4 Incomplete payments
On auth, `onIncompletePaymentFound(payment)` (and the portal's payment list) surface payments that were submitted but never completed. The backend must be able to **look up and `/complete` (or reconcile) them idempotently** — never double-grant.

### 3.5 What backend we need (minimum)
A **tiny stateless service** (Express/Cloudflare Worker/Lambda — fits the existing "thin backend" appetite) with **four endpoints**:
1. `POST /pi/verify` — verify `accessToken` via Pi `/me`, return trusted uid.
2. `POST /pi/approve` — call Pi `/approve`, persist the order (paymentId → sku/amount/uid).
3. `POST /pi/complete` — call Pi `/complete`, on 200 write the entitlement (to cloud save / SKR ledger).
4. `POST /pi/reconcile` — finish any incomplete payment idempotently.
Plus secret storage for the **Server API Key** and a small **orders table** (paymentId, sku, amount, uid, status). This is the **same backend the staged T2 cloud-save / pack-verifier already anticipates** (`DATA_ARCHITECTURE_DECISION` step 4; `WORK_ORDER_skr_store_design` §6 Stage 2) — **build it once, reuse for Pi.**

---

## 4. Recommended architecture — Unity WebGL + `.jslib` Pi bridge + thin backend

```
   PI BROWSER (mobile webview, KYC'd Pioneer)
   ┌────────────────────────────────────────────────────────────┐
   │  index.html  (our hosted page at validated domain)          │
   │   ├─ <script src="https://sdk.minepi.com/pi-sdk.js">        │
   │   ├─ Pi.init({version:"2.0"})                               │
   │   └─ Unity WebGL canvas (Unity 6 / URP build)               │
   │         │  C# [DllImport("__Internal")]  ⇅  PiBridge.jslib  │
   │         ▼                                                    │
   │   PiBridge.jslib  → calls Pi.authenticate / createPayment / │
   │                     Pi.Ads ; marshals results back to C#    │
   └───────────────┬─────────────────────────────┬──────────────┘
                   │ accessToken / paymentId/txid │ approve/complete
                   ▼                              ▼
        ┌─────────────────────┐        ┌────────────────────────────┐
        │  Pi servers          │◀──────│  OUR THIN BACKEND           │
        │  api.minepi.com/v2   │ Key   │  /verify /approve /complete │
        │  (/me /payments/...)  │        │  /reconcile  + orders table │
        └─────────────────────┘        └──────────────┬─────────────┘
                                                       │ writes entitlement
                                                       ▼
                                         T2 cloud save / ISkrLedger (staged)
```

### 4.1 The `.jslib` bridge (the only new client glue)
- A `PiBridge.jslib` in `Assets/Plugins/WebGL/` exposes JS functions Unity calls via `[DllImport("__Internal")]` (Unity's standard WebGL interop). It wraps `Pi.authenticate` / `Pi.createPayment` / `Pi.Ads.showAd` and returns results to C# via `SendMessage` / `unityInstance` callbacks.
- A C# `IPiPlatform` seam (Core) abstracts it — **`WebGLPiPlatform` (real) vs `EditorPiPlatform` (stub)** — so the game runs in the Editor and on non-Pi targets unchanged. This mirrors the existing `ISkrLedger` / `ISaveProvider` seam pattern; **no gameplay code learns about Pi.**
- **No new C# in this WO** — this is the design; the bridge is the follow-up implementation WO.

### 4.2 Hosting / packaging
- Unity WebGL output (`index.html` + Build/ + framework/wasm/data) hosted on **HTTPS** at the registered domain, with **Brotli/gzip compression** and correct MIME headers (standard Unity WebGL hosting).
- `validation-key.txt` at domain root for Pi domain validation (§1).
- The Pi `pi-sdk.js` script tag added to the Unity WebGL **HTML template** (custom `WebGLTemplates/Pi/index.html`).

---

## 5. How Pi payments map onto OUR monetization (do NOT duplicate)

Pi is **a new rail on the existing PackStore / SKR design**, not a new store. Reference, don't rebuild:

| Our existing piece | What Pi adds | Reference |
|---|---|---|
| **PackStore** (Stripe/SOL/USDC/SKR-rail; `packs.json`) | Add **`CurrencyKind.Pi`** as one more rail; a pack's `pricing` gains a `pi` field. Buying a pack in Pi runs the §3 approve/complete flow. | `WORK_ORDER_skr_store_design.md` §3; `WalletService`/`CurrencyKind` |
| **SKR held balance** (`ISkrLedger`, staged local→cloud→Solana) | Pi becomes a **real-currency on-ramp that credits SKR** — the `skr-pouch-*` SKUs purchasable with Pi. Pi ≠ SKR; **Pi buys SKR**, exactly like USD/SOL do today. | `WORK_ORDER_skr_store_design.md` §1.2, §6 |
| **Backend payment verifier** (anticipated at T2 step 4) | The §3.5 `/approve` + `/complete` service **is** that verifier, specialized for Pi. Same orders-table + entitlement-writer. | `DATA_ARCHITECTURE_DECISION_2026-06-27.md` step 4 |
| **Entitlement fulfillment** (`OwnedItemIds` + economy + token tray) | **Unchanged.** Pi completion → backend → the one existing grant sink. | `WORK_ORDER_skr_store_design.md` §3 rule 4 |
| **Ethical covenant** (cosmetics/convenience only) | **Unchanged and still binding** — Pi buys the same cosmetic/convenience SKUs; never power. | `WORK_ORDER_skr_store_design.md` §1.3 |

**Net:** the only genuinely new artifacts are (1) the `PiBridge.jslib` + `IPiPlatform` seam, (2) the `CurrencyKind.Pi` rail + `pi` price field, and (3) the thin approve/complete backend (shared with cloud-save). Everything downstream of "payment completed" already exists.

---

## 6. Constraints, risks & unknowns (honest list)

### 6.1 Hard technical risks
1. **Unity WebGL on mobile Pi Browser is the #1 risk — unproven for us.** Unity 6 added Android/iOS browser support, but mobile WebGL is constrained by **single-threaded JS/WASM CPU, limited/absent multithreading, mobile-browser memory caps, and large initial download** (wasm + data). Pi Browser is a **mobile webview** — exactly the constrained case. Our hub+Knight build must be **aggressively size-optimized** (this is *why* `DATA_ARCHITECTURE_DECISION` T1 Addressables-remote exists — stream heroes/enemies, ship a tiny base bundle). **Must be spiked before any commitment.** [Unity Web mobile compatibility](https://docs.unity3d.com/6000.4/Documentation/Manual/webgl-browsercompatibility.html), [Unity Web runtime updates](https://unity.com/blog/engine-platform/web-runtime-updates-enhance-browser-experience), [Unity→WebGL mobile guide 2026](https://ilogos.biz/unity-to-webgl-porting-guide/)
2. **Initial load time / data caps.** Even an optimized URP build is multiple MB; mobile users on data may bounce. Need Brotli, Addressables-remote streaming, a loading screen, and a hard size budget.
3. **Input/UX.** Our UI is code-built uGUI (good — no UXML), but touch input, safe-area, and the Pi payment dialog overlay must be tested in the actual Pi Browser, not desktop Chrome.

### 6.2 Platform / policy risks
4. **KYC + app review gate real-Pi revenue.** Real-Pi (mainnet) payments require the user to be **KYC-verified** and generally the app to be **approved/listed**; development uses **Sandbox/Testnet**. Throughput and exact review criteria for listing in the Pi ecosystem directory are **portal-gated — need a developer account to confirm current SLA.** [Open Network update](https://minepi.com/blog/open-network-update/)
5. **Pi as real currency / value volatility.** Pi's market price and external convertibility are volatile and region-restricted; pricing packs **in Pi** needs a pricing policy (peg to a USD target? fixed Pi amounts?). Treat Pi like the SOL/USDC rail — a **secondary optional rail**, not the primary one.
6. **Compliance / store policy.** Shipping as a Pi web app **sidesteps Apple/Google IAP cuts** (it's a website), but also means **no App Store / Play Store distribution via this channel** — Pi Browser is the storefront. Keep Pi as **one distribution channel**, not the only one.
7. **Ecosystem maturity.** Pi's developer docs are partly community-maintained and have moved/changed; some endpoint details (exact `/complete` body, ads availability per region) should be **re-verified against the live portal** at implementation time, not taken as frozen. [community-developer-guide](https://pi-apps.github.io/community-developer-guide/)

### 6.3 Unknowns to close before Phase 1+
- [ ] Does our optimized Unity WebGL hub+Knight build **actually run acceptably** inside Pi Browser on a mid-range phone? (**Phase 0 spike answers this.**)
- [ ] Current Pi **app-review/listing** requirements + timeline (portal account).
- [ ] Any **revenue share / fees** Pi takes on payments.
- [ ] Whether **Pi Ads** are available in our target regions and worth wiring for earned-SKR drops.
- [ ] Pricing policy for packs denominated in Pi (peg vs fixed).

---

## 7. Phased plan (de-risked, owner-gated at each step)

**Phase 0 — Viability spike (DO THIS FIRST; ~days, no game logic).**
Host a **trivial Unity WebGL build** (loading screen + a button) at a real HTTPS domain, add `validation-key.txt`, register the app + validate the domain in `develop.pi`, open it in **Pi Browser on a real phone**. Add the `pi-sdk.js` tag and call `Pi.init` + `Pi.authenticate(['username'])`. **Gate:** does it load + authenticate at acceptable speed on mobile? **If no → stop / re-scope** (e.g. a lightweight companion web app rather than the full game).

**Phase 1 — Auth-only integration (low risk).**
`PiBridge.jslib` + `IPiPlatform` seam + `WebGLPiPlatform`; `/verify` backend endpoint (token → uid via `/me`). Sign the Pioneer in, greet by username, persist Pi uid against the (local→cloud) save. **No money yet.** Proves the full client↔bridge↔backend pipe.

**Phase 2 — Payments (Sandbox → Mainnet).**
Add `Pi.createPayment` to the bridge; build the `/approve` + `/complete` + `/reconcile` backend (§3.5) + orders table; wire **`CurrencyKind.Pi`** into PackStore and the `skr-pouch-*` SKUs. Develop entirely in **Sandbox/Testnet**, then flip to mainnet. **Gate:** end-to-end test buy of a Token Pouch in Pi credits SKR via the existing fulfillment path.

**Phase 3 — Listing + (optional) Ads.**
Submit for app review / ecosystem listing; finalize domain, privacy, support. Optionally wire `Pi.Ads` rewarded ads → earned-SKR drops (covenant-safe).

**Parallelism:** Phases 0–1 are **fully isolated** (own domain, own backend, a seam the game ignores) → run in the Monetization/Backend lane without touching gameplay or the Unity gate, per §9.

---

## 8. What NOT to do (scope guard)

- **Do NOT** greenfield a new store/wallet/economy — Pi is a **rail on PackStore + an on-ramp to SKR**. Reuse `WORK_ORDER_skr_store_design.md` + `WalletService`/`CurrencyKind`.
- **Do NOT** put the **Server API Key** in the WebGL bundle or any client code — backend secret only.
- **Do NOT** grant any SKU on the client's word — **only after a 200 from Pi `/complete`** (server-verified).
- **Do NOT** commit to payments/listing before the **Phase 0 mobile-WebGL spike** proves viability.
- **Do NOT** build a Pi-specific backend — build the **shared thin approve/complete + entitlement service** the staged T2 cloud-save already needs.
- **Do NOT** introduce any combat/power grant via Pi — the SKR covenant firewall (`WORK_ORDER_skr_store_design.md` §5.4) still binds.
- **Do NOT** hand-edit scene files or write `.cs` from this WO — design only; implementation is a follow-up WO.

---

## 9. Sources

- Pi Developers landing — [minepi.com/developers](https://minepi.com/developers/)
- Pi Developer Portal guide (registration, URL declaration, `validation-key.txt` domain validation, Server API Key) — [pi-apps.github.io/.../devPortal](https://pi-apps.github.io/community-developer-guide/docs/gettingStarted/devPortal/)
- Pi App Platform SDK guide (web apps, any-domain, frameworks) — [pi-apps.github.io/.../piAppPlatformSDK](https://pi-apps.github.io/community-developer-guide/docs/gettingStarted/piAppPlatform/piAppPlatformSDK/)
- Pi App Platform APIs guide — [pi-apps.github.io/.../piAppPlatformAPIs](https://pi-apps.github.io/community-developer-guide/docs/gettingStarted/piAppPlatform/piAppPlatformAPIs/)
- Pi SDK reference (`Pi.init`, `authenticate` scopes, `createPayment`, `Pi.Ads`, `nativeFeaturesList`) — [github.com/pi-apps/pi-platform-docs/SDK_reference.md](https://github.com/pi-apps/pi-platform-docs/blob/master/SDK_reference.md)
- Pi payments server flow + security note — [github.com/pi-apps/pi-platform-docs/payments.md](https://github.com/pi-apps/pi-platform-docs/blob/master/payments.md)
- Pi backend integration guide (`Authorization: Key`, approve/complete) — [github.com/pi-apps/pi-sdk-integration-guide](https://github.com/pi-apps/pi-sdk-integration-guide)
- Platform API endpoint discussion (`api.minepi.com/v2/payments/.../approve`) — [github.com/pi-apps/pi-platform-docs/issues/45](https://github.com/pi-apps/pi-platform-docs/issues/45)
- Pi Open Network (mainnet) launch — [minepi.com/blog/open-network-update](https://minepi.com/blog/open-network-update/)
- Pi KYC / mainnet migration state 2026 — [coinfomania.com/.../open-mainnet-kyc-migration-updates-2026](https://coinfomania.com/pi-network-news-open-mainnet-kyc-migration-updates-2026/)
- Unity Web (WebGL) mobile browser compatibility — [docs.unity3d.com/6000.4/.../webgl-browsercompatibility](https://docs.unity3d.com/6000.4/Documentation/Manual/webgl-browsercompatibility.html)
- Unity Web runtime updates (mobile browser support) — [unity.com/blog/.../web-runtime-updates](https://unity.com/blog/engine-platform/web-runtime-updates-enhance-browser-experience)
- Unity→WebGL mobile porting guide 2026 — [ilogos.biz/unity-to-webgl-porting-guide](https://ilogos.biz/unity-to-webgl-porting-guide/)

### Internal canon referenced (do not duplicate)
- `docs/DATA_ARCHITECTURE_DECISION_2026-06-27.md` — staged local→cloud→Solana; T1 Addressables-remote (the size lever Pi WebGL needs); T2 cloud backend (shared with the Pi verifier).
- `WorkOrders/WORK_ORDER_skr_store_design.md` — held-SKR premium store, `ISkrLedger`, PackStore on-ramp, ethical covenant + validator firewall.
- Memory `data-architecture-hybrid-db-direction`, `combat-pivot-single-hero-northstar` (V1 = hub + Knight = the small build that can fit WebGL).

> **OWNER RULING 2026-08-21 (verbal, this session):** Pi accepts WEB UI ONLY and no web-UI push has been made; Pi offers no funding and takes 30% commission. Owner will revisit at some point. The DEEP file is the live decision doc - this feasibility pass is the older of the two.
