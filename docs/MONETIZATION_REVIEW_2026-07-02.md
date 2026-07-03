# Monetization Review — Pi Testbed (2026-07-02)

**Type:** First-ever monetization review. Decision doc — a reader should be able to say YES/NO, not "which option?".
**Scope:** audit of the built stack (from code + data, not comments) + Pi ecosystem research + THE recommended V1 model for the Pi testground.
**Binding canon honored:** SKR = the real Solana/Seeker token, non-custodial only, no mint, no game-held withdrawable balance (memory `skr-separate-ingame-currency-real-token-readonly`). Pi payments = the platform's own server-verified rail, never a game-custody balance. Soft economy stays the existing non-token currencies. V1 ships zero crypto custody.
**Owner framing (2026-07-02):** this is a TESTING GROUND, not a revenue play — price low enough that curiosity converts; **the product is the telemetry** (success metric = quality of learnings per week, not Pi collected). Loot boxes are an owner-interest option analyzed in §5, not blanket-banned.

---

## 1. Inventory — what is built and how wired (file-anchored)

Verified from code this session, cross-checked against `docs/audits/AUDIT_monetization_2026-06-28.md` (still accurate) and `CANON_GROUND_TRUTH_2026-07-01.md`.

### 1.1 The PackStore stack (~70% built; end-to-end on a stub)

| Layer | Files | State |
|---|---|---|
| Pack data | `Assets/Resources/Data/Canonical/packs.json` (+ StreamingAssets mirror, keep identical) | **13 authored packs** (v2, 2026-06-28): 5 price-ladder (Hearth Spark $1.99 → Founder's Vow $49.99) + 8 themed bundles (Frostfall, Embergrove, Spring Awakening, Starter's Hand, Echo Patron, Hero Wardrobe, Realm Defender, Builder's Cache). Pricing object = `{usd, usdc, sol, skr}` — **no `pi` field yet**. Every cosmetic SKU now has a matching `cosmetics.json` entry (the 06-28 dangling-SKU gap was fixed). |
| Catalog loader | `Assets/_Modules/Wallet/PackCatalog.cs` | Built. Typed `PackDef`, Resources-first (WebGL-safe), SKU/tier lookup. `AmountFor(CurrencyKind)` switches Sol/Usdc/Skr — the exact seam a `Pi` rail extends. |
| Store UI | `Assets/_Modules/Wallet/PackStore.cs` | Built, code-built uGUI (bypasses its own dead UXML). One card per pack, currency chips, Buy/Owned, fully FlowTrace-instrumented. **No live entry point** — `MarketplaceInteractor.OpenStore()` is never called; the `PackStoreUI` GameObject must be enabled externally. |
| Wallet seam | `Assets/_Modules/Wallet/WalletService.cs`, `StubWalletProvider.cs`, `SolanaWalletProvider.cs` (`#if SOLANA_SDK`, inert) | Built behind `IWalletProvider`. **Everything runs on the devnet stub** — no real-money rail is wired anywhere. Mainnet flip owner-gated. |
| Entitlement/grant | `PackStore.ApplyPackContents` → `GameState.OwnedItemIds` (`Core/State/GameState.cs:63`) → `SaveSchema` persist (v28) | Built + self-verifying. Applies crystals/food/coins; **drops the `glimmer` field** (not in `GameState.Resources`); **convenience tokens (instant-build etc.) are displayed but never granted** (no token tray). `founderOnly` shows a tag but is not date-gated. |
| Glimmer top-ups | `Assets/_Modules/Wallet/CryptoPaymentManager.cs` | Built (SOL/SKR/USDC → Glimmer, SKR +25% bonus) — separate path from packs, stub-backed. |

### 1.2 Currencies (what exists, who earns/spends it)

- **Soft wallet** — `GameState.Resources` (`ResourceBalance {crystals, food, coins}`, starter 250/80/15). **Gold = Coins, granted on kills** (WO-432/433, `Village/Enemies/Enemy.cs:2024`) and spent in the gear shop (`PartyShopVM` — live, `ff.partyshop` default ON) and building research.
- **Gathered materials** — `GameState.Stone/Iron/Wood` (+ Food) fed by the **echo workforce** harvest loop (wired, offline via real clock). This is the "wood/iron/grain" life-force economy — **stays 100% off-chain** per canon.
- **Glimmer** — cosmetic soft currency (`Cosmetics/GlimmerCurrencyService.cs`), earned by milestones, spends in the Glimmer cosmetic shop (`CosmeticCatalog.cs` + `cosmetics.json`, 12 items 50–140 Glimmer).
- **Voidshards** — rare currency in save; peripheral.
- **Premium currency: none live.** SKR/SOL/USDC are stubbed rails, not balances. This is correct per canon — do not introduce a held premium token.

### 1.3 Cosmetic/wardrobe pipeline (what a cosmetic SKU can actually LOOK like today)

- `CosmeticCatalog` + `CosmeticApplier` + `cosmetics.json` are built; pack-exclusive entries exist (`unlockMethod: achievement`, glimmerCost 0 — entitlement-owned, equippable).
- **Reality check (canon 2026-07-01): the Blink full-body rig is JUNKED** — hero = one Tripo model, static armor, no mesh-swap; `BlinkWardrobe` survives only conceptually and WO-456 (data-driven wardrobe) is an unimplemented spec draft. Consequence: **"hero-outfit" SKUs cannot render as real outfit swaps today.** What IS visually deliverable now: **pet skins, building palettes, banners, heart-lantern skins, weapon/shield flair (Offset Forge attachments), and material tints on the Tripo hero.** SKU selection in §4 respects this — sell only what can visibly show up.
- `BattlePassManager` (WO-73) exists but is dormant (needs an authored `BattlePassData` SO; Glimmer-priced premium track). A battle-pass-lite is a later season lever, not V1-testbed.

### 1.4 Pi / payments plumbing (the web side)

- **Sign-in: SHIPPED + verified live (2026-07-01).** `Assets/Plugins/WebGL/PiBridge.jslib` + `IPiPlatform`/`WebGLPiPlatform`/`EditorPiPlatform` seam + `PiSignInController` → `api/pi/verify.js` (Vercel) validates the accessToken against Pi's `/v2/me`. CORS solved for the `pinet.com` cross-origin case; COEP root cause fixed.
- **Payments backend: CODE-READY, NOT DEPLOYED.** `pi-backend/src/index.ts` — a standalone Cloudflare Worker with `/approve`, `/complete`, `/reconcile` + idempotency KV, corrected against the real Pi Platform API (`POST /v2/payments/{id}/approve|complete`, `Authorization: Key`). V1 hardcodes one entitlement (`pi_pack_small`).
- **Client payment path: NOT built.** `PiBridge.jslib` has no `CreatePayment`; `CurrencyKind` has no `Pi`; packs have no Pi price.
- **Telemetry rail: SHIPPED end-to-end.** `EventTracker.cs` → `api/events/track.js` → Neon `analytics_events` (JSONB properties, batched, fire-and-forget) — plus `WebTrace` (`?trace=1`) → `FlowTrace` → `api/trace` → Vercel logs → Neon. **The testbed's measurement pipe already exists.**
- Hosting: game LIVE on itch (`denellestudios`); Vercel hosts the API only (game bundle blocked by the 100 MB/file limit until WO-545 Addressables). Pi Browser loads the app fine; Pi rendering verified in Pi Desktop.

### 1.5 Bottom line of the audit

**Sellable today for real money: nothing.** But the distance is short and the design docs already agree on the shape (`PI_INTEGRATION_SPEC.md`, owner-resolved 2026-06-26; `WorkOrders/WORK_ORDER_pi_browser_integration_DEEP.md`, verified against Pi's live API). What's missing is exactly four things: (1) a deployed approve/complete server, (2) `Pi.createPayment` in the bridge + a `CurrencyKind.Pi` rail, (3) a live store entry point, (4) Pi prices on the pack data. Everything downstream of "payment completed" — grant, persistence, cosmetics resolution — already runs headless on the stub. **Do not greenfield anything.**

---

## 2. The Pi payments integration shape (decided)

**Decision: run the payment endpoints as Vercel functions in `api/pi/` next to the proven `verify.js`, with idempotency + orders in Neon — not the undeployed Cloudflare Worker.** Rationale: one backend, one deploy pipeline (proven live 07-01), one observability rail (Vercel logs + Neon), no new Cloudflare account/KV to own, and the orders table doubles as the telemetry funnel's purchase ground truth. Port `pi-backend/src/index.ts`'s corrected logic verbatim (it is the reference implementation); keep the Worker directory as reference, banner it superseded.

```
Pi Browser (itch/pinet host)                          Vercel (existing project)
  Unity WebGL ── PiBridge.jslib ── Pi.createPayment     api/pi/verify.js      (LIVE — sign-in)
      │  onReadyForServerApproval(paymentId) ─────────► api/pi/approve.js     (NEW)
      │  onReadyForServerCompletion(paymentId, txid) ─► api/pi/complete.js    (NEW)
      │  onIncompletePaymentFound ────────────────────► api/pi/reconcile.js   (NEW)
      │                                                    │ Authorization: Key <PI_API_KEY> (Vercel env secret)
      ▼                                                    ▼
  PackStore.ApplyPackContents (grant on complete=200)   api.minepi.com/v2/payments/{id}/approve|complete
  EventTracker funnel events ─────────────────────────► api/events/track.js → Neon analytics_events
                                                        pi_orders table (Neon) = idempotency + entitlement map
```

Non-negotiable rules (from Pi's own docs, already encoded in the Worker):
- **Two-phase server mediation.** Grant ONLY on a 200 from `/complete`. Before granting, verify the `PaymentDTO` server-side (`GET /v2/payments/{id}`: `transaction.verified`, amount matches the order) — never trust the client callback alone.
- **Idempotency by `paymentId`** (Neon `pi_orders` row; a retry never double-grants). **Map paymentId → pack SKU at approve time** (replace the Worker's hardcoded `pi_pack_small` with `metadata.sku` validated against `PackCatalog`).
- **`onIncompletePaymentFound` → `/reconcile` on every auth** — Pi enforces one open payment per user; a stale payment blocks all future purchases. This is the known failure mode; FlowTrace the whole `createPayment → approve → sign → complete` chain (§12).
- **Secrets server-side only** (`PI_API_KEY` as a Vercel env var; never in the WebGL bundle).
- **Testnet first.** App Network is fixed at registration — register a **Testnet app** in `develop.pi` for development (the monthly hackathon explicitly accepts Testnet demos), and a Mainnet app when the owner clears dev-KYC + the listing rules.
- **Pi-only transactions in the Pi build** (listing rule): when `IPiPlatform` reports the Pi environment, the PackStore shows ONLY the Pi price chip — hide Sol/USDC/SKR. Platform-conditional display, not data removal.

---

## 3. THE recommended V1 model — "Low-Pi Curiosity Shop"

**One coherent model: deterministic cosmetic + supporter packs from the existing 13-pack shelf, priced trivially low in Pi (server-verified U2A), zero pay-to-win, no ads, telemetry as the primary product.** A compliant chance-based "Echo Cache" is staged as an optional slice 5 behind the compliance conditions in §5 — the shop does not wait for it.

### 3.1 Launch SKUs (all from existing `packs.json`; prices are new low-Pi amounts)

Pi trades ≈ **$0.115/Pi** (2026-07-02). The owner's thesis — Pi's low perceived value means low spend friction — sets the price policy: **every SKU cheap enough that curiosity converts**. USD-reference is irrelevant for the testbed; add a `pi` field to the `pricing` object (data-driven, re-tunable without code — the config-peg the DEEP WO recommended).

| SKU (exists today) | Pi price | ≈USD | Why this one |
|---|---|---|---|
| `hearth-spark` | **1 π** | $0.12 | The friction probe — does anyone pay *anything*? |
| `starters-hand` | **3 π** | $0.35 | Value/onboarding conversion test |
| `bloomtide-bundle` (Spring Awakening) | **3 π** | $0.35 | Cheap seasonal cosmetic test |
| `frostfall-bundle` | **5 π** | $0.58 | Seasonal cosmetic-forward (pet skin + palette render TODAY) |
| `realm-defender-bundle` | **5 π** | $0.58 | Weapon/shield flair — Offset Forge makes this visible |
| `folks-thanks` | **8 π** | $0.92 | The supporter tier — tests goodwill payment |
| `patron-of-elarion` | **15 π** | $1.73 | Price ceiling probe; nothing above this in the testbed |

Held back: `founders-vow` (founder window not enforced in code), `hero-wardrobe-pack` + other hero-outfit-anchored bundles (outfit swaps undeliverable post-Blink — re-shelve when the Tripo wardrobe exists or re-author contents as tints/flair), `builders-cache`/`echo-patron-pack` (convenience-heavy; the token tray doesn't grant tokens yet — selling them would ship a known lie).

**Content-integrity precondition:** before the shelf goes live, every launch SKU's granted cosmetics must *visibly appear* (pet skin, palette, banner, flair, tint) and the two grant gaps must close for launch SKUs only — apply `glimmer` on grant (or strip it from launch-pack data) and either wire the minimal token tray or remove convenience lines from launch-pack data. Sell nothing that resolves to nothing.

### 3.2 Telemetry — the actual product (first-class)

Success metric = **learnings per week**, not Pi collected. Reuse the shipped rail wholesale: `EventTracker.cs` → `api/events/track.js` → Neon `analytics_events`; `WebTrace/FlowTrace → api/trace` stays the error/diagnostic channel. New events only, no new pipe.

Event spec (snake_case, JSONB properties; playerId = a **salted-hash pseudonym** of the Pi uid, never the raw uid — see §3.3 — so the funnel is per-Pioneer without any analytics row identifying the Pioneer):

| Event | Properties | Learning it feeds |
|---|---|---|
| `store_open` | source (hub/HUD/quest), session_n | Does anyone find/enter the store? Entry-point placement |
| `store_sku_view` | sku, dwell_ms, position | Shelf attention — what draws the eye |
| `store_pay_start` | sku, pi_amount | Intent — tap-through rate per SKU/price |
| `store_pay_approved` / `store_pay_completed` | sku, paymentId, latency_ms | Rail health + wallet-sign drop-off (THE Pi-specific funnel step) |
| `store_pay_cancelled` / `store_pay_error` | sku, step, reason | Where the funnel dies (approval wait? wallet sign? incomplete-payment block?) |
| `grant_applied` | sku, cosmetics[], economy | Grant integrity (pairs with the Neon `pi_orders` ground truth) |
| `cosmetic_equipped_after_buy` | sku, cosmetic_id, minutes_since_buy | Regret/delight signal — bought-but-never-equipped = a dud SKU |
| `store_repeat_visit` / repeat purchase (derived) | days_since_first_buy | Retention of paying curiosity |

Weekly ritual: one Neon query pack (funnel per SKU: views → pay_start → completed; price-point conversion curve 1π vs 3π vs 5π vs 8π vs 15π; equip-after-buy rate; error/step-drop table) reviewed with the owner every week. Refund/regret proxy = completed-but-never-equipped + support pings; Pi has no chargeback rail, so regret must be inferred — which is exactly why `cosmetic_equipped_after_buy` is mandatory, not nice-to-have.

### 3.3 Privacy posture (owner standing value — a named differentiator, not a footnote)

**"We watch behavior, never people."** All shopper/funnel telemetry is **pseudonymous by construction**:

- **What we collect:** behavioral events only (§3.2) keyed by `player_key = HMAC-SHA256(server_salt, pi_uid)` — a salted-hash pseudonym computed server-side (salt = a Vercel env secret, never shipped in the client, rotatable to unlink history). Enough to see funnels, cohorts, and price curves; not enough to name anyone.
- **What we NEVER collect in analytics rows:** raw Pi uid, Pi username, wallet addresses, IP (not stored in `analytics_events`), device fingerprints, or any cross-site identifier. **No fingerprinting. No third-party trackers/SDKs** — the pipe is entirely first-party (our client → our Vercel function → our Neon).
- **The one deliberate exception:** the `pi_orders` payment table keeps the real Pi uid + paymentId + txid — that is an operational payment record (reconcile, support, dispute), required by the rail, access-controlled, and **never joined into analytics** except through the hashed `player_key`.
- Current code already leans this way (`EventTracker.cs` sends `BoundWallet | "anonymous"`); WO-D formalizes the hash so the Pi rollout starts private-by-design rather than retrofitting.

This is also strategically aligned: "limit data collection" is one of Pi's seven listing rules, and a privacy-respecting store is a legible differentiator in an ecosystem full of data-hungry apps — say it on the store page in one line: *"We don't track you. We count taps, not people."*

### 3.4 Why this model (and not the alternatives)

- **Cosmetics/supporter packs are what converts in cozy/mid-core web games** (~80% of F2P revenue in cosmetic-led titles; battle pass displaced loot boxes as the modern conversion tool — ours stays dormant until a season cadence exists). Typical payer conversion is 2–5%; at testbed scale the *rate* is the data, not the revenue.
- **It scores on Pi's own rubrics.** The monthly hackathon (10,000 π/winner, last-day-of-month deadline; July 31 is the target) explicitly scores "integration of Pi cryptocurrency" — a live, dignified U2A purchase loop is direct rubric points. Pi's review culture ("proof before profit") favors real utility; a playable game selling honest cosmetics fits; gambling-flavored mechanics would cut against it (§5).
- **It respects every canon line:** no custody (Pi flows wallet→app via Pi's rail; SKR untouched in the Pi build), soft economy off-chain, no power sales, reuse-don't-greenfield.

---

## 4. Implementation work orders (real slices, ordered)

| # | Slice | Contents | Size |
|---|---|---|---|
| **WO-A** | **Pi pay server rail** | Port `pi-backend` logic to `api/pi/approve.js`/`complete.js`/`reconcile.js` (pattern of `verify.js`); Neon `pi_orders` table (paymentId PK, uid, sku, amount, status, txid, ts) = idempotency + entitlement map; PaymentDTO verification before complete; register the Testnet app in `develop.pi`, set `PI_API_KEY` env. Testable with curl against Pi sandbox before any client work. | ~1–2 days |
| **WO-B** | **Client rail** | `PiBridge.jslib` `CreatePayment` + callback marshaling (per `PI_INTEGRATION_SPEC.md` §2 contract, TCS-keyed by paymentId); `IPiPlatform` extension + Editor stub; `CurrencyKind.Pi` + `pi` pricing field in `PackCatalog`/`packs.json` (both copies); `onIncompletePaymentFound` → reconcile on every auth; FlowTrace the full chain. Gate: end-to-end Testnet buy of Hearth Spark → `ApplyPackContents` → SKU persists in save. | ~2–3 days |
| **WO-C** | **Store entry + honest shelf** | Re-entry-point the PackStore (hub marketplace interactor or a HUD button — code-built canvas, no PanelSettings dependency, mirror the PartyShop escape hatch); platform-conditional Pi-only pricing chips; low-Pi price data pass on the 7 launch SKUs; content-integrity pass (glimmer-on-grant or strip; convenience lines wired via minimal token tray or removed; verify every launch cosmetic renders). | ~2–3 days |
| **WO-D** | **Telemetry funnel (privacy-by-design)** | The §3.2 event set through the existing `EventTracker` → `analytics_events`; server-side `player_key = HMAC(salt, pi_uid)` pseudonymization per §3.3 (salt as Vercel env secret; raw uid/username never written to analytics rows); the weekly Neon query pack; paymentId joins to `pi_orders` only via the hashed key. | ~1 day |
| **WO-E** | *(owner-gated)* **Echo Cache — Testnet only** | The chance-pack experiment per §5.5 (Test-Pi priced, cosmetics-only, odds shown, pity + dupe protection) — only after WO-A–D have produced two weeks of deterministic baseline data (otherwise the loot-box signal has nothing to compare against). Mainnet chance purchases stay NO-GO per §5.5. | ~2 days |

Sequence: WO-A ∥ WO-D (both server-side, no Unity gate) → WO-B → WO-C → felt-verify on Testnet in Pi Browser → owner KYC/Mainnet decision → July 31 hackathon submission with the Testnet build.

**Hard external gates found:** (1) Mainnet real-Pi requires developer KYC + a Mainnet-registered app — and **App Network is fixed at registration**, so the existing registration's network must be checked; a separate Testnet app is needed for dev regardless. (2) Whether a registered-but-unlisted Mainnet app can take real Pi *pre-listing* is unverified in official docs — assume listing review is on the critical path to real-Pi revenue and test with a 0.01 π payment the moment Mainnet registration exists. (3) The Pi-only-transactions listing rule means the Pi build must hide all non-Pi rails. None of these block Testnet development or the hackathon.

---

## 5. Loot boxes priced in Pi — analyzed option (owner decision, fully loaded)

*(Owner thesis: Pi's low perceived value → low spend friction → ideal chance-mechanic testbed. Analyzed, not banned. The owner decides; here is the decision fully loaded.)*

### 5.1 Pi's own policy — the binding constraint

Pi's [App Studio Community Guidelines](https://minepi.com/appstudio_community_guidelines/) **explicitly prohibit** "offering or facilitating gambling, betting, or lottery-related services involving Pi tokens, **either directly or indirectly**" — violation → app paused/removed. Caveat: that document formally governs App Studio apps; the general Developer Terms and the mainnet listing rules only say "compliance with Pi ecosystem policies and applicable law," and nothing in Pi's docs addresses cosmetic loot boxes as distinct from gambling. But ecosystem listing review is **discretionary** and the hackathon is judged by the same Core Team — a mechanic that *looks like* gambling-with-Pi risks failing review or losing rubric points even if a lawyer would defend it. Whether a cosmetics-only box counts as "indirectly" is Pi's call; if pursued on Mainnet, ask in the Pi developer channels **before** building.

### 5.2 Gambling-law exposure (Pi is exchange-traded)

The universal test: real-money stake + chance + prize of real-world value. Current landscape: **Belgium** formally treats paid loot boxes as licensable gambling (industry answer: geo-block); the **Netherlands (2022)** and **Austria (Supreme Court, Dec 2025)** ruled FIFA-style packs are NOT gambling when embedded in a broader game with non-cashable rewards — the EU court trend currently favors loot boxes; **UK** = self-regulation (odds disclosure, under-18 consent); **US** = no gambling statute, but consumer-protection/dark-pattern theories live, and a pending NY bill explicitly names **cryptocurrency-resalable contents** as the aggravating factor; **China/Korea** = odds (and pity-ceiling) disclosure by law.

**Does Pi-pricing make it worse?** On the stake side, barely — exchange-traded Pi (~$0.12) satisfies "real money in" the same way fiat does. The danger is the **prize** side: chance mechanics that pay out anything tradeable/cashable (tokens, Pi, transferable items) tip into gambling in most frameworks AND squarely into Pi's "involving Pi, indirectly" clause. **Rule: Pi may go IN; Pi/tradeable value must NEVER come OUT of a chance mechanic.** Cosmetics-only, non-tradeable rewards ≈ fiat-equivalent risk.

### 5.3 The compliant variant — seven stacked conditions

A Pi-priced loot box is defensible only with ALL of: (1) rewards cosmetics-only, non-tradeable, no cash-out, no player transfer; (2) odds disclosed prominently pre-purchase, per rarity tier; (3) pity ceiling (disclosed) + duplicate protection; (4) age gate; (5) geo-fence Belgium; (6) low price (our testbed prices already are); (7) optionally soft-currency intermediation (Pi→gems→box) — which *helps* the gambling analysis but *is itself* the currency-obfuscation dark pattern and reads worst against Pi's "indirectly" wording, so we would NOT use it: sell the box in Pi directly, transparently, or not at all. Seven conditions to reach the risk level a deterministic pack has by default.

### 5.4 Does the existing PackStore already deliver the experience?

Largely yes. The pack-opening moment (buy → contents grant → reveal) is the same dopamine beat with contents visible up front — the direction the industry itself retreated to (Overwatch 2, Fortnite abandoned paid boxes for visible contents; battle pass displaced the loot box as the top conversion tool). A **rotating/limited shelf** (which SKUs show this week) adds the scarcity-discovery feel with zero chance element. The delta a real loot box adds is only the pre-purchase uncertainty — the single most-regulated mechanic in games, and the one thing Pi's guideline names.

### 5.5 GO/NO-GO recommendation

- **NO-GO on Mainnet / the listed Pi app.** Not primarily for gambling law (manageable with §5.3) but because the binding constraint is Pi itself: an explicit no-gambling-involving-Pi guideline + discretionary review + Core-Team-judged hackathon. A chance purchase is the single easiest way to fail Pi review or lose "community alignment" points — against a July 31 hackathon target, unacceptable risk for zero learning we can't get otherwise.
- **CONDITIONAL GO as a Testnet experiment (recommended path for the owner's thesis).** On the Testnet app, purchases are made in valueless Test-Pi → the "real-money stake" element collapses, gambling law is out of frame, and Pi's Mainnet review isn't in the loop. Ship the **"Echo Cache"** there (WO-E): 2 π-test, cosmetics-only pool drawn from existing `cosmetics.json` entries, per-tier odds shown on the card, duplicate protection, 10-box pity for the rare tier, full funnel telemetry (`store_pay_*` + `cache_opened` + `cache_reveal_dwell_ms` + equip-after-open). Run it two weeks against the deterministic baseline; the telemetry answers the owner's actual question — do chance mechanics convert/retain better in a low-value-currency environment? — without touching real Pi.
- **If the Testnet data says chance wins and the owner wants it on Mainnet:** the dignified split is **earned-chance, paid-deterministic** — free/earned Echo Caches (drop from raids/milestones) with paid deterministic packs. Zero stake → zero gambling elements → zero Pi-guideline exposure, full dopamine retained. A *paid* Mainnet box only after an explicit written OK from Pi developer support, plus all §5.3 conditions.

**Unverified items the owner should know:** whether the App Studio gambling clause formally binds regular Dev Portal apps; whether Pi review treats cosmetic boxes as "indirect" gambling; the Austria Supreme Court ruling is single-sourced (Esports Legal News, Jan 2026).

---

## 6. Non-goals (what we will NOT do, and why)

1. **No pay-to-win.** The covenant (packs carry cosmetics/economy-convenience only, never combat power) is load-bearing canon and also what keeps Pi review + player trust intact.
2. **No ads.** Owner's pay-once-no-ads dignity stance; the Pi Ad Network's RPMs are structurally low anyway — not worth the dignity cost for a testbed whose product is learnings.
3. **No dark patterns.** No countdown scarcity, no currency obfuscation (Pi prices shown as Pi, directly), no sunk-cost traps. The disclaimer line in `packs.json` stays.
4. **No token custody, no mint, no game-held SKR/Pi balance.** Pi moves wallet→app through Pi's own verified rail and becomes an entitlement, never a stored withdrawable balance. SKR stays out of the Pi build entirely (also required by Pi's Pi-only-transactions rule).
5. **No greenfield store/wallet/economy.** Everything in §4 is a rail or an entry point on the existing PackStore stack.
6. **No fiat/Stripe inside the Pi build** (listing rule) and **no GCV pricing** (community folklore; price at market, config-pegged).
7. **No A2U Pi payouts in V1** (rewards paid in Pi) — official docs still carry a Testnet-only caveat, it requires the app wallet's private seed server-side (custody-adjacent), and it's off-canon for V1.
8. **No revenue targets.** The testbed's KPI is learnings/week. If it also collects Pi, that's upside, booked as speculative (Pi is ~96% off peak and thinly convertible).
9. **No identity harvesting.** No raw Pi uid/username in analytics, no fingerprinting, no third-party trackers — behavior is measured pseudonymously per §3.3. Privacy is a stated product value, not a compliance posture.

---

## Appendix — key sources

- Internal: `docs/audits/AUDIT_monetization_2026-06-28.md`; `WorkOrders/WORK_ORDER_pi_browser_integration_DEEP.md` (2026-06-28, five-stream synthesis, sources within); `PI_INTEGRATION_SPEC.md` (owner-resolved contracts); `pi-backend/src/index.ts` (reference approve/complete implementation); `CANON_GROUND_TRUTH_2026-07-01.md`.
- Pi platform (verified 2026-07-02): `github.com/pi-apps/pi-platform-docs` — `platform_API.md`, `payments.md`, `payments_advanced.md`; community-developer-guide — Developer Portal, Mainnet vs Testnet, Mainnet Listing Requirements; `minepi.com/developers/pi-hackathon/` (10,000 π monthly, last-day deadline, Pi-integration scored).
- Market/norms: CoinMarketCap/CoinGecko (PI ≈ $0.115, 2026-07-02); GameMakers F2P cosmetics (~80% cosmetic revenue share); Kevuru/Stripe (2–5% payer conversion); Colorado Law Review + ACM on loot-box/dark-pattern regulation.

> **UPDATE 2026-07-02 (owner):** during Pi setup, Pi had the owner explicitly create a NEW wallet (separate from her personal Pioneer wallet) — i.e. the app-side/receiving wallet likely already exists. Verify which network it is bound to (Testnet vs Mainnet) as the first step of WO-A; the "create app wallet" checklist item may already be done.
> **Dev/app wallet (owner-provided 2026-07-02):** `GADEKVJ4RFYKRTRSBIP3UBX5IQUP5PJKQTDC3GLUXBPQDN7L5CO2NMAY` — the receiving end for WO-A U2A payments. First WO-A step remains: confirm which network (Testnet/Mainnet) this wallet + the app registration are bound to.
