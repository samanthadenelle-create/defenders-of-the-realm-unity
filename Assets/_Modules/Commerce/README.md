# `_Modules/Commerce/` — `DeNelle.Commerce`

**The rail-neutral half of the store.** Created by **WO-1282** (2026-08-30).

## Why this assembly exists

Google asked the owner for an artifact to verify with testers. A Google Play build must not
carry the Solana wallet rail, so `AndroidBuild.BuildGooglePlayAab()` refuses to build until
`GooglePlayPackagingGate.AssertSourceIsolation()` passes. One of its four conditions is:

> `Assets/_Modules/Village/DeNelle.Village.asmdef` must NOT reference `"DeNelle.Wallet"`.

Village never needed the *rail* — it needed the *contracts*: pack data, the shortfall resolver,
a way to focus the storefront, and a way to tell the battle pass a bout finished. Those moved
here. `PackStore` (3546 lines, constructs a `WalletService`), `CurrencyKind`, `WalletService`,
`PurchaseGate`, `BattlePassService` and everything else rail-bound stayed in `DeNelle.Wallet`.

## The one rule

> ## ⛔ `DeNelle.Commerce` NEVER references `DeNelle.Wallet` or `DeNelle.Web3`.
> `DeNelle.Commerce.asmdef` references **`DeNelle.Core` and nothing else**, and it must stay that
> way. `DeNelle.Wallet` references Commerce — one direction, never back.
>
> **The tell that the boundary has been crossed is a `CurrencyKind` in a Commerce file.** That enum
> (`Sol`/`Usdc`/`Skr`, `WalletService.cs`) *is* the rail. If you find yourself wanting to add the
> reference to make a name resolve, stop: you are re-breaking the Play artifact, and the gate that
> catches it runs at BUILD time, not at compile time.

## ⚠ The namespace is `DeNelle.Wallet`, deliberately

The moved types kept the **`DeNelle.Wallet` namespace**. That is not leftover mess — it is a live
runtime contract:

* `Assets/_Modules/Core/Promo/PromoCodeService.cs` resolves `"DeNelle.Wallet.PackContents"` and
  `"DeNelle.Wallet.PackStoreVM"` as **string literals** by reflection, walking every loaded
  assembly. Renaming the namespace compiles perfectly clean and turns promo-code redemption into a
  silent runtime no-op.
* C# namespaces and assemblies are orthogonal. The Play build excludes an **assembly**, and the
  assembly is `DeNelle.Commerce`. Nothing about the exclusion needs the namespace to change.

It is the same reason `PackDef.LegacySkus` can never be pruned and `PackEconomy` keeps the field
name `Food` for the authored key `stone`: a name that something else resolves at runtime is an
interface, and interfaces do not get renamed for tidiness.

**New** types authored here (the three seams) use the `DeNelle.Commerce` namespace — they carry no
legacy contract.

## Contents

| File | Namespace | What it is |
|---|---|---|
| `PackCatalog.cs` | `DeNelle.Wallet` | Moved from `Wallet/`. `StoreBand`, `PackPricing`, `PackEconomy`, `BoostSpec`, `ConvenienceItemDef`, `PackContents`, `PackDef`, `PackCatalogData`, `PackCatalog`. The covenant firewall + the redeemable-convenience list live here. |
| `ShortfallPackOffer.cs` | `DeNelle.Wallet` | Moved from `Wallet/`. `ShortfallOffer` + `ShortfallPackOffer` — the WO-1037 shortfall → impulse-pack resolver. Never grants, charges or routes. |
| `StoreFocusRequest.cs` | `DeNelle.Commerce` | The "open the store on THIS sku" **latch** (WO-1253's `PackStore.RequestFocusSku`, lifted). A latch, not an event: the request is made before the panel exists. |
| `StorefrontRegistry.cs` | `DeNelle.Commerce` | A **lazy resolver** for the storefront's scene host. Lazy because the host is disabled in the scene and never runs `Awake`, so it can never push itself into a registry. |
| `ArenaOutcomeRelay.cs` | `DeNelle.Commerce` | Village publishes a finished arena bout; the battle pass subscribes at boot. **Outcome-shaped `(win, streak, perfect)`, never an amount** — the one-door XP rule is unchanged. |

## What moved OUT of `PackDef`, and where it went

`AmountFor(CurrencyKind)`, `AmountLabel(CurrencyKind)`, `UsdApprox` and `IsServerPinnedSku` each
name a rail-bound type, so they became **extension methods** in
`Assets/_Modules/Wallet/SolanaPackPricing.cs`. Every call site is unchanged (`pack.AmountFor(...)`
still works from any file in `namespace DeNelle.Wallet`) with **one** exception: `pack.UsdApprox`
became **`pack.UsdApprox()`** — C# has no extension properties.

## Where the seams are wired

| Seam | Registered by | When |
|---|---|---|
| `StorefrontRegistry.RegisterResolver` | `Wallet/PackStoreBootstrap.RegisterOpener` | `BeforeSceneLoad` |
| `ArenaOutcomeRelay.RegisterHandler` | `Wallet/BattleMonthlyPanelsBootstrap.RegisterOpeners` | `BeforeSceneLoad` |
| `PackCatalog.BuildGatedPackProvider` | `Wallet/MainnetCanaryCatalog.RegisterGatedPack` | `BeforeSceneLoad`, `#if MAINNET_CANARY_TEST` only |

**An unregistered seam is a legitimate state on a Play build** — there is no storefront, no battle
pass and no canary in it. Every one of them traces the distinction (§12) rather than assuming the
comfortable reading, because on a Seeker build an unregistered seam is a defect.

`StoreFocusRequest` needs no registration at all: it is a plain latch, so it cannot fail silently
the way a hook can.

## Pinned by

* `Assets/Editor/Regression/GooglePlayPackagingGate.cs` — fails the AAB build if
  `DeNelle.Village.asmdef` names `DeNelle.Wallet` again.
* `Assets/Editor/Regression/ImpulsePackRegression.cs` — `[no-grant-route]` reads
  `Commerce/ShortfallPackOffer.cs` by path.
* `Assets/Editor/Regression/BattleMonthlyRegression.cs` — `[xp-one-door]` asserts BOTH halves: the
  publish in `ArenaProgressStore` **and** the subscription under `_Modules/Wallet`.
* `Assets/Editor/Regression/BuilderSkuRegression.cs` — `[manage]` asserts `ManageScreenVM.BuySlot`
  still calls `RequestFocusSku(PermanentBuilderSku)`.
