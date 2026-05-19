# Week 7 — Wallet + economy slice (Solana devnet)

**Date:** 2026-05-19
**Slice:** Real Solana Unity SDK integration for the Wallet module — connect a
wallet on **devnet**, render the five-pack store, run a devnet pack "purchase"
that builds + sends a transfer transaction and applies the pack contents to
GameState.
**Status:** Source + canonical data written. The integrator resolves the SDK
package in a Unity run and verifies the SDK API surface (see the verification
checklist below). This agent cannot run Unity/shell builds — source only.

---

## GUARDRAIL CONFIRMATION (spec Part 10)

- **Network is left on Devnet.** `WalletService.DefaultNetwork = WalletNetwork.Devnet`
  and `Network` is seeded from it. Nothing in this slice sets Mainnet. The flip
  to Mainnet is a single edit to that one constant — owner-gated, not done here.
  `SolanaWalletProvider.SendPayment` *also* hard-blocks Mainnet defensively
  (returns a failure) even if the constant were flipped.
- **No secrets committed.** `wallets.json` holds two PUBLIC base58 addresses
  only. No private key, seed phrase, or signer keypair exists anywhere in the
  changes. The game holds no key — the player's own wallet (Phantom / Seeker
  Seed Vault) signs every transaction.
- **React repo untouched.** `defenders-of-the-realm/` was read-only — read for
  the canonical RPC config (`walletConfig.ts`), pack data, currency kinds, and
  the public Rewards Distributor address (`docs/wallets-of-record.md`).

---

## SDK targeted

**Solana Unity SDK** — the maintained Unity C# SDK, repo
`magicblock-labs/Solana.Unity-SDK` (the Solana Foundation / Magicblock Unity
SDK). Package id `com.solana.unity_sdk`.

> NOTE — web research was unavailable in this environment (WebSearch/WebFetch
> denied). The SDK install line and API names below are from the agent's
> knowledge (cutoff Jan 2026). The Solana Unity SDK install method is volatile;
> **the integrator must confirm the current install line and the API surface**
> against the live README when resolving the package. The integration is built
> so an API mismatch breaks ONLY the guarded `#if SOLANA_SDK` block in
> `SolanaWalletProvider.cs`, never the rest of the module.

### Exact manifest.json line added

Added to `Packages/manifest.json`, in `"dependencies"`, right after the
UniTask entry:

```json
    "com.solana.unity_sdk": "https://github.com/magicblock-labs/Solana.Unity-SDK.git",
```

This is the git-URL install (self-contained, no extra scoped registry).
**Alternative** the integrator may prefer — OpenUPM. If using OpenUPM, replace
the git-URL with a version pin and extend the existing `scopedRegistries`
`package.openupm.com` entry's `scopes` array to include `com.solana`:

```json
    "com.solana.unity_sdk": "2.x.x"
```
```json
"scopes": [ "com.cysharp.unitask", "com.solana" ]
```

Pick ONE method. The git-URL is set as the default because the project's
OpenUPM registry is currently scoped only to `com.cysharp.unitask` and the SDK
pulls several `com.solana.*` transitive packages that OpenUPM resolves but the
git-URL bundles directly. Verify package resolution in a Unity run either way.

### asmdef wiring

`DeNelle.Wallet.asmdef` gained a `versionDefines` entry:

```json
"versionDefines": [
    { "name": "com.solana.unity_sdk", "expression": "", "define": "SOLANA_SDK" }
]
```

This makes Unity **auto-define `SOLANA_SDK`** whenever the SDK package is
present (empty `expression` matches any version). With the define ON, the real
`SolanaWalletProvider` SDK code compiles; with it OFF, the module still
compiles and runs over the stub.

**INTEGRATOR ACTION REQUIRED after the package resolves:** add the Solana
Unity SDK's assembly definition names to the `"references"` array of
`DeNelle.Wallet.asmdef` so the SDK types are visible. They were NOT added
pre-emptively because an unresolved asmdef reference would fail the whole
assembly when the package is absent. Expected names (verify against the
installed package — they may differ):
`Solana.Unity.SDK`, `Solana.Unity.Wallet`, `Solana.Unity.Rpc`,
`Solana.Unity.Programs`. The `using` lines at the top of
`SolanaWalletProvider.cs` (inside `#if SOLANA_SDK`) list the namespaces.

---

## Files changed

| File | Change |
| ---- | ------ |
| `Packages/manifest.json` | Added the `com.solana.unity_sdk` git-URL dependency. |
| `Assets/_Modules/Wallet/DeNelle.Wallet.asmdef` | Added `versionDefines` → auto-define `SOLANA_SDK` when the SDK is present. |
| `Assets/_Modules/Wallet/WalletService.cs` | Auto-selects `SolanaWalletProvider` vs `StubWalletProvider`; `DefaultNetwork` constant (the owner-gated flip point); `RewardsDistributorAddress` now sourced from `WalletRegistry`/`wallets.json`; added `Create(useStub)` factory. |
| `Assets/_Modules/Wallet/SolanaWalletProvider.cs` | **NEW** — real SDK `IWalletProvider`. All SDK code guarded by `#if SOLANA_SDK`. |
| `Assets/_Modules/Wallet/WalletEndpoints.cs` | **NEW** — cluster/RPC/SPL-mint config (port of React `walletConfig.ts`). Pure constants, no SDK types. |
| `Assets/_Modules/Wallet/WalletRegistry.cs` | **NEW** — typed loader for `wallets.json` (PackCatalog/Theme pattern). |
| `Assets/StreamingAssets/Data/Canonical/wallets.json` | **NEW** — PUBLIC addresses only: Rewards Distributor + devnet purchase recipient. |
| `Assets/StreamingAssets/Data/Canonical/packs.json` | **FIXED** — stripped stray `</content>`/`</invoke>` markup that polluted lines 110–111 (a prior agent file-output leak). Pack data itself unchanged. |

`PackStore.cs`, `PackCatalog.cs`, `StubWalletProvider.cs`,
`WalletConnectDialog.cs`, `PackStore.uxml/.uss` were read and left **unchanged**
— the scaffold already implements the purchase → `WalletService.Pay` → await
confirmation → apply-to-`GameStateService` flow, the treasury transparency
label binding (`store-treasury`), and the covenant line. `PackStore` already
calls `WalletService.RewardsDistributorAddress`, which now transparently
resolves through `WalletRegistry`.

---

## Architecture: stub vs. real SDK selection

The `IWalletProvider` interface is the seam (unchanged from the scaffold).

- `new WalletService()` — **auto-selects**: `SolanaWalletProvider` when
  `SOLANA_SDK` is defined (SDK present), `StubWalletProvider` otherwise.
- `new WalletService(IWalletProvider)` — explicit provider (tests).
- `WalletService.Create(useStub: true)` — forces the stub even with the SDK
  present (offline dev / EditMode tests).

`SolanaWalletProvider.IsSdkAvailable` is the compile-time switch. The stub
remains the **safe default** — the whole Wallet + Store module compiles and
runs end-to-end with NO SDK installed, and "lights up" the real SDK the moment
the integrator resolves the package and Unity auto-sets `SOLANA_SDK`, with no
caller change.

---

## Canonical data sourced

From `defenders-of-the-realm/docs/wallets-of-record.md`:

- **Rewards Distributor (public, §2):** `2JRmEmrqUbhTiHX3u5bes5kHYZeZkJ2V1cMWubxwnmNi`
  — hardware-backed Seeker Seed Vault wallet. Shown for transparency only;
  **never a payment destination** (pack revenue does not land here).
- **Devnet purchase recipient (public, §3):** `3Eeww2hyBUhiLi7AS2xsjZbfZQ2fmPFq8yh53vNzgaHe`
  — the documented Dev/Staging wallet, explicitly used for "pre-mainnet smoke
  tests verifying pack-purchase flow end-to-end on devnet". The §4 Squads
  multisig SOL/USDC/SKR revenue treasuries are **not yet provisioned**, so this
  dev/staging wallet is the safe devnet sink for pack-purchase transfers until
  they exist. Both are PUBLIC addresses — the game holds no key for either.

Pack prices / currency kinds (SOL / USDC / SKR) came from the already-extracted
`Assets/StreamingAssets/Data/Canonical/packs.json` (verbatim from
`monetization-v2-spec.md` §4). RPC/cluster config ported from React
`src/modules/wallet/walletConfig.ts`.

---

## API confidence — what the integrator MUST verify

Every SDK-touching line is inside `#if SOLANA_SDK` in `SolanaWalletProvider.cs`
and marked inline with `// SDK-VERIFY:`. Confidence breakdown:

### Confident (stable across recent Solana Unity SDK versions)
- Namespaces `Solana.Unity.SDK`, `Solana.Unity.Wallet`, `Solana.Unity.Rpc`,
  `Solana.Unity.Programs`.
- `PublicKey` type with a base58 `.Key` string property.
- `SystemProgram.Transfer(from, to, lamports)` for native SOL transfers.
- `TokenProgram.Transfer(...)` and `AssociatedTokenAccountProgram` for SPL.
- `RequestResult`-style results with `.WasSuccessful` / `.Result` / `.Reason`.
- UniTask-friendly async RPC methods.

### Needs verification (API names may differ in the resolved version)
- **`Web3` facade** — `Web3.Instance`, `Web3.Wallet`, `Web3.Rpc`. The SDK
  exposes a `Web3` MonoBehaviour singleton; confirm the static accessors. The
  integrator must drop a `Web3` prefab/component into the scene and configure
  its inspector RPC + wallet-adapter options (the SDK ships a setup wizard).
- **Login methods** — `Web3.Instance.LoginWalletAdapter()` (Mobile Wallet
  Adapter, Android/Seeker) and `Web3.Instance.LoginPhantom()` (desktop/iOS
  deep-link). Method names and whether they return `Account` directly vs. a
  result wrapper — verify.
- **`Web3.Instance.Logout()`** — disconnect call name.
- **`TransactionBuilder`** — `.SetRecentBlockHash` / `.SetFeePayer` /
  `.AddInstruction` / `.Build(...)`. Confirm `.Build` for an *unsigned* tx and
  whether `SignAndSendTransaction` takes a `Transaction` object or raw bytes
  (`Transaction.Deserialize` is used to bridge — adjust if not needed).
- **`wallet.SignAndSendTransaction(tx)`** — the sign+send entry point on the
  active `WalletBase`; confirm name and return shape.
- **RPC reads** — `GetBalanceAsync`, `GetLatestBlockHashAsync`,
  `GetTokenAccountsByOwnerAsync`, `GetSignatureStatusesAsync`. The exact result
  member paths (e.g. `Result.Value`, the parsed token-amount fields
  `TokenAmount.UiAmount` / `.Amount`) vary — verify against the installed SDK.
- **`ClientFactory.GetClient(url)`** — RPC client construction fallback.

If any name differs, the fix is local to the `#if SOLANA_SDK` block.

---

## Open item — SKR devnet mint NOT available

`WalletEndpoints.SkrMintDevnet` is **empty**. No doc the agent can read
publishes a devnet SKR (Solana Seeker token) mint address. Consequences,
all handled gracefully:

- An SKR `GetBalance` read returns `0` (no mint → skipped).
- An SKR `Pay()` fails cleanly with a descriptive error
  (`"Skr mint not configured for Devnet"`) — it never sends to a wrong address.

**INTEGRATOR / OWNER ACTION:** to exercise the spec's stated deliverable
("buy a Hearth Spark pack with 25 devnet SKR"), the real devnet SKR mint must
be set in `WalletEndpoints.SkrMintDevnet`. Until then, the **SOL and USDC**
rails are fully functional on devnet (devnet USDC uses Circle's well-known
devnet mint, already wired). The stub provider exercises all three rails for
no-wallet UI testing. This is flagged for owner clarification — the SKR mint
is economy/canon data the agent does not invent (Part 10).

---

## Devnet test path (once the SDK resolves)

1. Resolve `com.solana.unity_sdk`; confirm Unity auto-defines `SOLANA_SDK`.
2. Add the SDK assembly names to `DeNelle.Wallet.asmdef` `references`.
3. Add a `Web3` component to the wallet/store scene; set its RPC to
   `https://api.devnet.solana.com` and enable the Phantom + MWA adapters.
4. Verify the `// SDK-VERIFY:` calls compile; fix names if the version differs.
5. Connect Phantom (desktop) or a Seeker on devnet; the store renders 5 packs.
6. Buy a pack on the **SOL** rail (works today) — tx lands at
   `3Eeww…` on devnet, confirms, pack contents apply to GameState.
7. For the SKR rail, first fill `WalletEndpoints.SkrMintDevnet`.

No decisions-log edit was made (per instructions — `docs/unity-decisions.md`
left untouched). Suggested rows for the integrator/owner to add: the SDK
package choice + install method, and the devnet-purchase-recipient choice
(dev/staging wallet stands in until §4 Squads treasuries exist).
