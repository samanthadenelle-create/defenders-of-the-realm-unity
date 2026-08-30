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
