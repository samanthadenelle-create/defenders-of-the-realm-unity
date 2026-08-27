# WORK ORDER 766 — Real Seeker/Android wallet connect (Solana Mobile SDK)

**Status:** CLOSED 2026-08-27 — owner felt-tested PASS on APK 2026.08.27.343739.
(`com.solana.unity_sdk` @ git tag v1.2.9) + `SOLANA_SDK` set for the ANDROID define group only
(Standalone/desktop deliberately keeps the stub) + full `SDK-VERIFY` sweep against the v1.2.9 sources
(drift fixed: no `LoginPhantom` in v1.2.9; `Logout()` is sync void; `Web3` MonoBehaviour host now
lazily created; Tx build moved to the documented Transaction-model pattern) + MWA `<queries>`
manifest via `Assets/Plugins/Android/MobileWalletAdapter.androidlib` + sibling
`WalletProviderSelectionRegression`. REMAINING AT GATE: orchestrator resolves the UPM package
(first compile may surface residual SDK-VERIFY signatures + a possible `com.unity.textmeshpro`
dependency conflict on Unity 6), enables **Custom Main Gradle Template** (SDK injects its
AndroidX/Guava fixes there, per its install docs), wires `[wallet-provider]` into
`DataRegression.RunAll`, commits generated `.meta` files, rebuilds the APK. OWNER'S HANDS REQUIRED
ON-DEVICE: the first MWA approval flow on the Seeker (tap Connect Wallet -> approve in Seed
Vault/Phantom; then the save-auth message-sign prompt).
**Lane:** Wallet / Web3 / Android. Scope: **MODERATE-LARGE** (SDK install + define + verify unverified calls + MWA Android bits + rebuild). Integration work, not greenfield — the provider is written.
**Owner intent:** connect a REAL Seeker/Android wallet (owner's + a tester friend's) to test the wallet integration. Confirmed SAFE (no money risk) because purchases are disabled — see §3.

---

## 0. What the wallet is FOR (owner-confirmed model)

Two SEPARATE jobs; this WO targets identity+save first, payments later:
1. **Identity + cloud save (near-term).** Connect wallet → `GameStateService.BindWallet(address)` → `BoundWallet` becomes the `playerId` key for the cloud save. The full save JSON is POSTed to `SaveUrl = BackendBase + "/api/game/save"` (`GameStateService.cs:913`), a deployed merge-upsert store, keyed by `playerId = _state.BoundWallet` (`:1290`). A **guest fallback** (`GuestWalletPrefix + HashDeviceId`, `:939`) keys the save before any wallet. Wallet **message**-signing (WO-121, `TryAttachAuthHeaders`, `:1314`) authenticates the save; no-op/offline-safe when off.
2. **Payments (later, mainnet).** Buy SKR/SOL/USDC packs — separate, NOT in scope here.

## 1. Current state (code-verified 2026-07-24, RCA)

- Wallet scaffold complete: `IWalletProvider` seam, `WalletService`, `WalletConnectDialog`, `PackStore`, devnet+mainnet RPC config (`WalletEndpoints.cs`).
- **Only `StubWalletProvider` compiles on EVERY target** — `WalletService.cs:282-294` picks it because `SolanaWalletProvider.IsSdkAvailable == false` (needs `SOLANA_SDK` define, `SolanaWalletProvider.cs:72-79`). The stub fabricates a RANDOM address (`:70-85`), mock balances, fake signatures. So it never touches a real wallet, and the "bound wallet" is a throwaway id (not stable across connects — can't recover a save).
- The **real** `SolanaWalletProvider` is fully written (real Tx builder, RPC confirm) but entirely inside `#if SOLANA_SDK` (never compiled) with `// SDK-VERIFY:` markers (unverified vs the actual SDK API). Its Android branch already calls `Web3.Instance.LoginWalletAdapter()` (MWA/Seed Vault) — `SolanaWalletProvider.cs:114-117`.
- **No Solana Mobile stack present:** no SDK package in `Packages/manifest.json`, no `Assets/Plugins/Android/`, no MWA `AndroidManifest`/`<queries>`/`.aar`, `SOLANA_SDK` unset.
- `SkrMintDevnet = ""` (`WalletEndpoints.cs:54`) — SKR devnet mint unprovisioned (irrelevant if testing identity/save + mainnet).
- Android build is READY: `AndroidBuild.BuildSeekerApk` (`AndroidBuild.cs`), `BuildOptions.None` (dev-menu-free, `:81`), IL2CPP/ARM64, keystore signing (`keystore.properties` present → updatable installs).

## 2. The work

1. **Add the Solana Unity SDK** the provider is written against — magicblock-labs `Solana.Unity-SDK` (`SolanaWalletProvider.cs:5-7`) — to `Packages/manifest.json`.
2. **Set `SOLANA_SDK`** in Scripting Define Symbols (Android target; ProjectSettings or `.rsp`) so `SolanaWalletProvider` compiles + `WalletService` selects it.
3. **Verify the `// SDK-VERIFY:` calls** against the real SDK API (Web3 facade, `LoginWalletAdapter`, TransactionBuilder, TokenProgram) — fix any signature drift. Compile clean.
4. **MWA Android wiring** the SDK requires: any `Assets/Plugins/Android/` `.aar`, `AndroidManifest` `<queries>`/intent-filter/deep-link for a wallet app + Seed Vault. (Per the SDK's docs.)
5. **Identity/save first:** confirm `BindWallet` fires from the real connect (`WalletSkinBootstrap.cs:71` already calls it) and the real address becomes `playerId`. Test the cloud-save round-trip keyed to a REAL mainnet address.
6. **Rebuild** `AndroidBuild.BuildSeekerApk` (already release/dev-menu-free) → install on Seeker + generic Android (Phantom/Solflare for MWA).
7. **Payments = OUT OF SCOPE** — keep `RealmStorePurchase` off (release default) so no transfer path exists during wallet testing.

## 3. Safety (owner-confirmed — SAFE, no money risk)
- Purchases disabled in release (store "Coming soon", `RealmStorePurchase` off) → the app **never constructs a transfer transaction** → the wallet is never asked to approve a spend.
- Wallet CONNECT reveals the address (+ optional auth **message** signature) — gasless, moves no funds.
- Save-auth = **message** signature, not a transaction — cannot transfer.
- ∴ Real MAINNET wallet connect for identity/save is safe; **devnet unnecessary** for this (devnet only matters when rehearsing real PAYMENTS). Discipline: message-sign = safe; transaction-approve = the only spend, and the app produces none with purchases off.

## 4. Acceptance criteria
- [ ] `SOLANA_SDK` set; `SolanaWalletProvider` compiles; `WalletService` selects it on Android (not the stub).
- [ ] On a Seeker/Android APK, "Connect Wallet" opens the REAL wallet (Seed Vault / Phantom via MWA) and returns the user's REAL address.
- [ ] `BindWallet(realAddress)` sets `playerId`; the cloud save round-trips against `/api/game/save` keyed to that address (two devices / two wallets → two distinct saves).
- [ ] NO transfer/purchase transaction is ever constructed (store stays "Coming soon"); no spend prompt appears.
- [ ] `COMPILE_GATE_OK` (+ regression); APK release-signed, dev-menu-free, updatable.

## 5. Notes / sequence
- Precursor deliverable (no WO needed): a **stub Android APK NOW** for on-device gameplay + store-UX + guest-id cloud-save testing (can't touch a real wallet). Good while this WO is done.
- Then this WO → real-wallet APK for owner + tester friend.
- Payments/mainnet purchase = a LATER WO (devnet rehearsal optional then).
- Data source: read-only RCA 2026-07-24 (all file:line cited), per §12.
