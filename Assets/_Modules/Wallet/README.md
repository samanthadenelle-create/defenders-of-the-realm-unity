# Wallet — `DeNelle.Wallet`

Monetization + crypto wallet layer (~70% built — **do NOT greenfield**, see
CLAUDE.md §8). Store scene-wiring currently DISABLED pending own PanelSettings.

## Files

- `PackStore` — the existing store implementation (3546 lines; constructs a `WalletService`)
- ⚠ **`PackCatalog` IS NO LONGER IN THIS MODULE.** WO-1282 moved it — with `PackDef`,
  `PackContents`, `PackPricing`, `PackEconomy`, `ConvenienceItemDef`, `BoostSpec`, `StoreBand`,
  `PackCatalogData` and `ShortfallPackOffer`/`ShortfallOffer` — to the rail-neutral
  **`_Modules/Commerce/` (`DeNelle.Commerce`)**, so `DeNelle.Village` could stop referencing this
  assembly and a Google Play artifact can exclude the Solana rail whole. **They kept the
  `DeNelle.Wallet` NAMESPACE** (`PromoCodeService` resolves it as a reflection string literal), so
  every `using DeNelle.Wallet;` here still resolves them — only the assembly changed. See
  `_Modules/Commerce/README.md`.
- `SolanaPackPricing` — WO-1282. The RAIL half of `PackDef`, as extension methods:
  `AmountFor(CurrencyKind)`, `AmountLabel(CurrencyKind)`, `UsdApprox()`. They could not go to
  Commerce because `CurrencyKind` *is* the rail. ⚠ `pack.UsdApprox` is now `pack.UsdApprox()` —
  C# has no extension properties.
- ⛔ **`PackStore` HAS TWO SEPARATE QUESTIONS ABOUT PI AND BOTH MUST SURVIVE (WO-1323).**
  `PiDisplay` (**who is LOOKING** — read off `CurrencySkinResolver.Active`, skin id `pi` +
  `SkinAuthMode.PiSdk`) decides what the shelf may **say**; `PiRailOwnsTheStore` (**who takes the
  money** — `PaymentProviders.Current.Channel`) decides what may be **charged**. They are not the
  same fact: on 2026-09-02 the owner's real Pi Browser session resolved the **skin** to Pi while the
  **channel** never registered, so every price label fell through to the `$SKR` branch and a Pi
  player was quoted a token this game has never held. Every SKR figure and every piece of Solana
  wallet furniture in `PackStore` now sits behind `PiDisplay`; the SKR skin is unchanged because the
  predicate is false there. **Never collapse the two** — one way quotes SKR at a Pi player, the
  other offers a Buy the rail cannot settle.
- ⛔ **The Pi price is NEVER computed here.** The shelf's Pi figures come from `/api/pi/quote`
  (server-side, CoinGecko `low_24h`, fail-closed) through `IDisplayPriceRefresher`, implemented by
  `PiBrowserPaymentProvider` over the one Pi endpoint client (`PiPaymentEndpoints`). There is no
  rate on this side, no USD→Pi converter, and a refused or expired quote **clears** the cached
  figure rather than leaving it on the shelf. No `pi` price is authored in `packs.json` and none may
  be. Pinned by `[store-pi-skin]` (`StorePiSkinCurrencyRegression`).
- `WalletService`, `WalletRegistry`, `WalletEndpoints` — wallet abstraction
- `SolanaWalletProvider`, `StubWalletProvider` — providers (stub for dev/tests)
- `MwaSessionStore` — the MWA `auth_token` sealed under an AndroidKeyStore AES-GCM key and bound to
  the wallet it was issued for, so a relaunch reauthorizes SILENTLY instead of re-prompting
  (owner-reproduced on a Seeker, 2026-08-17). Fails closed off-device — never a plaintext fallback —
  cleared on explicit disconnect, and never logged. Pinned by `[wallet-session]`.
- `CryptoPaymentManager`, `WalletConnectDialog`
- `Tests/` — wallet service/registry/stub provider tests

## ⛔ This assembly is EXCLUDED from a Google Play artifact

`DeNelle.Wallet.asmdef` carries `defineConstraints: ["!GOOGLE_PLAY"]`, and
`GooglePlayPackagingGate.AssertSourceIsolation()` refuses to build the AAB if
`DeNelle.Village.asmdef` names it. **Nothing outside Wallet/Web3/DevTools/Editor may reference this
assembly.** When another module needs something from here, the answer is a seam in
`DeNelle.Commerce` that this module registers at boot — see the seam table in
`_Modules/Commerce/README.md`. `PackStoreBootstrap`, `BattleMonthlyPanelsBootstrap` and
`MainnetCanaryCatalog` each own one of those registrations.

Related: `_Modules/Commerce/` (the rail-neutral half), `Web3/` module (Jupiter swap), `docs/monetization-v2-spec.md`,
`docs/wallets-of-record.md`, WO-72–80, WO-131.

> Maintenance: update this README when files are added/removed.
