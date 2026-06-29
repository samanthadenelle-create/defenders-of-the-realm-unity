# SECURITY, COMPLIANCE & HARDENING AUDIT — Consolidated

**Project:** Defenders of the Realm / Echoes of Elarion (`C:\eoa`)
**Date:** 2026-06-28
**Type:** PLANNING ONLY — no code was changed by this consolidation. Read-only audit synthesis.
**Sources consolidated:** (1) Economy / Payment Client-Trust, (2) Code Security, (3) Monetization Compliance, (4) Privacy / Crypto / Platform-Policy, (5) Code & Build Hardening.

> **Confidence note.** Findings A1–A6, B-series, E-series are sourced directly from cited code (file:line) and are HIGH confidence. The compliance domain (C/D-series) mixes verified code/data facts (HIGH) with legal-exposure assessments that are **risk opinions, not legal advice** — items marked *needs counsel* require qualified crypto/gaming/privacy lawyers before launch. External regulatory citations are listed inline in §2D and reproduced in §5.

---

## 1. EXECUTIVE SUMMARY

The project is **architecturally well-prepared but not yet hardened for real money or web3**. The high-severity *code* classes are clean: no committed secrets, no unsafe-deserialization RCE vector (no `TypeNameHandling`/`BinaryFormatter`), all traffic is TLS with no cert-validation bypass, no client-generated security tokens. The team has deliberately built seams (`IWalletProvider`, `ISaveProvider`, the documented `save.js` "BUILT-TO-FLIP" plan, the Pi two-phase server handshake) so the server-authoritative flip is a **policy change, not a rewrite**.

The problem is that **the flip has not happened and several documented protections are fiction.** The entire economy is currently **client-authoritative**: saves are plaintext/unsigned PlayerPrefs, pack/cosmetic entitlements are granted locally and never sent to the server, payment "success" is decided client-side, and the cloud save endpoint explicitly trusts client-asserted balances. This is acceptable for a no-real-value devnet build and becomes a direct exploit the moment SKR/Pi/Solana packs carry real value — the stated near-term direction. Separately, the **monetization "covenant" build-gate that canon repeatedly claims (`SkrStakingRegression`) does not exist** — the cosmetic/convenience firewall lives only in JSON comments and one un-gated shallow test. And the **compliance controls the white paper asserts as shipped (privacy policy, consent, "no analytics", age gate, treasuries) are unbuilt or actively contradicted by live analytics** — a misrepresentation risk if shipped as written.

**Bottom line:** the designed *content* is genuinely clean (zero loot boxes / gacha / randomized purchases; combat power never sold; full price disclosure; capped non-inflationary reward pool). The work before real money is **executing the flip the codebase already documents** + **building the enforcement gates that are currently imaginary** + **standing up the legal/privacy controls** — not redesigning.

### LAUNCH-BLOCKER LIST (must be fixed/cleared before taking real money or shipping web3)

> A blocker here = "this must be resolved before a public, money-enabled or on-chain release." Several are not code at all — they are legal sign-offs and published policies.

| # | Blocker | Domain | Refs |
|---|---------|--------|------|
| LB-1 | **Server holds entitlements + currency.** Move pack/cosmetic ownership and balances server-side; client reads, never writes them as truth. Today entitlements are granted 100% client-side and never synced; the save endpoint accepts client-asserted balances up to 1e9. | Economy | A2, A3 |
| LB-2 | **Grant only after server-verified, idempotent payment.** No client may be the arbiter of "paid." Verify on-chain tx (recipient, mint, amount ≥ price, tx-signature unused) / Pi `/complete` 200 before any grant; bind SKU↔amount. | Economy | A4, A5, C-PI |
| LB-3 | **Sign/HMAC the local save** and treat it as untrusted cache; reject on mismatch. Plaintext unsigned save lets players mint currency / own every pack offline, then sync up. | Economy/Hardening | A1, E-SAVE |
| LB-4 | **Save-auth ON + fail-CLOSED for any non-local provider.** `BackendAuthConfig.Enforced` defaults OFF and the client silently sends unauthenticated on any signing failure; guest identity is a raw device fingerprint. Abort sync rather than send unauthed. | Code/Hardening | B-AUTH |
| LB-5 | **Build the `MonetizationCovenantRegression` gate** that canon already claims exists (`SkrStakingRegression`). One real, build-wired validator that rejects out-of-allowlist convenience kinds, any combat/stat grant, and any probability/roll field. Wire the existing `PackCatalogTest` into the headless gate. | Monetization | C-COV, C-TEST |
| LB-6 | **COPPA age gate + mixed-audience handling.** No age screening exists; cozy art + pets + real-money + persistent identifiers = classic mixed-audience exposure under amended COPPA (full compliance 2026-04-22). | Privacy | D-COPPA |
| LB-7 | **Publish Privacy Policy + Terms; add consent + DSAR/deletion across all wallet-keyed tables; reconcile the "no analytics" claim** with the live wallet-keyed analytics pipeline. | Privacy | D-PRIV, D-ANALYTICS, D-CTRL |
| LB-8 | **Legal sign-offs (needs counsel):** MSB/money-transmitter status for SKR payout + on-ramp; same-mint decision + Howey framing for the token rebate; skill-contest/sweepstakes opinion before enabling reward Streams B/C. Keep token rebate + B/C default-OFF until signed. | Crypto/Regulatory | D-MSB, D-HOWEY, D-SWEEP |
| LB-9 | **No crypto purchase of digital goods in any native Apple/Google build.** Keep crypto rails to Solana dApp Store + Pi Browser web; native iOS/Android must use compliant IAP or strip crypto unlocks. | Platform | D-PLATFORM |
| LB-10 | **If on-chain save ships, keep PII off-chain** (opaque hash/pointer only) — immutable ledger breaks GDPR/CPRA erasure. | Privacy | D-ONCHAIN |
| LB-11 | **Gate the release-reachable dev grant panel.** `AdminOverlay`/`HelpMenu` "Load resources (full base)" ships and is usable in release builds (the auth check is a no-op); a player can mint +50k currency. | Hardening | E-ADMIN |

---

## TOP 10 RISKS

| Rank | ID | Risk (one line) | Sev | Domain | Blocker |
|------|----|-----------------|-----|--------|---------|
| 1 | A2/A3 | Entitlements + currency are client-authoritative; never validated server-side | Critical | Economy | LB-1 |
| 2 | A1/E-SAVE | Save is plaintext, unsigned PlayerPrefs JSON — trivially edited to mint currency / own every pack | Critical | Economy/Hardening | LB-3 |
| 3 | A4/A5 | Payment "success" is client-decided; no server tx verify, no idempotency, no SKU↔amount binding | High | Economy | LB-2 |
| 4 | C-COV | Claimed monetization covenant build-gate (`SkrStakingRegression`) does not exist — firewall is comment-only | High | Monetization | LB-5 |
| 5 | D-MSB | Real liquid SKR payout + on-ramp = potential money-transmitter/MSB + AML exposure | High | Crypto | LB-8 |
| 6 | B-AUTH | Cloud save-auth OFF by default and fails OPEN; guest identity = raw device fingerprint | High | Code/Hardening | LB-4 |
| 7 | E-ADMIN | Dev "Load resources (full base)" grant panel reachable + usable in release builds | High | Hardening | LB-11 |
| 8 | D-COPPA | No age gate; mixed-audience app takes real money + persistent identifiers | High | Privacy | LB-6 |
| 9 | D-ANALYTICS | Wallet-keyed analytics collected with no consent/opt-out, contradicting public "no analytics" claim | High | Privacy | LB-7 |
| 10 | D-SWEEP | Leaderboard/tournament token-prize Streams B/C = skill-contest/sweepstakes/gambling exposure | High | Crypto | LB-8 |

---

## 2. FINDINGS BY DOMAIN

Severity scale: **Critical / High / Med / Low.** Effort: **S** (≤½ day) / **M** (days–~1 wk) / **H** (1–2 wk+). Each item lists every audit that raised it (deduped).

### 2A. ECONOMY / PAYMENT SECURITY

**A1 — Saves are plaintext, unsigned PlayerPrefs JSON (mint currency / own every pack)** — *also raised by Hardening (E-SAVE) and Code Security (#5).*
- **Where:** `Assets/_Modules/Core/State/LocalSaveProvider.cs:31-38` (`PlayerPrefs.SetString(slot, json)`); `GameStateService.Save()` `:266-294`; `SaveSchema.Validate` `:363`.
- **Risk:** No HMAC/signature/checksum/encryption. `SaveSchema.Validate` only coerces non-negative/finite and clamps to ceilings — no integrity check. A player edits the blob to set `resources.*`, appends every pack SKU to `ownedItemIds`, relaunches; the game loads it as truth (`ApplyPersisted` `:403`). Owning a pack/cosmetic costs nothing. Load path is hardened against *corruption* but not *tampering*.
- **Severity:** Critical (once currency/packs carry real value).
- **Remediation:** Keyed HMAC over the serialized state, written alongside payload; reject-with-`FlowTrace.Fail` on mismatch (treat as corrupt → fresh state). Treat local save as untrusted cache; server authoritative (A2/A3) overwrites tampered local on next authenticated sync.
- **Effort:** HMAC ~S; server-authoritative folds into A3.

**A2 — Pack/cosmetic entitlements granted 100% client-side, never sent to server**
- **Where:** `Assets/_Modules/Wallet/PackStore.cs:626` `ApplyPackContents`; sync delta `GameStateService.BuildDeltaPayload()` `:1199` / `SendDelta` `:1146` **omit `OwnedItemIds`** entirely.
- **Risk:** Entitlement state lives only in the local save; server has no record and cannot validate. Combined with A1, a player grants every pack for free; nothing server-side contradicts it. The pack's currency top-up is laundered into `Resources`, which the server accepts within bounds (A3).
- **Severity:** Critical.
- **Remediation:** Entitlement granting behind a server endpoint keyed to a verified purchase (receipt / on-chain tx); add `ownedItemIds` to the authoritative server record; client reads entitlements, never writes them.
- **Effort:** M–H (1–2 wk; the Pi/SKR WOs already scope this service).

**A3 — Cloud economy is explicitly client-owned; server applies anti-grief bounds only, not authority**
- **Where:** `api/game/save.js:25-34, 100-176` — accepts client `crystals/food/coins/stone/iron/wood/voidshards/bestWave`, guarded only by `MAX_RESOURCE=1e9`, best_wave anti-rollback, anti-wipe (>95% drop). Comment: *"soft currency is CLIENT-OWNED; the guards above are anti-grief only."*
- **Risk:** An authenticated player can push any balance up to 1e9; no server-side derivation of balances from gameplay events.
- **Severity:** High (the file itself flags this must flip when currency buys real value).
- **Remediation:** Execute the documented BUILT-TO-FLIP plan (`save.js:213-230`): server accepts gameplay *events*, recomputes the wallet server-side (wave rewards server-computed; IAP/on-chain entitlements re-fetched, idempotent on tx hash); wallet columns read-only to the client; field-strip guards become hard 4xx rejects.
- **Effort:** H.

**A4 — Payment "success" is client-decided; grant fires on client-asserted Ok with no server verify/idempotency** — *also raised by Monetization (C-PI).*
- **Where:** `PackStore.Purchase` `:579-585` grants on `result.Ok`; `WalletService.Pay` `WalletService.cs:412-451`; `StubWalletProvider.SendPayment` `:119` fabricates success; `SolanaWalletProvider.SendPayment` `:230` confirms via the client's own RPC poll. No server re-verifies the on-chain tx before self-grant.
- **Risk:** A patched client returns `Ok=true` (or points `_provider` at the stub) and grants with no payment. No idempotency on `TxSignature` — a confirmed tx can be replayed to grant multiple times; paid amount is never validated against pack price.
- **Severity:** High.
- **Remediation:** Grant only after a server verifies: correct recipient treasury, correct mint, amount ≥ pack price, `tx_signature` unused (idempotent). Mirror the Pi two-phase pattern on the Solana rail.
- **Effort:** M–H.

**A5 — CryptoPaymentManager Glimmer top-up repeats the client-trust grant**
- **Where:** `Assets/_Modules/Wallet/CryptoPaymentManager.cs:159-226` `SendFlatPayment` → on client `result.Ok` calls `GrantGlimmer` `:235` via reflection into `GlimmerCurrencyService`; SKR `+25%` bonus computed client-side `:132-145`.
- **Risk:** Same as A4 for the Glimmer rail; no server validates the tx, no `txId` idempotency, SKR bonus client-controlled.
- **Severity:** Med (High when Glimmer is buyable for real value).
- **Remediation:** Route through the same server-verified entitlement path as A4; compute bonus server-side.
- **Effort:** M.

**A6 — AdminOverlay currency-grant gated only by a runtime PlayerPrefs flag, not compile-stripped** — *deep-dup with Hardening E-ADMIN/E-DEVTOOLS (the same exposure from two angles); see E-ADMIN for the primary write-up.*
- **Where:** `Assets/_Modules/HUD/AdminOverlay.cs:26` (plain `MonoBehaviour`); grants `:572-575` (`AddCrystals`), `:617+` (`GrantSpendable`); gated only by `FeatureFlags.DevHotkeys` `:146`, read from plaintext `PlayerPrefs.GetInt("ff."+name)` (`FeatureFlags.cs:265`). Contrast `DevPanelController` which IS compile-stripped (`#if DEVELOPMENT_BUILD || UNITY_EDITOR`, `:44`).
- **Severity:** Med–High (see E-ADMIN — Hardening confirms the Help-menu button is the live entry point and the auth check is a no-op).
- **Remediation / Effort:** see E-ADMIN.

> **Correctly designed — keep:** the `IWalletProvider`/`ISaveProvider` seam isolates the SDK and makes the flip a policy change; `api/_lib/wallet-auth.js` (ed25519 nonce challenge, payload-hash binding, single-use burn, wallet==playerId) is correct anti-replay design (just not switched on — see B-AUTH); the Pi `/approve`+`/complete` two-phase server handshake is the right model; mainnet is owner-gated (`WalletService.cs:224`, `SolanaWalletProvider.cs:239`).
>
> **Residual / verify:** `Assets/_Modules/Core/Promo/PromoCodeService.cs` and `Assets/_Modules/Core/Referral/ReferralService.cs` as additional free-grant vectors — confirm rewards are server-validated + idempotent (code comments indicate the server also gates; see B-PREFS).

### 2B. CODE SECURITY

**B-CLEAN — High-severity code classes are clean (no action).**
- No hardcoded secrets/keys: `wallets.json` holds only public on-chain addresses, "NO private keys", regression-guarded (`Assets/_Modules/Wallet/Tests/WalletRegistryTest.cs:81`). Broad scan for `sk_live/AIza/AKIA/ghp_/BEGIN…PRIVATE KEY/mnemonic/seed_phrase` → zero hits; no `.env`. `SolanaWalletProvider.cs:28` holds no signing material (delegated to MWA/Seed Vault).
- No unsafe deserialization: every `JsonConvert`/`JsonUtility` parse targets plain DTOs; **no `TypeNameHandling`/`SerializationBinder`/`BinaryFormatter`/`XmlSerializer` anywhere** → no .NET gadget-chain RCE. (Recommend pinning Newtonsoft `MaxDepth` to harden against hostile deeply-nested JSON DoS — Low, S.)
- Reflection (~40 sites) all use hardcoded type/method/field literals; no network/user-controlled type names reach `Type.GetType` → not an injection vector (maintainability/fragility only).
- Randomness: security-relevant nonce is server-issued (`GameStateService.cs:1063`); all `System.Random`/`UnityEngine.Random`/`Guid` usage is gameplay or trace IDs — none gate a security decision.

**B-AUTH — Fail-open save authentication; auth OFF by default; guest identity is a device fingerprint** — *raised by Code Security (#4), Economy (M3), and Hardening (M4); consolidated here.*
- **Where:** `GameStateService.cs:1002-1057` (`TryAttachAuthHeaders`), `:843-861`; `BackendAuthConfig.cs:53` (`Enforced` defaults false). Server side `api/game/save.js:91-94` + `api/_lib/wallet-auth.js` correctly require a wallet-signed single-use nonce and reject mismatched wallets — solid when used.
- **Risk:** With the flag off, the client sends no auth headers and **silently skips headers on any signing failure** (`:1043`, `:1049`) — every failure path "sends without auth headers." Guest `playerId = "guest-local-" + SystemInfo.deviceUniqueIdentifier` (a stable hardware fingerprint used as network identity — privacy/GDPR-adjacent). If a real rail is enabled without flipping `Enforced` and wiring a real MWA signer, saves either silently fail or (if the server gate is relaxed to ship) any wallet string could read/overwrite another player's record. Today: stub signer + 401 means cloud writes effectively don't happen (offline-only → currently safe); the seam ships now.
- **Severity:** High (Med if the backend independently rejects unauthed writes).
- **Remediation:** Make enabling any real payment rail hard-depend on `Enforced=true` + a real signer; **fail CLOSED** (abort sync) when a real signer can't sign rather than sending unauthed; startup assert that refuses cloud sync on a real rail without auth; hash/salt the device id rather than sending raw.
- **Effort:** S client / M with backend.

**B-PREFS — Client-authoritative trust in plaintext PlayerPrefs (entitlement/currency/progression)** — *overlaps Economy A1/M1.*
- **Where & risk:**
  - `Assets/_Modules/Cosmetics/BattlePassManager.cs:284/292` — `_hasPremium` (paid battle-pass entitlement) read straight from PlayerPrefs; set to 1 → premium free. **Med** (High if premium gates real paid content).
  - `Assets/_Modules/Village/Arena/ArenaWalletService.cs:107/119` (SKR balance as editable string); `Assets/_Modules/Cosmetics/GlimmerCurrencyService.cs:259/273` (premium currency in PlayerPrefs JSON) — edit to mint. Documented STUB/offline; **Med**, must be server-authoritative before any real-value economy.
  - `ReferralService.cs:238` (`_hasClaimedReferral`) / `PromoCodeService.cs:157` (`_redeemedLocally`) — local-only dedup; **mitigated** (server also rejects, `:188`). **Low** given server gate.
  - Progression flags `TechTree.cs:46/56`, `ResourceBuildingState.cs:62/147` — local-only unlocks; fine single-player, flag if they ever drive multiplayer/leaderboard. **Low**.
- **Remediation:** Validate premium + all currency balances server-side; local PlayerPrefs = cache only.
- **Effort:** M (needs server ledger).

**B-URLFLAG — Unvalidated URL → feature-flag activation on WebGL**
- **Where:** `Assets/_Modules/Core/FeatureFlags.cs:281-307` (`ApplyUrlActivationOnce`) parses `Application.absoluteURL` query and writes PlayerPrefs; today only honors `trace=1`, wrapped in try/catch.
- **Risk:** Bounded now; the pattern (URL param → flips flag) could enable debug/cheat features via crafted link if more keys are added.
- **Severity:** Low.
- **Remediation:** Allowlist of URL-activatable flags; never activate gameplay/economy flags this way.
- **Effort:** S.

**B-JSONINJ — Hand-rolled JSON body in Jupiter swap**
- **Where:** `Assets/_Modules/Web3/JupiterSwapService.cs:261-265` (`ExecuteSwapAsync`) builds the `/swap` POST body by raw string concat of `userPublicKey` (unescaped) + `quote.RoutePlan` (verbatim).
- **Risk:** `userPublicKey` is base58 (no quotes) so practically unexploitable, but it's a hand-rolled JSON body. (`HelpMenu.cs:314` does it right with a `JsonEncode` helper.)
- **Severity:** Low.
- **Remediation:** Serialize via DTO + `JsonUtility.ToJson`/Newtonsoft.
- **Effort:** S.

### 2C. MONETIZATION COMPLIANCE

> **Meta-finding:** the designed *content* honestly upholds the cosmetic/convenience covenant and contains **zero gambling/loot-box mechanics today**. The failure is that **enforcement is fictional** — the covenant lives in JSON comments and one un-gated shallow test, while the named validators were never built.

**C-COV — Claimed `SkrStakingRegression` / combat-category build-gate DOES NOT EXIST**
- **Where:** asserted in `Assets/StreamingAssets/Data/Canonical/skr_staking.json` (lines 2, 82, 85 — "SkrStakingRegression fails the build if a perk kind outside this set appears") and `battle_monthly_packs.sample.json:5` ("NO 'combat' kind — validator rejects"). Project-wide search for `SkrStaking`, `combat.kind`, `RejectCombat`, `convenienceAllowList`, `perkKindEnum`, `payToWin` → **zero implementing code.** No `PackCategory` enum, no grant-kind validator, no combat-category rejection, no build gate inspecting pack/SKR/staking contents.
- **Risk:** The protection the project believes it has is imaginary; any edit can break the covenant undetected. (Current content is clean, so not a content violation yet.)
- **Severity:** High.
- **Remediation:** Build a `MonetizationCovenantRegression` editor gate, wired into `RegressionSuite.RunAll`, that loads every monetization JSON and FAILS on: any `convenience.kind` outside the allowlist; any perk/grant `kind` outside the enum; any `category`/grant kind == combat/stat; any probability/odds/roll field; any non-zero combat stat. Closes C-RANDOM permanently.
- **Effort:** M.

**C-TEST — Only real check (`PackCatalogTest`) is shallow AND not wired into the headless gate**
- **Where:** `Assets/Data/Tests/PackCatalogTest.cs` (EditMode NUnit) checks name/tagline/theme for banned words (`loot, gacha, random, mystery, lottery, spin, gamble`), contents present, one `founderOnly`, prices climb, disclaimer present — but does NOT validate `convenience.kind` against the allowed set, does NOT inspect economy amounts, has no concept of a "combat" item. `Assets/Editor/RegressionSuite.cs` (headless `RunAll`) states it CANNOT run EditMode/PlayMode tests (`:31-35`, footer `:1178-1196`); `DataRegression.cs` covers loot-tables/crafting/economy only. So even the one real test does not gate a build.
- **Severity:** High.
- **Remediation:** Wire EditMode `-runTests` into the check-in gate (the footer of `RegressionSuite.cs` documents how); extend it to validate convenience/economy, not just name strings. Bundle with C-COV.
- **Effort:** M.

**C-KIND — `ConvenienceItemDef.Kind` is an unvalidated string; no allowlist at load or runtime**
- **Where:** `Assets/_Modules/Wallet/PackCatalog.cs:59`. Nothing constrains it to `instant-build / instant-repair / harvest-auto-collect / xp-weekend`. `PackStore.ApplyPackContents` doesn't even apply convenience tokens (`PackStore.cs:659-661`, "no token tray yet") — no runtime chokepoint either.
- **Severity:** Med.
- **Remediation:** Validate `Kind` against an enum/allowlist in `PackCatalog` load; reject on miss (`FlowTrace.Fail`). Part of C-COV's fix.
- **Effort:** S.

**C-PI — Pi backend verifies approve/complete but SKU grant is client-side and not bound to amount/SKU** — *overlaps Economy A4.*
- **Where:** `pi-backend/src/index.ts:14-16, 62-83`. Two-phase `/approve` + `/complete` against `api.minepi.com` is correct anti-spoof bones (marks complete only on Pi 200). But: (a) `PackStore.ApplyPackContents` still grants client-side ("V1 = client self-grants on a SERVER-VERIFIED completion"); (b) `/complete` hardcodes `entitlement="pi_pack_small"` (`:79`) regardless of amount/memo/SKU, never validates paid amount vs ordered pack price; (c) `Access-Control-Allow-Origin: "*"` (`:31`). The design WO (`WORK_ORDER_pi_browser_integration.md:228`) mandates "Do NOT grant any SKU on the client's word" + a server-held orders table — implementation does only half.
- **Severity:** Med (per WO, acknowledged V1-minimal).
- **Remediation:** Persist `{paymentId → sku, expectedAmount, uid}` at `/approve`; at `/complete` validate Pi's reported amount == expected and write the **specific** entitlement server-side; client reads entitlement from server. Tighten CORS to the Pi app origin.
- **Effort:** M.

**C-RANDOM — No randomized rewards today, but nothing prevents adding one (latent)**
- **Severity:** Low→Med (latent). Closed permanently by C-COV's "reject probability/roll fields" rule. Not a standalone blocker.

**C-CLAIM — "never power" marketing vs. selling gameplay currency + `xp-weekend` 2x-XP**
- **Where:** economy bundles sell build currency (Founder's Vow = 15,000 crystals) and `xp-weekend` 2x-XP accelerates leveling → faster talent/skill unlocks (temporal advantage). `skr_staking.json convenienceAllowList` includes `echo_storage_slot`, `passive_accrual_hours` (cap-raises).
- **Risk:** Defensible for a PvE/single-hero game (no competitive integrity), but stretches the literal "SKR buys only time and beauty, never power" claim (`skr_store.json:11`).
- **Severity:** Low.
- **Remediation:** Confirm acceptable for PvE; optionally soften copy to "convenience and beauty"; keep XP-accel out of any future PvP. Not a blocker.

**C-FOMO — FOMO levers are present but currently the benign versions**
- **Where:** `monthlyCards` use a claim-pool "missed days never lost" model (`battle_monthly_packs…json:182,190`) — explicitly non-predatory; Founder launch-window scarcity (`packs.json:93`, `skr_store.json:109`); 35-day time-boxed battle-pass season earned by play only.
- **Severity:** Low. Ensure the founder window is genuinely honored; avoid adding countdown-pressure or losable streaks. Not a blocker.

### 2D. PRIVACY & CRYPTO / REGULATORY

> **These are risk assessments, not legal advice.** Items D-COPPA, D-ONCHAIN, D-MSB, D-HOWEY, D-SWEEP, D-MICA require qualified crypto/gaming/privacy counsel before launch. **Meta-finding:** `docs/whitepaper.md` §7 asserts *"No analytics; no cookies; no third-party trackers"* and that policy/deletion/access endpoints *"will ship before any user-facing public release"* — but `Assets/_Modules/Core/Analytics/EventTracker.cs` + `api/events/track.js` + `api/schema.sql` (`analytics_events`) actively collect wallet-keyed behavioral analytics. That contradiction is itself a misrepresentation risk if shipped as written.

**Privacy (PII / analytics / GDPR-CCPA / COPPA)**

**D-COPPA — No age gate / COPPA exposure.** No age-screening anywhere; cozy Ghibli tower-defense + pets is a classic mixed-audience that attracts under-13s, and the app takes real-money payments + collects persistent identifiers. Amended COPPA (effective 2025-06-23, full compliance 2026-04-22) requires separate verifiable parental consent + tightened monetization. FTC's *Genshin Impact / Cognosphere* settlement targeted exactly this fact pattern. **High. Blocker.** *Fix:* neutral age screen; treat as mixed-audience; block data collection + payments for under-13 (or VPC). Sources: [FTC COPPA](https://www.ftc.gov/legal-library/browse/rules/childrens-online-privacy-protection-rule-coppa), [Goodwin](https://www.goodwinlaw.com/en/insights/publications/2025/01/alerts-practices-dpc-ftc-issues-long-awaited-new-coppa-rules), [Koley Jessen](https://www.koleyjessen.com/insights/publications/ftcs-strengthened-childrens-online-privacy-rules-now-in-effect).

**D-PRIV — Wallet address = persistent PII used as universal join key.** `api/schema.sql` keys every table (`player_data`, `analytics_events`, `leaderboard_scores`, `player_profiles`, `promo_redemptions`, `tower_swaps`, `bug_reports`) on the base58 wallet. Under GDPR/CCPA/CPRA an identifying wallet is personal data; cross-table linkage builds a rich profile with no stated legal basis/consent. **High. Blocker.** *Fix:* privacy policy + lawful basis; consent for analytics; DSAR + deletion cascading by `player_id`; data-minimization review. Sources: [Securiti CPRA vs GDPR](https://securiti.ai/cpra-vs-gdpr/), [Reform GDPR vs CCPA](https://www.reform.app/blog/gdpr-vs-ccpa-cross-border-data-compliance-compared).

**D-ANALYTICS — Analytics collected with no consent/opt-out, contradicting the public claim.** `EventTracker.cs` auto-fires `session_start` on boot (platform/appVersion/unityVersion) + `purchase_completed{packId,price}`, `wave_completed`, etc., tagged with wallet; persisted to PlayerPrefs and POSTed to `defenders-of-the-realm-v2.vercel.app`. No consent gate, no DNT, no opt-out; white paper says "No analytics." **High. Blocker.** *Fix:* consent banner / settings toggle; reconcile or retract the "no analytics" representation; honor GPC/CCPA opt-out of "sharing." Source: code + whitepaper §7.

**D-ONCHAIN — On-chain save sync would make erasure impossible.** `docs/persistence-onchain-spec.md` (v1.1 candidate) syncs save to a Solana PDA. Writing personal/profile data on-chain breaks GDPR/CPRA right-to-erasure; EDPB gives blockchain no exemption — compliant pattern is off-chain data + on-chain hash only. **High (if shipped). Blocker if on-chain save enabled.** *Fix:* keep PII off-chain, store only opaque hashes/pointers (already the stated `DATA_ARCHITECTURE_DECISION` direction — enforce it). Sources: [Chainlink GDPR](https://chain.link/article/blockchain-gdpr-compliance-guide), [CPRA/blockchain](https://www.internetandtechnologylaw.com/cpra-privacy-blockchain/).

**D-BUGREPORT — Free-text bug reports may carry PII and land in the wrong datastore.** `bug_reports` stores a 4000-char free-text `description` + device/scene context + screenshot path; `schema.sql` flags a host mismatch — `HelpMenu` POSTs to the *older* `defenders-of-the-realm.vercel.app` deployment, so reports land in a different project's DB. **Med.** *Fix:* fix host target; cap/scrub free-text; define retention; include in DSAR scope.

**D-SOCIAL — Unverified social handles + public leaderboard usernames.** `api/profile/social.js` stores claimed X/Discord handles with no OAuth verification (impersonation), surfaced publicly when `public=true`. (Good: unlink purges data; server-side profanity denylist `api/_lib/username-policy.js`; opt-in only.) **Low–Med.** *Fix:* OAuth verification before `public`; keep purge-on-unlink; document retention.

**D-CTRL — Documented privacy controls are unbuilt.** Privacy Policy/Terms, `/privacy`, `/terms`, deletion + data-access endpoints, `docs/privacy-compliance-matrix.md` are all "will ship" — none exist in-tree; IP-log ≤7-day retention is asserted, not configured. **Med. Blocker.** *Fix:* build + publish before any public/data-collecting release; produce the privacy matrix.

**Crypto / Financial optics (AML-KYC / securities / real-money)**

**D-MSB — Real-money payout of a liquid exchange-traded token + on-ramp = money-transmitter/MSB (FinCEN) exposure.** SKR is the real Solana Mobile Seeker SPL token (`WORK_ORDER_skr_staking_and_seeker.md` §1; same-mint-vs-separate-token is an OPEN owner decision). Accepting SKR/SOL/USDC for packs **and** distributing convertible SKR to player wallets (Reward Streams A/B/C + staking rebate; `monetization-v2-spec.md` §12, `wallets-of-record.md` §2) is real-money OUT, not closed-loop currency — the highest-exposure design choice. Closed-loop in-game currency is exempt; open/transferable/exchange-convertible is not; the "Pi buys SKR → credits SKR balance" on-ramp + wallet payouts is the trigger. **High. Blocker (needs counsel).** *Fix:* counsel on MSB status; if triggered, FinCEN registration + AML program, or redesign so the redeemable asset is non-convertible; fund rewards from yield only. Sources: [Wilson Sonsini — inadvertent MSBs](https://www.wsgr.com/en/insights/how-gaming-companies-can-become-inadvertent-money-services-businesses.html), [FinCEN Notice 2025](https://www.fincen.gov/system/files/2025-08/FinCEN-Notice-CVCKIOSK.pdf).

**D-HOWEY — Security-token (Howey) optics on staking.** "Stake SKR → earn" can read as expectation of profit from others' efforts. **Strong mitigation already designed:** cosmetic-first loyalty, capped pre-funded pool (`paid+reserved≤funded` invariant), no APY advertised, no new emission, token rebate (option b) gated behind legal sign-off (`WORK_ORDER_skr_staking_and_seeker.md` §A4–A6). SEC 2025 framework: utility/consumptive use + no profit pitch stays out of security territory. **Med (token rebate = blocker; cosmetics = not; needs counsel).** *Fix:* hold the design discipline; ship cosmetics+discount first, token rebate only post-counsel; never advertise yield/returns. Sources: [WilmerHale SEC/Howey 2026](https://www.wilmerhale.com/en/insights/client-alerts/20260324-the-secs-new-framework-for-crypto-assets-under-howey), [Cointelegraph SEC 2025](https://cointelegraph.com/explained/secs-2025-guidance-what-tokens-are-and-arent-securities), [Perkins Coie liquid staking](https://perkinscoie.com/insights/update/sec-statement-liquid-staking-helpful-guidance-caveat).

**D-SWEEP — Skill-contest / sweepstakes / gambling law for reward Streams B & C.** Weekly leaderboard + seasonal tournament pay token prizes. Spec correctly flags this (`monetization-v2-spec.md` §12.4), gates B/C behind a legal opinion (`docs/contests-legal-opinion.md` placeholder — **not filled**), ships Stream A first, plans geo-fencing. 2025–26 enforcement against sweepstakes models tightened sharply. **High (for B/C). Blocker for B/C (needs counsel).** *Fix:* obtain the jurisdictional skill-vs-sweepstakes opinion before enabling B/C; geo-fence; keep `enabled=false` default. Sources: [WilmerHale gaming H2-2025](https://www.wilmerhale.com/en/insights/client-alerts/20260205-legal-developments-in-the-gaming-industry-second-half-of-2025), [Sweepstakes compliance](https://www.capermint.com/blog/sweepstakes-casino-compliance-architecture-for-us-operators/).

**D-KYC — KYC/AML on payout recipients is weak.** Only "soft KYC (email attestation) for prizes >50 SKR" (`monetization-v2-spec.md` §12.3); relies on upstream Pi KYC + Solana wallet identity. (Good: OFAC/SDN screening designed into anti-cheat + wallet behavior scoring, `whitepaper §6.4`, `wallets-of-record.md` §6.) **Med (partial blocker).** *Fix:* real KYC for material payouts; sanctions screening before any payout; documented AML/OFAC procedure. Sources: [Pi KYC/MiCA](https://www.mexc.com/learn/article/pi-network-kyc-deadline-march-14-2025-complete-guide-to-verification/1).

**D-MICA — EU MiCA / CASP + whitepaper exposure.** Studio is not the SKR issuer (Solana Mobile is) → low issuer risk, but offering crypto payments + token rewards to EU users and publishing a "white paper" touches MiCA's CASP/marketing perimeter (full enforcement 2026-07-01). **Med (monitor; needs counsel).** *Fix:* counsel on whether reward distribution/on-ramp = regulated CASP activity for EU users; geo-fence if needed; ensure the marketing "white paper" isn't a MiCA crypto-asset whitepaper trigger. Sources: [ESMA MiCA](https://www.esma.europa.eu/esmas-activities/digital-finance-and-innovation/markets-crypto-assets-regulation-mica), [Sumsub MiCA 2026](https://sumsub.com/blog/crypto-regulations-in-the-european-union-markets-in-crypto-assets-mica/).

**D-TAX — Crypto tax / irreversibility / no refunds.** Spec notes crypto receipts are owner's tax burden and crypto buys are non-refundable, with no refund UI (`monetization-v2-spec.md` §10/§11); records USD-at-receipt for basis (good). **Low–Med.** *Fix:* disclose no-refund clearly pre-purchase; keep the USD-basis entitlements log.

**D-TERMS — No surfaced refund policy / terms / "not an investment" disclosure for crypto rails.** (Monetization audit #8.) **Med (if crypto/SKR ships).** *Fix:* add purchase ToS, refund terms, crypto risk/non-investment disclaimer to the store flow before wallet-rail go-live. Blocker for crypto-rail launch, not devnet/stub. Folds with D-CTRL/D-TAX.

**Platform policy (Apple / Google / Solana dApp Store / Pi)**

**D-PLATFORM — Native iOS/Android + crypto purchase of digital goods = hard policy conflict.** Apple/Google forbid unlocking content via crypto and require IAP for digital goods; selling cosmetics for SKR/SOL/USDC or via an external Pi rail inside a native app violates 3.1.1 / Play billing. Roadmap's "Android-broad second" (`whitepaper §9.2`) collides head-on. **High (if native). Blocker for App/Play native.** *Fix:* keep crypto to Solana dApp Store (crypto-native, zero-fee) + Pi Browser web rail; if Play/App Store, strip crypto purchase of digital goods or use compliant IAP. Sources: [Apple Review Guidelines](https://developer.apple.com/app-store/review/guidelines/), [Fenwick — Apple loot odds](https://www.fenwick.com/insights/publications/apple-now-requires-disclosure-of-loot-box-odds).

**D-WALLETENROLL — Apple/Google crypto-wallet org-enrollment + exchange licensing.** Wallet/exchange-like functionality must come from an org-enrolled developer; on-ramp/"how to get SKR" swaps may invoke exchange rules. (Loot-box odds-disclosure rule is N/A — no loot boxes — a genuine advantage.) **Med.** *Fix:* enroll as organization; keep on-ramp as an external link, not in-app exchange.

**D-DAPP — Solana dApp Store gating (lowest-friction path).** Requires publisher KYC/KYB, a dedicated NEW signing key (a Play-used key is rejected), publisher wallet funded ~0.2 SOL, asset spec, Publisher Policy compliance; review ~2–5 days. **Med (process gate).** *Fix:* complete portal KYC/KYB; mint separate keystore; confirm contest posture; produce listing assets. Sources: [Solana Mobile publishing](https://docs.solanamobile.com/dapp-publishing/overview).

**D-PIRAIL — Pi rail = mandatory server-verified payments + KYC + app review.** Real-Pi payments require KYC'd Pioneers, the server-side `/approve`+`/complete` handshake (never grant on client word), Server API Key kept server-only, domain validation, app review. Unity-WebGL-in-Pi-Browser is unproven (`WORK_ORDER_pi_browser_integration.md` §6 — the #1 technical risk). **Med (de-risk first).** *Fix:* Phase-0 spike before committing; keep API key server-side; grant only on Pi `/complete` 200 (ties to C-PI). Source: [Pi payments docs](https://github.com/pi-apps/pi-platform-docs/blob/master/payments.md).

**D-WHITEPAPER — Stale/over-claiming canon vs. reality.** `docs/whitepaper.md` presents shipped security/compliance posture (cyber audit, pentest, treasuries, "no analytics") that the tree shows as deferred or contradicted (treasuries "pending Squads", analytics live, policies unbuilt). Submitting this to a grant committee or store reviewer as current is a representation risk. **Med (pre-external).** *Fix:* reconcile the white paper to ground truth (per CLAUDE.md §15) before external use.

> **Positive controls (credit where due):** no loot boxes / gacha / randomized purchases; full pre-purchase price disclosure; combat power never sold; capped non-inflationary reward pool with a regression-gated invariant; treasury multisig discipline; wallet separation; OFAC screening designed in; payout rails default-OFF until audit/pentest close. These materially lower optics risk *if the framing discipline holds*.

### 2E. HARDENING

**E-ADMIN — Dev "Load resources" grant panel reachable + usable in RELEASE builds (economy bypass)** — *consolidates Hardening H1 + Economy A6.*
- **Where:** `Assets/_Modules/HUD/HelpMenu.cs:190-198, 410-437` + `Assets/_Modules/HUD/AdminOverlay.cs:366-390`. The Settings/Help menu always wires a "Dev tools" button → `AdminOverlay.Open()`; the comment says AdminOverlay "SHIPS in release builds (NOT #if-gated)." `AdminOverlay.SetOpen()` calls `IsAuthorised()` but **ignores the result** (the `if (!IsAuthorised()) { /* still allow */ }` block is a no-op); `OwnerWalletAddress` is `""` so the gate is dead.
- **Risk:** Any player in the shipped .exe/WebGL opens it and taps "Load resources (full base)" → `OnLoadResources` grants +50k Gold/Wood/Iron, +25k Food/Crystals into the real spendable wallet (`EconomyService.GrantSpendable`), plus "Set Level 10", "+100 Wisdom", "Reset Yarn". Direct economy-integrity hole with monetization planned. (The DevHotkeys *chord* is correctly gated; the Help-menu *button* is the hole.)
- **Severity:** High.
- **Remediation:** Gate the launcher AND grant handlers behind `Debug.isDebugBuild || Application.isEditor || FeatureFlags.DevHotkeys` (or a real owner-wallet check that actually blocks `SetOpen` on failure). Honor `IsAuthorised()` — return early in `SetOpen(true)` when unauthorized and not a dev build. Best fix: compile-strip like `DevPanelController` (`#if DEVELOPMENT_BUILD || UNITY_EDITOR`).
- **Effort:** S (1–2 hr).
- **Verify:** confirm whether `AdminOverlay` is present in any shipped scene (Economy audit could not verify scene membership; Hardening confirms the Help-menu entry point reaches it).

**E-DEVTOOLS — Other dev-only tools reach players through the same ungated panel**
- **Where:** `AdminOverlay.cs:476 (OnVfxParade)`, `:520 (OnSeatingEditor)`, `:292 (OnOrientAsset)` → `SeatingEditorOverlay` (writes `offsets.json`), `VfxParadeRuntime`, `TowerPlacementRotateMenu.OpenDevOrient`. `SeatingEditorOverlay.cs` header says "DEV-ONLY … not exposed to normal players," but its only entry point is the ungated panel.
- **Severity:** Med. Same root cause as E-ADMIN; one gate covers all of them.
- **Effort:** S (folds into E-ADMIN).

**E-FLOWTRACE — `FlowTrace.Enabled = true` by default ships hot-path logging into release/WebGL**
- **Where:** `Assets/_Modules/Core/Diagnostics/FlowTrace.cs:24` — master switch defaults on, runtime bool, not `#if`-stripped. Hundreds of `Step`/`Enter` sites fire `Debug.Log` (only `Throttle`/`Once` rate-limited).
- **Risk:** Real frame cost on the Pi WebGL target (string interp + alloc + console pump) and dumps internal flow/state (wallet ids, save lengths, roster contents) into the browser console. No build-time strip.
- **Severity:** Med (perf + log spam + minor info-leak).
- **Remediation:** Default `Enabled = Application.isEditor || Debug.isDebugBuild;` (or drive from `FeatureFlags`), and/or wrap call bodies in `[System.Diagnostics.Conditional("ENABLE_FLOWTRACE")]` so release strips calls entirely. Keep the runtime toggle for opt-in remote triage.
- **Effort:** S.

**E-COREREG — `CoreServices` HUD/Audio/HudModel registration silently overwrites; registry not thread-guarded**
- **Where:** `Assets/_Modules/Core/CoreServices.cs:41, 55, 72`. `RegisterHud`/`RegisterAudio`/`RegisterHudModel` overwrite with no replace-warning (unlike `RegisterJupiter`/`RegisterWalletSigner` which log). A scene-additive race silently steals the slot (the `ReferenceEquals` unregister guard is correct, so it won't null a live newer service). Static mutable shared state, no thread guard (fine today — all main-thread — but undocumented).
- **Severity:** Low.
- **Remediation:** Mirror the Jupiter replace-warning on the other slots; XML note that registration is main-thread-only.
- **Effort:** S.

**E-FTTHREAD — `FlowTrace` static collections are not thread-safe**
- **Where:** `FlowTrace.cs:131 (s_nextAt)`, `:148 (s_seen)`, `:52 (s_muted)` — `Throttle`/`Once`/`Mute` mutate plain `Dictionary`/`HashSet` with no lock. UniTask continuations resume main-thread by default (latent), but any `Task.Run`/threadpool callsite calling FlowTrace can corrupt the dictionary (torn read → exception). `WebTraceSink` buffer is correctly locked; FlowTrace's own state is not.
- **Severity:** Low.
- **Remediation:** Guard the three collections with a lock, or `[ThreadStatic]`/`ConcurrentDictionary`; cheapest: document + assert main-thread-only.
- **Effort:** S.

**E-QUITSYNC — `OnApplicationQuit` save-sync is fire-and-forget async (won't complete on quit)**
- **Where:** `GameStateService.cs:869-872` — `SyncToBackend(highPriority:true).Forget()` cannot finish before teardown, so the final cloud delta is routinely lost. Local `Save()` is synchronous elsewhere so local progress isn't lost.
- **Severity:** Low.
- **Remediation:** Rely on the offline queue (already persists to `dotr-sync-queue`) and flush next launch; document that the network flush is best-effort.
- **Effort:** S.

**E-VFXMAT — `VfxPool` builds ~62 `new Material(...)` at bootstrap, never destroyed**
- **Where:** `Assets/_Modules/Village/Vfx/VfxPool.cs:302-311` (`ApplyEmissiveMaterial`). Each pooled primitive gets a fresh `new Material(sh)`; the pool is `DontDestroyOnLoad`, never released. Bounded (not a growing leak) but unmanaged GPU/material memory with no teardown.
- **Severity:** Low.
- **Remediation:** Cache one shared material per (color, emissive) kind; or `Destroy` in `OnDestroy`.
- **Effort:** S.

> **Verified clean (no action):** RenderTexture lifecycle (all five RT owners null→`Release()`→`Destroy()` in `Dispose`/`OnDestroy`); `AlwaysIncludedShaders` (URP Lit/Unlit/Particles present, `EnsureShadersIncluded.cs` applied, belt-and-suspenders `Shader.Find` fallbacks); CompileGate (compile + NUL-byte scan, streamed, early-out); `GameStateService.Load` (migrate→validate→Guard→`FlowTrace.Fail`, no silent blanking); WebTraceSink (bounded ring buffer, lock-guarded, never-throw); DevHotkeys chord gated behind `FeatureFlags.DevHotkeys` (default OFF).

---

## 3. PRIORITIZED REMEDIATION ROADMAP

### PHASE 0 — CRITICAL, do now (before any real-value flip)
1. **A1/E-SAVE** — HMAC the local save; treat as untrusted cache. *(S, code)*
2. **A2 + A3** — Server holds entitlements + currency; client reads, never writes (the documented `save.js` BUILT-TO-FLIP). *(H, code+backend)* — gates LB-1.
3. **A4 + C-PI** — Grant only after server-verified, idempotent payment; bind SKU↔amount; mirror Pi two-phase on Solana. *(M–H, code+backend)* — gates LB-2.
4. **B-AUTH** — `Enforced=ON` for any non-local provider; fail-CLOSED on signing failure; startup assert. *(S client / M backend)* — gates LB-4.

### PHASE 1 — HIGH (launch-readiness, parallel to Phase 0 where lane-disjoint)
5. **E-ADMIN + E-DEVTOOLS** — One gate (or compile-strip) on the AdminOverlay/HelpMenu dev-tools entry. *(S)* — highest-leverage single fix; gates LB-11.
6. **C-COV + C-TEST + C-KIND** — Build `MonetizationCovenantRegression`, wire `PackCatalogTest`/EditMode `-runTests` into the headless gate, allowlist `ConvenienceItemDef.Kind`. *(M)* — gates LB-5; closes C-RANDOM.
7. **A5** — Route Glimmer top-up through the A4 server-verified path; bonus server-side. *(M)*
8. **B-PREFS** — Server-validate `BattlePassManager._hasPremium` + all currency balances; PlayerPrefs = cache. *(M)*
9. **Privacy build-out (D-COPPA, D-PRIV, D-ANALYTICS, D-CTRL):** age gate; publish Privacy Policy + Terms; consent + opt-out; DSAR/deletion cascading by `player_id`; reconcile/retract the "no analytics" claim. *(M–H, code+legal)* — gates LB-6, LB-7.
10. **Legal sign-offs (needs counsel) — D-MSB, D-HOWEY, D-SWEEP:** keep token rebate + reward Streams B/C default-OFF until MSB/Howey/sweepstakes opinions are filled (`docs/contests-legal-opinion.md`). *(legal)* — gates LB-8.
11. **D-PLATFORM** — No crypto purchase of digital goods in native Apple/Google builds; keep crypto to dApp Store + Pi web. *(design/build)* — gates LB-9.
12. **E-FLOWTRACE** — Strip FlowTrace in release for the Pi WebGL perf path. *(S)*

### PHASE 2 — MEDIUM
13. **D-ONCHAIN** — If/when on-chain save ships, enforce PII-off-chain (hash/pointer only). *(design+code)* — gates LB-10 (conditional).
14. **D-BUGREPORT** — Fix the `bug_reports` host target; cap/scrub free-text; retention; DSAR scope. *(S–M)*
15. **D-KYC** — Real KYC for material payouts + sanctions screening before any payout. *(legal+backend)*
16. **D-TERMS/D-TAX** — Purchase ToS, refund terms, crypto non-investment disclaimer pre-crypto-launch. *(design+legal)*
17. **D-WHITEPAPER** — Reconcile white paper to ground truth before any external/grant/store submission. *(docs)*
18. **D-DAPP / D-PIRAIL / D-WALLETENROLL** — Process gates: publisher KYC/KYB, separate signing key, Pi Phase-0 WebGL spike, org enrollment. *(process)*

### PHASE 3 — LOW / deferred
19. **B-URLFLAG** — Allowlist URL-activatable flags. *(S)*
20. **B-JSONINJ** — Serialize Jupiter swap body via DTO. *(S)*
21. **Newtonsoft `MaxDepth`** hardening against nested-JSON DoS. *(S)*
22. **E-COREREG / E-FTTHREAD** — Replace-warnings + main-thread-only documentation/asserts. *(S each)*
23. **E-QUITSYNC** — Document quit network flush as best-effort; rely on offline queue. *(S)*
24. **E-VFXMAT** — Cache shared materials / destroy in `OnDestroy`. *(S)*
25. **C-CLAIM / C-FOMO** — Design review: soften "never power" copy if desired; honor founder window; keep XP-accel out of any PvP. *(design)*
26. **D-SOCIAL** — OAuth-verify social handles before public surfacing. *(M)*

---

## 4. DEDUP MAP (which audits raised each finding)

| Consolidated ID | Economy | Code Sec | Monetization | Privacy/Crypto | Hardening |
|---|---|---|---|---|---|
| A1 / E-SAVE | C1 | #5 | — | — | M5 |
| A2 | C2 | — | — | — | — |
| A3 | H1 | — | — | — | — |
| A4 / C-PI | H2 | — | (note) | — | — |
| A5 | M1 | — | — | — | — |
| A6 / E-ADMIN | M4 | — | — | — | H1, M3 |
| B-AUTH | M3 | #4 | — | — | M4 |
| B-PREFS | M1(part) | #5 | — | — | — |
| C-COV/C-TEST/C-KIND | — | — | #1,#2,#3,#6 | — | — |
| D-MSB/HOWEY/SWEEP | — | — | #4,#8 | C1–C5 | — |
| D-COPPA/PRIV/ANALYTICS/CTRL | — | — | — | P1,P2,P3,P7 | — |
| E-FLOWTRACE | — | — | — | — | M2 |

---

## 5. SOURCES (privacy / crypto / regulatory)

- FTC COPPA rule — https://www.ftc.gov/legal-library/browse/rules/childrens-online-privacy-protection-rule-coppa
- Goodwin — new COPPA rules — https://www.goodwinlaw.com/en/insights/publications/2025/01/alerts-practices-dpc-ftc-issues-long-awaited-new-coppa-rules
- Koley Jessen — COPPA in effect — https://www.koleyjessen.com/insights/publications/ftcs-strengthened-childrens-online-privacy-rules-now-in-effect
- Securiti — CPRA vs GDPR — https://securiti.ai/cpra-vs-gdpr/
- Reform — GDPR vs CCPA — https://www.reform.app/blog/gdpr-vs-ccpa-cross-border-data-compliance-compared
- Chainlink — blockchain GDPR — https://chain.link/article/blockchain-gdpr-compliance-guide
- Internet & Tech Law — CPRA/blockchain — https://www.internetandtechnologylaw.com/cpra-privacy-blockchain/
- Wilson Sonsini — inadvertent MSBs — https://www.wsgr.com/en/insights/how-gaming-companies-can-become-inadvertent-money-services-businesses.html
- FinCEN Notice 2025 — https://www.fincen.gov/system/files/2025-08/FinCEN-Notice-CVCKIOSK.pdf
- WilmerHale — SEC/Howey 2026 — https://www.wilmerhale.com/en/insights/client-alerts/20260324-the-secs-new-framework-for-crypto-assets-under-howey
- Cointelegraph — SEC 2025 guidance — https://cointelegraph.com/explained/secs-2025-guidance-what-tokens-are-and-arent-securities
- Perkins Coie — liquid staking — https://perkinscoie.com/insights/update/sec-statement-liquid-staking-helpful-guidance-caveat
- WilmerHale — gaming H2-2025 — https://www.wilmerhale.com/en/insights/client-alerts/20260205-legal-developments-in-the-gaming-industry-second-half-of-2025
- Capermint — sweepstakes compliance — https://www.capermint.com/blog/sweepstakes-casino-compliance-architecture-for-us-operators/
- ESMA — MiCA — https://www.esma.europa.eu/esmas-activities/digital-finance-and-innovation/markets-crypto-assets-regulation-mica
- Sumsub — MiCA 2026 — https://sumsub.com/blog/crypto-regulations-in-the-european-union-markets-in-crypto-assets-mica/
- Pi KYC/MiCA — https://www.mexc.com/learn/article/pi-network-kyc-deadline-march-14-2025-complete-guide-to-verification/1
- Apple App Review Guidelines — https://developer.apple.com/app-store/review/guidelines/
- Fenwick — Apple loot-box odds — https://www.fenwick.com/insights/publications/apple-now-requires-disclosure-of-loot-box-odds
- Fenwick — Google loot-box odds — https://www.fenwick.com/insights/publications/google-play-now-requires-disclosure-of-loot-box-odds
- Solana Mobile dApp publishing — https://docs.solanamobile.com/dapp-publishing/overview
- Helius — publishing Solana mobile apps — https://www.helius.dev/blog/publishing-solana-mobile-apps
- Pi payments docs — https://github.com/pi-apps/pi-platform-docs/blob/master/payments.md
- Pi developer guide — https://pi-apps.github.io/community-developer-guide/docs/gettingStarted/devPortal/

**Caveat:** This is a security/compliance risk assessment, not legal advice. Items D-COPPA, D-ONCHAIN, D-MSB, D-HOWEY, D-SWEEP, D-MICA, D-KYC require qualified crypto/gaming/privacy counsel before launch.
