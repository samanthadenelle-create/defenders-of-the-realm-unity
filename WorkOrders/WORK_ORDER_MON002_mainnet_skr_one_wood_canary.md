# MON002 — Mainnet SKR canary: 1 SKR for exactly 1 wood

**Status:** READY TO IMPLEMENT — OWNER AUTHORIZED ONE LOW-VALUE MAINNET TEST; DO NOT OPEN GENERAL SALES

---

## ⛔ CORRECTED 2026-08-22 — THIS SPEC WOULD HAVE SENT 1,000 SKR, NOT 1

**Mainnet SKR has SIX decimals, not nine.** Read off the chain, and confirmed independently by the
owner from the explorer (mint authority `FMNn5sorEBbEoGQGrh7y3xSbYGt116F12FpL2VTsohiw`).

As originally written this WO pinned `decimals 9` and `1_000_000_000` base units. Against a
6-decimal mint that is **1,000 SKR**. Every occurrence is now corrected to `decimals 6` /
`1_000_000`.

**Where the 9 came from, because the mechanism matters more than the number:** the *Devnet* test
mint `3BwWSAUZmyngXDSZiCawEnP7iLgY5ANNopBDz94AB77N` **is** 9 decimals — we minted it ourselves. That
figure was carried into the Mainnet spec as though it described the real token. ⚠ **The Devnet path
legitimately uses 9 and must NOT be "corrected"** — the two mints genuinely differ.

| | mint | decimals | 1 SKR = |
|---|---|---|---|
| Devnet (ours, test) | `3BwWSAUZ…AB77N` | **9** | `1_000_000_000` |
| Mainnet (Solana Mobile's, real) | `SKRbvo6Gf7…NPGZhW3` | **6** | `1_000_000` |

> ### ⛔ THE BACKEND CANNOT PROTECT THE FUNDS HERE, AND THAT IS THE WHOLE POINT.
> Verification is **post-transfer**: the client sends, the chain settles, and only then does
> `/verify` compare `tokenAmount.decimals` to `contract.decimals`. A 9-vs-6 mismatch fails that
> check *after* the money is gone — outcome: **1,000 SKR transferred, no entitlement granted.**
> The exact-equality guards protect correctness, never funds. Any figure that decides an on-chain
> AMOUNT must be verified against the chain **before** the first transaction, not asserted from a
> document.

**Standing rule this establishes:** decimals are read from the mint, never from a doc. Pin them in a
regression that asserts against the chain-verified value, so this cannot silently drift back.

## ⭐ RECIPIENT RULING 2026-08-22 (CLI, owner delegated: *"you own the seat so ill follow your lead"*)

The canary recipient is **`2VePaneS3xX2EdzSbe4JdiovRffboLJV4yNVmVTkeuCg`**, pinned as the
**CANARY recipient — explicitly NOT the production treasury.**

⚠ Verified on-chain: that address is **on the ed25519 curve** and owned by the System Program, i.e.
a **plain wallet, not a Squads multisig vault** (a vault is program-derived and therefore off-curve).
It is accepted here because the canary proves the *rail* and the amount is 1 SKR.

⛔ **A real Squads vault address is a HARD PRECONDITION for anything beyond this single transaction.**
Do not let this placeholder become the production treasury by default — that is exactly how a
temporary value becomes permanent.

**Its SKR token account does not exist yet.** Neither derived ATA is present on mainnet, so the
treasury has never held SKR. Create it as a deliberate, funded step; do not let a transfer create it
incidentally.

| | value |
|---|---|
| Mint token program | classic `TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA` (**not** Token-2022) |
| Canary recipient owner | `2VePaneS3xX2EdzSbe4JdiovRffboLJV4yNVmVTkeuCg` |
| Derived recipient ATA | `ApxAy5uqivjcfxd1E5XDtubY7b4SACfTPAKfuSdVrpAy` |

---
**Created:** 2026-08-22
**Lane:** MON — isolated monetization verification
**Parent:** `WORK_ORDER_1147_MON_purchasing_verified_entitlement.md`
**Nature:** Standalone execution order. Do not hand-edit `BOARD.html`; use the repository board rebuild process if this is later tracked there.

---

## 0. Owner ruling and outcome

Implement one deliberately tiny, owner-only Mainnet canary that uses the same payment, verification,
entitlement, fulfillment, receipt, and recovery architecture already proven on Devnet.

The immutable commercial contract is:

| Field | Ruled value |
|---|---|
| SKU | `mainnet-wood-canary` |
| Player-facing name | `Mainnet Verification` |
| Network | `mainnet-beta` |
| Rail | SPL token transfer |
| Currency | `SKR` |
| Official Mainnet SKR mint | `SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3` |
| Decimals | `9` |
| Price | exactly `1 SKR` |
| Price in base units | exactly `1_000_000` |
| Reward | exactly `1 wood` |
| Purchase limit | once per entitled wallet |
| Public availability | never; owner/test allowlist plus explicit canary build gate |
| USD text | optional estimate only, approximately `$0.006`; never transaction or backend authority |

**Definition of done:** one real Mainnet payment from the owner's wallet is shown before approval as
exactly 1 SKR to the verified treasury token account; the chain settles it; the backend records a
wallet-bound entitlement; the game delivers exactly 1 wood once; the receipt and transaction history
are readable; relaunch/retry/reinstall cannot charge or grant twice; and an ordinary production build
cannot expose the canary.

This proves the production-shaped Mainnet rail. It does **not** authorize general Mainnet sales, change
normal pack prices, or flip the public store flag.

---

## 1. Proven baseline — verify at HEAD, do not reimplement blindly

Record the inspected commit SHA and mark every claim `CONFIRMED`, `CHANGED`, or `STALE` in the Result.

- The Devnet SKR canary completed end to end for wallet
  `CHKKFkPGz8VZfjpsZjJTqfAUW7vMpdNkkqCVuCcZsfkC`.
- Proven Devnet transaction:
  `5FA9ygfVAiDQKywjM7WaZADGhjA6QJCUhGdKgDGNCBKWhfuxtjpBRDpFnrQzSpAsx72HT9LvdT9vLn9NLZJyyGGX`.
- Devnet mint was `3BwWSAUZmyngXDSZiCawEnP7iLgY5ANNopBDz94AB77N`; this address must never be
  used for the Mainnet canary.
- The proven Devnet contract was 25 SKR and granted Hearth Spark. Do not mutate or replace it.
- Backend routes exist at `api/purchases/verify.js`, `reconcile.js`, and `fulfill.js`.
- The server-owned catalog exists at `api/_lib/purchase-catalog.js`.
- `purchase_entitlements` exists with signature, wallet, SKU, rail, network, currency, expected and
  observed amount, expected and observed recipient, chain slot, status, and fulfillment timestamps.
- The legacy database column is named `expected_lamports`/`observed_lamports`. For SKR these values are
  token **base units**, not lamports. Do not write new logic that treats them as SOL lamports. A schema
  rename is outside this canary unless independently migrated and proven safe.
- `Assets/Editor/Regression/MonetizationActivationRegression.cs` and
  `test/purchases.verify.test.js` pin the Devnet contract today.
- `WalletService.DefaultNetwork` is Devnet and `SolanaWalletProvider.SendPayment` deliberately blocks
  Mainnet. The canary must open Mainnet narrowly, not remove those protections globally.
- `FeatureFlags.RealmStorePurchase` remains fail-closed for ordinary builds.

**Stop:** If the Devnet receipt cannot still reconcile, or the current backend cannot distinguish
`verified` from `fulfilled`, repair that regression before touching Mainnet.

---

## 2. Non-negotiable money laws

1. The canonical sequence is:
   **select contract → connect wallet → preview exact transfer → wallet signs → chain finalizes →
   server verifies → durable entitlement exists → client grants → save verifies → backend records
   fulfillment → receipt appears**.
2. The client never supplies authoritative price, recipient, mint, decimals, network, or reward. It
   supplies identity and transaction evidence; the server resolves the immutable contract by SKU.
3. Frontend and backend price must match exactly in integer base units. For this SKU both must resolve
   to `1_000_000`. Any mismatch is a hard refusal before wallet approval.
4. Never use floating-point arithmetic to construct or verify an SPL token amount.
5. The transaction must be an SPL `transferChecked` (or SDK-equivalent checked instruction) for the
   official mint, decimals 6, exact amount, authenticated signer, and exact server-owned recipient ATA.
6. A wallet-returned signature is not payment proof. The backend independently queries Mainnet and
   verifies finality, success, signer, mint, amount, and recipient.
7. The signature is the idempotency key. One transaction can bind to one wallet, one network, one SKU,
   and one entitlement only.
8. Payment submission, verification, and fulfillment are separate states. A paid-but-unfulfilled item
   is recoverable without asking the player to pay again.
9. Exactly 1 wood is granted only after durable verification. No gemstone, crystal, cosmetic, bonus,
   random roll, or other pack content is allowed.
10. A fulfilled entitlement restored after reinstall restores ownership/receipt state; it must not
    replay the resource grant.
11. A success receipt is shown only after backend fulfillment is durable. A chain success by itself is
    displayed as `Payment received — delivering item`, not `Complete`.
12. The approximate `$0.006` reference may be shown as secondary copy only. SKR base units are the sole
    charge authority; exchange-rate drift never changes the amount.
13. No private key, seed phrase, auth token, RPC secret, signed transaction bytes, or wallet session
    payload may enter source, logs, screenshots, or commits.

---

## 3. Safety envelope and build gating

Create one explicit build symbol, recommended name `MAINNET_CANARY_TEST`.

The canary is reachable only when all of the following are true:

- the build contains `MAINNET_CANARY_TEST`;
- the connected wallet equals the owner allowlist entry
  `CHKKFkPGz8VZfjpsZjJTqfAUW7vMpdNkkqCVuCcZsfkC`;
- the server environment explicitly enables the Mainnet canary SKU;
- the client and server independently resolve the exact ruled contract;
- a verified Mainnet treasury owner and derived SKR associated token account are configured;
- the ordinary purchase safety gate is satisfied without enabling other Mainnet SKUs.

The canary must be absent or inert in every build without that symbol. Do not encode a broad
`Mainnet = allowed` boolean. Make the permission a conjunction of environment, SKU, wallet allowlist,
and build provenance.

Add a packaged-build oracle that fails if:

- `MAINNET_CANARY_TEST` appears in an ordinary release build;
- the canary SKU is visible without the symbol;
- any non-canary SKU can reach Mainnet under the symbol;
- a Devnet mint or endpoint is paired with `mainnet-beta`;
- the official Mainnet SKR mint is paired with Devnet;
- the public purchase flag has been defaulted on as a side effect.

The server allowlist is authoritative. A client-only wallet check is useful UX but not security.

---

## 4. Phase A — freeze and independently prove the Mainnet contract

- [ ] Add a separate server-owned Mainnet contract row for `mainnet-wood-canary`; leave the Devnet
      Hearth Spark row unchanged.
- [ ] Pin `currency: 'SKR'`, `network: 'mainnet-beta'`, mint, decimals `6`, and
      `amountBaseUnits: 1_000_000` as exact constants/integers.
- [ ] Author a canary-only client presentation/grant definition that promises exactly 1 wood.
- [ ] Do not insert this test product into the normal production shelf/catalog unless its loader can
      exclude it completely without the canary build symbol. Prefer an isolated canary definition.
- [ ] Identify the production treasury **owner public key**. It may be the same owner key used in
      another environment, but this must be explicitly confirmed.
- [ ] Derive the Mainnet SKR associated token account from that owner plus the official mint using the
      standard Associated Token Account program. Never copy a Devnet ATA into Mainnet config.
- [ ] Query Mainnet and prove the derived ATA exists, is owned by the SPL Token program, has the
      official mint, and its token-account authority is the intended treasury owner.
- [ ] Pin the exact derived recipient ATA in both independently owned configurations and parity tests.
- [ ] Verify the owner test wallet has at least 1 SKR plus enough SOL for fees. Never log balances with
      any secret material.
- [ ] Produce a preflight table containing SKU, network, signer, mint, decimals, base units, recipient
      owner, recipient ATA, reward, build SHA, and backend deployment ID.

**Gate A:** two independent computations—client/build fixture and server contract test—produce the
same ruled values. At least one expected-value test must hard-code the contract instead of deriving its
expectation from the catalog it is testing.

**Owner input needed only if not discoverable from deployed configuration:** the intended Mainnet
treasury owner public key. Do not infer a revenue recipient from the Devnet transfer.

---

## 5. Phase B — narrow Mainnet transaction path

- [ ] Preserve Devnet as the normal default network.
- [ ] Replace the unconditional Mainnet refusal with a narrow refusal exception that accepts only the
      canary SKU under `MAINNET_CANARY_TEST` and all safety-envelope conditions.
- [ ] Resolve network per immutable purchase intent; do not mutate a global network value that could
      leak into unrelated wallet actions.
- [ ] Build a checked SPL transfer using the official mint and exact integer base units.
- [ ] Obtain and preserve the deterministic transaction signature from the signed wire transaction
      before RPC submission. Persist the pending intent before transport ambiguity can occur.
- [ ] Bind persisted pending state to wallet, SKU, network, currency, mint, recipient, amount, and
      signature. Reject a pending record if any binding differs.
- [ ] Preview, in readable text before wallet approval: `1 SKR`, `Mainnet`, shortened-but-expandable
      mint, exact recipient, and `You receive: 1 wood`.
- [ ] Distinguish cancel, wallet rejection, insufficient SKR, insufficient SOL fee, expired blockhash,
      RPC rejection, RPC timeout/unknown, submitted, finalized, verified, fulfilled, and rejected.
- [ ] Disable duplicate Buy taps while an intent is active. If submission is ambiguous, expose
      `Check payment`/recovery; never create a replacement transaction automatically.
- [ ] Ensure every terminal or recoverable path releases the transaction world hold and restores UI
      input. The game must remain paused/held while the wallet flow overlays it.

**Gate B:** a dry run can build and inspect the unsigned transaction without signing it, and all ruled
fields match. Cancellation returns to an actionable store state and grants nothing.

---

## 6. Phase C — backend verification and durable fulfillment

- [ ] Extend `purchaseContract(network, sku)` without weakening the Devnet contract.
- [ ] Make the Mainnet contract unavailable unless the server canary environment flag is present.
- [ ] Authenticate ownership by a fresh wallet-signed challenge; do not trust a wallet string in JSON.
- [ ] Query a configured Mainnet RPC at `finalized` commitment (or document and owner-approve a stricter
      equivalent). Never accept Devnet evidence for this SKU.
- [ ] Verify transaction success, signer, official mint, decimals, exact base units, exact recipient ATA,
      and contract/SKU binding.
- [ ] Compare observed amount with `String(contract.amountBaseUnits)` or an exact integer equivalent.
      Do not compare an SKR amount to a field described semantically as SOL lamports.
- [ ] Reject underpayment, overpayment, split/ambiguous transfer, wrong mint, wrong recipient, wrong
      signer, wrong network, failed instruction, unfinalized transaction, reused signature, and
      signature previously bound to a different SKU or wallet.
- [ ] Allow only the owner wallet at verify and reconcile boundaries, not merely at catalog lookup.
- [ ] Insert/return one entitlement atomically. Database unique constraints remain the last replay
      defense under concurrent requests.
- [ ] Store the exact expected and observed amount, mint/network/rail, expected and observed recipient,
      signer wallet, slot, status, and timestamps. If mint is not yet a schema column, either add it via
      a reviewed migration or prove the SKU/network/currency immutable contract makes it reconstructible;
      record that decision in the Result.
- [ ] Fulfillment returns a server-signed/authoritative grant contract of exactly `{ wood: 1 }` or a
      stable equivalent; never accept a client-proposed reward.
- [ ] Reconcile returns only entitlements belonging to the authenticated wallet and exact environment.
- [ ] Operational logs join submission, verification, entitlement, fulfillment, and receipt by signature,
      but redact authentication material.

**Gate C:** backend tests cover valid Mainnet fixture plus wrong signer, wallet, SKU, network, mint,
decimals, amount, recipient, finality, failure, replay, concurrent verify, fulfilled retry, and cross-
environment reconciliation. No test performs a real Mainnet transfer.

---

## 7. Phase D — client grant, receipt, and post-payment UX

- [ ] Add exactly 1 to the authoritative wood inventory only after verified entitlement retrieval.
- [ ] Capture wood-before, grant, save, reload, and wood-after evidence; require `after = before + 1`.
- [ ] Mark local fulfillment with the backend entitlement/signature before a retry can replay the grant,
      while retaining a crash-safe recovery path if saving fails mid-operation.
- [ ] A fulfilled backend row plus missing local receipt restores the receipt/ownership marker, not wood.
- [ ] On success, show a legible modal/toast: `Mainnet Verification complete`, `1 wood received`,
      `Paid 1 SKR`, and a shortened transaction ID with Copy/View action.
- [ ] Animate or otherwise visibly confirm wood increased; do not rely only on a tiny status line.
- [ ] On return from the wallet, never silently fall back to the store. Show one of: Pending,
      Delivering, Complete, Cancelled, or Needs help.
- [ ] `Check payment` must reconcile the existing signature and cannot initiate a new charge.
- [ ] Receipt history must show the canary tied to the connected wallet and transaction signature.
- [ ] Text must remain readable at the device's tested resolution and safe-area settings.

**Gate D:** simulated verified entitlement proves exact-once grant across duplicate callbacks, process
death before grant, death after grant before receipt, relaunch, wallet reconnect, and reinstall.

---

## 8. Phase E — automated gates before any real signature

Run canonical repository gates and record command, start/end time, commit SHA, log path, and result:

- [ ] Clean Unity compile gate.
- [ ] Full DataRegression with zero monetization failures.
- [ ] `test/purchases.verify.test.js` and all API tests.
- [ ] Wallet/provider tests for build-symbol, owner allowlist, SKU isolation, network isolation, checked
      transfer, exact integer amount, cancel, expiration, ambiguity, and recovery.
- [ ] Entitlement tests for uniqueness, authenticated wallet binding, exact grant, save failure, restart,
      reinstall, replay, and cross-network refusal.
- [ ] Store commerce/state and transaction-world-hold regressions.
- [ ] Independent client/server price-parity regression pinning exactly `1_000_000`.
- [ ] Canonical data mirror parity if any mirrored catalog is touched.
- [ ] Secret scan and static scan for test/stub wallet paths.
- [ ] Android build with an artifact manifest that records the canary define, git SHA, version code,
      package ID, backend base URL, network, and APK SHA-256.
- [ ] Android build without the define proving the canary is absent and Mainnet remains refused.
- [ ] R2/artifact parity gate if used by the canonical build pipeline.

No oracle may compute its expectation solely from the same mutable constants it validates.

**Gate E:** both artifacts pass: canary build exposes only the canary to only the owner; clean build
exposes none of it. Do not install the canary APK until this gate is green.

---

## 9. Phase F — physical-device Mainnet matrix

This phase spends real SKR. Perform in order. Capture timestamped screenshots/logs at each numbered step
and give the owner live status updates.

### F1 — no-spend rehearsal

- [ ] Install the exact canary APK and record its hash/version/SHA.
- [ ] Connect only wallet `CHKK...ZsfkC`; prove another wallet is refused before transaction creation.
- [ ] Open the dedicated canary card and verify it says exactly 1 SKR for 1 wood.
- [ ] Open wallet preview. Compare network, signer, mint, recipient, and amount to the signed preflight
      table. Capture the preview without exposing secrets.
- [ ] Cancel in the wallet. Confirm no signature, no entitlement, no wood, no stuck overlay, and an
      explicit Cancelled state.
- [ ] Reopen and reach the identical preview a second time.

**Mandatory human checkpoint:** The owner personally reads the wallet preview. Do not approve if it
contains any transfer other than exactly 1 SKR of the official mint to the verified recipient ATA.

### F2 — one real payment

- [ ] Record starting wallet SKR and SOL balances, treasury SKR balance, and in-game wood.
- [ ] Approve exactly one transaction.
- [ ] Record the deterministic signature immediately.
- [ ] If the wallet or RPC reports expired/timeout/unknown, do not retry payment. Use reconciliation on
      that signature until the backend establishes finalized success or definitive failure.
- [ ] Confirm Mainnet explorer/RPC shows the authenticated wallet, official mint, exact recipient ATA,
      exact `1_000_000` base units, decimals 6, successful checked transfer, and finalized status.
- [ ] Confirm wallet SKR decreased exactly 1 SKR, excluding only SOL network fees from the SOL balance.
- [ ] Confirm treasury SKR increased exactly 1 SKR.
- [ ] Confirm exactly one entitlement row exists and joins signature → wallet → SKU → Mainnet → SKR →
      expected/observed amount → recipient → fulfilled status.
- [ ] Confirm in-game wood increased exactly 1 and no other inventory changed.
- [ ] Confirm the receipt is legible and offers Copy/View transaction.

Only one real payment is authorized by this WO.

### F3 — recovery and replay

- [ ] Close/relaunch; receipt and ownership remain, wood does not increase again.
- [ ] Tap the product again; it shows Owned/Completed or opens receipt, never wallet approval.
- [ ] Reconcile the same signature; backend returns the same entitlement, no second row or grant.
- [ ] Clear only safe local receipt/cache state or reinstall per the existing authenticated restore path;
      reconnect the same wallet and confirm restoration without another charge or wood grant.
- [ ] Disconnect/reconnect and verify no entitlement leaks to another wallet.
- [ ] Confirm all transaction holds/input locks are released.

**Gate F:** every item is green with a joined evidence packet. Any uncertainty is `PENDING/RECOVER`, not
permission to submit a second transaction.

---

## 10. Hard stop conditions

Stop before signing—or stop further activity after an ambiguous submission—and report immediately if:

- displayed price, wallet preview, transaction instruction, server contract, or database amount differ;
- any value other than exactly `1_000_000` base units is requested;
- the mint is not `SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3`;
- network is not Mainnet, or a Devnet address/RPC appears;
- the recipient ATA or its authority is unverified;
- the signer is not the allowlisted owner wallet;
- more than one instruction transfers value, or an unexpected SOL/token transfer is present;
- the backend deployment/catalog/migration is not confirmed before device testing;
- reward can occur before durable verification;
- an existing pending signature is replaced by a new charge attempt;
- a retry, callback, restart, or reinstall grants a second wood;
- success UI appears before fulfillment, or return from wallet has no intelligible state;
- the normal release artifact exposes this SKU or enables other Mainnet products;
- test/stub providers, secrets, or private material appear in the artifact or logs;
- unrelated dirty-tree changes cannot be separated safely.

After a real signature exists, never delete its pending record to make the UI clean. Reconcile or route
to support using that signature.

---

## 11. Initial implementation allowlist

Inspect first; edit only files required by the implementation. Shared files require hunk-by-hunk review.

Expected seams:

- `api/_lib/purchase-catalog.js`
- `api/purchases/verify.js`
- `api/purchases/reconcile.js`
- `api/purchases/fulfill.js`
- backend migration/config files for `purchase_entitlements` if truly required
- `test/purchases.verify.test.js` and new focused Mainnet canary tests
- `Assets/_Modules/Wallet/SolanaWalletProvider.cs`
- `Assets/_Modules/Wallet/WalletService.cs`
- `Assets/_Modules/Wallet/PurchaseGate.cs`
- `Assets/_Modules/Wallet/PackStore.cs`
- `Assets/_Modules/Wallet/PackCatalog.cs` only if the isolated canary contract requires it
- `Assets/_Modules/Core/FeatureFlags.cs`
- the wallet endpoint/config source containing SKR mint, RPC, and recipient resolution
- `Assets/Editor/Regression/MonetizationActivationRegression.cs`
- relevant store-commerce, wallet-selection, and transaction-world-hold regressions
- canonical mirrored data only if the canary cannot remain isolated
- canonical build scripts/define manifest only as required for `MAINNET_CANARY_TEST`
- this WO's Result section or a sibling `.RESULT.md`

Do not touch `BOARD.html` manually. Do not stage unrelated UI, dungeon, VFX, ads, siege, or art changes.

---

## 12. Commit, deploy, and rollback contract

1. Record `git status`, HEAD SHA, branch, and existing dirty files before editing.
2. Freeze the client/server request-response contract before parallel work.
3. Stage explicit paths or hunks only; never broad-stage a dirty tree.
4. Review `git diff --cached --check`, `git diff --cached --name-only`, and the complete staged diff.
5. Commit implementation and tests only after automated Gate E is green. The commit message must say the
   Mainnet canary remains owner-only and general sales remain off.
6. Deploy the backend canary configuration before installing the APK; record deployment identity.
7. Tag/archive the exact tested APK and server deployment evidence.
8. After Gate F, disable the server canary SKU and produce a clean build without
   `MAINNET_CANARY_TEST`, unless the owner explicitly requests another canary.
9. Reconciliation for the completed entitlement must remain available after new canary purchases are
   disabled.
10. A kill switch may stop new purchases; it must never prevent recovery of already-paid transactions.

Rollback after a pre-payment failure: disable server canary, remove/disable canary build distribution,
leave public purchasing off. Rollback after payment: first preserve and fulfill/reconcile the paid
entitlement, then disable new purchases. Never roll back by deleting transaction evidence.

---

## 13. Evidence packet and Result template

Store evidence outside source control if it contains wallet/account data; link safe/redacted artifacts.

```text
MON002 RESULT
HEAD/build commit:
Implementation commit(s):
Backend deployment ID/time:
APK filename / SHA-256 / version code:
Build define manifest:
Owner wallet (public):
Treasury owner (public):
Treasury SKR ATA:
SKU/network/mint/decimals/base units/reward:

Baseline audit: PASS | FAIL
Client/server exact-price parity: PASS | FAIL
Clean-build canary exclusion: PASS | FAIL
Compile/DataRegression/API tests: PASS | FAIL
Cancel rehearsal: PASS | FAIL
Real payment: PASS | FAIL | NOT RUN
Transaction signature:
Explorer/RPC finality evidence:
Wallet delta:
Treasury delta:
Entitlement row evidence:
Wood before/after:
Receipt/UI evidence:
Relaunch/retry/reinstall exact-once evidence:
Canary server disabled after test: YES | NO
Clean non-canary build produced: YES | NO

Decision:
MAINNET CANARY VERIFIED | HOLD/RECOVER | NOT RUN

Important: MAINNET CANARY VERIFIED does not mean PUBLIC SALES ENABLED.
Open a separate owner-approved release order to expose normal products.
```

---

## 14. CLI handoff summary

Mimic the **architecture and state machine** proven by the Devnet Hearth Spark transaction, not its
network, mint, amount, recipient ATA, SKU, or reward. Add one isolated Mainnet contract: 1 SKR for 1
wood. Prove exact client/server parity and cancellation before the sole authorized real payment. Treat
an ambiguous submission as recoverable and never charge again. Capture the full wallet → chain →
backend → entitlement → inventory → receipt join. Then disable the canary and prove the ordinary build
cannot expose it.
