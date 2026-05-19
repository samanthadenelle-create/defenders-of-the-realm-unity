# Security Audit — Defenders of the Realm (Unity v2 foundation)

**Auditor:** Security review pass (read-only)
**Date:** 2026-05-19
**Scope:** Wallet/crypto module, repo secrets, save/load layer, data-loader trust boundaries.
**Project:** `C:\Users\Kayden-Laptop\Documents\defenders-unity`
**Out of scope:** the v1 React repo (`defenders-of-the-realm/`) — not audited per instruction.

---

## Executive summary

The Week 7 Solana wallet slice was reviewed against the spec's Part 10 crypto contract.
The core guardrails hold: no private keys, seed phrases, or signer keypairs exist
anywhere in the repository; `wallets.json` carries only two PUBLIC base58 addresses,
both matching the canonical `docs/wallets-of-record.md`; the network ships as Devnet
and is gated by a single `const`; and the transaction-building flow correctly leaves
all signing with the player's own wallet — the game holds no key.

Two defensive layers protect the devnet boundary: the `WalletService.DefaultNetwork`
constant AND a hard `Mainnet`-block inside `SolanaWalletProvider.SendPayment`. The
Solana SDK source pinned in `manifest.json` is a reputable repo but is referenced as
an **unpinned git branch URL**, which is the single most material finding — a supply-
chain exposure rather than a contract breach.

No Critical findings. One High (unpinned SDK dependency). The remainder are Medium/Low
hardening items, none of which block the Week 7 deliverable.

### Part 10 crypto-guardrails verdict: **PASS**

All four Part 10 crypto/secret obligations are satisfied:

| Part 10 obligation | Status | Evidence |
|---|---|---|
| No private keys / seed phrases / signer keypairs committed | PASS | Repo-wide scan clean; `.gitignore` covers `*.keypair.json`, `**/secrets/`, `*.seed`. |
| `wallets.json` holds PUBLIC addresses only | PASS | Two base58 addresses, both public, verbatim from `wallets-of-record.md` §2/§3. |
| Network defaults to Devnet; Mainnet gated behind a deliberate constant change | PASS | `WalletService.DefaultNetwork = Devnet`; plus a defense-in-depth Mainnet block in `SolanaWalletProvider.SendPayment`. |
| Game does not hold keys; player's wallet signs | PASS | Provider builds an *unsigned* tx; `wallet.SignAndSendTransaction` delegates signing to Phantom / Seeker Seed Vault. |

The PASS is qualified only by SEC-001 (unpinned SDK URL) — that is a supply-chain
hygiene issue, not a violation of the no-secrets / devnet-only / no-keys contract.

---

## Findings by severity

| Severity | Count |
|---|---|
| Critical | 0 |
| High | 1 |
| Medium | 4 |
| Low | 4 |
| **Total** | **9** |

---

## Findings

### SEC-001 — Solana Unity SDK pinned to an unpinned git branch URL

- **Severity:** High
- **Location:** `Packages/manifest.json:13` — `com.solana.unity_sdk` dependency
- **Description:** The SDK is declared as
  `"com.solana.unity_sdk": "https://github.com/magicblock-labs/Solana.Unity-SDK.git"`
  — a bare git URL with no `#` revision/tag suffix. Unity resolves this to the
  repository's default branch HEAD at resolution time. The SDK is the most
  security-sensitive dependency in the project (it builds and submits on-chain
  transactions). A bare branch URL means: (a) the exact code that ships is not
  reproducible build-to-build; (b) any compromise of, or malicious commit to, the
  upstream default branch is pulled silently on the next package resolve; (c) there
  is no integrity pin. The upstream repo (`magicblock-labs/Solana.Unity-SDK`) is the
  recognized maintained Unity Solana SDK (Magicblock / Solana Foundation lineage) and
  is a reasonable choice of source — the issue is purely the *pinning method*, not
  the *vendor*. `week7-wallet.md` itself flags the install line as volatile and
  unverified (web research was unavailable to the writing agent).
- **Recommendation:** Pin to an immutable revision before any build that touches a
  real wallet. Either (a) append a commit SHA or release tag to the git URL
  (`...Solana.Unity-SDK.git#vX.Y.Z`), or (b) switch to the OpenUPM version-pinned
  install documented as the alternative in `week7-wallet.md` (`"com.solana.unity_sdk": "2.x.x"`
  with the `com.solana` scope added to the OpenUPM `scopedRegistries` entry). After
  pinning, have the integrator review the SDK's `package.json`/transitive deps and
  record the chosen revision in `docs/unity-decisions.md`. Treat any future SDK
  version bump as a reviewed change, not an automatic pull.

### SEC-002 — `WalletEndpoints` uses public rate-limited RPC endpoints with no integrity/abuse controls

- **Severity:** Medium
- **Location:** `Assets/_Modules/Wallet/WalletEndpoints.cs:38-43`
- **Description:** `DevnetRpcUrl`/`MainnetRpcUrl` point at the public
  `api.devnet.solana.com` / `api.mainnet-beta.solana.com` endpoints. For devnet QA
  this is acceptable and the file says so. However the Mainnet RPC constant is
  present and live; if the owner ever flips `DefaultNetwork` to Mainnet, the build
  would transact over a shared, rate-limited, unauthenticated public endpoint —
  unsuitable for production (dropped confirmations, MITM-by-rate-limit, no SLA). The
  file comment acknowledges a dedicated provider would be needed but ships the
  public URL as the resolved value.
- **Recommendation:** Acceptable for the devnet v2 foundation. Before any mainnet
  enablement, the Mainnet RPC must be swapped to a dedicated authenticated provider
  (Helius / Triton / QuickNode) and the API key kept out of the repo (env-var or
  build-time injection — consistent with `whitepaper.md` "RPC keys env-var only").
  Consider gating the Mainnet endpoint constants behind the same owner-approval note
  as the network flip so they cannot be reached accidentally.

### SEC-003 — SPL-token transfer always prepends a recipient ATA-creation instruction

- **Severity:** Medium
- **Location:** `Assets/_Modules/Wallet/SolanaWalletProvider.cs:300-308` (`SendPayment`, SPL branch)
- **Description:** The USDC/SKR path unconditionally adds
  `AssociatedTokenAccountProgram.CreateAssociatedTokenAccount(from, to, mint)` with
  the **sender as the rent payer**, with an inline comment "Harmless to always
  include on devnet QA." On Solana, submitting a create-ATA instruction for an ATA
  that already exists causes the whole transaction to fail (the account is already
  initialized) — so on the *second* and every subsequent SPL purchase to the same
  recipient, the transaction would be rejected. It also silently charges the player
  ~0.002 SOL of rent for an account they do not own. This is a correctness bug with
  a (minor) security/economic angle: the player pays rent for the treasury's token
  account and cannot complete a repeat purchase.
- **Recommendation:** Query whether the recipient ATA exists first (an
  `idempotent`-create instruction if the resolved SDK exposes one, or a
  `GetAccountInfo` pre-check) and only add the create instruction when the ATA is
  absent. This is inside the `#if SOLANA_SDK` block already flagged for integrator
  verification — fold the fix into that pass. Re-verify with the resolved SDK API.

### SEC-004 — No amount/recipient confirmation surfaced to the user before signing

- **Severity:** Medium
- **Location:** `Assets/_Modules/Wallet/PackStore.cs:268-327` (`Purchase`); `WalletService.Pay`
- **Description:** The purchase flow goes button-click → `WalletService.Pay` →
  `SolanaWalletProvider.SendPayment` with no in-game confirmation step echoing the
  exact amount, currency, and destination address before the transaction is built.
  The status banner only says "Confirming … on Devnet…" *after* the call is already
  in flight. The pack amount is read from `packs.json` and the recipient from
  `wallets.json`; if either canonical file were tampered with (see SEC-007), the
  player would sign a transfer they never explicitly reviewed. The wallet app itself
  (Phantom/Seeker) does show a sign prompt, which is the real backstop — but the
  game should not rely solely on the wallet UI to be the player's only line of
  defense on amount/destination.
- **Recommendation:** Add an explicit in-game confirm modal before `Pay()` that
  shows pack name, the resolved native amount + currency, and the
  short-form destination address. Low effort, meaningful trust-boundary improvement,
  and consistent with the cozy-covenant "never required to spend" posture.

### SEC-005 — `WalletRegistry` / `PackCatalog` hard-coded fallbacks mask a missing or corrupt canonical file

- **Severity:** Medium
- **Location:** `Assets/_Modules/Wallet/WalletRegistry.cs:87-92,144-202`
- **Description:** When `wallets.json` fails to load or parse, `WalletRegistry`
  silently falls back to compile-time-constant addresses (`FallbackRewardsDistributor`,
  `FallbackDevnetRecipient`) and only logs a warning. For a *display* address this is
  benign, but `DevnetPurchaseRecipientAddress` is an actual **payment destination**.
  A scenario where `wallets.json` is absent/corrupt yet the build still routes real
  transfers to a hard-coded constant means the payment destination is no longer
  governed by the auditable canonical file — the trust anchor moves into a code
  constant that a reviewer of `wallets.json` would not see. The fallback values are
  currently correct, so there is no live exposure; the concern is the *pattern* —
  a corrupt-file condition degrades silently instead of failing closed for a
  money-movement path. (`PackCatalog` has the milder version: it returns an empty
  catalog, which fails safe.)
- **Recommendation:** For the payment-recipient path specifically, prefer fail-closed:
  if `wallets.json` cannot be loaded, `SendPayment` should refuse to build a transfer
  rather than fall back to a constant. Keep the fallback for the *display* address
  only. At minimum, escalate the fallback log from `LogWarning` to `LogError` and
  surface it in the store UI so a missing canonical file is impossible to miss.

### SEC-006 — Save data is fully trusted on the client; no integrity protection on `PlayerPrefs['dotr-save']`

- **Severity:** Medium → relevant to the anti-cheat posture, Low for v2 foundation
- **Location:** `Assets/_Modules/Core/State/GameStateService.cs:114-202`; `SaveSchema.cs`
- **Description:** The save is plain JSON in `PlayerPrefs` (Windows registry / Android
  shared-prefs / a plist), with no signature, HMAC, or obfuscation. `SaveSchema.Validate`
  does a solid job rejecting NaN/Infinity and clamping to non-negative integers, and
  `SaveMigrator` rejects future-version saves — so a *malformed* save is handled
  gracefully. But a *well-formed* edited save is fully trusted: a player can set
  `resources.crystals`, `voidshards`, `bestWave`, `ownedItemIds` (pack entitlements),
  `petBonds`, etc. to any non-negative value and the game accepts it. Critically,
  `ownedItemIds` is the **pack-entitlement ledger** — `PackStore.IsOwned` reads it —
  so a tampered save grants paid pack contents for free. There is no server-side
  authority in the v2 foundation, and the spec frames the React parity as
  localStorage, so this is partly by-design; but it should be a recorded, conscious
  acceptance, not an unflagged gap, given the project has real-money packs and a
  `docs/anti-cheat-spec.md`.
- **Recommendation:** For the v2 foundation, accept and document the limitation
  explicitly in `docs/unity-decisions.md`. Before any mainnet economy launch:
  (a) make pack entitlements server-authoritative (verify on-chain tx or a backend
  receipt rather than trusting `ownedItemIds`); (b) consider an HMAC over the save
  payload keyed by a per-install secret to make casual editing detectable. The
  battle RNG is already seed-deterministic per the spec's anti-cheat note — extend
  that same "verifiable, not trusted" principle to entitlements.

### SEC-007 — StreamingAssets canonical JSON is deserialized with no schema/shape enforcement

- **Severity:** Low
- **Location:** `Assets/_Modules/Wallet/PackCatalog.cs:203-233`; `WalletRegistry.cs:144-175`;
  pattern shared by the other `Data/Canonical/*.json` loaders.
- **Description:** `JsonConvert.DeserializeObject<T>` is used on files read from
  `Application.streamingAssetsPath`. On desktop/editor this is read-only app content
  bundled in the build, so the trust boundary is low. Two notes: (1) there is no
  positive schema validation on these files (the spec's Part 4 calls for
  `SchemaTests.cs` per data file — those tests guard build-time drift, not runtime);
  (2) Newtonsoft `TypeNameHandling` is left at its safe default (`None`) in these
  loaders and in `SaveSchema.JsonSettings`, so there is **no** polymorphic-type
  deserialization gadget exposure — good. The residual risk is only that a
  malformed-but-parseable canonical file (e.g. a negative pack price, an empty
  recipient address) flows into the wallet flow unchecked.
- **Recommendation:** Confirm `TypeNameHandling` is never raised from `None` in any
  loader (it currently is not — keep it that way). Add lightweight runtime sanity
  checks for the wallet-critical files: reject a `PackDef` with a non-positive price
  on a rail being purchased, and reject a `WalletEntry` whose `Address` is not a
  plausible base58 length. The existing `SchemaTests.cs` requirement (Part 4) should
  also cover `wallets.json`.

### SEC-008 — No base58 / address-format validation before constructing `PublicKey`

- **Severity:** Low
- **Location:** `Assets/_Modules/Wallet/SolanaWalletProvider.cs:197,258-259,287`
- **Description:** Addresses from `wallets.json` and the connected account are passed
  straight into `new PublicKey(...)`. A malformed address (wrong length, non-base58
  characters) would throw inside the SDK; the surrounding `try/catch` converts that
  to a clean `PaymentResult.Failure`, so there is no crash or unsafe behavior — but
  the failure is late and generic. `WalletAccount.ShortAddress` and
  `WalletEntry.ShortAddress` already assume base58 shape without checking.
- **Recommendation:** Add a small `IsLikelyBase58Address(string)` guard (length
  32–44, base58 alphabet) and validate the recipient address right after it is read
  from `WalletRegistry`, returning a descriptive failure before any tx work. Cheap
  defense-in-depth that pairs well with SEC-005's fail-closed recommendation.

### SEC-009 — `metroCertificatePassword` field present in `ProjectSettings.asset` (empty — informational)

- **Severity:** Low (informational — currently no exposure)
- **Location:** `ProjectSettings/ProjectSettings.asset:603` — `metroCertificatePassword:`
- **Description:** The repo-wide secret scan surfaced the standard Unity
  `metroCertificatePassword` key (UWP/Metro signing-certificate password). It is
  **empty** — no secret is committed. Flagged only so it is on the record: this
  field, and the Android keystore fields, are exactly where a credential could
  later leak into git if a build is signed locally. `ProjectSettings.asset` is a
  tracked file and is not (and should not be) gitignored.
- **Recommendation:** No action needed now. When release signing is set up
  (Android keystore for the Seeker build, per the Week 8 APK deliverable), ensure
  keystore passwords and the keystore file itself are injected at build time and
  never committed — add the keystore path/`*.keystore` to `.gitignore` at that
  point. Verify `metroCertificatePassword` / `androidKeystorePass` /
  `androidKeyaliasPass` remain empty in every commit.

---

## What was verified clean (no finding)

- **No keypair/seed/secret files** anywhere in the repo. `Glob` for
  `*.keypair`, `*.key`, `*.pem`, `*.secret` returned nothing; the content scan for
  private-key/seed/mnemonic/API-key/token patterns found only documentation and
  code *comments asserting the absence* of secrets — no actual secret material.
- **`.gitignore` covers the crypto-secret surface** — `*.keypair.json`,
  `**/secrets/`, `*.seed` are explicitly listed (lines 74-77), plus build
  artifacts (`*.apk`, `*.aab`) and `.claude/settings.local.json`.
- **`wallets.json` addresses are correct and public** — `2JRmE…nmNi` (Rewards
  Distributor) and `3Eeww…gaHe` (Dev/Staging recipient) match
  `docs/wallets-of-record.md` §2 and §3 verbatim; both documented public.
- **Devnet default is real and double-guarded** — `WalletService.DefaultNetwork`
  is `Devnet`; `SolanaWalletProvider.SendPayment` independently rejects `Mainnet`;
  `SetNetwork(Mainnet)` logs a Part 10 warning. The agent does not flip it.
- **Signing stays with the player** — the provider builds an unsigned tx
  (`Build(Array.Empty<Account>())`) and hands it to `wallet.SignAndSendTransaction`;
  no signer keypair, no `Account` with a secret key, is ever held by game code.
- **No unsafe deserialization gadget** — `TypeNameHandling` is left at the safe
  default (`None`) in `SaveSchema.JsonSettings` and the canonical-data loaders;
  `allowUnsafeCode: false` in `DeNelle.Wallet.asmdef`.
- **Save loader fails safe on malformed input** — parse errors, missing payloads,
  failed migration, and validation failures all fall back to fresh defaults with a
  logged error rather than crashing or trusting garbage; NaN/Infinity rejected.
- **No stray agent markup** — the `</content>` / `</invoke>` leak that
  `week7-wallet.md` reported in `packs.json` is confirmed cleaned; a repo-wide scan
  found no remaining stray closing tags.

---

## Recommended priority order

1. **SEC-001** — pin the Solana SDK to an immutable revision before any build that
   touches a real wallet (the one item that should not ship unaddressed).
2. **SEC-003** — fix the always-create-ATA bug; it breaks repeat SPL purchases and
   over-charges the player rent.
3. **SEC-005 / SEC-008** — make the payment-recipient path fail-closed and add
   address-format validation.
4. **SEC-004** — add an in-game confirm-before-sign modal.
5. **SEC-006** — record the client-trusted-save limitation in the decisions log;
   plan server-authoritative entitlements before mainnet.
6. **SEC-002 / SEC-007 / SEC-009** — mainnet-readiness hardening; no action needed
   for the devnet v2 foundation.

_End of audit. Tend the Heart. Hold the keys._
