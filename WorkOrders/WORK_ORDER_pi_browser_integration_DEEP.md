# WORK ORDER — Pi Browser Integration (DEEP synthesis, decision-grade)

**Type:** RESEARCH SYNTHESIS + DECISION DOC + PHASED PLAN. **Legwork only — NO `.cs`, no game code.** Implementation is gated, follow-up WOs per phase.
**Status:** DRAFT FOR OWNER REVIEW — decision-grade. Not yet READY TO IMPLEMENT.
**Author lane:** Monetization/Backend + Distribution (CLAUDE.md §9 — isolated from gameplay; no Unity gate held).
**Date:** 2026-06-28 (Pi2Day 2026).
**Relationship to existing canon:** This is the **deep, five-stream synthesis** that supersedes the shorter `WorkOrders/WORK_ORDER_pi_browser_integration.md` as the decision document (that draft remains valid as the quick-reference; this one is the full picture for the go/no-go call). It **layers on top of** — and does **not** duplicate — the four ratified design WOs it references:
- `PI_INTEGRATION_SPEC.md` (owner-resolved 2026-06-26 wire-ready contracts)
- `docs/DATA_ARCHITECTURE_DECISION_2026-06-27.md` (staged local→cloud→Solana; T1 Addressables-remote; T2 cloud backend)
- `WorkOrders/WORK_ORDER_skr_store_design.md` (held-SKR premium token + store)
- `WorkOrders/WORK_ORDER_offline_storage_logic.md` + `WorkOrders/WORK_ORDER_economy_store_packs.md` (the packs Pi would sell)

Pi is a **new payment rail + distribution channel** bolted onto the existing economy — **never a new economy, never the spine**.

---

## 1. EXECUTIVE VERDICT

### 1.1 The one-paragraph answer
**Yes — we can tie the game into Pi Browser, and the integration plumbing is low-risk wiring this repo has already designed correctly and partly built (the `pi-backend/` Cloudflare Worker is code-ready). The binding question is NOT Pi — it is whether our ~186 MB Unity 6 / URP / WebGL build survives the iOS WKWebView memory ceiling on a real phone.** That single unknown gates everything downstream. The cheapest, most decisive next action is the **mobile-webview gate test** (host the existing build, open it in Pi Browser on a real iPhone + Android) — already sequenced first by both owner and CLI (`PI_INTEGRATION_SPEC.md` sequencing note; `docs/webgl-hosting-notes.md`). Do not fund the bridge/payment integration until that gate passes.

### 1.2 Verdict table

| Question | Verdict | Confidence |
|---|---|---|
| Is the platform real and live? | **Yes.** Pi launched **Open Network (open mainnet) Feb 20, 2025**; Developer Portal (`develop.pi` / `pi://develop.pinet.com`), JS SDK v2.0, Payments API, Ads, are live; 220+ mainnet apps run in Pi Browser today. Smart contracts (Protocol 20 groundwork Mar 2026 → Protocol 23 activated mainnet **2026-05-11**). [[Open Network launch](https://minepi.com/blog/open-network-launch-date/)] [[Pi Day 2026](https://minepi.com/blog/pi-day-2026/)] [[crypto.news first-year](https://crypto.news/pi-networks-first-year-on-open-mainnet-what-actually-happened/)] | **High** |
| What kind of app does Pi run? | **Web apps only.** A Pi app is a website you host at your own HTTPS domain, registered in the portal, **loaded inside Pi Browser via an iFrame**. There is **no native game runtime** — integration is the **JavaScript SDK over a webview**. [[Pi Browser Intro](https://pi-apps.github.io/community-developer-guide/docs/gettingStarted/piBrowserIntroduction/)] | **High** |
| Can our Unity game be that web app? | **Yes, via a Unity WebGL build** at a validated URL, with a **`.jslib` bridge** that calls `Pi.authenticate` / `Pi.createPayment` / `Pi.Ads`. The repo's worry that "crypto SDKs don't run on WebGL" (`docs/webgl-hosting-notes.md`) **does not apply** — the Pi SDK is page-level JavaScript reached over the bridge, never a native lib compiled into the wasm. | **High** |
| Are real Pi payments usable now? | **Yes on mainnet**, with **Sandbox/Testnet** for dev (`Pi.init({sandbox:true})`). Payments now **persist across sessions** (single-session limit removed, Pi Day 2026). But real-Pi only works for **KYC-verified + migrated** users — a gated minority (~16–17M of 60M+ claimed). [[Pi Day 2026](https://minepi.com/blog/pi-day-2026/)] [[cryptotimes KYC/migration](https://www.cryptotimes.io/2026/05/13/pi-network-explains-ai-powered-kyc-as-mainnet-migration-surpasses-16-7m-users/)] | **High** |
| How hard? | **Medium overall, front-loaded on ONE risk.** SDK wiring = days. Backend = **already coded** (`pi-backend/src/index.ts`). The hard/unknown part is **Unity-WebGL-on-mobile viability** (size/memory/load on iOS WKWebView) + the **KYC/listing** gate for live revenue. | **Medium** |
| What is blocked NOW vs later? | **Blocked NOW (must spike first):** proof that our heavy build loads on a real phone in Pi Browser. **Blocked LATER (external gates):** developer KYC, app review/listing, mainnet payment go-live. **Not blocked:** the backend (deployable to Testnet today), the architecture, the economy mapping. | **High** |

### 1.3 What's blocked NOW vs later — explicit

**Blocked right now (one hard gate):**
- **The mobile-webview viability gate.** Our build is **~186 MB Brotli** (`WebGL.data.br` 174 MB + `WebGL.wasm.br` 13 MB) — **2–4× the practical mobile-WebGL ceiling** (community comfortable total ≈ 50–100 MB). iOS WKWebView has a **~1.4–1.5 GB single-page memory ceiling** and Unity WebGL routinely balloons to 700 MB–1.5 GB on iOS; memory-growth steps can crash-reload the tab. **No public example of a heavy Unity 3D WebGL title shipping in Pi Browser exists** — the ecosystem norm is sub-megabyte HTML5. This is the live-or-die unknown. [[Unity iOS WebGL stability](https://discussions.unity.com/t/stability-of-large-games-with-unity-webgl-on-ios/888745)] [[Unity memory/crash iOS](https://discussions.unity.com/t/webgl-memory-increment-issue-and-crash-on-ios/894771)] [[Unity mobile web optimization](https://docs.unity3d.com/Manual/web-optimization-mobile.html)]

**Blocked later (external, sequence after the gate):**
- **Developer KYC** — required before you can submit for mainnet listing and before *users* can pay real Pi.
- **App review / ecosystem listing** — the 7 listing rules (§4.2); the 2025 pre-approval gate was removed, so eligible devs can apply directly. [[Mainnet Listing Requirements](https://pi-apps.github.io/community-developer-guide/docs/gettingStarted/mainnetListingRequirements/)]
- **Mainnet payment go-live** — develop on Testnet/Sandbox, flip to mainnet only after the above.

**Not blocked (safe to do in parallel, any time):**
- Deploy `pi-backend/` to **Testnet** (it is standalone, no-regret, already corrected against the live Pi API).
- Register a **Testnet** app in the Developer Portal; host `validation-key.txt`; grab the Server API Key.
- Run the size-reduction work (Addressables-remote / texture diet) that the gate test will likely require anyway.

---

## 2. RECOMMENDED ARCHITECTURE

### 2.1 The shape (unchanged from `PI_INTEGRATION_SPEC.md` — confirmed correct by all five streams)

```
   PI BROWSER  (mobile webview iFrame; KYC'd + migrated Pioneer)
   ┌──────────────────────────────────────────────────────────────────┐
   │  index.html  (our WebGLTemplates/Pi template, validated domain)   │
   │   ├─ <script src="https://sdk.minepi.com/pi-sdk.js">              │
   │   ├─ Pi.init({ version:"2.0" [, sandbox:true] })                  │
   │   └─ Unity WebGL canvas (Unity 6 / URP / IL2CPP)                  │
   │         │  C# [DllImport("__Internal")]  ⇅  PiBridge.jslib        │
   │         ▼                                                          │
   │  PiBridge (DontDestroyOnLoad GameObject "PiBridge")               │
   │   C#→JS: CreatePayment({paymentId,amount,memo}) → Pi.createPayment │
   │   JS→C#: SendMessage("PiBridge","OnPiCallback", json)             │
   └───────────────┬──────────────────────────────┬───────────────────┘
        accessToken│  paymentId / txid             │ approve / complete
                   ▼                               ▼
        ┌─────────────────────┐         ┌─────────────────────────────────┐
        │  Pi servers          │◀───────│  pi-backend/ (Cloudflare Worker) │
        │  api.minepi.com/v2   │  Key   │  /approve /complete /reconcile   │
        │  /me  /payments/{id} │  <key> │  + idempotency KV (paymentId)    │
        └─────────────────────┘         └────────────────┬────────────────┘
                                                          │ on /complete 200
                                                          ▼
                                   PackStore.ApplyPackContents  → entitlement
                                   (→ ISkrLedger / GameState.Resources / OwnedItemIds)
```

**Two-phase, server-mediated payment is mandatory and non-negotiable** — Pi requires a server holding the secret API key to bracket every payment with `/approve` (before the user signs) and `/complete` (after the txid exists). The frontend can never be trusted to confirm money moved. [[Pi SDK reference](https://github.com/pi-apps/pi-platform-docs/blob/master/SDK_reference.md)] [[payments.md](https://github.com/pi-apps/pi-platform-docs/blob/master/payments.md)] [[Payment Flow](https://pi-apps.github.io/community-developer-guide/docs/importantTopics/paymentFlow/piPaymentFlow/)]

### 2.2 The three genuinely-new artifacts (everything else already exists)

1. **`PiBridge.jslib` + `IPiPlatform` seam** (the only new client glue). Wraps `Pi.authenticate` / `Pi.createPayment` / `Pi.Ads.showAd`; marshals results to C# via `SendMessage`. A `WebGLPiPlatform` (real) vs `EditorPiPlatform` (stub) seam means the game runs unchanged in the Editor and on non-Pi targets — mirrors the existing `ISkrLedger` / `ISaveProvider` pattern. **No gameplay code learns about Pi.** Contract is fully specified in `PI_INTEGRATION_SPEC.md` §2.
2. **`CurrencyKind.Pi` rail + a `pi` price field** on packs — one more rail beside `Sol`/`Usdc`/`Skr` in `WalletService.CurrencyKind` (`WORK_ORDER_economy_store_packs.md` §1; `WORK_ORDER_skr_store_design.md` §3).
3. **The thin `/approve` + `/complete` backend** — **already built** as `pi-backend/` (Cloudflare Worker, `src/index.ts` + `wrangler.toml`), corrected against the live API (path-based `/v2/payments/{id}/approve`, `Authorization: Key`, `/complete` body `{ txid }`, idempotency KV, `/reconcile`). Grant point = the existing `PackStore.ApplyPackContents`. Deploy is owner-gated (needs Cloudflare account + Pi credentials). [[pi-backend/README.md](../pi-backend/README.md)]

**Everything downstream of "payment completed" already exists** — the entitlement writer, `OwnedItemIds`, the economy service, the token tray.

### 2.3 The SDK contract (what the bridge/backend implement) — verified

- **Init:** `Pi.init({ version:"2.0", sandbox?:boolean })`. Script-include only; `window.Pi` exists only inside Pi Browser. [[SDK_reference](https://github.com/pi-apps/pi-platform-docs/blob/master/SDK_reference.md)]
- **Auth:** `Pi.authenticate(scopes, onIncompletePaymentFound) → { accessToken, user:{uid,username} }`. Scopes: `username`, `payments` (required for `createPayment`), `wallet_address`. **Backend MUST verify** `accessToken` via `GET https://api.minepi.com/v2/me` (`Authorization: Bearer <accessToken>`) — never trust the frontend's claimed identity.
- **Pay (U2A):** `Pi.createPayment({amount, memo, metadata}, {onReadyForServerApproval, onReadyForServerCompletion, onCancel, onError})`. The UI is **not interactive until the server approves**; approval retries ~every 10s if your `/approve` fails.
- **Server calls** (base `https://api.minepi.com/v2`, `Authorization: Key <Server API Key>`, key server-side only): `POST /payments/{id}/approve`, `POST /payments/{id}/complete` body `{ "txid": "..." }`, `POST /payments/{id}/cancel`, `GET /payments/incomplete_server_payments` (A2U reconcile). **Deliver goods ONLY on a 200 from `/complete`.** [[platform_API](https://github.com/pi-apps/pi-platform-docs/blob/master/platform_API.md)]
- **Incomplete-payment handling is mandatory and easy to miss:** Pi enforces **one outstanding payment per user at a time** — a stale payment **blocks all new payments for that user** until completed/cancelled. `onIncompletePaymentFound` fires on every auth; the handler must `/complete` (if on-chain) or `/cancel`. **Idempotency:** store `paymentId`+`txid` the moment you see them; check before granting so a retry never double-grants. [[Payment Flow](https://pi-apps.github.io/community-developer-guide/docs/importantTopics/paymentFlow/piPaymentFlow/)]
- **No published rate limits** in Pi's docs — treat as "undocumented, not unlimited"; design defensively (backoff + idempotency + scheduled reconcile).
- **Ads (optional, secondary):** `Pi.Ads.showAd("rewarded")` → verify server-side via `GET /v2/ads_network/status/:adId`, reward **only** on `mediator_ack_status === "granted"`. Feature-detect with `Pi.nativeFeaturesList().includes("ad_network")`. [[ads.md](https://github.com/pi-apps/pi-platform-docs/blob/master/ads.md)]
- **A2U payouts** (rewards) need the official `pi-backend` (pi-nodejs) package + the app wallet **private seed** (server-side only) — out of V1 scope; note for later. [[pi-nodejs README](https://github.com/pi-apps/pi-nodejs/blob/main/README.md)]

### 2.4 How Pi reuses the staged data + SKR + offline-storage + pack designs (REFERENCE — do NOT duplicate)

Pi is **one more rail on the existing PackStore + on-ramp to SKR**, sitting cleanly on the staged local→cloud→Solana architecture. Reference these; rebuild nothing:

| Existing piece (its WO) | What Pi adds | Why it just works |
|---|---|---|
| **Staged data: local→cloud→Solana** (`docs/DATA_ARCHITECTURE_DECISION_2026-06-27.md`) | Pi's `/approve`+`/complete` service **is** the T2 backend payment-verifier the cloud stage already anticipates (step 4). **Build it once, reuse for Pi.** | Same orders-table + entitlement-writer; Pi is just the first concrete consumer. |
| **T1 Addressables-remote** (same doc; staged as **WO-545**) | This is the **size lever Pi WebGL needs** — stream heroes/enemies/textures off a CDN so the base bundle is a tiny instant-load shell, not a 186 MB wall. | Unity explicitly recommends exactly this for mobile WebGL; already on our roadmap. [[Unity mobile web optimization](https://docs.unity3d.com/Manual/web-optimization-mobile.html)] |
| **SKR held-token + store** (`WORK_ORDER_skr_store_design.md`) | Pi becomes a **real-currency on-ramp that credits SKR** — the `skr-pouch-*` SKUs become purchasable with Pi exactly as USD/SOL/USDC do. **Pi ≠ SKR; Pi buys SKR.** Balance flows through the `ISkrLedger` seam (`LocalSkrLedger` V1 → cloud → Solana). | `ISkrLedger` already abstracts where the balance lives; adding a rail doesn't touch the ledger, catalog, or player. |
| **Offline-storage tiers** (`WORK_ORDER_offline_storage_logic.md`) | Pi can buy the **`skrFastTrack` / `packLinkedTiers`** storage upgrades (own a bigger barn now) — a pure **convenience** time-saver, covenant-safe. | The tier ladder + pack-linked grants already exist as data; Pi is just another way to pay for the same grant. |
| **Economy packs** (`WORK_ORDER_economy_store_packs.md`) | Resource / farming-boost / storage packs gain a **Pi price** alongside gold/soft/SKR. Buying in Pi runs the §2.1 flow → same `ApplyPackContents`. | The pack schema already plans multi-rail pricing; `CurrencyKind.Pi` is additive. |
| **PI_INTEGRATION_SPEC.md "starved Pi economy"** (§3) | V1 hook = **ONE pack + ONE timer-skip** entitlement, generous free path, Pi = optional accelerator/sink, **no pay-to-win**. | The minimal proof-of-loop is already specified and matches the ethical covenant. |

**The ethical covenant is unchanged and still binding:** Pi buys the **same cosmetic/convenience SKUs** — never power. The SKR validator firewall (`WORK_ORDER_skr_store_design.md` §5.4 — a `combat` category fails the build) still gates every grant Pi can reach.

### 2.5 Pricing policy for Pi (new, informed by the competitive stream)

- **Price packs in TINY Pi amounts.** The canonical Pi micro-transaction is FruityPi's **0.1 Pi power-up**; developer sets the price, no platform-enforced price. Match real ~$0.12–0.15 market value, **not** the GCV myth ($314,159/Pi — Pi moderators publicly reject it; pricing to it kills willingness to spend). [[FruityPi](https://www.ainvest.com/news/pi-network-launches-fruitypi-game-boost-chain-activity-2506/)] [[GCV myth rejected](https://coinfomania.com/pi-network-moderators-reject-gcv-myth-as-map-of-pi-2-0-nears-launch/)]
- **Keep the soft/idle economy 100% off-chain.** Wood/iron/grain, life-force, echo workforce stay local soft currency — Pi only ever touches the **hard cosmetic/convenience store**. This mirrors CiDi Games' deliberate separation of settlement-coin vs score vs trade-token. [[CiDi structured economy](https://www.hokanews.com/2026/05/cidi-games-builds-structured-in-game.html)]
- **Treat Pi income as speculative upside, not bankable revenue.** PI is ~95% off its peak, convertible only via tier-2 exchanges, not legal tender anywhere. Book/plan in fiat-equivalent; Pi is a bonus channel. [[crypto.news first-year](https://crypto.news/pi-networks-first-year-on-open-mainnet-what-actually-happened/)]
- **Open question (route to owner):** peg Pi prices to a USD target (re-quote as Pi price moves) vs fixed Pi amounts. Recommend a config-driven peg so the data, not code, sets the number (owner thinks in data structures).

---

## 3. PHASED PLAN (de-risked, owner-gated at each step)

### Phase 0 — Mobile-webview viability gate (DO THIS FIRST; ~hours-to-days; no game logic, no bridge)
**The cheapest decisive bit of information in the whole Pi track.** Both owner and CLI already sequenced it first.
1. Host the current `Builds/WebGL/` on **itch.io** (purpose-built for big WebGL; handles 186 MB) **or a CDN/object store** (S3+CloudFront / Cloudflare R2 / Backblaze). **Not Vercel** — the 174 MB single `.data.br` risks free-tier rejection. [[webgl-hosting-notes](../docs/webgl-hosting-notes.md)]
2. Open it in **Pi Browser on a real iPhone (WKWebView = worst case)** AND **a real Android (Chromium = best case)**.
3. **Oracle:** does it (a) load, (b) hold steady FPS, (c) survive ~10 min without a `webglcontextlost` / crash-reload — on **each** device?
- **GATE:** If iOS fails (likely at 186 MB) → the verdict is **not** "Pi is unviable" — it's "**do the Addressables-remote + texture-diet shrink (WO-545) first, re-test, then build the bridge.**" Android-Chromium will probably pass today; iOS-WKWebView is the gate the size pass exists to clear.
- **Note:** can be run with a thin auth-only test page too (Phase 1 overlaps), but the *size/memory* answer comes from the real build.

### Phase 1 — Pi auth-only (low risk; proves the whole pipe without money)
- Register a **Testnet** app in `develop.pi`; host `validation-key.txt` at domain root; verify domain; grab Server API Key.
- Add `pi-sdk.js` to a `WebGLTemplates/Pi/index.html`; call `Pi.init` + `Pi.authenticate(['username'])`.
- Build `PiBridge.jslib` + `IPiPlatform` seam + `WebGLPiPlatform`; backend `/verify` endpoint (accessToken → uid via `/me`).
- Sign the Pioneer in, greet by username, persist the Pi uid against the (local→cloud) save. **No money yet.**
- **GATE:** auth round-trips client↔bridge↔backend at acceptable mobile speed.

### Phase 2 — Pi payments for packs (Sandbox → Mainnet)
- Add `Pi.createPayment` to the bridge; deploy `pi-backend/` (`/approve` + `/complete` + `/reconcile` + idempotency KV) — **start on Testnet** (swap `PI_BASE`).
- Wire `CurrencyKind.Pi` into PackStore + the `skr-pouch-*` SKUs (and the §2.4 economy/offline-storage packs).
- Wire `onIncompletePaymentFound` → `/reconcile` on every auth (mandatory).
- Instrument the full flow with FlowTrace (`createPayment → /approve → sign → /complete`) per CLAUDE.md §12 — stuck/incomplete payments are the known failure mode.
- **GATE:** end-to-end Testnet buy of a Token Pouch in Pi credits SKR via the existing `ApplyPackContents` path; incomplete-payment recovery works. Then flip to mainnet.

### Phase 3 — Listing + KYC + grant/hackathon submission
- Complete **developer KYC** (required to list and to take real Pi).
- Comply with the **7 listing rules** (§4.2): functional/professional UI, Pi Auth only (no email/3rd-party login), Pi-only transactions, minimize external redirection, limit data collection, no trademark/branding violations (**domain cannot start with "pi"; do not put "Pi" in the game name without permission**).
- Tighten the backend for mainnet: CORS `*` → the Pi app origin; per-pack entitlement mapping (V1 = one `pi_pack_small`); move grant authority server-side once a game backend exists (anti-cheat).
- Submit to the **ecosystem directory**; optionally cultivate **Directory Staking** for discovery (CiDi drew 3.19M Pi staked). Optionally wire `Pi.Ads` rewarded → earned-SKR drops (covenant-safe).
- **Submit a Pi Hackathon entry** at a month-end deadline + file a **Pi Network Ventures** application (§5.2).

**Parallelism (CLAUDE.md §9):** Phases 0–2 backend live in the Monetization/Backend lane, fully isolated — own domain, own Worker, a seam the game ignores — so they never touch gameplay or the single Unity gate. The `pi-backend/` Worker is standalone and safe to deploy/test on Testnet at any time, regardless of the game-side gate.

---

## 4. RISKS & UNKNOWNS (with honest confidence levels)

### 4.1 Technical
| Risk | Assessment | Confidence |
|---|---|---|
| **Unity WebGL on iOS WKWebView (THE gate).** 186 MB build vs ~1.4 GB iOS page-memory ceiling; Unity WebGL balloons to 700 MB–1.5 GB on iOS; growth steps crash-reload; no precedent of a heavy Unity 3D title in Pi Browser. | **High risk it fails at current size; high confidence the Addressables-remote + texture diet is the mitigation** (already staged as WO-545). Android likely passes today. | **Med-High** on the risk; **Med** on whether the shrink fully clears iOS (unproven for us). |
| **First-load download size** over mobile data (174 MB `.data`). | Real bounce risk; needs Brotli (have it), Addressables streaming, loading shell, hard size budget. No reliable webview caching guarantee — treat cold open as the whole budget. | **High** |
| **Safari/Metal shader + IndexedDB-in-iframe quirks.** URP shaders usually fine but must be verified on a real iPhone; IndexedDB disabled in Safari iframes can affect Unity's caching/save path. | Tractable, but verify on device, not in-editor. Touch input is already a solved lane (recent iPad fix commit). | **Med** |
| **Backend correctness / incomplete-payment handling.** | **Low risk** — already coded + corrected against the live API; idempotency KV + `/reconcile` present. Standard, well-documented flow. | **High** (low risk) |

### 4.2 Platform / policy
| Risk | Assessment | Confidence |
|---|---|---|
| **KYC + migration gate the paying audience.** Only ~16–17M KYC'd+migrated of 60M+ claimed; design for a **gated minority**, handle un-migrated users gracefully. | Real constraint; do not model the headline user count as paying users. | **High** |
| **App review tightening ("proof before profit" / PiRC1).** Utility-first posture; "illusory utility" targeted for removal. | **A real playable game with genuine convenience/cosmetic utility fits this well** — net positive if we lead with use, not token speculation. | **Med-High** |
| **Branding restriction.** Using "Pi" in app name/branding is restricted; domain cannot start with "pi"; trademark compliance is an explicit hackathon requirement. | Easy to comply — **scrub any "Pi" from game branding** pending permission; Pi is a payment rail, not the brand. | **High** |
| **Listing throughput / revenue-share / exact review SLA** are **portal-gated** — need a developer account to confirm current terms. | Unknown until we register; the 2025 removal of the pre-approval gate is favorable. | **Low** (undocumented externally) |

### 4.3 Economic
| Risk | Assessment | Confidence |
|---|---|---|
| **PI value volatility / illiquidity.** ~$0.12–0.15, ~95% off peak; tier-2 exchanges only; not legal tender. | **Treat Pi as soft/volatile in-ecosystem currency, never reliable real-money revenue.** Don't peg the economy to a stable USD value. Cosmetic/convenience ONLY. | **High** |
| **Circular "ghost-app" economy.** Most Pi spending is Pioneers paying Pioneers; many apps show little real engagement; novelty spikes then fades. | Real ceiling on monetization; **instrument our own funnel from day one** — no credible LTV/ARPU/D30 data exists for Pi games. | **High** |
| **Ad-network ceiling.** Closed earn-loop, structurally low RPMs. | **Do not model ads as a revenue pillar** — marginal supplementary income at best. | **High** |
| **Reputation overhang** (persistent "scam/MLM" criticism, centralized nodes). | Keep Pi an **optional rail**, never the spine; removable without gutting the game. | **Med** |

### 4.4 Unknowns to close before committing past Phase 1
- [ ] Does our **optimized** (post-WO-545) build run acceptably in Pi Browser on a **mid-range phone**? (Phase 0 answers the current build; re-test after the shrink.)
- [ ] Current Pi **app-review/listing** SLA + any **revenue share/fees** Pi takes. (Portal account.)
- [ ] **Pricing policy** for Pi-denominated packs — USD-peg vs fixed Pi.
- [ ] Whether **Pi Ads** are available in target regions and worth wiring.
- [ ] Whether iOS passes at all even shrunk (the one genuinely unproven-for-us item).

---

## 5. CONCRETE NEXT-STEPS CHECKLIST + OPPORTUNITY CALENDAR

### 5.1 Next-steps checklist (ordered, owner-gated)
1. **[NOW, cheapest] Run Phase 0 gate test.** Host current `Builds/WebGL/` (itch.io/CDN, not Vercel) → open in Pi Browser on a real iPhone + Android → record load/FPS/10-min-survival. **This single result decides the whole track.**
2. **[Parallel, no-regret] Register a Testnet app** in `develop.pi`; host `validation-key.txt`; capture the **Server API Key** + app wallet. (Needs Pi Browser + a Pi account.)
3. **[Parallel, no-regret] Deploy `pi-backend/` to Testnet** (`wrangler kv namespace create PAYMENT_KV`; `wrangler secret put PI_API_KEY` / `PI_APP_ID`; `wrangler deploy`). Owner action — needs Cloudflare account + Pi credentials.
4. **[If Phase 0 iOS fails] Schedule WO-545 (Addressables-remote) + texture-diet** as the prerequisite shrink; re-run the gate. (This is on the roadmap regardless.)
5. **[After gate passes] Greenlight Phase 1** (auth-only) as a follow-up IMPLEMENTATION WO — `PiBridge.jslib` + `IPiPlatform` seam + `/verify`.
6. **[Phase 2 follow-up WO] Payments** — `CurrencyKind.Pi` + Sandbox→mainnet + `onIncompletePaymentFound`→`/reconcile` + FlowTrace.
7. **[Phase 3] Developer KYC; scrub "Pi" from branding; comply with the 7 listing rules; submit listing + a hackathon entry + a Ventures application.**
8. **[Ongoing] Instrument our own funnel** (auth → store-open → buy → complete) — Pi's headline numbers are not our numbers.

### 5.2 Opportunity calendar (next Pi2Day / hackathon / grant windows)

| Window | When | What it is | For us |
|---|---|---|---|
| **Pi2Day 2026** | **TODAY, 2026-06-28** | Pi's mid-year event; current campaigns ("Vibe Coder", Launchpad SLICE testnet token) **close today**; **prizes are Pi merch only — no cash grant.** The canon's "missed Pi2Day grant" is essentially correct: **there is no one-shot Pi2Day grant to miss.** [[Upcoming Pi2Day 2026](https://minepi.com/blog/upcoming-pi2day-2026/)] | Nothing to chase today; the money lives in the two channels below. |
| **★ Pi Hackathon (BEST near-term)** | **Monthly — deadline 11:59pm PST the LAST DAY of each month.** Next: **2026-06-30** (this Tuesday — too soon for a real entry), then **2026-07-31**, monthly thereafter. | Year-round via the Brainstorm app. **10,000 Pi / winner**; strong apps may get **Pi Core Team support + Testnet Ecosystem listing**. Needs: demo link + ≤3-min video + description (+ GitHub if PiOS-licensed). [[Pi Hackathon](https://minepi.com/developers/pi-hackathon/)] | **Target 2026-07-31** with a Testnet build of the hub+Knight slice. A playable game fits directly; gaming is a named priority. |
| **★ Pi Network Ventures (BIGGEST, caveated)** | **Rolling — no public deadline.** | **$100M fund** (Pi + USD), gaming an **explicitly named target sector**. Apply anytime via Google Form (`forms.gle/joz5nBwRyV2SDEQG9` — **verify the live link before submitting**). [[Pi Ventures](https://minepi.com/ventures/)] [[crypto.news $100M fund](https://crypto.news/pi-network-ventures-100-million-fund/)] | **File as a low-cost long-shot.** RISK: 13 months in, **only one disclosed investment** (OpenMind, Oct 29 2025); token/USD split, check sizes, criteria undisclosed; $100M headline degraded if PI-denominated. Apply, don't count on it. |

**Recommended funding moves:** (1) prepare a **hackathon submission for the 2026-07-31 deadline** using a Testnet build (the ≤3-min video + demo link the project can already produce; leverage existing `GRANT_DEMO_VALIDATION.md` assets); (2) **file a Pi Ventures application** as a cheap option; (3) keep Pi payments **behind a feature flag** — never hard-depend on Pi for the V1 economy.

### 5.3 What NOT to do (scope guard — carried from the existing WOs, still binding)
- **Do NOT** greenfield a new store/wallet/economy — Pi is a **rail on PackStore + an on-ramp to SKR**. Reuse `WORK_ORDER_skr_store_design.md` + `WalletService`/`CurrencyKind`.
- **Do NOT** put the **Server API Key** (or wallet seed) in the WebGL bundle or any client code — backend secret only.
- **Do NOT** grant any SKU on the client's word — **only after a 200 from Pi `/complete`**.
- **Do NOT** commit to payments/listing before the **Phase 0 mobile-WebGL gate** proves viability.
- **Do NOT** build a Pi-specific backend — the **shared thin approve/complete + entitlement service** is the staged T2 cloud-save verifier (build once, reuse).
- **Do NOT** introduce any combat/power grant via Pi — the SKR covenant firewall still binds.
- **Do NOT** put "Pi" in the game name/branding, or use a domain starting with "pi", without permission.
- **Do NOT** model Pi revenue as stable real money, or ad revenue as a pillar.
- **Do NOT** hand-edit scene files or write `.cs` from this WO — design only.

---

## 6. SOURCES

### Pi platform / portal / SDK
- Pi Browser Introduction (iFrame, iOS cookie caveat) — https://pi-apps.github.io/community-developer-guide/docs/gettingStarted/piBrowserIntroduction/
- Developer Portal guide (registration, `validation-key.txt`, Server API Key) — https://pi-apps.github.io/community-developer-guide/docs/gettingStarted/devPortal/
- Mainnet Listing Requirements (7 rules) — https://pi-apps.github.io/community-developer-guide/docs/gettingStarted/mainnetListingRequirements/
- SDK reference (`init`/`authenticate`/`createPayment`/`Ads`) — https://github.com/pi-apps/pi-platform-docs/blob/master/SDK_reference.md
- Platform API (`/me`, `/payments/*`, auth schemes) — https://github.com/pi-apps/pi-platform-docs/blob/master/platform_API.md
- payments.md (security: deliver only on /complete 200) — https://github.com/pi-apps/pi-platform-docs/blob/master/payments.md
- Payment Flow (incomplete-payment handling) — https://pi-apps.github.io/community-developer-guide/docs/importantTopics/paymentFlow/piPaymentFlow/
- ads.md (rewarded-ad server verify) — https://github.com/pi-apps/pi-platform-docs/blob/master/ads.md
- pi-nodejs (A2U / wallet seed) — https://github.com/pi-apps/pi-nodejs/blob/main/README.md
- Pi Demo App (reference impl) — https://github.com/pi-apps/demo/blob/main/doc/development.md

### Mainnet / KYC / payments status
- Open Network launch (Feb 20, 2025) — https://minepi.com/blog/open-network-launch-date/
- Pi Day 2026 (App Studio mainnet payments, persistent integrations) — https://minepi.com/blog/pi-day-2026/
- KYC + migration (16.7M) — https://www.cryptotimes.io/2026/05/13/pi-network-explains-ai-powered-kyc-as-mainnet-migration-surpasses-16-7m-users/
- First year on open mainnet (price/liquidity reality) — https://crypto.news/pi-networks-first-year-on-open-mainnet-what-actually-happened/
- App-review tightening / "proof before profit" — https://coinpedia.org/news/pi-network-update-for-2026-forget-pump-and-dump-pi-wants-proof-before-profit/ ; https://www.hokanews.com/2026/04/pi-network-tightens-developer-rules.html

### Unity WebGL on mobile (the gate)
- Unity — Optimize Web for mobile (Addressables, ASTC, no threads) — https://docs.unity3d.com/Manual/web-optimization-mobile.html
- iOS WebGL stability / memory ceiling — https://discussions.unity.com/t/stability-of-large-games-with-unity-webgl-on-ios/888745
- WebGL memory increment / crash on iOS — https://discussions.unity.com/t/webgl-memory-increment-issue-and-crash-on-ios/894771
- iOS 18 WKWebView crash loading Unity WebGL — https://discussions.unity.com/t/ios-18-using-wkwebview-to-load-webgl-crashes-on-release/952271
- Unity WebGL compression done right — https://miltoncandelero.github.io/unity-webgl-compression

### Competitive / opportunity
- CiDi Games beta (81K users / 1.2M sessions / 3.19M Pi staked) — https://minepi.com/announcement/cidi-games-beta/ ; https://www.kucoin.com/news/flash/pi-network-ventures-cidi-games-launches-10-instant-games-on-pi-browser-hits-1-2m-sessions
- FruityPi (0.1 Pi power-up, dev-set pricing) — https://www.ainvest.com/news/pi-network-launches-fruitypi-game-boost-chain-activity-2506/
- Hackathon 2025 winners (RUN FOR PI, Blind_Lounge — Pi-as-sink pattern) — https://minepi.com/blog/hackathon-2025-winners/
- GCV myth rejected — https://coinfomania.com/pi-network-moderators-reject-gcv-myth-as-map-of-pi-2-0-nears-launch/
- Pi Hackathon (10,000 Pi/month, last-day deadline) — https://minepi.com/developers/pi-hackathon/
- Pi Network Ventures ($100M, gaming targeted) — https://minepi.com/ventures/ ; https://crypto.news/pi-network-ventures-100-million-fund/
- Pi2Day 2026 (campaigns, no cash grant) — https://minepi.com/blog/upcoming-pi2day-2026/

### Internal canon referenced (do NOT duplicate)
- `PI_INTEGRATION_SPEC.md` — owner-resolved backend/bridge/economy/anti-tamper contracts (the wire-ready spec).
- `pi-backend/` (`src/index.ts`, `wrangler.toml`, `README.md`) — the code-ready Cloudflare Worker (`/approve` `/complete` `/reconcile`, idempotency KV, grant via `PackStore.ApplyPackContents`).
- `docs/webgl-hosting-notes.md` — the 186 MB build facts + itch.io/Vercel hosting + `vercel.json` Brotli headers.
- `docs/DATA_ARCHITECTURE_DECISION_2026-06-27.md` — staged local→cloud→Solana; T1 Addressables-remote (WO-545, the size lever); T2 cloud backend (shared with the Pi verifier).
- `WorkOrders/WORK_ORDER_skr_store_design.md` — held-SKR premium token + store; `ISkrLedger`; PackStore on-ramp; covenant + validator firewall.
- `WorkOrders/WORK_ORDER_offline_storage_logic.md` — storage tiers + SKR fast-track + pack-linked tiers (Pi-buyable convenience).
- `WorkOrders/WORK_ORDER_economy_store_packs.md` — resource/boost/storage packs + multi-rail pricing (Pi as one more rail).
- `WorkOrders/WORK_ORDER_pi_browser_integration.md` — the shorter precursor draft this synthesis deepens/supersedes as the decision doc.
- Memory: `data-architecture-hybrid-db-direction`, `combat-pivot-single-hero-northstar` (V1 hub+Knight = the small build that must fit WebGL).
