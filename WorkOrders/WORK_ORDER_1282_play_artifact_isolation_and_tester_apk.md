# WORK ORDER 1282 — Play artifact isolation + tester APK variant

**Status:** IN PROGRESS 2026-08-30 — Lanes A+B LANDED, all four source-isolation conditions PASS (`PLAY_SOURCE_ISOLATION_OK`) and an AAB builds. ⛔ NOT SHIPPABLE: the ARTIFACT is dirty (see FINDING) and PIN-1b (wallet-free identity) is unanswered. Lane C cut per owner ruling.
**Minted:** 2026-08-30 (CLI seat, main line; banner bumped 1282 -> 1283 in the same edit)
**Lane:** Monetization/Backend + Assembly structure (isolated — see §9)
**Supersedes nothing. Continues:** `WORK_ORDER_1255_payment_provider_seam_google_play_rail.md`
(its RESULT §"Safe next slice" is the spec seed for this WO)

---

## Why now (owner trigger, 2026-08-30)

Google asked the owner for **an APK to verify with testers**. The repo cannot produce a
Play-compliant Android artifact today, and the artifact it *can* produce is the wrong one:

- `AndroidBuild.BuildGooglePlayAab()` (`Assets/Editor/AndroidBuild.cs:86`) calls
  `GooglePlayPackagingGate.AssertSourceIsolation()` (:88) **before** building, and that gate
  refuses. Its own class doc states it *"deliberately rejects the current source graph until the
  storefront has been split out of Wallet and the MWA Android library has a real per-artifact
  exclusion mechanism."*
- The only buildable Android artifact is `BuildSeekerApk()` (:74) — `DAPP_STORE` define, Solana SDK
  compiled in, `MobileWalletAdapter.androidlib` packaged unconditionally. Handing that to Play
  tester verification submits the Solana wallet rail into Play review, which is precisely what
  Gate 0 exists to prevent.

**Verified against the live tree 2026-08-30** — all four gate conditions currently fail:

| `GooglePlayPackagingGate.InspectSourceIsolation()` condition | Live tree |
|---|---|
| `Assets/_Modules/Wallet/DeNelle.Wallet.asmdef` contains `!GOOGLE_PLAY` | ❌ 0 matches |
| `Assets/_Modules/Web3/DeNelle.Web3.asmdef` contains `!GOOGLE_PLAY` | ❌ 0 matches |
| `DeNelle.Village.asmdef` must NOT reference `"DeNelle.Wallet"` | ❌ it does (1 match) |
| `Assets/Plugins/Android/MobileWalletAdapter.androidlib.meta` carries a Play exclusion | ❌ 0 matches |

This matches, unchanged, the four blockers WO-1255's RESULT recorded. Nothing has regressed; the
work was deliberately left fail-closed and is now being pulled forward by an external deadline.

---

## OWNER PINS — PIN-1 PART 1 RESOLVED 2026-08-30

**⚠ MY EARLIER FRAMING OF PIN-1 WAS TOO BROAD AND IS CORRECTED HERE.** It read "the owner must
approve the fiat-only Play store design", implying a storefront had to be invented. It does not.
Google requires **Google Play Billing** for digital goods, and that rail is ALREADY BUILT and
dormant from WO-1255:

| Piece | Where |
|---|---|
| Unity IAP SDK | `Packages/manifest.json:18` — `com.unity.purchasing: 5.0.4` |
| Receipt verification | `api/purchases/google-play-verify.js` |
| Verified-purchase fulfilment | `api/purchases/google-play-fulfill.js` |
| Account binding (HMAC-pseudonymous) | `api/purchases/google-play-binding.js` |
| Ledger | `api/_lib/google-play-purchases.js`, `api/migrations/20260828_0007_google_play_purchase_state.sql` (applied to Neon) |

### ✅ PIN-1a RESOLVED — pricing (owner ruling 2026-08-30: *"30% when now i get 0 is fine"*)
**The Play shop ships the SAME SKU ladder at the SAME prices as the dApp Store** ($1.99–$49.99, plus
the planned $99+ prestige tier). Google's 30% cut is ACCEPTED. Rationale, in the owner's terms: the
app is live on the Solana dApp Store but **no purchase has ever completed**, so the real comparison
is 70% of a sale against 100% of nothing. Do NOT re-open this to "optimise" Play pricing, and do NOT
author a separate Play price ladder — one ladder, two rails.

### ⛔ PIN-1b STILL OPEN — identity, and it is the REAL blocker
WO-1255's RESULT: *"There is no safe wallet-free Play account/session issuer yet and no durable
`IGooglePlayGrantApplier` that atomically records purchase-token settlement with the local pack
mutation."*

On Seeker the **wallet IS the identity** (saves and entitlements are keyed by `BoundWallet`). A Play
player has no wallet. Something else must key their saves and entitlements before a purchase can be
fulfilled durably — **Firebase Auth is already in the project and is the obvious candidate**. Until
this is answered, a Play build can display a shop and take money it cannot reliably grant against.
This is a bigger gap than the artifact work in this WO and should be sized separately.

**PIN-2:** external Play Console app + service-account configuration is not present (WO-1255 RESULT).
Not needed to *build* the artifact; needed before any receipt-verification test. Out of scope here.

**PIN-3: ✅ ANSWERED 2026-08-30** — *"delete them they are dead code."* `TowerSwapService` /
`TowerSwapMenu` deleted; Lane A landed.

---

## ⛔ FINDING 2026-08-30 — SOURCE ISOLATION IS NOT ARTIFACT CLEANLINESS

All four `InspectSourceIsolation()` conditions now PASS (`PLAY_SOURCE_ISOLATION_OK`, proven by the
gate on a real run) and an AAB built: `Builds/Android/EchoesOfElarion-GooglePlay.aab`, 523 MB.

**IT IS DIRTY. DO NOT UPLOAD IT.** The build was cut short before
`GooglePlayPackagingGate.AssertBuiltArtifact` could run, so no `PLAY_ARTIFACT_*` verdict was ever
emitted. An independent scan of the artifact with the gate's own `ForbiddenArtifactTokens` found:

| Token | Where |
|---|---|
| `solana` | `base/assets/bin/Data/Managed/Resources/Solana.Unity.Metaplex.dll-resources.dat`, `…KeyStore.dll…`, `…Rpc.dll…` |
| `mwa/` | `base/dex/classes2.dex` — the Mobile Wallet Adapter **Java** classes |
| `solana-wallet`, `mobilewalletadapter`, `walletadapter` | `base/assets/bin/Data/Managed/Metadata/global-metadata.dat` |
| `solflare`, `seed vault` | `base/assets/Data/Canonical/wallets.json` |

*(False positives, excluded: `phantom` = the "Phantom Hunter" enemy; `connect wallet` = a
`canon-strings.json` UI string.)*

### THE THREE DOORS NOTHING CLOSED

Assembly define constraints do not reach any of these. This is why the source graph can be clean
while the artifact is not:

1. **`Assets/Resources/SolanaUnitySDK`** — anything under a `Resources/` folder is force-included in
   EVERY build by construction. `.asmdef` constraints are irrelevant to it. This is the same
   force-include hazard `SupercyanGearAddressableMarker`'s header already warns about (WO-191/408).
2. **`com.solana.unity_sdk`** in `Packages/manifest.json`
   (`magicblock-labs/Solana.Unity-SDK.git#v1.2.9`) — a project-level UPM dependency. It compiles in
   regardless of what any `.asmdef` says, and its Android aar is the most likely source of `mwa/`
   in `classes2.dex`.
3. **`Assets/Resources/Data/Canonical/wallets.json`** — the on-chain address registry, also
   force-included via `Resources/`.

### What this means for the WO

`GooglePlayPackagingGate`'s class doc frames isolation as *"the storefront split out of Wallet and
the MWA Android library excluded."* **That framing is incomplete** and should be corrected: two
further doors (Resources force-include, and the package manifest) admit the rail into the artifact.
Closing them is a distinct piece of work from Lanes A and B — likely a build-time preprocessor that
relocates/strips the Resources payload plus a per-variant package strategy. Size it before promising
a Play date.

**⚠ AND JUDGE BY THE ARTIFACT SCAN, NEVER BY `PLAY_SOURCE_ISOLATION_OK`.** Today proved they are
independent: source green, artifact dirty. `AssertBuiltArtifact` is the authority.

---

## Scope

### Lane A — split the rail-neutral store/grant contracts out of Wallet

The load-bearing blocker: `DeNelle.Village` references `DeNelle.Wallet`, so excluding Wallet from a
Play artifact breaks the player compile. Village does not need the *rail*; it needs the *contracts*.

1. Create a new rail-neutral assembly (suggested `DeNelle.Commerce`, `Assets/_Modules/Commerce/`)
   holding the store/grant **contracts and pure data only** — no Solana, no MWA, no Web3 types.
2. Move the rail-neutral types Village actually consumes out of `DeNelle.Wallet` into it.
   Determine that set from the compiler, not from assumption: the authority is what breaks when
   `"DeNelle.Wallet"` is removed from `DeNelle.Village.asmdef`.
3. `DeNelle.Wallet` keeps the **Solana implementation** of those contracts and references
   `DeNelle.Commerce`, never the reverse.
4. Remove `"DeNelle.Wallet"` from `Assets/_Modules/Village/DeNelle.Village.asmdef`.

**⚠ Read the `.asmdef`, not CLAUDE.md §5's table** — that table is explicitly a subset, and the
`.asmdef` is the authority on what may reference what.

---

### Lane A scoping — measured 2026-08-30, read this before starting

**7 Village files consume `DeNelle.Wallet`.** The set splits cleanly except in three places.

**RAIL-NEUTRAL → move to `DeNelle.Commerce`:** `ShortfallPackOffer` / `ShortfallOffer`
(`ShortfallPackOffer.cs:38-40`); the JSON DTOs `PackContents`, `PackEconomy`,
`ConvenienceItemDef`, `BoostSpec`, `PackCatalogData`.

> ### ⚠ CORRECTION 2026-08-30 — the battle pass is NOT in the movable set.
> An earlier draft of this section listed `BattlePassService` + battle-pass data +
> `RewardGrantWriter` as "the cleanest move in the set — zero references to PackCatalog".
> **That was wrong, and it was verified wrong at source before any code moved:**
> - `BattleMonthlyCatalog.cs:244` — `BattlePassSeason.HasPurchasablePremiumLane` calls
>   `PackCatalog.Find(PremiumPassSku)`. It is an instance property **on the very type proposed for
>   the move**.
> - `BattlePassService.cs:129` and `:483` both read `BattleMonthlyCatalog.ActiveSeason`, and
>   `BattleMonthlyCatalog` cannot move — it calls `PackCatalog.IsRedeemableConvenience` at `:604`.
>
> Both are hard compile dependencies in the **forbidden** direction (`Commerce → Wallet`). The only
> clean escape is inverting them into lazily-registered static hooks in Wallet
> (`Func<BattlePassSeason> SeasonProvider`, `Func<string,bool> PremiumSkuResolver`) — and **both
> fail SILENTLY when registration ordering is wrong**: an unregistered `SeasonProvider` makes the
> whole battle pass read as "no season" with no error, which is exactly the silent-failure class
> §12 exists to prevent. Do not add those seams without instrumenting them.
>
> Additional entanglement: `RewardGrantWriter` is itself rail-free, but it dispatches on
> `RewardGrant` / `RewardKind` / `RewardEconomy` / `RewardConvenience`, which live **inside**
> `BattleMonthlyCatalog.cs:74-197`, interleaved with `MonthlyCard` / `MonthlyDailyDrip` /
> `BattleMonthlyData` which stay. Moving it means splitting a 686-line live money-path file, and
> `RewardKind`'s `<see cref="BattleMonthlyCatalog...">` doc comments become unresolvable across the
> new boundary.
>
> **Estimate impact:** the "half a day, mechanical" line below was priced against a movable set that
> does not exist. Re-size Lane A only after PIN-3 is answered.

Full referrer map for the battle-pass types, for whoever picks this up: `ArenaProgressStore.cs:22`
(using), `:53`/`:67`; `BattleMonthlyCatalog.cs` (owns the DTOs); `MonthlyCardService.cs`;
`UI/SeasonTrackPanel.cs`; `Editor/Regression/BattleMonthlyRegression.cs`; and Core-side
`BattlePassData.cs` / `BattlePassReward.cs` / `Core/UtcDay.cs` (name-matched — verify actual
coupling before trusting).

**RAIL-BOUND → must stay in Wallet:** `CurrencyKind` (`WalletService.cs:45-53` — the enum *is* the
rail: `Sol`/`Usdc`/`Skr`), `WalletBalance` (:94), `PaymentResult` (:122 — carries a base58 tx
signature), `WalletAccount`, `WalletStatus`, `WalletNetwork`, `IWalletProvider`, `WalletService`
(:225 — hard-constructs `new SolanaWalletProvider()` at :411-413, not an interface seam),
`WalletConnectDialog`, `PurchaseGate`, `SkrValuationOracle`.

**The three seams that are NOT file moves:**

1. **`PackDef` is mixed.** Village uses only the rail-free SKU surface (`OwnsPermanentBuilder`,
   `Find`, `IsOnBrowsableShelf`, `PermanentBuilderSku`), but the same class carries
   `AmountFor(CurrencyKind)` (`PackCatalog.cs:297-323`), `AmountLabel(CurrencyKind)` (:377-388) and
   `PurchaseGate.DevnetCanarySku` (:346). Move `PackDef` + `PackPricing` as **data**; the two
   `CurrencyKind` methods become a Wallet-side extension; `:346` becomes a Commerce constant or a
   hook Wallet registers. This is a live 744-line money-path file — edit it carefully.
2. **`PackStore` cannot move** (3546 lines, 57 rail refs, `new WalletService()` at :307). Village
   needs exactly two things from it: a handle (`MarketplaceInteractor.cs:67`
   `FindAnyObjectByType<PackStore>`) and the static `RequestFocusSku` (`PackStore.cs:592`, a
   one-line setter). Seam: an `IStorefront` / `StoreFocusRequest` in Commerce; the concrete-type
   handle becomes the interface or a `PanelId` open-request.
3. **`TowerSwapService` + `TowerSwapMenu` — see PIN-3 below.**

**⛔ THE LANDMINE THE COMPILER WILL NOT CATCH.**
`Assets/_Modules/Core/Promo/PromoCodeService.cs:334-335` resolves `DeNelle.Wallet.PackContents` and
`DeNelle.Wallet.PackStoreVM` **by reflection, from `DeNelle.Core`**. Excluding Wallet — or moving
those types to Commerce without updating these two **string literals** — compiles perfectly clean
and silently turns promo-code redemption into a runtime no-op. Update them in the same commit.

**Also at risk:** `Assets/_Modules/DevTools/DeNelle.DevTools.asmdef` references Wallet under
`defineConstraints: ["UNITY_EDITOR || DEVELOPMENT_BUILD"]`. Excluded from a *release* Play build,
but it **breaks the compile if the Play artifact is ever built as a development build**
(`DevPanelController.cs:55` has `using DeNelle.Wallet;`). Give it its own `!GOOGLE_PLAY` or a
Wallet-free DevPanel path. `DeNelle.Web3.asmdef` also references Wallet but is `autoReferenced:
false` and only pulled by the EditMode tests — Lane B's `!GOOGLE_PLAY` on both covers it.

---

## ⛔ PIN-3 — OWNER DECISION, blocks Lane A

**What happens to `TowerSwapService` + `TowerSwapMenu`?** They are the **sole** Village consumers of
every genuinely rail-bound type (`CurrencyKind`, `WalletService`, `WalletConnectDialog`,
`WalletBalance`, `PaymentResult`). No rail-neutral extraction can carry them, so Village cannot go
Wallet-free until they are dealt with. They are the "Instant Tower Swap via Solana Pay" feature
(`TowerSwapService.cs:1-26`, 2.5 USDC per swap, Jupiter routing planned).

Evidence they are **not live**: zero `.unity`/`.prefab` references anywhere under `Assets/`; no
`#if` guards (they compile into every player unconditionally); the only code referrers are each
other, two `<see cref>` doc comments in `Tower.cs:61,91`, and
`Assets/Editor/Regression/ModalArbiterRegistrationRegression.cs:74`, which hard-names
`"TowerSwapMenu.cs"` in `NamedMustRegister`. The tower-defense pillar was removed 2026-06-09 (§8).

- **(a) DELETE both** — scene-unreferenced and feature-dead. `ModalArbiterRegistrationRegression.cs:74`
  must be amended in the SAME commit. **Lane A is then ~1.5 days.**
- **(b) RELOCATE both into Wallet** — keeps Solana tower-swap alive on the Seeker rail only, but
  requires breaking their `TowerData` / `Tower.AnyLongPressed` dependency on Village, which is a
  reference in the forbidden direction. **Lane A becomes a multi-day redesign of the
  Tower↔payment boundary.**

Recommendation: **(a)**. Do not start Lane A until this is answered — the two options differ by days,
not hours.

---

### Lane A — LANDED 2026-08-30 (edit-only; gate + commit held by the lead seat)

New assembly **`DeNelle.Commerce`** at `Assets/_Modules/Commerce/` (references `DeNelle.Core` and
nothing else, forever). `"DeNelle.Wallet"` is **REMOVED** from
`Assets/_Modules/Village/DeNelle.Village.asmdef`, so gate condition 3 of `InspectSourceIsolation()`
now passes. Conditions 1 + 2 (`!GOOGLE_PLAY` on Wallet + Web3) were already green in the live tree.
**Condition 4 (the androidlib) is still Lane B**, so `AssertSourceIsolation()` still refuses — that
is expected, not a regression.

- **Moved (files + `.meta`, GUIDs preserved):** `PackCatalog.cs`, `ShortfallPackOffer.cs`.
- **Namespace stayed `DeNelle.Wallet` deliberately** — `PromoCodeService.cs:334-335` resolves it as
  a reflection STRING LITERAL, so it is a live runtime contract, not tidiness. That also means the
  landmine this WO flagged **needed no edit**: the two strings are still correct.
- **`PackDef` lost 4 rail members** to `Assets/_Modules/Wallet/SolanaPackPricing.cs` as extension
  methods. One call-shape change in the whole refactor: `pack.UsdApprox` -> `pack.UsdApprox()`.
- **Three instrumented seams** in Commerce: `StoreFocusRequest` (a latch, so it cannot fail
  silently), `StorefrontRegistry` (lazy resolver — the store host is disabled in-scene and never
  runs `Awake`, so a push-registry would always be empty), `ArenaOutcomeRelay` (outcome-shaped,
  never an amount). Registered at `BeforeSceneLoad` by `PackStoreBootstrap` /
  `BattleMonthlyPanelsBootstrap`. `PackCatalog.BuildGatedPackProvider` is a fourth, registered only
  under `#if MAINNET_CANARY_TEST`.
- **The CORRECTION block's warning about silent hooks was honoured, not waved through:** every seam
  distinguishes "no handler because this build has no rail" (correct) from "no handler because the
  bootstrap did not run" (defect) in its own FlowTrace line, and `BattleMonthlyRegression`'s
  `[xp-one-door]` case now asserts BOTH the publish AND the subscription in source.
- **The battle pass did NOT move**, exactly as the CORRECTION block ruled. `BattleMonthlyCatalog`
  was not split and `RewardGrantWriter` was not touched.

Full detail: `Assets/_Modules/Commerce/README.md` and the 2026-08-30 DELTA in
`docs/MASTER_CATALOG/economy-meta.md`.

---

### Lane B — assembly + plugin exclusion

5. Add the `!GOOGLE_PLAY` define constraint to `DeNelle.Wallet.asmdef` and `DeNelle.Web3.asmdef`.
6. Give `MobileWalletAdapter.androidlib` a real per-artifact exclusion so the androidlib is not
   packaged when `GOOGLE_PLAY` is defined. A `defineConstraints` on an asmdef does **not** exclude
   an Android plugin — this needs a plugin-importer platform/exclusion mechanism, which is why
   WO-1255 called it out separately. Do not fake it with a runtime branch: the gate scans the
   **artifact**, and a stripped-at-runtime plugin is still bytes in the package.

### ~~Lane C — the Play APK variant~~ — CUT (owner ruling 2026-08-30)

**Do not build a second Play entry point.** The Play Console errors the owner hit
(*"You need to upload an APK or Android App Bundle"*, *"does not add or remove any app bundles"*,
*"doesn't allow any existing users to upgrade"*) are all one root cause — an empty release draft —
and that generic message is not permission to ship an APK. The owner ruled AAB-only.

`BuildGooglePlayAab()` **already emits the right artifact**: `buildAppBundle = true` (:116),
`EchoesOfElarion-GooglePlay.aab` (:44). It needs no change. The entire blocker is Lanes A + B —
it refuses at `AssertSourceIsolation()`, not at packaging.

If a Play **APK** is ever genuinely required, reopen this lane rather than improvising a second
build path: `AssertBuiltArtifact` (:63) is already format-agnostic (`ZipFile.OpenRead` + token
scan; an APK is a zip), so the change is a parameter rename and a `buildAppBundle = false` entry
point — **never a forked scanner** (§16: the push+verify pair drifted the moment it existed twice).

---

## Acceptance criteria

- [ ] `DeNelle.Village.asmdef` no longer contains `"DeNelle.Wallet"`, and the player compiles.
- [ ] `DeNelle.Wallet.asmdef` and `DeNelle.Web3.asmdef` both carry the `!GOOGLE_PLAY` constraint.
- [ ] `MobileWalletAdapter.androidlib` is excluded from a `GOOGLE_PLAY` artifact by importer
      configuration, proven by the artifact scan below — not by a runtime branch.
- [ ] `GooglePlayPackagingGate.AssertSourceIsolation()` logs **`PLAY_SOURCE_ISOLATION_OK`**.
- [ ] The Play **AAB** builds via the existing `BuildGooglePlayAab()` — unchanged — and
      `AssertBuiltArtifact` logs **`PLAY_ARTIFACT_CLEAN_OK`** on it —
      zero hits across every `ForbiddenArtifactTokens` entry (`solana`, `mobilewalletadapter`,
      `mobile_wallet_adapter`, `mwa/`, `jupiter`, `jup.ag`, `skrvaluation`, `walletadapter`,
      `solana-wallet`, `phantom`, `solflare`, `seed vault`, `connect wallet`, and both mint
      addresses).
- [ ] **The Seeker/dApp-Store path is UNCHANGED.** `BuildSeekerApk()` still produces a
      `DAPP_STORE` APK with the Solana rail fully intact. Prove it: build both artifacts from the
      same tree and confirm the Seeker APK still contains the MWA androidlib. A Play fix that
      silently strips the wallet from the Solana build is a worse regression than the gap it closes.
- [ ] `COMPILE_GATE_OK` on a fresh log.
- [ ] `DataRegression.RunAll` → `REGRESSION_OK <n>/<n> suites`, including `PLAY_PACKAGING_REGRESSION_OK`.
- [ ] Judged by **markers on fresh logs**, never exit codes (§16; memory
      `gates-report-success-without-proving-it`).

---

## What NOT to touch

- **Do not enable any monetization server flag, deploy any endpoint, or exercise a real purchase.**
  This WO produces a *clean artifact*. WO-1255's dormant ledger/verify path stays dormant.
- Do not modify `BuildSeekerApk` behaviour, the `DAPP_STORE` define, or `tools/r2-ship.ps1`.
- Do not weaken, bypass, or add an override flag to `GooglePlayPackagingGate`. It has no override
  today, deliberately — §16's `pre-push` hook has the same property for the same reason.
- Do not re-inline the R2 push/verify into any new build path; call `tools/r2-ship.ps1`.
- Do not restore a hand-maintained assembly dependency table in CLAUDE.md §5.

---

## Canon to update in the same commit (§15)

- `CLI_LANES_WO_NUMBERS.md` — already bumped at mint (1282 -> 1283).
- The relevant `docs/MASTER_CATALOG/<area>.md` section for the new assembly.
- `Assets/_Modules/README.md` — the code module map gains a module.
- Add a `STALE:` line or update wherever the Wallet/Village dependency is asserted.
