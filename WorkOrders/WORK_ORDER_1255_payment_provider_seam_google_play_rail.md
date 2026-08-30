# WORK ORDER 1255 — Payment-Provider Seam + Google Play Fiat Rail (one build, three channels)

**Status:** BLOCKED 2026-08-29 — storefront design is approved, but the fail-closed Play artifact gate still proves three architecture blockers: Wallet/Web3 lack a `!GOOGLE_PLAY` boundary, Village directly references Wallet, and the MWA Android plugin is unconditional. A signed Play AAB and licensed receipt test also require external Play Console/service credentials. Dormant verification/ledger and settlement remain implemented and regression-tested; nothing is deployed or enabled.
**DESIGN GATE CLEARED (owner, 2026-08-29):** The updated Play-channel store interface images are approved guidance: localized fiat presentation and no wallet/crypto UI. Store-facing implementation may proceed, but approval does not waive source-isolation, billing, signing, artifact-inspection, or regression gates.
**Minted:** 2026-08-28 (CLI seat; banner bumped 1255 → 1256 in the same edit)
**Owner directive (2026-08-28, BINDING):** *"I would prefer one build that ships all three ways and
the data pulled is only from that respective source."* — ONE project/build chain serving all three
distribution channels (Solana dApp Store / Google Play / Pi-WebGL), where every money-touching read
and write resolves ONLY through the active channel's provider at runtime. Google Play = **fiat only,
zero crypto surface** (owner, same session: *"with google we don't use crypto only fiat"*).
**Provenance:** synthesized from a two-lane research pass this session — Google Play policy (web)
+ a very-thorough repo monetization audit. Key findings restated inline so this WO stands alone.

---

## 1. Goal

List Echoes of Elarion on Google Play with the SAME codebase that ships to the Solana dApp Store
and (later) Pi. On Play, all 26 currently canonical pack SKUs sell through **Google Play Billing**
(Unity IAP) in each player's Play-localized fiat currency;
on Seeker they keep the live Solana/SKR rail; on Pi the existing Pi rail. The channel is resolved
once at boot, and from then on *only that channel's* payment provider, price source, and identity
rail are consulted — no cross-bleed, no dead crypto UI on Play, no Play Billing calls on Seeker.

## 2. The one-build tension — surface it, don't bury it (DECISION RECORDED)

`docs/NORTH_STAR.md:266-290` prescribes compiling crypto OUT of a store build (Google reviews
bundled SDKs, not just behavior). The owner's one-build preference supersedes that as the target
architecture, with this mitigation, which the CLI must implement rather than silently reverting to
per-channel defines:

- The single build chain stays; **channel separation is enforced at the packaging step, not by
  forking the project.** The Play artifact is an AAB whose Gradle/asmdef packaging EXCLUDES the
  `DeNelle.Wallet` + `DeNelle.Web3` assemblies and the Solana MWA plugin (they are already their
  own asmdefs — same mechanism as `DeNelle.Village.AdProviders`), while the Seeker APK includes
  them. One project, one pipeline, per-channel artifacts. This satisfies "one build that ships
  three ways" (one codebase/one chain) AND keeps the Solana SDK physically absent from the Play
  binary, which is the cheap way through review.
- If the CLI finds a hard blocker to per-artifact assembly exclusion, STOP and surface it — do not
  ship a Play AAB carrying the Solana SDK without an explicit owner ruling.

## 3. Scope — five lanes (file-disjoint where marked; sequence A → B/C parallel → D → E)

### Lane A — the `IPaymentProvider` seam (the core refactor)
- New interface in `DeNelle.Core` (mirror the `IAdService` pattern exactly —
  `Assets/_Modules/Core/Ads/IAdService.cs` is the in-repo template, including the
  provider-token-only-under-`/Providers/` rule pinned by `AdServiceSeamRegression`):
  `IPaymentProvider { ChannelId, GetDisplayPrice(sku), Purchase(sku, cb), RestorePurchases(cb),
  CanBuy(sku, out reason) }`.
- `SolanaPaymentProvider`: wraps the EXISTING path verbatim — `WalletService.Pay`
  (`Assets/_Modules/Wallet/WalletService.cs:589`), `PurchaseQuoteService`, the SKR quote/confirm
  flow. Behavior change on Seeker: ZERO. This is a move, not a rewrite.
- `GooglePlayBillingProvider`: Unity IAP (`com.unity.purchasing`) in its own asmdef with a
  defineConstraint (same optional-assembly construct as `LevelPlayInitializer` — see its header
  comment for WHY it must be an asmdef constraint and not a `#if`).
- Re-point `PackStore.Purchase` (`Assets/_Modules/Wallet/PackStore.cs:2176-2470`) at the seam.
  NOTE: PackStore currently lives in the Wallet asmdef — the store UI/VM must move (or split) so
  the storefront survives in a build with no Wallet assembly. The grant layer
  (`PackStoreVM.ApplyPackContents` → `EconomyService.GrantSpendablePurchased`) is already
  rail-agnostic; do not touch it.
- Rewrite `PurchaseGate` vocabulary provider-neutral (`SkrMintResolvable`, `PrimaryRail`,
  `ChecklistReport` at `PurchaseGate.cs:372-417` are Solana-specific today; they become
  provider-supplied checks).
- **Delete, do not port, the $4.99 wallet gate** (`PurchaseGate.WalletRequiredAboveUsd`,
  `PurchaseGate.cs:106`) on the Play rail — its premise (guest saves aren't durable) is false
  under Google-account entitlement restore. It stays on the Solana rail.

### Lane B — channel resolution & data isolation (the owner's "respective source" rule)
- Extend the EXISTING channel seam — `Assets/_Modules/Core/Platform/CurrencySkinResolver.cs`
  (already resolves `pi | skr | wallet` and ships the Pi rail in production) — with a `googleplay`
  channel. One resolution at boot; the provider registry hands back exactly one live
  `IPaymentProvider`.
- **Price source per channel:** Seeker reads packs.json rails + server quote (unchanged); Play
  reads localized prices from Play's own SKU catalog via Unity IAP — never from packs.json USD
  (that column is the authoring reference that seeds the Play console products, not a display
  source). Pi keeps its rail.
- **Play product ids MAP TO existing SKU ids, never replace them** — SKU ids are live save keys
  (`packs.json` `_schemaNotes.legacySkus`). A mapping table in the catalog, pinned by regression.
- Identity per channel: Seeker = wallet attestation (unchanged); Play = Firebase Auth / Google
  account keyed saves over the EXISTING non-wallet auth rail (`api/_lib/wallet-auth.js` already
  separates "prove you are you" from "prove you may be paid"); Pi = PiUid (unchanged).

### Lane C — server: Play receipt verification (file-disjoint from A/B; `api/` only)
- New proof branch in `api/purchases/verify.js`: validate the Google Play purchase token
  server-to-server (Google Play Developer API, service-account credential via Vercel env), check
  product id ↔ SKU mapping, then flow into the EXISTING `fulfill`/`reconcile`/idempotent
  paymentId ledger unchanged. Acknowledge/consume per Play's 3-day acknowledgment rule.
- Quote endpoint: `LIST` stays; `QUOTE` is a no-op/skipped for the Play channel (Play owns price).
- Restore: map `queryPurchases()` at boot onto the existing
  `PurchaseEntitlementVerifier.ReconcileAsync` call site (`PackStore.cs:2313`).

### Lane D — Play-build compliance sweep (things that trip review even with the seam done)
- `DAPP_STORE` and `SOLANA_SDK` OUT of the Play artifact's defines; `FeatureFlags.
  StakingPolishBonus` provably OFF (its own header at `FeatureFlags.cs:981-1010` already mandates
  this; `StakingComplianceRegression` pins it — extend the pin to the Play packaging config).
- Excluded from the Play artifact: wallet-connect UI, `TowerSwapService` Solana Pay path, the
  Jupiter swap call (`PackStore.cs:2011`), `SkrValuationOracle`, the "Token price moves with the
  market" disclaimer (`packs.json` `currencyDisclaimer` — must not render on Play).
- In-app account-deletion path (Play requirement once we have login).
- Ads: LevelPlay rail is channel-agnostic and stays; do NOT enter the Families program (Teen
  rating; the IARC questionnaire is a console task for the owner, note it in the RESULT).

### Lane E — gates & proof
- New `PaymentProviderSeamRegression` (mirror `AdServiceSeamRegression`): no concrete
  provider token (`Solana`, `GooglePlay`, `Skr`, mint addresses) outside `/Providers/`; exactly
  one live provider per resolved channel; Play channel resolves zero wallet types.
- SKU mapping parity check in `tools/schema-parity.mjs` (packs.json ↔ Play product mapping).
- Standard pre-ship ladder applies: `COMPILE_GATE_OK` + `REGRESSION_OK` + `UI_CAPTURE_OK` (open
  the PNGs — store screen on the Play channel must show USD prices and NO wallet UI) +
  `R2_PARITY_OK` on any artifact that reaches a device.
- Seeker regression proof: a full purchase-path regression on the Solana channel AFTER the seam
  lands — the live store must be bit-identical in behavior. The Mainnet canary flow
  (`MAINNET_CANARY_TEST`) is the felt-test instrument; owner performs the canary buy.

## 4. What NOT to touch
- The grant layer (`EconomyService.GrantSpendablePurchased`, coins via `AddCoins`) — already
  rail-agnostic.
- `api/purchases/quote.js` binding-quote logic for the Solana rail — it stays exactly as is.
- packs.json SKU ids, contents, or the USD ladder (fee math note: post-Epic-settlement Play fees
  are 20% gameplay-affecting / 9% cosmetic +5% if using Play's own billing — pricing REVIEW is a
  separate owner decision, not this WO).
- The ad rail, the R2 content pipeline, anything under `Assets/_Modules/Village/` gameplay.
- No `.unity` scene edits. No renumbering of any enum ordinals.

## 5. Acceptance criteria
1. One project, one build chain; three artifacts (Seeker APK, Play AAB, Pi WebGL) produced by the
   existing scripts extended, not forked.
2. Play AAB: contains no Solana/MWA SDK, no wallet UI reachable, all 26 currently canonical packs purchasable via
   Play Billing in USD, entitlements restore on reinstall via Google account, verify.js validates
   the purchase token server-side before any grant.
3. Seeker APK: behaviorally unchanged (canary purchase settles, quote flow intact, wallet
   identity intact).
4. Channel isolation proven by regression: the resolved channel's provider is the ONLY money
   surface that answers; cross-channel calls throw/fail closed, never silently fall through.
5. All Lane E markers green on fresh logs; owner felt-verifies the Play store screen capture and
   closes per §13 pipeline.

## 6. Open items owed to the owner (not blockers to starting A/B/C)
- Google Play Console account + app listing creation (owner task; needs the $25 registration and
  the IARC rating questionnaire).
- Service-account credential for server-side receipt validation (owner creates in Play Console;
  lands in Vercel env, never in the repo).
- Pricing review under the 20%/9% fee split (whether the USD ladder stays as authored).

## 7. Implementation clarifications (CLI audit 2026-08-28 — binding before Lane C/D)

1. **Play displays localized fiat, not hardcoded USD.** `ProductDetails` / Unity IAP store
   metadata is the display authority, including currency symbol and localized formatted price.
   The USD column remains authoring input only. A missing/unavailable Play product fails closed;
   it never falls back to packs.json USD or a crypto rail.
2. **Stamp the channel into the artifact.** Android cannot reliably infer Seeker-vs-Play from the
   OS. `BuildSeekerApk` stamps `DAPP_STORE`; the new `BuildGooglePlayAab` stamps `GOOGLE_PLAY` and
   removes `DAPP_STORE`/`SOLANA_SDK`. Runtime resolution consumes that immutable stamp. URL/UA
   routing remains exclusive to WebGL/Pi. Conflicting or absent release stamps fail closed.
3. **Packaging feasibility is Gate 0.** `PackStore` is currently compiled inside
   `DeNelle.Wallet`, and `com.solana.unity_sdk` is a direct Wallet asmdef reference with a
   package-driven `SOLANA_SDK` version define. Before store refactoring, produce an assembly/package
   inventory proving a Play AAB can retain storefront/grants while physically excluding Wallet,
   Web3, Solana SDK, MWA AARs, and crypto resources. If not, STOP per section 2.
4. **Pin Unity IAP deliberately.** The package is not currently installed. Add
   `com.unity.purchasing` only after Gate 0, pin the exact Unity-6-compatible released version in
   `Packages/manifest.json`, and isolate every `UnityEngine.Purchasing` token in the optional Play
   provider asmdef. Package absence must leave Core/Seeker/WebGL compiling.
5. **Classify every SKU before catalog creation.** The mapping table carries
   `sku`, `googleProductId`, and `productType` (`consumable | non_consumable | subscription`).
   Consumables are server-verified and consumed; durable items are acknowledged. Consumed items
   cannot be reconstructed by `queryPurchases`, so reinstall recovery comes from the game's
   server-side entitlement/save ledger, not a false universal "Restore Purchases" promise.
6. **Play Billing is not player authentication.** Define the Firebase/Google sign-in and account-
   linking UX separately. Set an HMAC/pseudonymous `obfuscatedAccountId` on the billing flow and
   validate it server-side. Never use email, raw Google identity, order id, or purchase token as a
   public player key.
7. **One server-owned state machine:** `CREATED -> PENDING | PURCHASED -> VERIFIED -> GRANTED ->
   CONSUMED/ACKNOWLEDGED`, with terminal `CANCELLED/VOIDED/REFUNDED`. Grant only `PURCHASED`, only
   after Google Developer API verification, and transactionally dedupe globally unique purchase
   tokens. Return Unity IAP `Pending` until the server grant and consume/ack step succeeds so a
   crash retries instead of losing or duplicating entitlement.
8. **Background lifecycle is in scope.** Add authenticated Google Cloud Pub/Sub RTDN ingestion,
   message-id dedupe, Developer API re-query (RTDN is a hint, not proof), pending-to-purchased
   handling, and Voided Purchases reconciliation. Record the explicit clawback policy for spent
   consumables; never silently drive balances negative.
9. **Testing requires Play delivery.** Proof uses license testers and an Internal App Sharing or
   internal-test-track AAB installed by Google Play. A sideloaded APK is not billing proof. Cover
   purchase success, user cancel, pending completion while closed, duplicate callback, network
   loss after charge, reinstall/account relink, refund/void, unavailable regional SKU, and all
   all canonical mapping rows (26 at implementation start; parity is data-derived, never hardcoded as an acceptance count).
10. **Play compliance is artifact inspection, not UI hiding.** Gate the final AAB/Gradle dependency
    report and extracted files for Solana/MWA libraries, crypto deep links, wallet strings, token
    mints, Jupiter endpoints, and crypto-only remote/catalog data. A runtime-hidden SDK or string
    still fails the owner directive.
11. **Account deletion is two paths.** Ship both an in-app deletion action and the externally
    reachable deletion URL required by Play account-deletion declarations, with documented effects
    on saves, durable entitlements, purchase history records retained for fraud/tax obligations,
    and future restore behavior.
