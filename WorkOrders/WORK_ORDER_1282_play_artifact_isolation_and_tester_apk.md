# WORK ORDER 1282 — Play artifact isolation + tester APK variant

**Status:** READY TO IMPLEMENT
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

## ⛔ OWNER PIN — BLOCKING, do not start Lane C without it

WO-1255's RESULT records: *"The owner-required Play storefront design is not approved."* A Play
build with `DeNelle.Wallet` excluded has **no storefront at all** unless a rail-neutral one exists.

**PIN-1:** the owner must approve the **fiat-only Play store design** (what a Play player sees where
the Solana pack rail is today). Until then Lanes A and B may proceed — they are structural and
rail-neutral — but Lane C must not invent a storefront.

**PIN-2:** external Play Console app + service-account configuration is not present (WO-1255 RESULT).
Not needed to *build* the artifact; needed before any receipt-verification test. Out of scope here.

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
