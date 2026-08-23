# Monetization implementation handoff — WO-1146, WO-1147, MON002

**Prepared for:** Claude CLI review, gates, explicit-path commit, deployment, and device evidence
**Prepared:** 2026-08-22
**Baseline HEAD inspected:** `cce94331149581047a4710033b3dd9bfeeb1ed99`

## Outcome

- **WO-1146 code:** implemented at baseline. Three placement-specific LevelPlay rewarded units,
  consent-before-init, main-thread ILRD forwarding, server-anchored placement accounting,
  earned-callback-only grants, duplicate/cross-unit callback refusal, and permanent refusal of the
  synchronous bypass are present. Public activation still requires dashboard/device evidence.
- **WO-1147 code:** implemented at baseline and already proven by the owner's successful Devnet SKR
  purchase. Verify/reconcile/fulfill, wallet authentication, deterministic-signature persistence,
  exact-once grant, fulfillment receipt, and transaction world hold are present.
- **MON002 code:** added in this handoff. Mainnet server contract, official mint, owner allowlist,
  isolated canary product, exact 1 SKR → 1 wood grant, explicit Mainnet RPC, network-bound recovery,
  and independent regressions are implemented. It remains fail-closed until the approved Mainnet
  recipient owner/ATA configuration is supplied.

## Changes attributable to this handoff

### Client/runtime

- `Assets/_Modules/Wallet/MainnetCanaryCatalog.cs` + `.meta`
- `Assets/_Modules/Wallet/PackCatalog.cs`
- `Assets/_Modules/Wallet/PackStore.cs`
- `Assets/_Modules/Wallet/PurchaseEntitlementVerifier.cs`
- `Assets/_Modules/Wallet/PurchaseGate.cs`
- `Assets/_Modules/Wallet/SolanaWalletProvider.cs`
- `Assets/_Modules/Wallet/WalletEndpoints.cs`
- `Assets/_Modules/Wallet/WalletRegistry.cs`
- `Assets/_Modules/Core/FeatureFlags.cs` — only the `MAINNET_CANARY_TEST` preprocessor hunk

### Backend

- `api/_lib/purchase-catalog.js`
- `api/purchases/verify.js`
- `api/purchases/reconcile.js`
- `api/purchases/fulfill.js`

### Regression

- `test/purchases.verify.test.js`
- `Assets/Editor/Regression/MainnetCanaryRegression.cs` + `.meta`
- `Assets/Editor/Regression/MonetizationActivationRegression.cs`
- `Assets/Editor/Regression/DataRegression.cs` — only the `mainnet-canary` registration hunk

### Documentation

- `WorkOrders/MON_ACTIVATION_IMPLEMENTATION_HANDOFF_2026-08-22.md`

## Do not stage as part of monetization

The worktree contained unrelated castle, jeweler, UI-kit, project-settings, logs, device captures,
and dev-tool artifacts. Inspect current status again: concurrent seats may have moved these paths.
Never broad-stage.

## Invariants now enforced

- Mainnet SKU: `mainnet-wood-canary`.
- Owner wallet: `CHKKFkPGz8VZfjpsZjJTqfAUW7vMpdNkkqCVuCcZsfkC`.
- Official Mainnet SKR mint: `SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3`.
- Mainnet decimals: 6 (Devnet test mint remains 9).
- Amount: exactly `1_000_000` base units = 1 Mainnet SKR.
- Reward: exactly 1 wood and nothing else.
- Client build gate: `MAINNET_CANARY_TEST`.
- Server kill switch: `MAINNET_CANARY_ENABLED=true`.
- Mainnet API/network spelling: `mainnet-beta`.
- Ordinary builds retain Devnet default, public purchase fail-closed behavior, and no canary product.
- Mainnet has no recipient fallback. Missing or invalid recipient refuses before wallet approval.

## Required environment configuration before a real transaction

Do not guess these values:

- `SOLANA_MAINNET_RPC_URL` — production-capable Mainnet RPC.
- `SOLANA_MAINNET_PURCHASE_RECIPIENT` — owner-approved treasury owner public key.
- `SOLANA_MAINNET_PURCHASE_RECIPIENT_ATA` — ATA derived from that owner plus the official SKR mint.
- `MAINNET_CANARY_ENABLED=true` — enable only for the test window, then remove/false.

Author the same approved recipient owner as `mainnetPurchaseRecipient` in both canonical
`wallets.json` mirrors so the client and backend agree. Verify the derived ATA on-chain before build.
No Devnet wallet or Rewards Distributor fallback is permitted.

## Gates already observed

- `node --test test/purchases.verify.test.js` → **12/12 PASS** after the implementation.
- `git diff --check` on attributable paths → no whitespace errors (line-ending warnings only).
- Claude's shared-tree compile gate emitted `COMPILE_GATE_OK` during implementation, but edits landed
  after that run began. It is not final evidence; rerun the gates below at the final tree.

## Claude CLI verification sequence

1. Inspect every attributable hunk and re-run `git status --short`.
2. Confirm the Mainnet treasury owner with the owner. Derive its official-SKR ATA independently and
   query Mainnet to prove mint and token authority.
3. Add the public recipient entry to both canonical wallet mirrors and the server environment.
4. Run:

   ```powershell
   node --test test/purchases.verify.test.js
   powershell -ExecutionPolicy Bypass -File .\run-unity-method.ps1 -Method DeNelle.Editor.CompileGate.Run -LogName mon-final-compile.log
   powershell -ExecutionPolicy Bypass -File .\run-unity-method.ps1 -Method DeNelle.Editor.DataRegression.RunAll -LogName mon-final-regression.log
   ```

5. Require `COMPILE_GATE_OK`, `REGRESSION_OK n/n suites`, `[monetization-activation]`, and
   `[mainnet-canary]` markers. Read failures; never trust process exit alone.
6. Build a clean artifact without owner-test defines and prove the canary is absent.
7. Build the owner sideload with:

   ```powershell
   .\overnight-apk-build.ps1 -Defines 'MAINNET_CANARY_TEST;MONETIZATION_LOCAL_TEST'
   ```

   Do not include `STORE_RAIL_LOCAL_TEST`; MON002 has its own narrower purchase gate.
8. Run the MON002 cancel-first/device matrix exactly as written in its WO. Only one real Mainnet
   payment is authorized.
9. Confirm chain, backend row, wallet, SKU, mint, recipient, amount, wood delta, receipt, relaunch,
   retry, and reinstall all join on the same signature.
10. Set `MAINNET_CANARY_ENABLED=false`, retain reconciliation, and produce another clean artifact.

## Activation decisions

- `REWARDED ADS ANDROID PUBLIC`: **HOLD pending final dashboard/device evidence and owner sign-off**.
- `PURCHASE DEVNET TESTER`: **PROVEN** by the owner transaction; keep public/default behavior governed
  by the release decision.
- `PURCHASE MAINNET CANARY`: **IMPLEMENTED, HOLD before transaction until recipient/RPC config and
  final gates are green**.
- `PURCHASE MAINNET PUBLIC`: **HOLD**. MON002 success does not authorize public sales.

## Commit contract

Claude CLI owns the commit. Stage the attributable paths above explicitly, with hunk selection for
`FeatureFlags.cs` and `DataRegression.cs`. Sweep the staged diff and the entire remaining worktree.
Do not commit unrelated concurrent-seat changes. Suggested message:

`feat(monetization): isolate owner-only Mainnet SKR canary [MON002]`
